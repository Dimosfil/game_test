using System;
using System.Collections.Generic;
using System.Linq;
using ColonyRTS.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace ColonyRTS.Tests
{
    [TestFixture]
    public sealed class ResearchTests
    {
        private static RuleSet Rules(bool depot = false, bool secondProducer = false, bool enemyProducer = false)
        {
            var rules = SimulationFixture.Rules();
            rules.VersionId = "qa-research-v1";
            rules.StartingMinerals = 500;
            rules.StartingGas = 150;
            rules.Definitions.Single(definition => definition.Id == "marine").Armor = 1;
            rules.Spawns = rules.Spawns.Where(spawn => spawn.Owner >= 0 && spawn.DefinitionId != "worker").ToArray();
            var spawns = rules.Spawns.ToList();
            if (depot) spawns.Add(SimulationFixture.Spawn("depot", 0, 12, 20));
            if (secondProducer) spawns.Add(SimulationFixture.Spawn("barracks", 0, 12, 22));
            if (enemyProducer) spawns.Add(SimulationFixture.Spawn("barracks", 1, 14, 14));
            rules.Spawns = spawns.ToArray();
            rules.Upgrades = new[]
            {
                new UpgradeDefinition { Id = "weapons-1", DisplayName = "Weapons 1", ProducerId = "barracks",
                    PrerequisiteId = "command", MineralCost = 75, GasCost = 25, ResearchTicks = 8,
                    DamageBonus = 2, AffectedDefinitionIds = new[] { "marine" } },
                new UpgradeDefinition { Id = "weapons-2", DisplayName = "Weapons 2", ProducerId = "barracks",
                    PrerequisiteId = "depot", PrerequisiteUpgradeId = "weapons-1", MineralCost = 100, GasCost = 50,
                    ResearchTicks = 10, DamageBonus = 3, AffectedDefinitionIds = new[] { "marine" } },
                new UpgradeDefinition { Id = "armor-1", DisplayName = "Armor 1", ProducerId = "barracks",
                    MineralCost = 60, GasCost = 20, ResearchTicks = 8, ArmorBonus = 8,
                    AffectedDefinitionIds = new[] { "marine" } }
            };
            return rules;
        }

        private static int Producer(Match match, int player = 0) =>
            match.CaptureState().Entities.First(entity => entity.Owner == player && entity.DefinitionId == "barracks").Id;

        private static CommandResult Research(Match match, int producer, string upgrade, int player = 0) =>
            SimulationFixture.Send(match, player, CommandKind.Research, producer, definition: upgrade);

        private static void Complete(Match match, int producer, string upgrade, int player = 0)
        {
            Assert.That(Research(match, producer, upgrade, player).Accepted, Is.True);
            SimulationFixture.Until(match, () => match.Observe(player).CompletedUpgrades.Contains(upgrade), 120);
        }

        [Test]
        public void AvailabilityIsNonmutatingAndMatchesSuccessfulResearchTransaction()
        {
            var match = new Match(Rules(), false, 91);
            var producer = Producer(match);
            var before = JsonUtility.ToJson(match.CaptureState());
            Assert.That(match.ResearchAvailability(0, producer, "weapons-1").Accepted, Is.True);
            Assert.That(JsonUtility.ToJson(match.CaptureState()), Is.EqualTo(before), "A UI availability query cannot reserve, charge or issue sequence numbers.");
            Assert.That(Research(match, producer, "weapons-1").Accepted, Is.True);
            Assert.That(match.Observe(0).Minerals, Is.EqualTo(425));
            Assert.That(match.Observe(0).Gas, Is.EqualTo(125));
            Assert.That(match.Find(producer).Research.Single().DefinitionId, Is.EqualTo("weapons-1"));
            Assert.That(match.Find(producer).Production, Is.Empty);
            Assert.That(match.CombatDamage(0, "marine"), Is.EqualTo(6), "Payment cannot grant effects before completion.");
            SimulationFixture.Until(match, () => match.Observe(0).CompletedUpgrades.Contains("weapons-1"), 20);
            Assert.That(match.Find(producer).Research, Is.Empty);
            Assert.That(match.CombatDamage(0, "marine"), Is.EqualTo(8));
            Assert.That(Research(match, producer, "weapons-1").Accepted, Is.False);
            Assert.That(match.Observe(0).CompletedUpgrades.Count(id => id == "weapons-1"), Is.EqualTo(1));
            Assert.That(match.Observe(0).Minerals, Is.EqualTo(425));
            Assert.That(match.Observe(0).Gas, Is.EqualTo(125));
        }

        [Test]
        public void ChainedUpgradeRequiresBothPreviousUpgradeAndCompletedBuilding()
        {
            var missingBuilding = new Match(Rules(), false);
            var producer = Producer(missingBuilding);
            Complete(missingBuilding, producer, "weapons-1");
            Assert.That(missingBuilding.ResearchAvailability(0, producer, "weapons-2").Accepted, Is.False);
            Assert.That(Research(missingBuilding, producer, "weapons-2").Accepted, Is.False);
            Assert.That(missingBuilding.Observe(0).Minerals, Is.EqualTo(425));
            var withBuilding = new Match(Rules(depot: true), false);
            producer = Producer(withBuilding);
            Assert.That(withBuilding.ResearchAvailability(0, producer, "weapons-2").Accepted, Is.False,
                "A completed tech building alone cannot skip the preceding research level.");
            Complete(withBuilding, producer, "weapons-1");
            Complete(withBuilding, producer, "weapons-2");
            Assert.That(withBuilding.CombatDamage(0, "marine"), Is.EqualTo(11), "Explicit one-time chain bonuses are additive.");
            Assert.That(withBuilding.Observe(0).Minerals, Is.EqualTo(325));
            Assert.That(withBuilding.Observe(0).Gas, Is.EqualTo(75));
        }

        [TestCase(74, 150)]
        [TestCase(500, 24)]
        public void CostRefusalDoesNotReserveSpendOrChangeProductionSupply(int minerals, int gas)
        {
            var rules = Rules();
            rules.StartingMinerals = minerals;
            rules.StartingGas = gas;
            var match = new Match(rules, false);
            var producer = Producer(match);
            var supply = match.Observe(0).SupplyUsed;
            Assert.That(match.ResearchAvailability(0, producer, "weapons-1").Accepted, Is.False);
            Assert.That(Research(match, producer, "weapons-1").Accepted, Is.False);
            Assert.That(match.Find(producer).Research, Is.Empty);
            Assert.That(match.Find(producer).Production, Is.Empty);
            Assert.That(match.Observe(0).CompletedUpgrades, Is.Empty);
            Assert.That(match.Observe(0).Minerals, Is.EqualTo(minerals));
            Assert.That(match.Observe(0).Gas, Is.EqualTo(gas));
            Assert.That(match.Observe(0).SupplyUsed, Is.EqualTo(supply));
        }

        [Test]
        public void DuplicateReservationsAreOwnerWideAndCancelRefundsThenUnlocksRetry()
        {
            var match = new Match(Rules(secondProducer: true), false);
            var producers = match.CaptureState().Entities.Where(entity => entity.Owner == 0 && entity.DefinitionId == "barracks").Select(entity => entity.Id).ToArray();
            Assert.That(Research(match, producers[0], "weapons-1").Accepted, Is.True);
            Assert.That(Research(match, producers[0], "armor-1").Accepted, Is.False, "The occupied producer has only one research slot.");
            Assert.That(Research(match, producers[1], "weapons-1").Accepted, Is.False, "Another producer cannot research the reserved same-owner upgrade.");
            Assert.That(match.Find(producers[0]).Research.Count, Is.EqualTo(1));
            Assert.That(match.Find(producers[1]).Research, Is.Empty);
            Assert.That(match.Observe(0).Minerals, Is.EqualTo(425));
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Cancel, producers[0]).Accepted, Is.True);
            Assert.That(match.Observe(0).Minerals, Is.EqualTo(500));
            Assert.That(match.Observe(0).Gas, Is.EqualTo(150));
            Assert.That(match.Find(producers[0]).Research, Is.Empty);
            Complete(match, producers[1], "weapons-1");
            Assert.That(match.CombatDamage(0, "marine"), Is.EqualTo(8));
        }

        [Test]
        public void OpponentsCanIndependentlyResearchTheSameUpgrade()
        {
            var match = new Match(Rules(enemyProducer: true), false);
            var own = Producer(match);
            var enemy = Producer(match, 1);
            match.QueueCommand(match.MakeCommand(0, CommandKind.Research, new[] { own }, definitionId: "weapons-1"));
            match.QueueCommand(match.MakeCommand(1, CommandKind.Research, new[] { enemy }, definitionId: "weapons-1"));
            match.Step();
            Assert.That(match.LastResults.Count, Is.EqualTo(2));
            Assert.That(match.LastResults.All(result => result.Accepted), Is.True,
                "A reservation belongs to its owner, not to the whole match.");
            SimulationFixture.Steps(match, 10);
            foreach (var player in new[] { 0, 1 })
            {
                Assert.That(match.Observe(player).CompletedUpgrades, Is.EquivalentTo(new[] { "weapons-1" }));
                Assert.That(match.Observe(player).Minerals, Is.EqualTo(425));
                Assert.That(match.Observe(player).Gas, Is.EqualTo(125));
                Assert.That(match.CombatDamage(player, "marine"), Is.EqualTo(8));
            }
        }

        [Test]
        public void TrainingAndResearchCannotUseProducerAtTheSameTime()
        {
            var match = new Match(Rules(), false);
            var producer = Producer(match);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Train, producer, definition: "marine").Accepted, Is.True);
            var wallet = match.Observe(0).Minerals;
            Assert.That(match.ResearchAvailability(0, producer, "weapons-1").Accepted, Is.False);
            Assert.That(Research(match, producer, "weapons-1").Accepted, Is.False);
            Assert.That(match.Find(producer).Production.Count, Is.EqualTo(1));
            Assert.That(match.Find(producer).Research, Is.Empty);
            Assert.That(match.Observe(0).Minerals, Is.EqualTo(wallet));
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Cancel, producer).Accepted, Is.True);
            Assert.That(Research(match, producer, "weapons-1").Accepted, Is.True);
            var supply = match.Observe(0).SupplyUsed;
            wallet = match.Observe(0).Minerals;
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Train, producer, definition: "marine").Accepted, Is.False);
            Assert.That(match.Find(producer).Production, Is.Empty);
            Assert.That(match.Find(producer).Research.Count, Is.EqualTo(1));
            Assert.That(match.Observe(0).Minerals, Is.EqualTo(wallet));
            Assert.That(match.Observe(0).SupplyUsed, Is.EqualTo(supply));
        }

        [Test]
        public void WrongOwnerAndMultipleProducersAreRejectedAtomically()
        {
            var match = new Match(Rules(secondProducer: true, enemyProducer: true), false);
            var enemy = Producer(match, 1);
            Assert.That(match.ResearchAvailability(0, enemy, "weapons-1").Accepted, Is.False);
            Assert.That(Research(match, enemy, "weapons-1").Accepted, Is.False);
            var own = match.CaptureState().Entities.Where(entity => entity.Owner == 0 && entity.DefinitionId == "barracks").Select(entity => entity.Id).ToArray();
            match.QueueCommand(match.MakeCommand(0, CommandKind.Research, own, definitionId: "weapons-1"));
            match.Step();
            Assert.That(match.LastResults.Single().Accepted, Is.False, "Research must explicitly choose exactly one producer.");
            Assert.That(own.All(id => match.Find(id).Research.Count == 0), Is.True);
            Assert.That(match.Find(enemy).Research, Is.Empty);
            Assert.That(match.Observe(0).Minerals, Is.EqualTo(500));
            Assert.That(match.Observe(1).Minerals, Is.EqualTo(500));
        }

        [Test]
        public void DestroyedProducerLosesPaidCostAndProgressButAllowsAnotherProducerToRetry()
        {
            var rules = Rules(secondProducer: true);
            rules.Definitions = rules.Definitions.Concat(new[]
            {
                new EntityDefinition { Id = "raider", MaxHealth = 1000, Radius = 350, Speed = 1000,
                    Sight = 12000, Damage = 1000, AttackRange = 10000, AttackCooldown = 100, BuildTicks = 1 }
            }).ToArray();
            rules.Spawns = rules.Spawns.Concat(new[] { SimulationFixture.Spawn("raider", 1, 12, 14) }).ToArray();
            var match = new Match(rules, false);
            var producers = match.CaptureState().Entities.Where(entity => entity.Owner == 0 && entity.DefinitionId == "barracks").Select(entity => entity.Id).ToArray();
            var raider = SimulationFixture.Id(match, "raider", 1);
            match.QueueCommand(match.MakeCommand(0, CommandKind.Research, new[] { producers[0] }, definitionId: "weapons-1"));
            match.QueueCommand(match.MakeCommand(1, CommandKind.Attack, new[] { raider }, targetId: producers[0]));
            match.Step();
            Assert.That(match.LastResults.All(result => result.Accepted), Is.True);
            Assert.That(match.Find(producers[0]), Is.Null, "The real combat/death path must remove the researching producer.");
            Assert.That(match.Observe(0).Minerals, Is.EqualTo(425), "Death is not cancellation and cannot refund research.");
            Assert.That(match.Observe(0).Gas, Is.EqualTo(125));
            Assert.That(match.Observe(0).CompletedUpgrades, Is.Empty);
            Assert.That(match.ResearchAvailability(0, producers[1], "weapons-1").Accepted, Is.True);
            Complete(match, producers[1], "weapons-1");
            Assert.That(match.Observe(0).Minerals, Is.EqualTo(350));
            Assert.That(match.Observe(0).Gas, Is.EqualTo(100));
        }

        [Test]
        public void DamageArmorAndMinimumOneApplyToExistingAndNewMatchingUnitsOnly()
        {
            var rules = Rules();
            foreach (var upgrade in rules.Upgrades) upgrade.ResearchTicks = 1;
            var match = new Match(rules, false);
            var producer = Producer(match);
            Complete(match, producer, "weapons-1");
            Complete(match, producer, "armor-1");
            Assert.That(match.CombatDamage(0, "marine"), Is.EqualTo(8));
            Assert.That(match.CombatArmor(0, "marine"), Is.EqualTo(9));
            Assert.That(match.CombatDamage(1, "marine"), Is.EqualTo(6));
            Assert.That(match.CombatArmor(1, "marine"), Is.EqualTo(1));
            Assert.That(match.CombatDamage(0, "worker"), Is.EqualTo(3));
            Assert.That(match.CombatArmor(0, "worker"), Is.Zero);
            var oldMarine = SimulationFixture.Id(match, "marine");
            var enemy = SimulationFixture.Id(match, "marine", 1);
            var snapshot = match.CaptureState();
            snapshot.Entities.Single(entity => entity.Id == enemy).Position = SimPoint.World(12, 14);
            match = Match.Restore(rules, snapshot);
            match.Step();
            Assert.That(match.Find(enemy).Health, Is.EqualTo(33), "8 damage minus the other owner's base armor 1.");
            Assert.That(match.Find(oldMarine).Health, Is.EqualTo(39), "Enemy damage 6 versus armor 9 must still deal exactly 1.");
            snapshot = match.CaptureState();
            snapshot.Entities.Single(entity => entity.Id == enemy).Position = SimPoint.World(38, 26);
            match = Match.Restore(rules, snapshot);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Train, producer, definition: "marine").Accepted, Is.True);
            SimulationFixture.Until(match, () => match.CaptureState().Entities.Count(entity => entity.Owner == 0 && entity.DefinitionId == "marine") == 2, 30);
            var freshId = match.CaptureState().Entities.Single(entity => entity.Owner == 0 && entity.DefinitionId == "marine" && entity.Id != oldMarine).Id;
            snapshot = match.CaptureState();
            snapshot.Entities.Single(entity => entity.Id == oldMarine).Position = SimPoint.World(8, 24);
            var fresh = snapshot.Entities.Single(entity => entity.Id == freshId);
            fresh.Position = SimPoint.World(8, 14); fresh.AttackTimer = 0;
            var hostile = snapshot.Entities.Single(entity => entity.Id == enemy);
            hostile.Position = SimPoint.World(12, 14); hostile.Health = 40; hostile.AttackTimer = 0;
            match = Match.Restore(rules, snapshot);
            match.Step();
            Assert.That(match.Find(enemy).Health, Is.EqualTo(33), "A matching unit trained after completion inherits owner-wide damage.");
            Assert.That(match.Find(freshId).Health, Is.EqualTo(39), "A newly trained unit also inherits owner-wide armor.");
        }

        [Test]
        public void ObservationAndCatalogAreDetachedAndHideEnemyResearchAndCompletedUpgrades()
        {
            var match = new Match(Rules(enemyProducer: true), false);
            var own = Producer(match);
            var enemy = Producer(match, 1);
            match.QueueCommand(match.MakeCommand(0, CommandKind.Research, new[] { own }, definitionId: "weapons-1"));
            match.QueueCommand(match.MakeCommand(1, CommandKind.Research, new[] { enemy }, definitionId: "armor-1"));
            match.Step();
            Assert.That(match.LastResults.All(result => result.Accepted), Is.True);
            var observation = match.Observe(0);
            Assert.That(observation.Entities.Single(entity => entity.Id == enemy).Research, Is.Empty,
                "Even a currently visible enemy producer cannot reveal its private research queue.");
            var ownRead = observation.Entities.Single(entity => entity.Id == own);
            Assert.That(ownRead.Research.Count, Is.EqualTo(1));
            ownRead.Research[0].PaidMinerals = 99999;
            ownRead.Research.Clear();
            observation.CompletedUpgrades.Add("armor-1");
            var catalogRead = match.Upgrade("weapons-1");
            catalogRead.DamageBonus = 999;
            catalogRead.AffectedDefinitionIds[0] = "worker";
            Assert.That(match.Find(own).Research.Single().PaidMinerals, Is.EqualTo(75));
            Assert.That(match.Observe(0).CompletedUpgrades, Is.Empty);
            Assert.That(match.Upgrade("weapons-1").DamageBonus, Is.EqualTo(2));
            Assert.That(match.Upgrade("weapons-1").AffectedDefinitionIds.Single(), Is.EqualTo("marine"));
            SimulationFixture.Steps(match, 10);
            Assert.That(match.Observe(0).CompletedUpgrades, Is.EquivalentTo(new[] { "weapons-1" }));
            Assert.That(match.Observe(1).CompletedUpgrades, Is.EquivalentTo(new[] { "armor-1" }));
            var completedRead = match.Observe(0).CompletedUpgrades;
            completedRead.Clear();
            Assert.That(match.CombatDamage(0, "marine"), Is.EqualTo(8));
            Assert.That(match.Observe(0).CompletedUpgrades, Is.EquivalentTo(new[] { "weapons-1" }));
        }

        [Test, Timeout(15000)]
        public void MidResearchJsonSavePreservesWorkingAiAndAcceptedReplay()
        {
            var rules = Rules();
            rules.Upgrades[0].ResearchTicks = 70;
            var original = new Match(rules, true, 965);
            Assert.That(Research(original, Producer(original), "weapons-1").Accepted, Is.True);
            SimulationFixture.Steps(original, 14);
            Assert.That(original.Find(Producer(original)).Research.Single().RemainingTicks, Is.GreaterThan(0));
            var aiBefore = original.ExportReplay().Commands.Count(command => command.Player == 1);
            var snapshot = JsonUtility.ToJson(original.CaptureState());
            var resumed = Match.Restore(rules, JsonUtility.FromJson<MatchState>(snapshot));
            for (var tick = 0; tick < 160; tick++)
            {
                original.Step(); resumed.Step();
                Assert.That(resumed.StateHash(), Is.EqualTo(original.StateHash()), "Mid-research AI save divergence at " + tick);
            }
            Assert.That(original.Observe(0).CompletedUpgrades, Does.Contain("weapons-1"));
            Assert.That(original.ExportReplay().Commands.Count(command => command.Player == 1), Is.GreaterThan(aiBefore));
            Assert.That(JsonUtility.ToJson(resumed.ExportReplay()), Is.EqualTo(JsonUtility.ToJson(original.ExportReplay())));
            var replay = Match.FromReplay(rules, JsonUtility.FromJson<ReplayData>(JsonUtility.ToJson(original.ExportReplay())));
            Assert.That(replay.StateHash(), Is.EqualTo(original.StateHash()));
        }

        [Test]
        public void BuildingPrerequisiteIsRequiredAtStartButCanBeLostDuringResearchAndSave()
        {
            var rules = Rules(depot: true);
            var match = new Match(rules, false);
            var producer = Producer(match);
            Complete(match, producer, "weapons-1");
            Assert.That(Research(match, producer, "weapons-2").Accepted, Is.True);
            var snapshot = match.CaptureState();
            snapshot.Entities.RemoveAll(entity => entity.Owner == 0 && entity.DefinitionId == "depot");
            snapshot.Players[0].SupplyCap -= rules.Definitions.Single(definition => definition.Id == "depot").SupplyProvided;
            match = Match.Restore(rules, snapshot);
            SimulationFixture.Until(match, () => match.Observe(0).CompletedUpgrades.Contains("weapons-2"), 30);
            Assert.That(match.CombatDamage(0, "marine"), Is.EqualTo(11));
        }

        [Test]
        public void MissingUpgradeCatalogRemainsCompatibleWithLegacyRules()
        {
            var rules = Rules();
            rules.Upgrades = null;
            var missing = new Match(rules, false);
            Assert.That(missing.Observe(0).CompletedUpgrades, Is.Empty);
            Assert.That(missing.CombatDamage(0, "marine"), Is.EqualTo(6));
            Assert.That(missing.Upgrade("unknown"), Is.Null);
            rules.Upgrades = new UpgradeDefinition[0];
            var empty = new Match(rules, false);
            Assert.That(empty.Observe(0).CompletedUpgrades, Is.Empty);
        }

        [TestCase("producer")]
        [TestCase("building-prerequisite")]
        [TestCase("upgrade-prerequisite")]
        [TestCase("affected-definition")]
        [TestCase("cycle")]
        [TestCase("duplicate")]
        public void InvalidUpgradeReferencesAndCyclesAreRejectedBeforeMatch(string corruption)
        {
            var rules = Rules();
            switch (corruption)
            {
                case "producer": rules.Upgrades[0].ProducerId = "missing"; break;
                case "building-prerequisite": rules.Upgrades[0].PrerequisiteId = "missing"; break;
                case "upgrade-prerequisite": rules.Upgrades[0].PrerequisiteUpgradeId = "missing"; break;
                case "affected-definition": rules.Upgrades[0].AffectedDefinitionIds[0] = "missing"; break;
                case "cycle": rules.Upgrades[0].PrerequisiteUpgradeId = "weapons-2"; break;
                case "duplicate": rules.Upgrades[1].Id = rules.Upgrades[0].Id; break;
            }
            Assert.That(() => new Match(rules, false), Throws.Exception, corruption);
        }

        [TestCase("minerals")]
        [TestCase("gas")]
        [TestCase("time")]
        [TestCase("damage")]
        [TestCase("armor")]
        public void InvalidUpgradeNumericFieldsAreRejectedBeforeMatch(string corruption)
        {
            var rules = Rules();
            switch (corruption)
            {
                case "minerals": rules.Upgrades[0].MineralCost = -1; break;
                case "gas": rules.Upgrades[0].GasCost = -1; break;
                case "time": rules.Upgrades[0].ResearchTicks = 0; break;
                case "damage": rules.Upgrades[0].DamageBonus = -1; break;
                case "armor": rules.Upgrades[0].ArmorBonus = -1; break;
            }
            Assert.That(() => new Match(rules, false), Throws.Exception, corruption);
        }

        [TestCase("unknown-completed")]
        [TestCase("duplicate-completed")]
        [TestCase("missing-chain")]
        [TestCase("unknown-active")]
        [TestCase("duplicate-reservation")]
        [TestCase("production-conflict")]
        [TestCase("inflated-refund")]
        [TestCase("invalid-timer")]
        public void CorruptResearchSaveIsRejectedAtRestoreBoundary(string corruption)
        {
            var rules = Rules(secondProducer: true);
            var match = new Match(rules, false);
            var producers = match.CaptureState().Entities.Where(entity => entity.Owner == 0 && entity.DefinitionId == "barracks").Select(entity => entity.Id).ToArray();
            Assert.That(Research(match, producers[0], "weapons-1").Accepted, Is.True);
            var snapshot = match.CaptureState();
            var primary = snapshot.Entities.Single(entity => entity.Id == producers[0]);
            switch (corruption)
            {
                case "unknown-completed": snapshot.Players[0].CompletedUpgrades.Add("missing"); break;
                case "duplicate-completed": snapshot.Players[0].CompletedUpgrades.AddRange(new[] { "armor-1", "armor-1" }); break;
                case "missing-chain": snapshot.Players[0].CompletedUpgrades.Add("weapons-2"); break;
                case "unknown-active": primary.Research[0].DefinitionId = "missing"; break;
                case "duplicate-reservation":
                    snapshot.Entities.Single(entity => entity.Id == producers[1]).Research.Add(new ProductionItem
                    { DefinitionId = "weapons-1", RemainingTicks = 7, TotalTicks = 8, PaidMinerals = 75, PaidGas = 25 });
                    break;
                case "production-conflict":
                    primary.Production.Add(new ProductionItem
                    { DefinitionId = "marine", RemainingTicks = 15, TotalTicks = 15, PaidMinerals = 50 });
                    snapshot.Players[0].SupplyUsed++;
                    break;
                case "inflated-refund": primary.Research[0].PaidMinerals++; break;
                case "invalid-timer": primary.Research[0].RemainingTicks = 0; break;
            }
            Assert.That(() => Match.Restore(rules, snapshot), Throws.Exception, corruption);
        }
    }
}
