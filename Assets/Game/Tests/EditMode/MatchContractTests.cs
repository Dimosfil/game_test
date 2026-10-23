using System;
using System.Linq;
using ColonyRTS.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace ColonyRTS.Tests
{
    [TestFixture]
    public sealed class MatchContractTests
    {
        [Test, Timeout(10000)]
        public void IdenticalSeedsAndInputsHaveIdenticalHashEveryTick()
        {
            var left = new Match(SimulationFixture.Rules(), true, 971);
            var right = new Match(SimulationFixture.Rules(), true, 971);
            for (var tick = 0; tick < 400; tick++)
            {
                if (tick == 0 || tick == 70)
                {
                    var target = SimPoint.World(12, 17 + tick / 70);
                    left.QueueCommand(left.MakeCommand(0, CommandKind.Move,
                        new[] { SimulationFixture.Id(left, "marine") }, target));
                    right.QueueCommand(right.MakeCommand(0, CommandKind.Move,
                        new[] { SimulationFixture.Id(right, "marine") }, target));
                }
                left.Step();
                right.Step();
                Assert.That(left.StateHash(), Is.EqualTo(right.StateHash()), "Divergence at tick " + tick);
            }
        }

        [Test]
        public void ForeignOwnershipAndMixedOwnershipCannotIssueOrders()
        {
            var match = new Match(SimulationFixture.Rules(), false);
            var own = SimulationFixture.Id(match, "marine");
            var foreign = SimulationFixture.Id(match, "marine", 1);
            var command = match.MakeCommand(0, CommandKind.Move, new[] { own, foreign }, SimPoint.World(15, 18));
            var before = match.CaptureState();
            match.QueueCommand(command);
            match.Step();
            Assert.That(match.LastResults.Single().Accepted, Is.False);
            foreach (var id in new[] { own, foreign })
            {
                Assert.That(match.Find(id).Position.X, Is.EqualTo(before.Entities.Single(e => e.Id == id).Position.X));
                Assert.That(match.Find(id).Position.Z, Is.EqualTo(before.Entities.Single(e => e.Id == id).Position.Z));
                Assert.That(match.Find(id).Orders, Is.Empty, "Rejection must be atomic for mixed selections.");
            }
        }

        [Test]
        public void DuplicateTrainingCommandChargesAndEnqueuesExactlyOnce()
        {
            var match = new Match(SimulationFixture.Rules(), false);
            var barracks = SimulationFixture.Id(match, "barracks");
            var before = match.Observe(0);
            var train = match.MakeCommand(0, CommandKind.Train, new[] { barracks }, definitionId: "marine");
            match.QueueCommand(train);
            match.QueueCommand(train);
            match.Step();
            Assert.That(match.LastResults.Count(r => r.Accepted), Is.EqualTo(1));
            Assert.That(match.LastResults.Count(r => !r.Accepted), Is.EqualTo(1));
            Assert.That(match.Find(barracks).Production.Count, Is.EqualTo(1));
            Assert.That(match.Observe(0).Minerals, Is.EqualTo(before.Minerals - 50));
            Assert.That(match.Observe(0).SupplyUsed, Is.EqualTo(before.SupplyUsed + 1));
        }

        [Test]
        public void InsufficientFundsCannotLeaveProductionOrSupplyReservation()
        {
            var rules = SimulationFixture.Rules();
            rules.StartingMinerals = 49;
            var match = new Match(rules, false);
            var barracks = SimulationFixture.Id(match, "barracks");
            var before = match.Observe(0);
            var result = SimulationFixture.Send(match, 0, CommandKind.Train, barracks, definition: "marine");
            Assert.That(result.Accepted, Is.False);
            Assert.That(match.Find(barracks).Production, Is.Empty);
            Assert.That(match.Observe(0).Minerals, Is.EqualTo(before.Minerals));
            Assert.That(match.Observe(0).SupplyUsed, Is.EqualTo(before.SupplyUsed));
        }

        [Test]
        public void TrainingReservesSupplyAndCancelRefundsLastQueuedItem()
        {
            var match = new Match(SimulationFixture.Rules(), false);
            var barracks = SimulationFixture.Id(match, "barracks");
            var before = match.Observe(0);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Train, barracks, definition: "marine").Accepted, Is.True);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Train, barracks, definition: "marine").Accepted, Is.True);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Train, barracks, definition: "marine").Accepted, Is.False,
                "Initial worker+marine and two reservations exhaust base cap 4.");
            Assert.That(match.Observe(0).Minerals, Is.EqualTo(before.Minerals - 100));
            Assert.That(match.Observe(0).SupplyUsed, Is.EqualTo(4));
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Cancel, barracks).Accepted, Is.True);
            Assert.That(match.Find(barracks).Production.Count, Is.EqualTo(1));
            Assert.That(match.Observe(0).Minerals, Is.EqualTo(before.Minerals - 50));
            Assert.That(match.Observe(0).SupplyUsed, Is.EqualTo(3));
            SimulationFixture.Steps(match, 20);
            Assert.That(match.State.Entities.Count(e => e.Owner == 0 && e.DefinitionId == "marine"), Is.EqualTo(2));
            Assert.That(match.Observe(0).SupplyUsed, Is.EqualTo(3), "Completion must not reserve supply twice.");
        }

        [Test, Timeout(10000)]
        public void MineralExhaustionCreditsOnlyDeliveredCargoExactlyOnce()
        {
            var match = new Match(SimulationFixture.Rules(), false);
            var worker = SimulationFixture.Id(match, "worker");
            var node = SimulationFixture.Id(match, "mineral", -1);
            var before = match.Observe(0).Minerals;
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Gather, worker, targetId: node).Accepted, Is.True);
            SimulationFixture.Until(match, () => match.Find(worker).Cargo > 0);
            Assert.That(match.Observe(0).Minerals, Is.EqualTo(before), "Mining itself cannot credit the player's wallet.");
            SimulationFixture.Until(match, () => match.Observe(0).Minerals == before + 17 && match.Find(worker).Cargo == 0);
            Assert.That(match.Find(node) == null || match.Find(node).ResourceRemaining == 0, Is.True);
            SimulationFixture.Steps(match, 100);
            Assert.That(match.Observe(0).Minerals, Is.EqualTo(before + 17), "Empty node cannot create minerals.");
        }

        [Test, Timeout(10000)]
        public void GasRequiresCompletedOwnedRefineryThenCreditsGasWallet()
        {
            var match = new Match(SimulationFixture.Rules(), false);
            var worker = SimulationFixture.Id(match, "worker");
            var gas = SimulationFixture.Id(match, "geyser", -1);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Gather, worker, targetId: gas).Accepted, Is.False);
            var point = match.Find(gas).Position;
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Move, worker, SimPoint.World(14, 11)).Accepted, Is.True);
            SimulationFixture.Until(match, () => match.Find(worker).Orders.Count == 0, 200);
            Assert.That(match.CanPlace("refinery", point, 0), Is.True);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Build, worker, point, definition: "refinery").Accepted, Is.True);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Gather, worker, targetId: gas).Accepted, Is.False,
                "An unfinished refinery cannot unlock gas.");
            SimulationFixture.Until(match, () => match.State.Entities.Any(e => e.DefinitionId == "refinery" && e.Owner == 0 && e.Completed));
            var beforeMinerals = match.Observe(0).Minerals;
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Gather, worker, targetId: gas).Accepted, Is.True);
            SimulationFixture.Until(match, () => match.Observe(0).Gas >= 5);
            Assert.That(match.Observe(0).Gas, Is.EqualTo(5));
            Assert.That(match.Observe(0).Minerals, Is.EqualTo(beforeMinerals), "Gas cargo must not enter the mineral wallet.");
            Assert.That(match.CanPlace("refinery", point, 0), Is.False, "Only one refinery may occupy a geyser.");
        }

        [Test]
        public void PlacementRejectsBoundsStaticTerrainAndExistingFootprints()
        {
            var match = new Match(SimulationFixture.Rules(), false);
            Assert.That(match.CanPlace("depot", SimPoint.World(0, 5), 0), Is.False);
            Assert.That(match.CanPlace("depot", SimPoint.World(48, 5), 0), Is.False);
            Assert.That(match.CanPlace("depot", SimPoint.World(21, 12), 0), Is.False);
            Assert.That(match.CanPlace("depot", SimPoint.World(5, 8), 0), Is.False);
            Assert.That(match.CanPlace("depot", SimPoint.World(11, 15), 0), Is.True);
        }

        [Test]
        public void HiddenPlacementCannotRevealWhetherUnknownFootprintIsOccupied()
        {
            var rules = SimulationFixture.Rules();
            var occupied = new Match(rules, false);
            var hiddenPoint = occupied.Find(SimulationFixture.Id(occupied, "command", 1)).Position;
            var snapshot = occupied.CaptureState();
            snapshot.Entities.Single(e => e.Owner == 1 && e.DefinitionId == "command").Position = SimPoint.World(30, 35);
            var empty = Match.Restore(rules, snapshot);
            Assert.That(occupied.CanPlace("depot", hiddenPoint, 0), Is.False);
            Assert.That(empty.CanPlace("depot", hiddenPoint, 0), Is.False,
                "A hidden empty site must return the same preview result as a hidden occupied site.");
            var occupiedResult = SimulationFixture.Send(occupied, 0, CommandKind.Build,
                SimulationFixture.Id(occupied, "worker"), hiddenPoint, definition: "depot");
            var emptyResult = SimulationFixture.Send(empty, 0, CommandKind.Build,
                SimulationFixture.Id(empty, "worker"), hiddenPoint, definition: "depot");
            Assert.That(occupiedResult.Accepted, Is.False);
            Assert.That(emptyResult.Accepted, Is.False);
            Assert.That(emptyResult.Reason, Is.EqualTo(occupiedResult.Reason), "Failure details must not be a fog oracle.");
            Assert.That(empty.Observe(0).Minerals, Is.EqualTo(500));
            Assert.That(occupied.Observe(0).Minerals, Is.EqualTo(500));
            Assert.That(empty.State.Entities.Count, Is.EqualTo(snapshot.Entities.Count));
            Assert.That(occupied.State.Entities.Count, Is.EqualTo(snapshot.Entities.Count));
        }

        [Test]
        public void InvalidBuildDoesNotSpendOrCreateBuilding()
        {
            var match = new Match(SimulationFixture.Rules(), false);
            var worker = SimulationFixture.Id(match, "worker");
            var before = match.Observe(0);
            var count = match.State.Entities.Count;
            var result = SimulationFixture.Send(match, 0, CommandKind.Build, worker,
                SimPoint.World(21, 12), definition: "depot");
            Assert.That(result.Accepted, Is.False);
            Assert.That(match.Observe(0).Minerals, Is.EqualTo(before.Minerals));
            Assert.That(match.State.Entities.Count, Is.EqualTo(count));
        }

        [Test, Timeout(10000)]
        public void BuildingCompletesThenProvidesSupply()
        {
            var match = new Match(SimulationFixture.Rules(), false);
            var worker = SimulationFixture.Id(match, "worker");
            var before = match.Observe(0);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Build, worker,
                SimPoint.World(11, 15), definition: "depot").Accepted, Is.True);
            Assert.That(match.Observe(0).Minerals, Is.EqualTo(before.Minerals - 100));
            Assert.That(match.Observe(0).SupplyCap, Is.EqualTo(before.SupplyCap));
            SimulationFixture.Until(match, () => match.State.Entities.Any(e => e.Owner == 0 && e.DefinitionId == "depot" && e.Completed));
            Assert.That(match.Observe(0).SupplyCap, Is.EqualTo(before.SupplyCap + 8));
            SimulationFixture.Steps(match, 30);
            Assert.That(match.Observe(0).SupplyCap, Is.EqualTo(before.SupplyCap + 8));
        }

        [Test]
        public void UnfinishedConstructionCancelRefundsAndReleasesFootprint()
        {
            var match = new Match(SimulationFixture.Rules(), false);
            var before = match.Observe(0);
            var point = SimPoint.World(11, 15);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Build,
                SimulationFixture.Id(match, "worker"), point, definition: "depot").Accepted, Is.True);
            var building = match.State.Entities.Single(e => e.Owner == 0 && e.DefinitionId == "depot");
            Assert.That(building.Completed, Is.False);
            Assert.That(match.CanPlace("depot", point, 0), Is.False);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Cancel, building.Id).Accepted, Is.True);
            Assert.That(match.Find(building.Id), Is.Null);
            Assert.That(match.Observe(0).Minerals, Is.EqualTo(before.Minerals));
            Assert.That(match.Observe(0).SupplyCap, Is.EqualTo(before.SupplyCap));
            Assert.That(match.CanPlace("depot", point, 0), Is.True);
        }

        [Test]
        public void ObservationDoesNotRevealUnknownEnemiesAndIsDetached()
        {
            var match = new Match(SimulationFixture.Rules(), false);
            var observation = match.Observe(0);
            var authoritativeWorker = match.Find(SimulationFixture.Id(match, "worker"));
            var originalOrderCount = authoritativeWorker.Orders.Count;
            Assert.That(observation.Entities.Any(e => e.Owner == 1), Is.False);
            Assert.That(observation.RememberedBuildings.Any(e => e.Owner == 1), Is.False);
            var workerId = SimulationFixture.Id(match, "worker");
            var worker = observation.Entities.Single(e => e.Id == workerId);
            worker.Health = -123;
            worker.Position = new SimPoint(-999999, -999999);
            worker.Orders.Add(new UnitOrder { Kind = OrderKind.Move, Target = new SimPoint(-1, -1) });
            observation.Visible[0] = !observation.Visible[0];
            observation.Explored[0] = !observation.Explored[0];
            observation.Entities.Clear();
            var fresh = match.Observe(0);
            Assert.That(match.Find(workerId).Health, Is.EqualTo(45));
            Assert.That(match.Find(workerId).Position.X, Is.EqualTo(8000));
            Assert.That(match.Find(workerId).Orders.Count, Is.EqualTo(originalOrderCount));
            Assert.That(fresh.Entities.Any(e => e.Id == workerId), Is.True);
            Assert.That(fresh.Visible[0], Is.Not.EqualTo(observation.Visible[0]));
            Assert.That(fresh.Explored[0], Is.Not.EqualTo(observation.Explored[0]));
        }

        [Test]
        public void ObservationCapacityAndOrderingDoNotRevealAdditionalHiddenEnemies()
        {
            var ordinaryRules = SimulationFixture.Rules();
            var hiddenArmyRules = SimulationFixture.Rules();
            hiddenArmyRules.Spawns = hiddenArmyRules.Spawns.Concat(Enumerable.Range(0, 100)
                .Select(i => SimulationFixture.Spawn("marine", 1, 35 + i % 10 * .5f, 29 + i / 10 * .5f))).ToArray();
            var ordinary = new Match(ordinaryRules, false);
            var hiddenArmy = new Match(hiddenArmyRules, false);
            var expected = ordinary.Observe(0);
            var observed = hiddenArmy.Observe(0);

            Assert.That(hiddenArmy.CaptureState().Entities.Count, Is.GreaterThan(observed.Entities.Count));
            Assert.That(JsonUtility.ToJson(observed), Is.EqualTo(JsonUtility.ToJson(expected)),
                "Hidden additions must not change observed entity membership, order, or public data.");
            Assert.That(observed.Entities.Capacity, Is.EqualTo(observed.Entities.Count),
                "Public backing capacity may describe only the actually observed population.");
            var opponent = hiddenArmy.Observe(1);
            Assert.That(opponent.Entities.Capacity, Is.EqualTo(opponent.Entities.Count));
            var repeated = hiddenArmy.Observe(0);
            Assert.That(JsonUtility.ToJson(repeated), Is.EqualTo(JsonUtility.ToJson(expected)));
            Assert.That(repeated.Entities.Capacity, Is.EqualTo(repeated.Entities.Count),
                "The other player's larger visible roster must not size this player's publication.");
        }

        [Test]
        public void EarlierObservationsRemainDetachedAcrossOtherPlayersStepsAndRestore()
        {
            var rules = SimulationFixture.Rules();
            var match = new Match(rules, false);
            int marine = SimulationFixture.Id(match, "marine");
            match.QueueCommand(match.MakeCommand(0, CommandKind.Move, new[] { marine }, SimPoint.World(16, 14)));
            match.Step();
            var retained = match.Observe(0);
            string retainedJson = JsonUtility.ToJson(retained);
            Assert.That(retained.Entities.Single(e => e.Id == marine).Orders.Count, Is.GreaterThan(0));

            match.Observe(1).Entities.Clear();
            var mutable = match.Observe(0);
            mutable.Entities.Single(e => e.Id == marine).Orders.Clear();
            mutable.Entities.Clear();
            Assert.That(JsonUtility.ToJson(match.Observe(0)), Is.EqualTo(retainedJson));
            for (int tick = 0; tick < 4; tick++)
            {
                match.Step();
                match.Observe(1);
                match.Observe(0);
            }
            Assert.That(JsonUtility.ToJson(retained), Is.EqualTo(retainedJson),
                "Later calls must not refill any list or mutate any entity held by an earlier consumer.");
            Assert.That(match.Find(marine).Position.X,
                Is.Not.EqualTo(retained.Entities.Single(e => e.Id == marine).Position.X));

            var restored = Match.Restore(rules, match.CaptureState());
            foreach (int player in new[] { 0, 1 })
            {
                var observed = restored.Observe(player);
                Assert.That(observed.Entities.Capacity, Is.EqualTo(observed.Entities.Count));
                Assert.That(JsonUtility.ToJson(observed), Is.EqualTo(JsonUtility.ToJson(match.Observe(player))));
            }
            Assert.That(JsonUtility.ToJson(retained), Is.EqualTo(retainedJson));
        }

        [Test]
        public void OwnObservationKeepsIssuedOrderButOmitsAuthoritativeNavigationPath()
        {
            var match = new Match(SimulationFixture.Rules(), false);
            var marine = SimulationFixture.Id(match, "marine");
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Move, marine, SimPoint.World(16, 14)).Accepted, Is.True);
            Assert.That(match.Find(marine).Path.Count, Is.GreaterThan(0), "Scenario must have an authoritative planned route.");
            var observed = match.Observe(0).Entities.Single(e => e.Id == marine);
            Assert.That(observed.Path, Is.Empty, "Authoritative pathfinding can encode hidden blockers and is diagnostic state only.");
            Assert.That(observed.PathIndex, Is.Zero);
            Assert.That(observed.Orders.Count, Is.EqualTo(1));
            Assert.That(observed.Orders[0].Kind, Is.EqualTo(OrderKind.Move));
            Assert.That(observed.Orders[0].Target.X, Is.EqualTo(16000));
            Assert.That(observed.Orders[0].Target.Z, Is.EqualTo(14000));
        }

        [Test]
        public void SnapshotFindAndRulesCannotMutateAuthoritativeMatch()
        {
            var match = new Match(SimulationFixture.Rules(), false);
            var hash = match.StateHash();
            var snapshot = match.CaptureState();
            snapshot.Entities.Clear();
            snapshot.Players[0].Minerals = -100;
            var stateView = match.State;
            stateView.Entities.Clear();
            match.Find(SimulationFixture.Id(match, "worker")).Health = -100;
            match.Rules.Definitions.Single(d => d.Id == "marine").Damage = 99999;
            Assert.That(match.StateHash(), Is.EqualTo(hash));
            Assert.That(match.Definition("marine").Damage, Is.EqualTo(6));
        }

        [Test, Timeout(10000)]
        public void JsonSaveLoadPreservesWorkingBotContinuationAndPendingInputs()
        {
            var rules = SimulationFixture.Rules();
            var original = new Match(rules, true, 777);
            SimulationFixture.Steps(original, 73);
            var queued = original.MakeCommand(0, CommandKind.Move,
                new[] { SimulationFixture.Id(original, "marine") }, SimPoint.World(12, 18));
            original.QueueCommand(queued);
            var saved = original.CaptureState();
            var json = JsonUtility.ToJson(saved);
            var resumed = Match.Restore(rules, JsonUtility.FromJson<MatchState>(json));
            Assert.That(resumed.StateHash(), Is.EqualTo(original.StateHash()));
            var botCommandsBefore = original.ExportReplay().Commands.Count(c => c.Player == 1);
            for (var i = 0; i < 400; i++)
            {
                original.Step();
                resumed.Step();
                Assert.That(resumed.StateHash(), Is.EqualTo(original.StateHash()), "Save divergence after " + i + " ticks.");
                Assert.That(JsonUtility.ToJson(resumed.CaptureState()), Is.EqualTo(JsonUtility.ToJson(original.CaptureState())),
                    "Save must preserve bot scheduling, sequence state, and queued commands as well as gameplay.");
            }
            var originalCommands = original.ExportReplay().Commands;
            Assert.That(originalCommands.Count(c => c.Player == 1), Is.GreaterThan(botCommandsBefore), "The bot must keep issuing commands after the save.");
            Assert.That(JsonUtility.ToJson(resumed.ExportReplay()), Is.EqualTo(JsonUtility.ToJson(original.ExportReplay())));
        }

        [Test, Timeout(10000)]
        public void AcceptedCommandReplayReproducesFinalGameplayHashWithoutRunningBotAgain()
        {
            var rules = SimulationFixture.Rules();
            var original = new Match(rules, true, 927);
            SimulationFixture.Send(original, 0, CommandKind.Train,
                SimulationFixture.Id(original, "barracks"), definition: "marine");
            SimulationFixture.Send(original, 0, CommandKind.Train,
                SimulationFixture.Id(original, "barracks"), definition: "nonexistent");
            SimulationFixture.Steps(original, 350);
            var replay = original.ExportReplay();
            Assert.That(replay.Commands.Any(c => c.DefinitionId == "nonexistent"), Is.False);
            Assert.That(replay.Commands.Any(c => c.Player == 1), Is.True, "Replay fixture must include bot commands.");
            var json = JsonUtility.ToJson(replay);
            var playback = Match.FromReplay(rules, JsonUtility.FromJson<ReplayData>(json));
            SimulationFixture.Until(playback, () => playback.State.Tick >= replay.FinalTick, replay.FinalTick + 1);
            Assert.That(playback.State.Tick, Is.EqualTo(original.State.Tick));
            Assert.That(playback.StateHash(), Is.EqualTo(original.StateHash()));
        }

        [Test, Timeout(10000)]
        public void InteractiveReplayStartsAtZeroAndStepsThroughRecordedCommandsOnly()
        {
            var rules = SimulationFixture.Rules();
            var original = new Match(rules, true, 913);
            SimulationFixture.Send(original, 0, CommandKind.Train,
                SimulationFixture.Id(original, "barracks"), definition: "marine");
            SimulationFixture.Steps(original, 200);
            var replay = original.ExportReplay();
            var playback = Match.BeginReplay(rules, replay);
            Assert.That(playback.Tick, Is.Zero, "The viewer needs the initial frame, before any command is executed.");
            Assert.That(playback.ExportReplay().Commands, Is.Empty);
            playback.Step();
            Assert.That(playback.Tick, Is.EqualTo(1));
            Assert.That(playback.Find(SimulationFixture.Id(playback, "barracks")).Production.Count, Is.EqualTo(1));
            SimulationFixture.Steps(playback, replay.FinalTick - 1);
            Assert.That(playback.StateHash(), Is.EqualTo(original.StateHash()));
            Assert.That(JsonUtility.ToJson(playback.ExportReplay()), Is.EqualTo(JsonUtility.ToJson(replay)),
                "Playback must execute the recorded bot commands exactly once and generate no new bot input.");
        }

        [Test]
        public void RestoreAndReplayRejectChangedRulesEvenIfVersionLabelMatches()
        {
            var match = new Match(SimulationFixture.Rules(), false);
            var changed = SimulationFixture.Rules();
            changed.Definitions.Single(d => d.Id == "marine").Damage++;
            Assert.That(() => Match.Restore(changed, match.CaptureState()), Throws.Exception);
            Assert.That(() => Match.FromReplay(changed, match.ExportReplay()), Throws.Exception);
        }

        [Test]
        public void RestoreRejectsDifferentVersionAndCorruptRuleHash()
        {
            var rules = SimulationFixture.Rules();
            var match = new Match(rules, false);
            var snapshot = match.CaptureState();
            snapshot.RuleVersion = "some-other-version";
            Assert.That(() => Match.Restore(rules, snapshot), Throws.Exception);
            snapshot = match.CaptureState();
            snapshot.RuleHash = "corrupt-hash";
            Assert.That(() => Match.Restore(rules, snapshot), Throws.Exception);
        }

        [Test]
        public void RestoreRejectsMalformedNestedObservationMemory()
        {
            var rules = SimulationFixture.Rules();
            var match = new Match(rules, false);
            var snapshot = match.CaptureState();
            snapshot.Players[0].RememberedBuildings.Add(null);
            Assert.That(() => Match.Restore(rules, snapshot), Throws.Exception,
                "Corrupt saves must fail at restore, before Observe or the next Step can crash.");
        }

        [Test]
        public void InvalidCatalogIsRejectedBeforeMatchStarts()
        {
            var rules = SimulationFixture.Rules();
            rules.Definitions[1].Id = rules.Definitions[0].Id;
            Assert.That(() => new Match(rules, false), Throws.Exception);
            rules = SimulationFixture.Rules();
            rules.Definitions.Single(d => d.Id == "marine").ProducerId = "missing-producer";
            Assert.That(() => new Match(rules, false), Throws.Exception);
            rules = SimulationFixture.Rules();
            rules.MapWidth = 0;
            Assert.That(() => new Match(rules, false), Throws.Exception);
        }

        [Test, Timeout(10000)]
        public void MovementRoutesAroundTerrainWithoutCrossingBlockedCells()
        {
            var match = new Match(SimulationFixture.Rules(), false);
            var marine = SimulationFixture.Id(match, "marine");
            var target = SimPoint.World(30, 14);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Move, marine, target).Accepted, Is.True);
            var reached = false;
            for (var i = 0; i < 700 && !reached; i++)
            {
                match.Step();
                var p = match.Find(marine).Position;
                Assert.That(p.X >= 20000 && p.X < 22000 && p.Z >= 0 && p.Z < 28000,
                    Is.False, "Movement crossed impassable terrain at " + p.X + "," + p.Z);
                reached = Math.Abs(p.X - target.X) < 300 && Math.Abs(p.Z - target.Z) < 300;
            }
            Assert.That(reached, Is.True, "A reachable destination around the north end of the wall must finish.");
        }

        [Test, Timeout(10000)]
        public void MovementToBlockedDestinationStopsAtReachableBoundary()
        {
            var match = new Match(SimulationFixture.Rules(), false);
            var marine = SimulationFixture.Id(match, "marine");
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Move, marine, SimPoint.World(21, 14)).Accepted, Is.True);
            SimulationFixture.Until(match, () => match.Find(marine).Orders.Count == 0, 300);
            var stopped = match.Find(marine).Position;
            Assert.That(stopped.X >= 20000 && stopped.X < 22000 && stopped.Z >= 0 && stopped.Z < 28000, Is.False);
            SimulationFixture.Steps(match, 40);
            Assert.That(match.Find(marine).Position.X, Is.EqualTo(stopped.X));
            Assert.That(match.Find(marine).Position.Z, Is.EqualTo(stopped.Z));
        }

        [Test, Timeout(10000)]
        public void NewFoundationInvalidatesWarmNavigationAndReroutesExistingMove()
        {
            var match = new Match(SimulationFixture.Rules(), false);
            var marine = SimulationFixture.Id(match, "marine");
            var destination = SimPoint.World(16, 14);
            // Cell-centre tie-breaking routes this move along Z=13.5. Place the
            // new footprint across that row, ahead of the moving marine.
            var foundation = SimPoint.World(11, 14);
            const long combinedRadiusSquared = 1350L * 1350L;
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Move, marine, destination).Accepted, Is.True);
            Assert.That(match.Find(marine).Path.Any(p =>
            {
                long dx = p.X - foundation.X, dz = p.Z - foundation.Z;
                return dx * dx + dz * dz < combinedRadiusSquared;
            }), Is.True, "This scenario must place the foundation across an already planned route.");
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Build,
                SimulationFixture.Id(match, "worker"), foundation, definition: "depot").Accepted, Is.True);
            var reached = false;
            for (var i = 0; i < 300 && !reached; i++)
            {
                var p = match.Find(marine).Position;
                long dx = p.X - foundation.X, dz = p.Z - foundation.Z;
                Assert.That(dx * dx + dz * dz, Is.GreaterThanOrEqualTo(combinedRadiusSquared),
                    "A cached route cannot cross the newly occupied footprint.");
                reached = Math.Abs(p.X - destination.X) < 300 && Math.Abs(p.Z - destination.Z) < 300;
                if (!reached) match.Step();
            }
            Assert.That(reached, Is.True, "The old route must replan around the new structure instead of retrying stale walkability forever.");
        }

        [Test]
        public void HiddenEnemyCannotBeAttackedByGuessingItsId()
        {
            var match = new Match(SimulationFixture.Rules(), false);
            var marine = SimulationFixture.Id(match, "marine");
            var enemy = SimulationFixture.Id(match, "marine", 1);
            var before = match.Find(enemy);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Attack, marine, targetId: enemy).Accepted, Is.False);
            Assert.That(match.Find(enemy).Health, Is.EqualTo(before.Health));
            Assert.That(match.Find(marine).Orders, Is.Empty);
        }

        [Test, Timeout(10000)]
        public void ExploredFogRemembersBuildingWithoutUpdatingItsHiddenHealth()
        {
            var rules = SimulationFixture.Rules();
            rules.Spawns.Single(s => s.DefinitionId == "command" && s.Owner == 1).Position = SimPoint.World(14, 23);
            rules.Spawns.Single(s => s.DefinitionId == "marine" && s.Owner == 0).Position = SimPoint.World(14, 18);
            var match = new Match(rules, false);
            var scout = SimulationFixture.Id(match, "marine");
            var enemyBase = SimulationFixture.Id(match, "command", 1);
            var seen = match.Observe(0).Entities.Single(e => e.Id == enemyBase);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Move, scout, SimPoint.World(8, 14)).Accepted, Is.True);
            SimulationFixture.Until(match, () => !match.Observe(0).Entities.Any(e => e.Id == enemyBase), 200);
            var observation = match.Observe(0);
            var index = 23 * rules.MapWidth + 14;
            Assert.That(observation.Visible[index], Is.False);
            Assert.That(observation.Explored[index], Is.True);
            Assert.That(observation.RememberedBuildings.Single(e => e.Id == enemyBase).Health, Is.EqualTo(seen.Health));
            var state = match.CaptureState();
            state.Entities.Single(e => e.Id == enemyBase).Health = 1;
            match = Match.Restore(rules, state);
            SimulationFixture.Steps(match, 5);
            var memory = match.Observe(0).RememberedBuildings.Single(e => e.Id == enemyBase);
            Assert.That(memory.Health, Is.EqualTo(seen.Health), "A hidden health change must not refresh last-known data.");
            memory.Health = -50;
            Assert.That(match.Observe(0).RememberedBuildings.Single(e => e.Id == enemyBase).Health, Is.EqualTo(seen.Health));
        }

        [Test]
        public void StimChargesHealthOnceRejectsRecastAndExpires()
        {
            var rules = SimulationFixture.Rules();
            var match = new Match(rules, false);
            var marine = SimulationFixture.Id(match, "marine");
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Stim, marine).Accepted, Is.True);
            Assert.That(match.Find(marine).Health, Is.EqualTo(40 - rules.StimHealthCost));
            Assert.That(match.Find(marine).StimRemaining, Is.GreaterThan(0));
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Stim, marine).Accepted, Is.False);
            Assert.That(match.Find(marine).Health, Is.EqualTo(40 - rules.StimHealthCost));
            SimulationFixture.Steps(match, rules.StimDurationTicks);
            Assert.That(match.Find(marine).StimRemaining, Is.Zero);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Stim, marine).Accepted, Is.True);
            Assert.That(match.Find(marine).Health, Is.EqualTo(40 - 2 * rules.StimHealthCost));
        }

        [Test]
        public void StimCannotKillItsCasterOrApplyToWorkers()
        {
            var rules = SimulationFixture.Rules();
            var match = new Match(rules, false);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Stim,
                SimulationFixture.Id(match, "worker")).Accepted, Is.False);
            var state = match.CaptureState();
            var marine = state.Entities.Single(e => e.Owner == 0 && e.DefinitionId == "marine");
            marine.Health = rules.StimHealthCost;
            match = Match.Restore(rules, state);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Stim, marine.Id).Accepted, Is.False);
            Assert.That(match.Find(marine.Id).Health, Is.EqualTo(rules.StimHealthCost));
            Assert.That(match.Find(marine.Id).StimRemaining, Is.Zero);
        }

        [Test, Timeout(10000)]
        public void VisibleAttackDestroysBaseAndFinishesMatch()
        {
            var rules = SimulationFixture.Rules();
            rules.Spawns.Single(s => s.DefinitionId == "marine" && s.Owner == 0).Position = SimPoint.World(37, 29);
            rules.Spawns = rules.Spawns.Where(s => !(s.Owner == 1 && s.DefinitionId != "command")).ToArray();
            rules.Definitions.Single(d => d.Id == "command").MaxHealth = 18;
            var match = new Match(rules, false);
            var enemyBase = SimulationFixture.Id(match, "command", 1);
            Assert.That(match.Observe(0).Entities.Any(e => e.Id == enemyBase), Is.True);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Attack,
                SimulationFixture.Id(match, "marine"), targetId: enemyBase).Accepted, Is.True);
            SimulationFixture.Until(match, () => match.State.Phase == MatchPhase.Finished, 200);
            Assert.That(match.State.Winner, Is.EqualTo(0));
            Assert.That(match.Find(enemyBase), Is.Null);
            var hash = match.StateHash();
            SimulationFixture.Steps(match, 10);
            Assert.That(match.StateHash(), Is.EqualTo(hash), "Finished simulation must stop advancing.");
        }
    }
}
