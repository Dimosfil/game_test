using System;
using System.Linq;
using ColonyRTS.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace ColonyRTS.Tests
{
    [TestFixture]
    public sealed class AdvancedOrderTests
    {
        private static RuleSet Rules()
        {
            var rules = SimulationFixture.Rules();
            rules.VersionId = "qa-advanced-orders-v1";
            rules.Spawns = rules.Spawns.Where(s => s.Owner >= 0 &&
                !(s.Owner == 1 && s.DefinitionId != "command")).ToArray();
            rules.StartingMinerals = 20;
            rules.StartingGas = 10;
            rules.RepairTicks = 4;
            rules.RepairHealth = 7;
            rules.RepairMineralCost = 3;
            rules.RepairGasCost = 1;
            foreach (var definition in rules.Definitions)
                definition.Repairable = definition.IsBuilding;
            return rules;
        }

        private static Match DamagedBase(RuleSet rules, int missing = 15)
        {
            var match = new Match(rules, false, 331);
            var snapshot = match.CaptureState();
            snapshot.Entities.Single(e => e.Owner == 0 && e.DefinitionId == "command").Health -= missing;
            return Match.Restore(rules, snapshot);
        }

        private static bool Near(SimPoint point, SimPoint target, int tolerance = 300)
        {
            long dx = point.X - target.X, dz = point.Z - target.Z;
            return dx * dx + dz * dz <= (long)tolerance * tolerance;
        }

        [Test, Timeout(10000)]
        public void PatrolReturnsToIssuedOriginRepeatsAndStopCancels()
        {
            var match = new Match(Rules(), false, 19);
            var marine = SimulationFixture.Id(match, "marine");
            var origin = match.Find(marine).Position;
            var destination = SimPoint.World(14, 16);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Patrol, marine, destination).Accepted, Is.True);
            SimulationFixture.Until(match, () => Near(match.Find(marine).Position, destination), 200);
            SimulationFixture.Until(match, () => Near(match.Find(marine).Position, origin), 200);
            SimulationFixture.Until(match, () => Near(match.Find(marine).Position, destination), 200);
            Assert.That(match.Find(marine).Orders[0].Kind, Is.EqualTo(OrderKind.Patrol));
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Stop, marine).Accepted, Is.True);
            var stopped = match.Find(marine).Position;
            SimulationFixture.Steps(match, 50);
            Assert.That(match.Find(marine).Orders, Is.Empty);
            Assert.That(Near(match.Find(marine).Position, stopped, 0), Is.True);
        }

        [Test, Timeout(10000)]
        public void ShiftPatrolUsesPrecedingMoveEndpointInsteadOfIssuePosition()
        {
            var match = new Match(Rules(), false);
            var marine = SimulationFixture.Id(match, "marine");
            var moveEnd = SimPoint.World(12, 14);
            var patrolEnd = SimPoint.World(16, 16);
            match.QueueCommand(match.MakeCommand(0, CommandKind.Move, new[] { marine }, moveEnd));
            match.QueueCommand(match.MakeCommand(0, CommandKind.Patrol, new[] { marine }, patrolEnd, append: true));
            match.Step();
            Assert.That(match.LastResults.All(r => r.Accepted), Is.True);
            var patrol = match.Find(marine).Orders.Last();
            Assert.That(patrol.Kind, Is.EqualTo(OrderKind.Patrol));
            Assert.That(Near(patrol.PatrolOrigin, moveEnd, 0), Is.True);
            SimulationFixture.Until(match, () => Near(match.Find(marine).Position, patrolEnd), 250);
            SimulationFixture.Until(match, () => Near(match.Find(marine).Position, moveEnd), 200);
            SimulationFixture.Until(match, () => Near(match.Find(marine).Position, patrolEnd), 200);
            Assert.That(match.Find(marine).Orders[0].Kind, Is.EqualTo(OrderKind.Patrol));
        }

        [Test, Timeout(10000)]
        public void QueuedPatrolAdvancesToNextPatrolAndLastSegmentRemainsCyclic()
        {
            var match = new Match(Rules(), false);
            var marine = SimulationFixture.Id(match, "marine");
            var first = SimPoint.World(12, 14);
            var second = SimPoint.World(16, 16);
            match.QueueCommand(match.MakeCommand(0, CommandKind.Patrol, new[] { marine }, first));
            match.QueueCommand(match.MakeCommand(0, CommandKind.Patrol, new[] { marine }, second, append: true));
            match.Step();
            Assert.That(match.LastResults.All(result => result.Accepted), Is.True);
            Assert.That(match.Find(marine).Orders.Count, Is.EqualTo(2));
            Assert.That(Near(match.Find(marine).Orders[1].PatrolOrigin, first, 0), Is.True);
            SimulationFixture.Until(match, () => Near(match.Find(marine).Position, first), 150);
            SimulationFixture.Until(match, () => Near(match.Find(marine).Position, second), 200);
            Assert.That(match.Find(marine).Orders.Count, Is.EqualTo(1));
            Assert.That(match.Find(marine).Orders[0].Kind, Is.EqualTo(OrderKind.Patrol));
            SimulationFixture.Until(match, () => Near(match.Find(marine).Position, first), 200);
            SimulationFixture.Until(match, () => Near(match.Find(marine).Position, second), 200);
        }

        [Test, Timeout(10000)]
        public void MoveAppendedWhilePatrolReturnsCompletesAfterOutboundEndpoint()
        {
            var match = new Match(Rules(), false);
            var marine = SimulationFixture.Id(match, "marine");
            var patrolEnd = SimPoint.World(13, 14);
            var moveEnd = SimPoint.World(16, 17);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Patrol, marine, patrolEnd).Accepted, Is.True);
            SimulationFixture.Until(match, () => match.Find(marine).Orders[0].PatrolReturning, 200);
            SimulationFixture.Steps(match, 5);
            Assert.That(match.Find(marine).Orders[0].PatrolReturning, Is.True);
            match.QueueCommand(match.MakeCommand(0, CommandKind.Move, new[] { marine }, moveEnd, append: true));
            match.Step();
            Assert.That(match.LastResults.Single().Accepted, Is.True);
            SimulationFixture.Until(match, () => Near(match.Find(marine).Position, patrolEnd), 100);
            SimulationFixture.Until(match, () => match.Find(marine).Orders.Count == 0, 250);
            Assert.That(Near(match.Find(marine).Position, moveEnd), Is.True,
                "An accepted queued Move must execute after the patrol's final outbound leg.");
        }

        [Test, Timeout(10000)]
        public void PatrolFightsVisibleEnemyThenResumesItsRoute()
        {
            var rules = Rules();
            var spawns = rules.Spawns.ToList();
            spawns.Add(SimulationFixture.Spawn("marine", 1, 13, 15));
            rules.Spawns = spawns.ToArray();
            rules.Definitions.Single(d => d.Id == "marine").MaxHealth = 6;
            // Keep the patrol alive while the stationary hostile can be killed in one shot.
            var match = new Match(rules, false);
            var snapshot = match.CaptureState();
            var enemy = snapshot.Entities.Single(e => e.Owner == 1 && e.DefinitionId == "marine");
            enemy.AttackTimer = 1000;
            match = Match.Restore(rules, snapshot);
            var marine = SimulationFixture.Id(match, "marine");
            var destination = SimPoint.World(17, 16);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Patrol, marine, destination).Accepted, Is.True);
            SimulationFixture.Until(match, () => match.Find(enemy.Id) == null, 150);
            Assert.That(match.Find(marine), Is.Not.Null);
            Assert.That(match.Find(marine).LastAttackTarget, Is.EqualTo(enemy.Id));
            SimulationFixture.Until(match, () => Near(match.Find(marine).Position, destination), 250);
            SimulationFixture.Until(match, () => Near(match.Find(marine).Position, SimPoint.World(8, 14)), 250);
        }

        [Test, Timeout(10000)]
        public void RepairChargesEachHealingCycleIncludingPartialLastCycleAndStopsAtCap()
        {
            var rules = Rules();
            var match = DamagedBase(rules);
            var worker = SimulationFixture.Id(match, "worker");
            var target = SimulationFixture.Id(match, "command");
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Repair, worker, targetId: target).Accepted, Is.True);
            SimulationFixture.Until(match, () => match.Find(target).Health == 1000, 150);
            SimulationFixture.Steps(match, 10);
            Assert.That(match.Find(target).Health, Is.EqualTo(1000));
            Assert.That(match.Observe(0).Minerals, Is.EqualTo(11), "15 HP uses 7 + 7 + 1 at three fixed cycle costs.");
            Assert.That(match.Observe(0).Gas, Is.EqualTo(7));
            Assert.That(match.Find(worker).Orders, Is.Empty);
        }

        [Test, Timeout(10000)]
        public void RepairWithoutFundsPausesWithoutHealingOrNegativeBalance()
        {
            var rules = Rules();
            rules.StartingMinerals = 2;
            var match = DamagedBase(rules);
            var worker = SimulationFixture.Id(match, "worker");
            var target = SimulationFixture.Id(match, "command");
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Repair, worker, targetId: target).Accepted, Is.True);
            SimulationFixture.Steps(match, 60);
            Assert.That(match.Find(target).Health, Is.EqualTo(985));
            Assert.That(match.Observe(0).Minerals, Is.EqualTo(2));
            Assert.That(match.Observe(0).Gas, Is.EqualTo(10));
            Assert.That(match.Find(worker).Orders[0].Kind, Is.EqualTo(OrderKind.Repair));
            var snapshot = match.CaptureState();
            snapshot.Players[0].Minerals = 20;
            match = Match.Restore(rules, snapshot);
            SimulationFixture.Until(match, () => match.Find(target).Health == 1000, 80);
            Assert.That(match.Observe(0).Minerals, Is.EqualTo(11));
            Assert.That(match.Observe(0).Gas, Is.EqualTo(7));
        }

        [Test]
        public void RepairRejectsForeignTargetBiologicalTargetAndNonWorkerActorAtomically()
        {
            var rules = Rules();
            var match = DamagedBase(rules);
            var snapshot = match.CaptureState();
            var foreign = snapshot.Entities.Single(e => e.Owner == 1 && e.DefinitionId == "command");
            foreign.Position = SimPoint.World(12, 8);
            foreign.Health -= 50;
            var marine = snapshot.Entities.Single(e => e.Owner == 0 && e.DefinitionId == "marine");
            marine.Health--;
            match = Match.Restore(rules, snapshot);
            var worker = SimulationFixture.Id(match, "worker");
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Repair, worker, targetId: foreign.Id).Accepted, Is.False);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Repair, worker, targetId: marine.Id).Accepted, Is.False);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Repair, marine.Id,
                targetId: SimulationFixture.Id(match, "command")).Accepted, Is.False);
            Assert.That(match.Find(foreign.Id).Health, Is.EqualTo(950));
            Assert.That(match.Find(marine.Id).Health, Is.EqualTo(39));
            Assert.That(match.Observe(0).Minerals, Is.EqualTo(20));
            Assert.That(match.Observe(0).Gas, Is.EqualTo(10));
            Assert.That(match.Find(worker).Orders, Is.Empty);
        }

        [Test]
        public void StopCancelsRepairBeforeItsFirstCycleWithoutPayment()
        {
            var match = DamagedBase(Rules());
            var worker = SimulationFixture.Id(match, "worker");
            var target = SimulationFixture.Id(match, "command");
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Repair, worker, targetId: target).Accepted, Is.True);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Stop, worker).Accepted, Is.True);
            SimulationFixture.Steps(match, 60);
            Assert.That(match.Find(target).Health, Is.EqualTo(985));
            Assert.That(match.Observe(0).Minerals, Is.EqualTo(20));
            Assert.That(match.Observe(0).Gas, Is.EqualTo(10));
            Assert.That(match.Find(worker).Orders, Is.Empty);
        }

        [Test]
        public void DamagedRepairableWorkerCannotRepairItselfOrSpendResources()
        {
            var rules = Rules();
            rules.Definitions.Single(d => d.Id == "worker").Repairable = true;
            var match = new Match(rules, false);
            var worker = SimulationFixture.Id(match, "worker");
            var snapshot = match.CaptureState();
            snapshot.Entities.Single(e => e.Id == worker).Health = 40;
            match = Match.Restore(rules, snapshot);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Repair, worker, targetId: worker).Accepted, Is.False);
            Assert.That(match.Find(worker).Health, Is.EqualTo(40));
            Assert.That(match.Find(worker).Orders, Is.Empty);
            Assert.That(match.Observe(0).Minerals, Is.EqualTo(20));
            Assert.That(match.Observe(0).Gas, Is.EqualTo(10));
        }

        [Test]
        public void MixedOwnershipRepairSelectionIsRejectedBeforeMutatingAnyWorker()
        {
            var rules = Rules();
            var spawns = rules.Spawns.ToList();
            spawns.Add(SimulationFixture.Spawn("worker", 1, 10, 10));
            rules.Spawns = spawns.ToArray();
            var match = DamagedBase(rules);
            var worker = SimulationFixture.Id(match, "worker");
            var foreign = SimulationFixture.Id(match, "worker", 1);
            var target = SimulationFixture.Id(match, "command");
            var command = match.MakeCommand(0, CommandKind.Repair, new[] { worker, foreign }, targetId: target);
            match.QueueCommand(command);
            match.Step();
            Assert.That(match.LastResults.Single().Accepted, Is.False);
            Assert.That(match.Find(worker).Orders, Is.Empty, "An earlier owned actor cannot receive a partial repair order.");
            Assert.That(match.Find(foreign).Orders, Is.Empty);
            Assert.That(match.Find(target).Health, Is.EqualTo(985));
            Assert.That(match.Observe(0).Minerals, Is.EqualTo(20));
            Assert.That(match.Observe(0).Gas, Is.EqualTo(10));
        }

        [Test]
        public void ObservationRedactsNondefaultNavigationGoalAndContactDistance()
        {
            var match = DamagedBase(Rules());
            var worker = SimulationFixture.Id(match, "worker");
            var target = SimulationFixture.Id(match, "command");
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Repair, worker, targetId: target).Accepted, Is.True);
            var diagnostic = match.Find(worker);
            Assert.That(diagnostic.Path.Count, Is.GreaterThan(0));
            Assert.That(diagnostic.PathStoppingDistance, Is.GreaterThan(0));
            Assert.That(diagnostic.PathTarget.X != 0 || diagnostic.PathTarget.Z != 0, Is.True);
            var observed = match.Observe(0).Entities.Single(e => e.Id == worker);
            Assert.That(observed.Path, Is.Empty);
            Assert.That(observed.PathIndex, Is.Zero);
            Assert.That(observed.PathTarget.X, Is.Zero);
            Assert.That(observed.PathTarget.Z, Is.Zero);
            Assert.That(observed.PathStoppingDistance, Is.Zero);
            Assert.That(observed.Orders.Single().Kind, Is.EqualTo(OrderKind.Repair));
            Assert.That(observed.Orders.Single().TargetId, Is.EqualTo(target));
        }

        [Test, Timeout(10000)]
        public void RepairFinishesWhenTargetDiesInsteadOfPayingForMissingEntity()
        {
            var rules = Rules();
            var match = DamagedBase(rules);
            var worker = SimulationFixture.Id(match, "worker");
            var barracks = SimulationFixture.Id(match, "barracks");
            var snapshot = match.CaptureState();
            snapshot.Entities.Single(e => e.Id == barracks).Health = 500;
            match = Match.Restore(rules, snapshot);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Repair, worker, targetId: barracks).Accepted, Is.True);
            snapshot = match.CaptureState();
            snapshot.Entities.RemoveAll(e => e.Id == barracks);
            match = Match.Restore(rules, snapshot);
            SimulationFixture.Steps(match, 20);
            Assert.That(match.Find(worker).Orders, Is.Empty);
            Assert.That(match.Observe(0).Minerals, Is.EqualTo(20));
            Assert.That(match.Observe(0).Gas, Is.EqualTo(10));
        }

        [Test, Timeout(10000)]
        public void NewOrdersSurviveJsonSaveContinuationAndAcceptedCommandReplay()
        {
            var rules = Rules();
            var original = new Match(rules, false, 691);
            var worker = SimulationFixture.Id(original, "worker");
            var building = SimulationFixture.Id(original, "barracks");
            var marine = SimulationFixture.Id(original, "marine");
            // Damage through the public combat scenario, so the replay starts from the same authored rules.
            // A stationary enemy uses its real attack on the building, then is killed by our patrol.
            var spawnList = rules.Spawns.ToList();
            spawnList.Add(SimulationFixture.Spawn("marine", 1, 7, 16));
            rules.Spawns = spawnList.ToArray();
            original = new Match(rules, false, 691);
            worker = SimulationFixture.Id(original, "worker");
            building = SimulationFixture.Id(original, "barracks");
            marine = SimulationFixture.Id(original, "marine");
            // Enemy attacks the barracks at Tick 0. Hold the friendly marine until some damage exists.
            original.QueueCommand(original.MakeCommand(1, CommandKind.Attack,
                new[] { SimulationFixture.Id(original, "marine", 1) }, targetId: building));
            original.QueueCommand(original.MakeCommand(0, CommandKind.Move, new[] { marine }, SimPoint.World(15, 14)));
            original.Step();
            SimulationFixture.Steps(original, 10);
            Assert.That(original.Find(building).Health, Is.LessThan(600));
            original.QueueCommand(original.MakeCommand(0, CommandKind.Repair, new[] { worker }, targetId: building));
            original.QueueCommand(original.MakeCommand(0, CommandKind.Patrol, new[] { marine }, SimPoint.World(8, 16)));
            original.Step();
            var saved = JsonUtility.ToJson(original.CaptureState());
            var resumed = Match.Restore(rules, JsonUtility.FromJson<MatchState>(saved));
            for (var tick = 0; tick < 180; tick++)
            {
                original.Step();
                resumed.Step();
                Assert.That(resumed.StateHash(), Is.EqualTo(original.StateHash()), "New-order save divergence at " + tick);
            }
            var replay = original.ExportReplay();
            Assert.That(replay.Commands.Any(c => c.Kind == CommandKind.Patrol), Is.True);
            Assert.That(replay.Commands.Any(c => c.Kind == CommandKind.Repair), Is.True);
            var played = Match.FromReplay(rules, JsonUtility.FromJson<ReplayData>(JsonUtility.ToJson(replay)));
            Assert.That(played.StateHash(), Is.EqualTo(original.StateHash()));
        }
    }
}
