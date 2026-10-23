using System;
using System.Linq;
using ColonyRTS.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace ColonyRTS.Tests
{
    public sealed class ProductionRestoreTests
    {
        static Match PaidQueue(RuleSet rules)
        {
            var match = new Match(rules, false);
            int producer = SimulationFixture.Id(match, "barracks");
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Train, producer, definition: "marine").Accepted, Is.True);
            return match;
        }

        [TestCase("worker")]
        [TestCase("neutral")]
        [TestCase("incomplete")]
        [TestCase("wrong-producer")]
        [TestCase("building")]
        [TestCase("resource")]
        [TestCase("free")]
        [TestCase("inflated-refund")]
        [TestCase("gas")]
        [TestCase("duration")]
        [TestCase("negative-remaining")]
        [TestCase("remaining-over-total")]
        [TestCase("queued-progress")]
        [TestCase("null")]
        public void CorruptProductionIsRejectedBeforeItCanSpawnOrRefund(string corruption)
        {
            var rules = SimulationFixture.Rules();
            var original = PaidQueue(rules);
            string originalHash = original.StateHash();
            var saved = original.CaptureState();
            var producer = saved.Entities.Single(e => e.Owner == 0 && e.DefinitionId == "barracks");
            var item = producer.Production[0];
            switch (corruption)
            {
                case "worker":
                    producer.Production.Clear();
                    saved.Entities.Single(e => e.Owner == 0 && e.DefinitionId == "worker").Production.Add(item);
                    break;
                case "neutral": producer.Owner = -1; break;
                case "incomplete": producer.Completed = false; break;
                case "wrong-producer": item.DefinitionId = "worker"; break;
                case "building": item.DefinitionId = "depot"; break;
                case "resource": item.DefinitionId = "mineral"; break;
                case "free": item.PaidMinerals = 0; break;
                case "inflated-refund": item.PaidMinerals++; break;
                case "gas": item.PaidGas++; break;
                case "duration": item.TotalTicks = item.RemainingTicks = 1; break;
                case "negative-remaining": item.RemainingTicks = -1; break;
                case "remaining-over-total": item.RemainingTicks = item.TotalTicks + 1; break;
                case "queued-progress":
                    producer.Production.Add(new ProductionItem { DefinitionId = "marine", TotalTicks = 15, RemainingTicks = 14, PaidMinerals = 50 });
                    break;
                case "null": producer.Production[0] = null; break;
            }
            var json = JsonUtility.ToJson(saved);
            Assert.That(() => Match.Restore(rules, JsonUtility.FromJson<MatchState>(json)), Throws.ArgumentException);
            Assert.That(original.StateHash(), Is.EqualTo(originalHash), "A failed restore cannot mutate the active match.");
        }

        [Test]
        public void LegalPaidQueueContinuesThroughJsonAndAcceptedCommandReplay()
        {
            var rules = SimulationFixture.Rules();
            var original = PaidQueue(rules);
            int producer = SimulationFixture.Id(original, "barracks");
            Assert.That(SimulationFixture.Send(original, 0, CommandKind.Train, producer, definition: "marine").Accepted, Is.True);
            var saved = original.CaptureState();
            var resumed = Match.Restore(rules, JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(saved)));
            for (int tick = 0; tick < 40; tick++)
            {
                original.Step(); resumed.Step();
                Assert.That(resumed.StateHash(), Is.EqualTo(original.StateHash()));
            }
            Assert.That(original.CaptureState().Entities.Count(e => e.Owner == 0 && e.DefinitionId == "marine"), Is.EqualTo(3));
            Assert.That(Match.FromReplay(rules, original.ExportReplay()).StateHash(), Is.EqualTo(original.StateHash()));
        }

        [Test]
        public void CompletedHeadWaitingForSpaceRestoresWithoutRechargingOrChangingItsRefund()
        {
            var rules = SimulationFixture.Rules();
            var original = PaidQueue(rules);
            var saved = original.CaptureState();
            var producer = saved.Entities.Single(e => e.Owner == 0 && e.DefinitionId == "barracks");
            producer.Production[0].RemainingTicks = 0;
            int minerals = saved.Players[0].Minerals;
            var restored = Match.Restore(rules, JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(saved)));
            Assert.That(restored.CaptureState().Players[0].Minerals, Is.EqualTo(minerals));
            Assert.That(SimulationFixture.Send(restored, 0, CommandKind.Cancel, producer.Id).Accepted, Is.True);
            Assert.That(restored.CaptureState().Players[0].Minerals, Is.EqualTo(minerals + 50));
        }
    }
}
