using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ColonyRTS.Simulation;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ColonyRTS.Editor
{
    /// <summary>Synthetic offline navigation measurements; never reads or modifies the open scene.</summary>
    public static class NavigationBenchmark
    {
        private const int SampleInterval = 100;
        private const double WatchdogSeconds = 60;
        private const int ArrivalToleranceMillimetres = 250;
        private const int StuckWindowToleranceMillimetres = 250;

        [Serializable]
        private sealed class Report
        {
            public string Status;
            public string StartedUtc;
            public string FinishedUtc;
            public string UnityVersion;
            public string OperatingSystem;
            public string Processor;
            public int ProcessorCount;
            public string PerformancePolicy = "Measurements only: no tick-latency pass/fail budget has been declared.";
            public string CorrectnessPolicy = "Geometry sampled at tick 1, each 100 ticks and final tick; this is not a continuous-collision proof. Separate 16-unit contract test checks every tick.";
            public string WatchdogPolicy = "60 seconds per case, checked between synchronous Step calls; a single Step can exceed this cooperative limit and is reported as timed out, never passed.";
            public List<CaseResult> Cases = new List<CaseResult>();
        }

        [Serializable]
        private sealed class CaseResult
        {
            public string Status;
            public int Units;
            public int Seed;
            public int MapWidth;
            public int MapHeight;
            public int RequestedTicks;
            public int CompletedTicks;
            public bool TimedOut;
            public double WatchdogSeconds;
            public double SetupMilliseconds;
            public double WallMilliseconds;
            public double TickTotalMilliseconds;
            public double TickP50Milliseconds;
            public double TickP95Milliseconds;
            public double TickP99Milliseconds;
            public double TickMaximumMilliseconds;
            public int ArrivalToleranceMillimetres;
            public int Arrived;
            public int NotArrived;
            public int StuckInLastWindow;
            public int StuckWindowTicks;
            public int StuckProgressThresholdMillimetres;
            public int GeometrySampleCount;
            public int MaximumOverlappingPairsObserved;
            public int MaximumWallIntersectionsObserved;
            public string CorrectnessStatus;
            public string RuleVersion;
            public string RuleHash;
            public string Error;
        }

        [MenuItem("Colony RTS/Navigation Benchmark")]
        public static void Run() => RunTicks(600, "navigation-benchmark.json");

        [MenuItem("Colony RTS/Navigation Benchmark Extended")]
        public static void RunExtended() => RunTicks(1800, "navigation-benchmark-extended.json");

        private static void RunTicks(int requestedTicks, string fileName)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Run the synthetic benchmark from an idle Editor.");
            var report = new Report
            {
                Status = "running", StartedUtc = DateTime.UtcNow.ToString("O"),
                UnityVersion = Application.unityVersion, OperatingSystem = SystemInfo.operatingSystem,
                Processor = SystemInfo.processorType, ProcessorCount = SystemInfo.processorCount
            };
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Evidence", fileName));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Write(path, report);
            foreach (var count in new[] { 200, 400, 800 })
            {
                report.Cases.Add(RunCase(count, requestedTicks));
                Write(path, report);
            }
            report.Status = report.Cases.All(c => c.Status == "completed") ? "completed" : "completed-with-incomplete-cases";
            report.FinishedUtc = DateTime.UtcNow.ToString("O");
            Write(path, report);
            Debug.Log("Navigation measurements written: " + path + "; " + report.Status);
        }

        private static CaseResult RunCase(int count, int requestedTicks)
        {
            var result = new CaseResult
            {
                Status = "running", Units = count, Seed = 1841,
                MapWidth = 128, MapHeight = 96, RequestedTicks = requestedTicks,
                WatchdogSeconds = WatchdogSeconds, ArrivalToleranceMillimetres = ArrivalToleranceMillimetres,
                StuckProgressThresholdMillimetres = StuckWindowToleranceMillimetres
            };
            var wall = Stopwatch.StartNew();
            var samples = new List<double>(requestedTicks);
            try
            {
                var rules = CreateRules(count);
                var match = new Match(rules, false, result.Seed);
                var initial = match.CaptureState();
                result.RuleVersion = rules.VersionId;
                result.RuleHash = initial.RuleHash;
                var ids = initial.Entities.Where(e => e.DefinitionId == "mover").Select(e => e.Id).ToArray();
                match.QueueCommand(match.MakeCommand(0, CommandKind.Move, ids, SimPoint.World(102, 48)));
                result.SetupMilliseconds = wall.Elapsed.TotalMilliseconds;
                var targets = new Dictionary<int, SimPoint>();
                var previousWindow = new Dictionary<int, SimPoint>();
                var lastWindow = new Dictionary<int, SimPoint>();
                var lastWindowTick = 0;
                EntityState[] latest = null;
                for (var tick = 0; tick < requestedTicks; tick++)
                {
                    if (wall.Elapsed.TotalSeconds >= WatchdogSeconds) { result.TimedOut = true; break; }
                    var start = Stopwatch.GetTimestamp();
                    match.Step();
                    samples.Add((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
                    result.CompletedTicks = match.Tick;
                    if (match.Tick != tick + 1) throw new InvalidOperationException("The benchmark match stopped advancing at tick " + match.Tick);
                    if (tick == 0)
                    {
                        if (match.LastResults.Count != 1 || !match.LastResults[0].Accepted)
                            throw new InvalidOperationException("The synthetic group move was rejected.");
                        latest = Movers(match);
                        foreach (var entity in latest)
                        {
                            if (entity.Orders.Count != 1) throw new InvalidOperationException("Missing initial group destination.");
                            targets.Add(entity.Id, entity.Orders[0].Target);
                            lastWindow.Add(entity.Id, entity.Position);
                        }
                        lastWindowTick = match.Tick;
                        SampleGeometry(latest, rules, result);
                    }
                    else if (match.Tick % SampleInterval == 0)
                    {
                        latest = Movers(match);
                        previousWindow = lastWindow;
                        lastWindow = latest.ToDictionary(e => e.Id, e => e.Position);
                        result.StuckWindowTicks = match.Tick - lastWindowTick;
                        lastWindowTick = match.Tick;
                        SampleGeometry(latest, rules, result);
                    }
                    if (wall.Elapsed.TotalSeconds >= WatchdogSeconds) { result.TimedOut = true; break; }
                }
                // Capture only at spaced measurement boundaries and once at final/timeout.
                if (latest == null || result.CompletedTicks != lastWindowTick)
                {
                    latest = Movers(match);
                    previousWindow = lastWindow;
                    result.StuckWindowTicks = result.CompletedTicks - lastWindowTick;
                    SampleGeometry(latest, rules, result);
                }
                foreach (var entity in latest)
                {
                    var arrived = targets.TryGetValue(entity.Id, out var target) &&
                        DistanceSquared(entity.Position, target) <= (long)ArrivalToleranceMillimetres * ArrivalToleranceMillimetres;
                    if (arrived) result.Arrived++;
                    else
                    {
                        result.NotArrived++;
                        if (previousWindow.TryGetValue(entity.Id, out var previous) &&
                            DistanceSquared(entity.Position, previous) <= (long)StuckWindowToleranceMillimetres * StuckWindowToleranceMillimetres)
                            result.StuckInLastWindow++;
                    }
                }
                result.CorrectnessStatus = result.MaximumOverlappingPairsObserved == 0 && result.MaximumWallIntersectionsObserved == 0
                    ? "no-violation-observed-at-samples" : "geometry-violation-observed";
                result.Status = result.TimedOut ? "timed-out" : result.CompletedTicks == requestedTicks ? "completed" : "incomplete";
            }
            catch (Exception exception)
            {
                result.Status = "failed";
                result.Error = exception.GetType().Name + ": " + exception.Message;
            }
            result.WallMilliseconds = wall.Elapsed.TotalMilliseconds;
            if (samples.Count > 0)
            {
                result.TickTotalMilliseconds = samples.Sum();
                samples.Sort();
                result.TickP50Milliseconds = Percentile(samples, .50);
                result.TickP95Milliseconds = Percentile(samples, .95);
                result.TickP99Milliseconds = Percentile(samples, .99);
                result.TickMaximumMilliseconds = samples[samples.Count - 1];
            }
            return result;
        }

        private static EntityState[] Movers(Match match) => match.CaptureState().Entities.Where(e => e.DefinitionId == "mover").ToArray();
        private static double Percentile(List<double> sorted, double percentile) => sorted[Math.Max(0, (int)Math.Ceiling(sorted.Count * percentile) - 1)];
        private static long DistanceSquared(SimPoint left, SimPoint right)
        {
            long dx = left.X - right.X, dz = left.Z - right.Z;
            return dx * dx + dz * dz;
        }

        private static void SampleGeometry(EntityState[] movers, RuleSet rules, CaseResult result)
        {
            result.GeometrySampleCount++;
            var overlaps = 0;
            var walls = 0;
            for (var i = 0; i < movers.Length; i++)
            {
                var point = movers[i].Position;
                for (var j = i + 1; j < movers.Length; j++)
                    if (DistanceSquared(point, movers[j].Position) < 700L * 700L) overlaps++;
                foreach (var block in rules.Blocks)
                {
                    var closest = new SimPoint(Math.Max(block.X * 1000, Math.Min(point.X, (block.X + block.Width) * 1000)),
                        Math.Max(block.Z * 1000, Math.Min(point.Z, (block.Z + block.Height) * 1000)));
                    if (DistanceSquared(point, closest) < 350L * 350L) walls++;
                }
            }
            result.MaximumOverlappingPairsObserved = Math.Max(result.MaximumOverlappingPairsObserved, overlaps);
            result.MaximumWallIntersectionsObserved = Math.Max(result.MaximumWallIntersectionsObserved, walls);
        }

        private static RuleSet CreateRules(int count)
        {
            var spawns = new List<SpawnDefinition>
            {
                Spawn("base", 0, 4, 4), Spawn("base", 1, 124, 92)
            };
            var columns = count <= 200 ? 20 : 40;
            for (var index = 0; index < count; index++)
                spawns.Add(Spawn("mover", 0, 8 + index % columns * 1.05f, 36 + index / columns * 1.05f));
            return new RuleSet
            {
                VersionId = "navigation-synthetic-v1", ProductTitle = "Navigation benchmark", MapName = "Broad choke",
                TickRate = 20, MapWidth = 128, MapHeight = 96, GatherTicks = 1, CargoAmount = 1,
                Definitions = new[]
                {
                    new EntityDefinition { Id = "base", IsBase = true, IsBuilding = true, MaxHealth = 1000,
                        Radius = 1000, Sight = 1000, SupplyProvided = 2000, BuildTicks = 1 },
                    new EntityDefinition { Id = "mover", MaxHealth = 40, Radius = 350, Speed = 4000,
                        Sight = 2000, SupplyCost = 1, BuildTicks = 1 }
                },
                Blocks = new[]
                {
                    new MapBlock { X = 62, Z = 0, Width = 2, Height = 34 },
                    new MapBlock { X = 62, Z = 62, Width = 2, Height = 34 }
                },
                Spawns = spawns.ToArray()
            };
        }

        private static SpawnDefinition Spawn(string definition, int owner, float x, float z) =>
            new SpawnDefinition { DefinitionId = definition, Owner = owner, Position = SimPoint.World(x, z) };
        private static void Write(string path, Report report) => File.WriteAllText(path, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
    }
}
