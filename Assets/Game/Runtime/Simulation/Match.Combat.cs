using System;
using System.Collections.Generic;

namespace ColonyRTS.Simulation
{
    public sealed partial class Match
    {
        readonly List<EntityState>[] combatantsByOwner = { new List<EntityState>(), new List<EntityState>() };
        MatchState combatIndexState;
        int combatIndexTick = -1, combatIndexAllocator = -1;

        List<EntityState> EnemyCombatants(int player)
        {
            if (player < 0 || player > 1) return state.Entities;
            if (!ReferenceEquals(combatIndexState, state) || combatIndexTick != state.Tick || combatIndexAllocator != state.NextEntityId)
            {
                combatantsByOwner[0].Clear(); combatantsByOwner[1].Clear();
                foreach (var entity in state.Entities)
                    if (entity.Owner >= 0 && entity.Owner <= 1 && entity.Health > 0) combatantsByOwner[entity.Owner].Add(entity);
                combatIndexState = state; combatIndexTick = state.Tick; combatIndexAllocator = state.NextEntityId;
            }
            return combatantsByOwner[1 - player];
        }

        void CompleteOrder(EntityState e) { if (e.Orders.Count > 0) e.Orders.RemoveAt(0); ClearPath(e); e.WorkTimer = 0; e.AvoidanceSide = 0; }
        void TickOrder(EntityState e)
        {
            var d = Def(e);
            if (e.Orders.Count == 0)
            {
                if (d.Damage > 0) Engage(e, false);
                return;
            }
            var order = e.Orders[0];
            switch (order.Kind)
            {
                case OrderKind.Move:
                    if (MoveToward(e, order.Target)) CompleteOrder(e);
                    break;
                case OrderKind.AttackMove:
                    if (!Engage(e, true) && MoveToward(e, order.Target)) CompleteOrder(e);
                    break;
                case OrderKind.Patrol:
                    // A queued successor turns this leg into a finite waypoint.
                    // Only the last patrol in the queue repeats indefinitely.
                    var patrolDestination = order.PatrolReturning && e.Orders.Count == 1 ? order.PatrolOrigin : order.Target;
                    if (!Engage(e, true) && MoveToward(e, patrolDestination))
                    {
                        if (e.Orders.Count > 1) CompleteOrder(e);
                        else { order.PatrolReturning = !order.PatrolReturning; ClearPath(e); }
                    }
                    break;
                case OrderKind.Attack:
                    var target = Entity(order.TargetId);
                    if (target == null || !IsVisible(e.Owner, target) || target.Owner == e.Owner) CompleteOrder(e);
                    else FireOrChase(e, target, true);
                    break;
                case OrderKind.Gather:
                case OrderKind.ReturnCargo:
                    Gather(e, order);
                    break;
                case OrderKind.Build:
                    var building = Entity(order.TargetId);
                    if (building == null || building.Completed) CompleteOrder(e);
                    else MoveToward(e, building.Position, Def(building).Radius + d.Radius + 250);
                    break;
                case OrderKind.Repair:
                    Repair(e, order);
                    break;
                case OrderKind.Hold:
                    Engage(e, false);
                    break;
            }
        }
        bool Engage(EntityState e, bool chase)
        {
            var d = Def(e);
            if (d.Damage <= 0) return false;
            EntityState target = null;
            long nearest = long.MaxValue;
            long ownSightSquared = (long)d.Sight * d.Sight;
            foreach (var other in EnemyCombatants(e.Owner))
            {
                if (other.Health <= 0 || other.Owner < 0 || other.Owner == e.Owner) continue;
                long distance = DistanceSquared(e.Position, other.Position);
                if (target != null && (distance > nearest || distance == nearest && other.Id >= target.Id)) continue;
                int range = chase ? d.Sight : d.AttackRange + Def(other).Radius;
                if (distance > (long)range * range) continue;
                // TickOrder actors are alive and completed: inside their own exact sight they
                // already provide a visibility witness. Hold can still rely on allies beyond it.
                if (distance > ownSightSquared && !IsVisible(e.Owner, other)) continue;
                target = other;
                nearest = distance;
            }
            if (target == null) return false;
            FireOrChase(e, target, chase); return true;
        }
        void FireOrChase(EntityState e, EntityState target, bool chase)
        {
            var d = Def(e);
            if (d.Damage <= 0) return;
            int range = d.AttackRange + Def(target).Radius;
            if (!Near(e.Position, target.Position, range))
            {
                if (chase && !d.IsBuilding && d.Speed > 0)
                {
                    if (e.Path.Count > 0 && !Near(e.Path[e.Path.Count - 1], target.Position, Math.Max(range, 1500))) ClearPath(e);
                    MoveToward(e, target.Position, Math.Max(range - 100, Def(e).Radius + Def(target).Radius + 100));
                }
                return;
            }
            ClearPath(e);
            if (e.AttackTimer > 0) return;
            int armor = target.Owner >= 0 ? CombatArmor(target.Owner, target.DefinitionId) : Def(target).Armor;
            int healthBefore = target.Health;
            target.Health -= Math.Max(1, CombatDamage(e.Owner, e.DefinitionId) - armor);
            if (target.Health <= 0 && (Def(target).IsBuilding || Def(target).IsResource)) InvalidateNavigation();
            e.LastAttackTick = state.Tick; e.LastAttackTarget = target.Id;
            e.AttackTimer = Math.Max(1, e.StimRemaining > 0 ? d.AttackCooldown * 2 / 3 : d.AttackCooldown);
            RecordCombatShot(e, target, healthBefore);
        }
    }
}
