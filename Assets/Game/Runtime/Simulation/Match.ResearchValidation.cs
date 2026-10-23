using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyRTS.Simulation
{
    public sealed partial class Match
    {
        static void ValidateResearchRules(RuleSet source, Dictionary<string, EntityDefinition> catalog)
        {
            var technology = new Dictionary<string, UpgradeDefinition>(StringComparer.Ordinal);
            foreach (var upgrade in source.Upgrades ?? new UpgradeDefinition[0])
            {
                if (upgrade == null || string.IsNullOrWhiteSpace(upgrade.Id) || technology.ContainsKey(upgrade.Id) ||
                    string.IsNullOrEmpty(upgrade.ProducerId) || !catalog.TryGetValue(upgrade.ProducerId, out var producer) || !producer.IsBuilding || producer.IsResource ||
                    upgrade.MineralCost < 0 || upgrade.GasCost < 0 || upgrade.ResearchTicks <= 0 || upgrade.DamageBonus < 0 || upgrade.ArmorBonus < 0 ||
                    upgrade.AffectedDefinitionIds == null || upgrade.AffectedDefinitionIds.Length == 0 ||
                    upgrade.AffectedDefinitionIds.Any(id => id == null || !catalog.ContainsKey(id)) ||
                    upgrade.AffectedDefinitionIds.Distinct(StringComparer.Ordinal).Count() != upgrade.AffectedDefinitionIds.Length)
                    throw new ArgumentException("Malformed or duplicate upgrade definition", nameof(source));
                if (!string.IsNullOrEmpty(upgrade.PrerequisiteId) && (!catalog.ContainsKey(upgrade.PrerequisiteId) || catalog[upgrade.PrerequisiteId].IsResource))
                    throw new ArgumentException("Unknown or invalid upgrade entity prerequisite: " + upgrade.Id, nameof(source));
                technology.Add(upgrade.Id, upgrade);
            }
            foreach (var upgrade in technology.Values)
                if (!string.IsNullOrEmpty(upgrade.PrerequisiteUpgradeId) && !technology.ContainsKey(upgrade.PrerequisiteUpgradeId))
                    throw new ArgumentException("Unknown prerequisite upgrade: " + upgrade.Id, nameof(source));
            foreach (var upgrade in technology.Values)
            {
                var visited = new HashSet<string>(StringComparer.Ordinal);
                for (var current = upgrade; current != null; current = string.IsNullOrEmpty(current.PrerequisiteUpgradeId) ? null : technology[current.PrerequisiteUpgradeId])
                    if (!visited.Add(current.Id)) throw new ArgumentException("Cyclic upgrade prerequisites", nameof(source));
            }
            foreach (var definition in catalog.Values)
            {
                var visited = new HashSet<string>(StringComparer.Ordinal);
                for (var current = definition; current != null; current = string.IsNullOrEmpty(current.PrerequisiteId) ? null : catalog[current.PrerequisiteId])
                    if (!visited.Add(current.Id)) throw new ArgumentException("Cyclic entity prerequisites", nameof(source));
                long damage = definition.Damage, armor = definition.Armor;
                foreach (var upgrade in technology.Values)
                    if (Array.IndexOf(upgrade.AffectedDefinitionIds, definition.Id) >= 0) { damage += upgrade.DamageBonus; armor += upgrade.ArmorBonus; }
                if (damage > int.MaxValue || armor > int.MaxValue) throw new ArgumentException("Combined combat values exceed integer range", nameof(source));
            }
        }

        void ValidateResearchState(MatchState saved)
        {
            var completed = new HashSet<string>[saved.Players.Length];
            for (int player = 0; player < saved.Players.Length; player++)
            {
                var list = saved.Players[player].CompletedUpgrades;
                if (list == null) throw new ArgumentException("Missing completed upgrades");
                completed[player] = new HashSet<string>(StringComparer.Ordinal);
                foreach (var id in list)
                    if (id == null || !upgrades.ContainsKey(id) || !completed[player].Add(id)) throw new ArgumentException("Unknown or duplicate completed upgrade");
                foreach (var id in list)
                {
                    var predecessor = upgrades[id].PrerequisiteUpgradeId;
                    if (!string.IsNullOrEmpty(predecessor) && !completed[player].Contains(predecessor)) throw new ArgumentException("Completed upgrade is missing its prerequisite");
                }
            }
            var reserved = new[] { new HashSet<string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal) };
            foreach (var producer in saved.Entities)
            {
                if (producer.Research.Count == 0) continue;
                if (producer.Owner < 0 || !producer.Completed || !Def(producer).IsBuilding || producer.Research.Count != 1 || producer.Production.Count > 0)
                    throw new ArgumentException("Invalid or occupied research producer");
                var item = producer.Research[0];
                if (item == null || item.DefinitionId == null || !upgrades.TryGetValue(item.DefinitionId, out var upgrade) ||
                    upgrade.ProducerId != producer.DefinitionId || item.TotalTicks != upgrade.ResearchTicks || item.RemainingTicks <= 0 || item.RemainingTicks > item.TotalTicks ||
                    item.PaidMinerals != upgrade.MineralCost || item.PaidGas != upgrade.GasCost || completed[producer.Owner].Contains(upgrade.Id) ||
                    !reserved[producer.Owner].Add(upgrade.Id)) throw new ArgumentException("Malformed, duplicate or mismatched research state");
                if (!string.IsNullOrEmpty(upgrade.PrerequisiteUpgradeId) && !completed[producer.Owner].Contains(upgrade.PrerequisiteUpgradeId))
                    throw new ArgumentException("Active research is missing its prerequisite upgrade");
            }
        }
    }
}
