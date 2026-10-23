using System.Linq;
using ColonyRTS.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace ColonyRTS.Tests
{
    public sealed class RallyTests
    {
        [Test]
        public void NewWorkerGathersAtVisibleResourceRallyAndReplayPreservesIntent()
        {
            var rules = SimulationFixture.Rules();
            var match = new Match(rules, false, 81);
            var home = SimulationFixture.Id(match, "command");
            var worker = SimulationFixture.Id(match, "worker");
            var mine = match.Observe(0).Entities.First(e => e.DefinitionId == "mineral");
            SimulationFixture.Send(match, 0, CommandKind.Stop, worker);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Move, home, mine.Position, mine.Id).Accepted, Is.True);
            Assert.That(match.Find(home).RallyTargetId, Is.EqualTo(mine.Id));
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Train, home, definition: "worker").Accepted, Is.True);
            SimulationFixture.Until(match, () => match.Observe(0).Entities.Count(e => e.Owner == 0 && e.DefinitionId == "worker") == 2, 30);
            var created = match.Observe(0).Entities.Single(e => e.Owner == 0 && e.DefinitionId == "worker" && e.Id != worker);
            Assert.That(created.GatherTargetId, Is.EqualTo(mine.Id));
            Assert.That(created.Orders[0].Kind, Is.EqualTo(OrderKind.Gather));
            var restored = Match.Restore(rules, JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(match.CaptureState())));
            for (int i = 0; i < 80; i++) { match.Step(); restored.Step(); }
            Assert.That(restored.StateHash(), Is.EqualTo(match.StateHash()));
            Assert.That(Match.FromReplay(rules, match.ExportReplay()).StateHash(), Is.EqualTo(match.StateHash()));
        }

        [Test]
        public void GroundRallyClearsResourceTargetAndNewWorkerMovesThere()
        {
            var match = new Match(SimulationFixture.Rules(), false);
            var home = SimulationFixture.Id(match, "command");
            var mine = match.Observe(0).Entities.First(e => e.DefinitionId == "mineral");
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Move, home, mine.Position, mine.Id).Accepted, Is.True);
            var target = SimPoint.World(14, 18);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Move, home, target).Accepted, Is.True);
            Assert.That(match.Find(home).RallyTargetId, Is.Zero);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Train, home, definition: "worker").Accepted, Is.True);
            SimulationFixture.Until(match, () => match.Observe(0).Entities.Count(e => e.Owner == 0 && e.DefinitionId == "worker") == 2, 30);
            var created = match.Observe(0).Entities.Where(e => e.Owner == 0 && e.DefinitionId == "worker").OrderByDescending(e => e.Id).First();
            Assert.That(created.Orders[0].Kind, Is.EqualTo(OrderKind.Move));
            Assert.That(created.Orders[0].Target.X, Is.EqualTo(target.X));
            Assert.That(created.Orders[0].Target.Z, Is.EqualTo(target.Z));
        }

        [Test]
        public void UnseenRallyTargetIsRejectedWithoutChangingExistingRally()
        {
            var match = new Match(SimulationFixture.Rules(), false);
            var home = SimulationFixture.Id(match, "command");
            var before = match.Find(home).RallyPoint;
            var hidden = match.CaptureState().Entities.Last(e => e.DefinitionId == "mineral");
            Assert.That(match.Observe(0).Entities.Any(e => e.Id == hidden.Id), Is.False);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Move, home, hidden.Position, hidden.Id).Accepted, Is.False);
            Assert.That(match.Find(home).RallyTargetId, Is.Zero);
            Assert.That(match.Find(home).RallyPoint.X, Is.EqualTo(before.X));
            Assert.That(match.Find(home).RallyPoint.Z, Is.EqualTo(before.Z));
        }
    }
}
