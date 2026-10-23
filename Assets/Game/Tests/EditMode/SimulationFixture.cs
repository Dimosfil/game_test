using System;
using System.Linq;
using ColonyRTS.Simulation;
using NUnit.Framework;

namespace ColonyRTS.Tests
{
    internal static class SimulationFixture
    {
        // Deliberately synthetic, compact values; these are not SC2 balance data.
        public static RuleSet Rules()
        {
            return new RuleSet
            {
                VersionId = "qa-synthetic-v1", ProductTitle = "Simulation contract tests",
                MapName = "QA split field", TickRate = 20, MapWidth = 48, MapHeight = 40,
                StartingMinerals = 500, StartingGas = 0, GatherTicks = 3, CargoAmount = 5,
                StimHealthCost = 5, StimDurationTicks = 20,
                Definitions = new[]
                {
                    new EntityDefinition { Id = "command", DisplayName = "Base", IsBuilding = true,
                        IsBase = true, MaxHealth = 1000, Radius = 1800, Sight = 6000,
                        SupplyProvided = 4, MineralCost = 200, BuildTicks = 40 },
                    new EntityDefinition { Id = "worker", DisplayName = "Worker", IsWorker = true,
                        MaxHealth = 45, Radius = 350, Speed = 3000, Sight = 5000,
                        Damage = 3, AttackRange = 700, AttackCooldown = 10, MineralCost = 50,
                        SupplyCost = 1, BuildTicks = 12, ProducerId = "command" },
                    new EntityDefinition { Id = "marine", DisplayName = "Marine", MaxHealth = 40,
                        Radius = 350, Speed = 3000, Sight = 6000, Damage = 6, AttackRange = 4000,
                        AttackCooldown = 5, MineralCost = 50, SupplyCost = 1, BuildTicks = 15,
                        ProducerId = "barracks", PrerequisiteId = "barracks" },
                    new EntityDefinition { Id = "depot", DisplayName = "Supply", IsBuilding = true,
                        MaxHealth = 300, Radius = 1000, Sight = 3000, MineralCost = 100,
                        SupplyProvided = 8, BuildTicks = 20 },
                    new EntityDefinition { Id = "barracks", DisplayName = "Barracks", IsBuilding = true,
                        MaxHealth = 600, Radius = 1400, Sight = 4000, MineralCost = 150,
                        BuildTicks = 30, PrerequisiteId = "depot" },
                    new EntityDefinition { Id = "refinery", DisplayName = "Refinery", IsBuilding = true,
                        MaxHealth = 300, Radius = 1000, Sight = 3000, MineralCost = 75,
                        BuildTicks = 20 },
                    new EntityDefinition { Id = "mineral", DisplayName = "Mineral", IsResource = true,
                        MaxHealth = 1, Radius = 900, ResourceAmount = 17, BuildTicks = 1 },
                    new EntityDefinition { Id = "geyser", DisplayName = "Gas", IsResource = true,
                        MaxHealth = 1, Radius = 900, ResourceAmount = 25, BuildTicks = 1 }
                },
                Blocks = new[] { new MapBlock { X = 20, Z = 0, Width = 2, Height = 28 } },
                Spawns = new[]
                {
                    Spawn("command", 0, 5, 8), Spawn("worker", 0, 8, 8),
                    Spawn("barracks", 0, 5, 14), Spawn("marine", 0, 8, 14),
                    Spawn("mineral", -1, 11, 8), Spawn("geyser", -1, 14, 8),
                    Spawn("command", 1, 41, 32), Spawn("worker", 1, 38, 32),
                    Spawn("marine", 1, 38, 26), Spawn("mineral", -1, 35, 32)
                }
            };
        }

        public static SpawnDefinition Spawn(string definition, int owner, float x, float z)
        {
            return new SpawnDefinition { DefinitionId = definition, Owner = owner, Position = SimPoint.World(x, z) };
        }

        public static int Id(Match match, string definition, int owner = 0)
        {
            return match.State.Entities.First(e => e.DefinitionId == definition && e.Owner == owner).Id;
        }

        public static CommandResult Send(Match match, int player, CommandKind kind, int id,
            SimPoint target = default, int targetId = 0, string definition = null)
        {
            var command = match.MakeCommand(player, kind, new[] { id }, target, targetId, definition);
            match.QueueCommand(command);
            match.Step();
            return match.LastResults.Single(r => r.Player == player && r.Sequence == command.Sequence);
        }

        public static void Steps(Match match, int count)
        {
            for (var i = 0; i < count; i++) match.Step();
        }

        public static void Until(Match match, Func<bool> predicate, int ticks = 1000)
        {
            for (var i = 0; i < ticks && !predicate(); i++) match.Step();
            Assert.That(predicate(), Is.True, "Scenario did not complete within " + ticks + " ticks.");
        }
    }
}
