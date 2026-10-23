using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ColonyRTS.Simulation;

namespace ColonyRTS.Diagnostics
{
    // Synthetic CPU workload, paired with the render fixture without changing its
    // camera coverage or visible model count as the simulation army travels away.
    public sealed class GraphicsSimulationFixture
    {
        readonly Match match;
        readonly int[] movers;
        double accumulator;
        public int TickCount => match.Tick;
        public int GroupCommandCount { get; private set; }
        public double FirstGroupCommandStepMs { get; private set; }
        public List<double> SampleStepTimes { get; } = new List<double>(4096);
        public int DiscardedCatchupFrames { get; private set; }

        public GraphicsSimulationFixture(int count)
        {
            int columns=(int)Math.Ceiling(Math.Sqrt(count*2));
            var spawns=new List<SpawnDefinition>
            {
                new SpawnDefinition { DefinitionId="command",Owner=0,Position=SimPoint.World(4,4) },
                new SpawnDefinition { DefinitionId="command",Owner=1,Position=SimPoint.World(154,122) }
            };
            for(int i=0;i<count;i++) spawns.Add(new SpawnDefinition { DefinitionId="marine",Owner=0,Position=SimPoint.World(12+i%columns*1.4f,14+i/columns*1.4f) });
            var rules=new RuleSet
            {
                VersionId="graphics-cpu-fixture-1",TickRate=20,MapWidth=160,MapHeight=128,GatherTicks=28,CargoAmount=5,
                Definitions=new[]
                {
                    new EntityDefinition { Id="command",IsBase=true,IsBuilding=true,MaxHealth=1500,Radius=2000,Sight=6000,SupplyProvided=2000,BuildTicks=1 },
                    new EntityDefinition { Id="marine",MaxHealth=45,Radius=380,Speed=3200,Sight=11000,Damage=6,AttackRange=5200,AttackCooldown=12,BuildTicks=1 }
                },
                Spawns=spawns.ToArray(),Blocks=new MapBlock[0]
            };
            match=new Match(rules,false,42);
            movers=match.Observe(0).Entities.Where(e=>e.DefinitionId=="marine").Select(e=>e.Id).ToArray();
            if(movers.Length!=count) throw new InvalidOperationException("CPU profile fixture has an unexpected army size.");
        }

        public void BeginSamples() { SampleStepTimes.Clear(); DiscardedCatchupFrames=0; }

        public void Advance(double delta)
        {
            // Match the product's bounded fixed-step budget. Report clamping, so
            // slow frames cannot silently appear to have sustained real time.
            if(delta>.25) DiscardedCatchupFrames++;
            accumulator+=Math.Min(delta,.25);
            int budget=16;
            while(accumulator>=.05&&budget-->0)
            {
                if(SampleStepTimes.Count==SampleStepTimes.Capacity)
                    throw new InvalidOperationException("CPU fixture sample capacity exhausted; reserve the requested interval before measuring.");
                bool group=match.Tick==10;
                if(group)
                {
                    match.QueueCommand(match.MakeCommand(0,CommandKind.Move,movers,SimPoint.World(100,85)));
                    GroupCommandCount++;
                }
                long start=Stopwatch.GetTimestamp();
                match.Step();
                // Include the same detached player observation produced each product tick.
                match.Observe(0);
                double ms=(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;
                SampleStepTimes.Add(ms);
                if(group)
                {
                    FirstGroupCommandStepMs=ms;
                    if(!match.LastResults.Any(r=>r.Accepted)) throw new InvalidOperationException("CPU fixture group order was rejected.");
                }
                accumulator-=.05;
            }
        }
    }
}
