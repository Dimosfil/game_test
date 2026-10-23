using System;

namespace ColonyRTS.Simulation
{
    public sealed partial class Match
    {
        void Repair(EntityState worker, UnitOrder order)
        {
            var target = Entity(order.TargetId);
            if (target == null || target.Id == worker.Id || target.Owner != worker.Owner || !target.Completed ||
                !Def(target).Repairable || target.Health >= Def(target).MaxHealth)
            {
                CompleteOrder(worker);
                return;
            }
            if (!MoveToward(worker, target.Position, Def(target).Radius + Def(worker).Radius + 250))
            {
                worker.WorkTimer = 0;
                return;
            }
            if (worker.WorkTimer < rules.RepairTicks) worker.WorkTimer++;
            if (worker.WorkTimer < rules.RepairTicks) return;
            var player = state.Players[worker.Owner];
            if (player.Minerals < rules.RepairMineralCost || player.Gas < rules.RepairGasCost) return;
            // One paid cycle heals only missing HP. Holding at a ready cycle while
            // funds are absent preserves the order without creating free repairs.
            player.Minerals -= rules.RepairMineralCost;
            player.Gas -= rules.RepairGasCost;
            target.Health += Math.Min(rules.RepairHealth, Def(target).MaxHealth - target.Health);
            worker.WorkTimer = 0;
            if (target.Health >= Def(target).MaxHealth) CompleteOrder(worker);
        }
    }
}
