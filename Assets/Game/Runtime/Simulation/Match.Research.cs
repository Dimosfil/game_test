using System;
using System.Linq;

namespace ColonyRTS.Simulation
{
    public sealed partial class Match
    {
        public UpgradeDefinition Upgrade(string id) => upgrades.TryGetValue(id ?? "", out var upgrade) ? (UpgradeDefinition)CopyData(upgrade) : null;

        /// <summary>Nonmutating projection of the same owner-only gate used when a Research command executes.</summary>
        public CommandResult ResearchAvailability(int player, int producerId, string upgradeId)
        {
            string reason = ResearchReason(player, producerId, upgradeId);
            return new CommandResult { Player = player, Accepted = reason == null, Reason = reason ?? "Accepted" };
        }
        string ResearchReason(int player, int producerId, string upgradeId)
        {
            if (player < 0 || player >= state.Players.Length) return "Invalid player";
            if (state.Phase != MatchPhase.Running) return "Match is not running";
            var producer = Entity(producerId);
            // Resolve ownership before inspecting private queues or completion of a guessed foreign entity.
            if (producer == null || producer.Owner != player || !Def(producer).IsBuilding) return "Research producer unavailable";
            if (!upgrades.TryGetValue(upgradeId ?? "", out var upgrade)) return "Unknown upgrade";
            if (producer.DefinitionId != upgrade.ProducerId) return "Invalid research producer";
            if (!producer.Completed) return "Research producer incomplete";
            var owner = state.Players[player];
            if (owner.CompletedUpgrades.Contains(upgrade.Id)) return "Upgrade already completed";
            if (state.Entities.Any(e => e.Health > 0 && e.Owner == player && e.Research.Any(item => item.DefinitionId == upgrade.Id))) return "Upgrade already in progress";
            if (producer.Research.Count > 0 || producer.Production.Count > 0) return "Producer busy";
            if (!string.IsNullOrEmpty(upgrade.PrerequisiteId) && !state.Entities.Any(e => e.Health > 0 && e.Owner == player && e.Completed && e.DefinitionId == upgrade.PrerequisiteId)) return "Prerequisite missing";
            if (!string.IsNullOrEmpty(upgrade.PrerequisiteUpgradeId) && !owner.CompletedUpgrades.Contains(upgrade.PrerequisiteUpgradeId)) return "Upgrade prerequisite missing";
            if (owner.Minerals < upgrade.MineralCost || owner.Gas < upgrade.GasCost) return "Insufficient resources";
            return null;
        }
        string StartResearch(int player, int producerId, string upgradeId)
        {
            string reason = ResearchReason(player, producerId, upgradeId);
            if (reason != null) return reason;
            var upgrade = upgrades[upgradeId]; var owner = state.Players[player];
            owner.Minerals -= upgrade.MineralCost; owner.Gas -= upgrade.GasCost;
            Entity(producerId).Research.Add(new ProductionItem { DefinitionId = upgrade.Id, RemainingTicks = upgrade.ResearchTicks,
                TotalTicks = upgrade.ResearchTicks, PaidMinerals = upgrade.MineralCost, PaidGas = upgrade.GasCost });
            return null;
        }
        void TickResearch(EntityState producer)
        {
            if (producer.Research.Count == 0) return;
            var item = producer.Research[0]; item.RemainingTicks--;
            if (item.RemainingTicks > 0) return;
            state.Players[producer.Owner].CompletedUpgrades.Add(item.DefinitionId);
            producer.Research.Clear();
        }
        public int CombatDamage(int player, string definitionId) => CombatValue(player, definitionId, false);
        public int CombatArmor(int player, string definitionId) => CombatValue(player, definitionId, true);
        int CombatValue(int player, string definitionId, bool armor)
        {
            if (player < 0 || player >= state.Players.Length) throw new ArgumentOutOfRangeException(nameof(player));
            if (!definitions.TryGetValue(definitionId ?? "", out var definition)) throw new ArgumentException("Unknown combat definition", nameof(definitionId));
            int value = armor ? definition.Armor : definition.Damage;
            foreach (var id in state.Players[player].CompletedUpgrades)
            {
                var upgrade = upgrades[id];
                if (Array.IndexOf(upgrade.AffectedDefinitionIds, definitionId) >= 0) value += armor ? upgrade.ArmorBonus : upgrade.DamageBonus;
            }
            return value;
        }
    }
}
