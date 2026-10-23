using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace ColonyRTS.Simulation
{
    public sealed partial class Match
    {
        static readonly Dictionary<Type, FieldInfo[]> fields = new Dictionary<Type, FieldInfo[]>();
        static readonly Dictionary<Type, Func<object, object>> objectCopiers = new Dictionary<Type, Func<object, object>>();
        static FieldInfo[] DataFields(Type type)
        {
            lock (fields)
            {
                if (!fields.TryGetValue(type, out var value))
                {
                    value = type.GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();
                    fields.Add(type, value);
                }
                return value;
            }
        }
        // DTOs are acyclic data. One reflection-based copier keeps every public contract field in snapshots.
        static object CopyData(object value)
        {
            if (value == null) return null;
            var type = value.GetType();
            if (type.IsValueType || value is string) return value;
            if (value is Array array)
            {
                var copy = Array.CreateInstance(type.GetElementType(), array.Length);
                for (int i = 0; i < array.Length; i++) copy.SetValue(CopyData(array.GetValue(i)), i);
                return copy;
            }
            if (value is IList list)
            {
                var copy = (IList)Activator.CreateInstance(type);
                for (int i = 0; i < list.Count; i++) copy.Add(CopyData(list[i]));
                return copy;
            }
            Func<object, object> copier;
            lock (objectCopiers)
            {
                if (!objectCopiers.TryGetValue(type, out copier))
                {
                    copier = BuildObjectCopier(type);
                    objectCopiers.Add(type, copier);
                }
            }
            return copier(value);
        }
        static Func<object, object> ReflectionObjectCopier(Type type) => source =>
        {
            var copy = Activator.CreateInstance(type);
            CopyFields(source, copy, DataFields(type));
            return copy;
        };
        static Func<object, object> BuildObjectCopier(Type type)
        {
#if ENABLE_IL2CPP
            return ReflectionObjectCopier(type);
#else
            var source = Expression.Parameter(typeof(object), "source");
            var target = Expression.Variable(type, "copy");
            var body = TypedCopyBody(Expression.Convert(source, type), target, DataFields(type));
            var copier = Expression.Lambda<Func<object, object>>(Expression.Convert(body, typeof(object)), source);
            try { return copier.Compile(); }
            catch (NotSupportedException) { return ReflectionObjectCopier(type); }
#endif
        }
        // The target is newly constructed and unpublished. Reusing its constructor-created lists
        // avoids throwing away empty lists, while every source reference remains deeply detached.
        static List<T> CopyList<T>(List<T> source, List<T> target)
        {
            if (source == null) return null;
            if (target == null) target = new List<T>(source.Count);
            else
            {
                target.Clear();
                if (target.Capacity < source.Count) target.Capacity = source.Count;
            }
            bool immutable = typeof(T).IsValueType || typeof(T) == typeof(string);
            for (int i = 0; i < source.Count; i++) target.Add(immutable ? source[i] : (T)CopyData(source[i]));
            return target;
        }
        static Expression TypedCopyBody(Expression source, ParameterExpression target, FieldInfo[] selectedFields)
        {
            var expressions = new List<Expression> { Expression.Assign(target, Expression.New(target.Type)) };
            var clone = typeof(Match).GetMethod(nameof(CopyData), BindingFlags.NonPublic | BindingFlags.Static);
            var cloneList = typeof(Match).GetMethod(nameof(CopyList), BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var field in selectedFields)
            {
                Expression value = Expression.Field(source, field);
                var destination = Expression.Field(target, field);
                if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                    value = Expression.Call(cloneList.MakeGenericMethod(field.FieldType.GetGenericArguments()), value, destination);
                else if (!field.FieldType.IsValueType && field.FieldType != typeof(string))
                    value = Expression.Convert(Expression.Call(clone, Expression.Convert(value, typeof(object))), field.FieldType);
                expressions.Add(Expression.Assign(destination, value));
            }
            expressions.Add(target);
            return Expression.Block(new[] { target }, expressions);
        }
        static void CopyFields(object source, object target, FieldInfo[] selectedFields)
        {
            foreach (var field in selectedFields) field.SetValue(target, CopyData(field.GetValue(source)));
        }
        static EntityState CopyEntity(EntityState e) => (EntityState)CopyData(e);
        static EntityDefinition CopyDefinition(EntityDefinition d) => (EntityDefinition)CopyData(d);
        static RuleSet CopyRules(RuleSet source) => (RuleSet)CopyData(source);
        static GameCommand CopyCommand(GameCommand command) => (GameCommand)CopyData(command);
        public MatchState CaptureState() => (MatchState)CopyData(state);
        static string RulesHash(RuleSet source) => DataHash(source);
        static string DataHash(object source)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                WriteData(writer, source); writer.Flush();
                using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
        static void WriteData(BinaryWriter writer, object value)
        {
            writer.Write(value != null); if (value == null) return;
            var type = value.GetType();
            if (type.IsEnum) { writer.Write(Convert.ToInt32(value)); return; }
            if (value is string text) { writer.Write(text); return; }
            if (value is bool boolean) { writer.Write(boolean); return; }
            if (value is int integer) { writer.Write(integer); return; }
            if (value is long longInteger) { writer.Write(longInteger); return; }
            if (value is uint unsigned) { writer.Write(unsigned); return; }
            if (value is IList list)
            {
                writer.Write(list.Count); foreach (var item in list) WriteData(writer, item); return;
            }
            foreach (var field in DataFields(type)) { writer.Write(field.Name); WriteData(writer, field.GetValue(value)); }
        }
        /// <summary>Gameplay fingerprint. Excludes AI scheduling, command transport/history and issued sequences;
        /// replay reproduces accepted gameplay inputs, while snapshots preserve all of that continuation metadata.</summary>
        public string StateHash()
        {
            var gameplay = CaptureState();
            gameplay.Bots = new BotState[0]; gameplay.PendingCommands.Clear(); gameplay.CommandLog.Clear();
            foreach (var player in gameplay.Players) { player.IssuedSequence = 0; player.LastProcessedSequence = 0; }
            return DataHash(gameplay);
        }
        public static Match Restore(RuleSet rules, MatchState saved)
        {
            if (saved == null) throw new ArgumentNullException(nameof(saved));
            var match = new Match(rules, false, saved.Seed);
            if (saved.RuleVersion != rules.VersionId || saved.RuleHash != match.state.RuleHash) throw new ArgumentException("Save rule version or hash mismatch", nameof(saved));
            match.ValidateState(saved);
            match.state = (MatchState)CopyData(saved);
            return match;
        }
        public ReplayData ExportReplay() => new ReplayData { RuleVersion = state.RuleVersion, RuleHash = state.RuleHash,
            Seed = state.Seed, FinalTick = state.Tick, Commands = state.CommandLog.Select(CopyCommand).ToList() };
        /// <summary>Start a replay at tick zero. The host advances it using the same fixed Step loop.</summary>
        public static Match BeginReplay(RuleSet rules, ReplayData replay)
        {
            if (replay == null) throw new ArgumentNullException(nameof(replay));
            var match = new Match(rules, false, replay.Seed);
            if (replay.RuleVersion != rules.VersionId || replay.RuleHash != match.state.RuleHash) throw new ArgumentException("Replay rule version or hash mismatch", nameof(replay));
            if (replay.FinalTick < 0 || replay.Commands == null) throw new ArgumentException("Invalid replay", nameof(replay));
            foreach (var command in replay.Commands)
            {
                if (command == null || command.Tick < 0 || command.Tick >= replay.FinalTick) throw new ArgumentException("Replay command outside timeline", nameof(replay));
                match.QueueCommand(command);
            }
            match.replaying = true;
            return match;
        }
        public static Match FromReplay(RuleSet rules, ReplayData replay)
        {
            var match = BeginReplay(rules, replay);
            while (match.state.Tick < replay.FinalTick)
            {
                if (match.state.Phase != MatchPhase.Running) throw new ArgumentException("Replay timeline exceeds match end", nameof(replay));
                match.Step();
                if (match.results.Any(result => !result.Accepted)) throw new ArgumentException("Replay contains rejected command", nameof(replay));
            }
            return match;
        }

        void ValidateState(MatchState saved)
        {
            if (saved.Tick < 0 || saved.NextEntityId < 1 || !Enum.IsDefined(typeof(MatchPhase), saved.Phase) || saved.Winner < -1 || saved.Winner > 1 ||
                saved.Entities == null || saved.Players == null || saved.Players.Length != 2 || saved.Bots == null || saved.PendingCommands == null || saved.CommandLog == null)
                throw new ArgumentException("Malformed match state");
            var ids = new HashSet<int>();
            foreach (var e in saved.Entities)
            {
                if (e == null || e.Id <= 0 || e.Id >= saved.NextEntityId || !ids.Add(e.Id) || !definitions.ContainsKey(e.DefinitionId ?? "") ||
                    e.Owner < -1 || e.Owner > 1 || e.Health <= 0 || e.Health > definitions[e.DefinitionId].MaxHealth || !Inside(e.Position) ||
                    e.Orders == null || e.Path == null || e.Production == null || e.Research == null || e.PathIndex < 0 || e.PathIndex > e.Path.Count || e.PathStoppingDistance < 0 || e.AvoidanceSide < -1 || e.AvoidanceSide > 1 || e.ResourceRemaining < 0 || e.Cargo < 0)
                    throw new ArgumentException("Malformed entity state");
                if (e.Orders.Any(o => o == null || !Enum.IsDefined(typeof(OrderKind), o.Kind)) || e.Path.Any(point => !Inside(point)))
                    throw new ArgumentException("Malformed entity orders, production or path");
                ValidateProductionState(e);
            }
            for (int i = 0; i < saved.Players.Length; i++)
            {
                var p = saved.Players[i];
                if (p == null || p.Id != i || p.Minerals < 0 || p.Gas < 0 || p.Explored == null || p.Explored.Length != rules.MapWidth * rules.MapHeight || p.RememberedBuildings == null)
                    throw new ArgumentException("Malformed player state");
                if (p.RememberedBuildings.Any(e => e == null || !definitions.ContainsKey(e.DefinitionId ?? "") ||
                    !definitions[e.DefinitionId].IsBuilding || e.Owner < 0 || e.Owner > 1 || e.Owner == p.Id || e.Id <= 0 ||
                    !Inside(e.Position) || e.Orders == null || e.Path == null || e.Production == null || e.Research == null || e.Research.Count > 0)) throw new ArgumentException("Malformed remembered building");
            }
            ValidateResearchState(saved);
            if (saved.Bots.Any(b => b == null || b.Player < 0 || b.Player > 1 || b.NextThinkTick < 0)) throw new ArgumentException("Malformed bot state");
            if (saved.PendingCommands.Concat(saved.CommandLog).Any(c => c == null || c.EntityIds == null || c.Tick < 0)) throw new ArgumentException("Malformed command state");
        }
        static void ValidateRules(RuleSet source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(source.VersionId) || source.TickRate != 20 || source.MapWidth < 4 || source.MapHeight < 4 || source.MapWidth > 512 || source.MapHeight > 512 ||
                source.StartingMinerals < 0 || source.StartingGas < 0 || source.GatherTicks <= 0 || source.CargoAmount <= 0 || source.StimHealthCost < 0 || source.StimDurationTicks < 0 ||
                source.Definitions == null || source.Definitions.Length == 0 || source.Spawns == null || source.Blocks == null)
                throw new ArgumentException("Invalid rules: fixed 20 Hz, bounded map, positive economy timers and complete arrays required", nameof(source));
            if (source.RepairTicks < 0 || source.RepairHealth < 0 || source.RepairMineralCost < 0 || source.RepairGasCost < 0 ||
                (source.Definitions.Any(d => d != null && d.Repairable) && (source.RepairTicks <= 0 || source.RepairHealth <= 0)))
                throw new ArgumentException("Repairable definitions require positive repair timers/health and nonnegative costs", nameof(source));
            var catalog = new Dictionary<string, EntityDefinition>(StringComparer.Ordinal);
            foreach (var d in source.Definitions)
            {
                if (d == null || string.IsNullOrWhiteSpace(d.Id) || catalog.ContainsKey(d.Id) || d.MaxHealth <= 0 || d.Radius <= 0 || d.Radius > Math.Max(source.MapWidth, source.MapHeight) * 1000 || d.Speed < 0 || d.Sight < 0 ||
                    d.Damage < 0 || d.Armor < 0 || d.AttackRange < 0 || (d.Damage > 0 && d.AttackCooldown <= 0) || d.MineralCost < 0 || d.GasCost < 0 || d.SupplyCost < 0 || d.SupplyProvided < 0 || (!d.IsResource && d.BuildTicks <= 0) || d.BuildTicks < 0 || d.ResourceAmount < 0 ||
                    (d.IsBase && !d.IsBuilding) || (d.IsWorker && (d.IsBuilding || d.IsResource))) throw new ArgumentException("Invalid or duplicate entity definition", nameof(source));
                catalog.Add(d.Id, d);
            }
            foreach (var d in source.Definitions)
            {
                if (!string.IsNullOrEmpty(d.ProducerId) && (!catalog.ContainsKey(d.ProducerId) || !catalog[d.ProducerId].IsBuilding)) throw new ArgumentException("Unknown or invalid producer: " + d.Id);
                if (!string.IsNullOrEmpty(d.PrerequisiteId) && !catalog.ContainsKey(d.PrerequisiteId)) throw new ArgumentException("Unknown prerequisite: " + d.Id);
            }
            ValidateResearchRules(source, catalog);
            foreach (var block in source.Blocks)
                if (block == null || block.X < 0 || block.Z < 0 || block.Width <= 0 || block.Height <= 0 || block.X + block.Width > source.MapWidth || block.Z + block.Height > source.MapHeight) throw new ArgumentException("Invalid map block");
            foreach (var spawn in source.Spawns)
            {
                if (spawn == null || !catalog.ContainsKey(spawn.DefinitionId ?? "") || spawn.Owner < -1 || spawn.Owner > 1 || spawn.Position.X < 0 || spawn.Position.Z < 0 || spawn.Position.X >= source.MapWidth * 1000 || spawn.Position.Z >= source.MapHeight * 1000 ||
                    (catalog[spawn.DefinitionId].IsResource ? spawn.Owner != -1 : spawn.Owner < 0)) throw new ArgumentException("Invalid spawn");
            }
            for (int player = 0; player < 2; player++) if (!source.Spawns.Any(s => s.Owner == player && catalog[s.DefinitionId].IsBase)) throw new ArgumentException("Both players need a base spawn");
        }
    }
}
