using System.Linq;
using ColonyRTS.Diagnostics;
using ColonyRTS.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace ColonyRTS.Tests
{
    public sealed class CombatProfileFixtureTests
    {
        [Test]
        public void CombatFixtureKeepsCatalogAndRealBasesWithExactlyFourHundredMarinesPerSide()
        {
            var asset=Resources.Load<TextAsset>("rts-rules");
            Assert.That(asset,Is.Not.Null);
            var rules=JsonUtility.FromJson<RuleSet>(asset.text);
            var definitions=rules.Definitions.ToDictionary(d=>d.Id,d=>JsonUtility.ToJson(d));
            var nonMarineSpawns=rules.Spawns.Where(s=>s.DefinitionId!="marine").Select(s=>JsonUtility.ToJson(s)).ToArray();
            var fixture=SessionPerformanceProfile.CreateCombatFixtureRules(rules);
            foreach(var definition in fixture.Definitions)
                Assert.That(JsonUtility.ToJson(definition),Is.EqualTo(definitions[definition.Id]),"The performance fixture must not alter combat stats.");
            Assert.That(fixture.Spawns.Where(s=>s.DefinitionId!="marine").Select(s=>JsonUtility.ToJson(s)),Is.EqualTo(nonMarineSpawns));
            var match=new Match(fixture,false);
            var initial=match.CaptureCombatDiagnostics();
            Assert.That(initial.LiveMarines0,Is.EqualTo(400));Assert.That(initial.LiveMarines1,Is.EqualTo(400));
            Assert.That(initial.MarineHealth0+initial.MarineHealth1,Is.EqualTo(800L*fixture.Definitions.Single(d=>d.Id=="marine").MaxHealth));
            Assert.That(match.Observe(0).Entities.Count(e=>e.DefinitionId=="marine"),Is.EqualTo(800),"All fighters must be initially observed.");
            match.Step();
            var after=match.CaptureCombatDiagnostics();
            Assert.That(match.Observe(0).Phase,Is.EqualTo(MatchPhase.Running),"Retained bases must keep the real match running.");
            Assert.That(after.Shots0,Is.GreaterThan(0));Assert.That(after.Shots1,Is.GreaterThan(0));
            Assert.That(after.MarineHealth0+after.MarineHealth1,Is.LessThan(initial.MarineHealth0+initial.MarineHealth1),"Ordinary shots must remove ordinary health.");
        }
    }
}
