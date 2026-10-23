using System;
using System.Linq;
using ColonyRTS.Presentation;
using ColonyRTS.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace ColonyRTS.Tests
{
    public sealed class UiCommandCatalogTests
    {
        [Test]
        public void NewCatalogProducerAndUnitAreAvailableWithoutAnyUiIdBranch()
        {
            var rules = SimulationFixture.Rules();
            var factory = new EntityDefinition { Id="qa-factory", DisplayName="Factory", IsBuilding=true, MaxHealth=200, Radius=1000, BuildTicks=1 };
            var unit = new EntityDefinition { Id="qa-soldier", DisplayName="Soldier", ProducerId=factory.Id, MaxHealth=40, Radius=300, BuildTicks=2, MineralCost=25 };
            rules.Definitions = rules.Definitions.Concat(new[] { factory,unit }).ToArray();
            rules.Spawns = rules.Spawns.Concat(new[] { SimulationFixture.Spawn(factory.Id,0,12,20) }).ToArray();
            var match = new Match(rules,false);
            var commands = new UiCommandCatalog(rules,new UiCommandLayout { TrainKey="Q" });
            Assert.That(commands.Buildings.Select(d=>d.Id),Does.Contain(factory.Id));
            int factoryId = SimulationFixture.Id(match,factory.Id);
            var choice = commands.ChooseTraining(match.Observe(0),new[] { factoryId },unit.Id);
            Assert.That(choice.Available,Is.True);
            Assert.That(choice.ProducerId,Is.EqualTo(factoryId));
            Assert.That(SimulationFixture.Send(match,0,CommandKind.Train,choice.ProducerId,definition:choice.DefinitionId).Accepted,Is.True);
            match.Step();
            Assert.That(match.Observe(0).Entities.Any(e=>e.DefinitionId==unit.Id),Is.True);
        }

        [Test]
        public void MixedSelectionChoosesProducerForTheRequestedUnitAndNeverAnEnemy()
        {
            var match = new Match(SimulationFixture.Rules(),false);
            var commands = new UiCommandCatalog(match.Rules,new UiCommandLayout {TrainKey="Q"});
            int home=SimulationFixture.Id(match,"command"), barracks=SimulationFixture.Id(match,"barracks");
            var choice=commands.ChooseTraining(match.Observe(0),new[]{home,barracks},"marine");
            Assert.That(choice.ProducerId,Is.EqualTo(barracks));
            Assert.That(choice.DefinitionId,Is.EqualTo("marine"));
            Assert.That(commands.ChooseTraining(match.Observe(0),new[]{SimulationFixture.Id(match,"command",1)},"worker").Available,Is.False);
        }

        [Test]
        public void ActualResourceKeepsBuildOrderingAndShortcutsAndUsesCatalogCosts()
        {
            var rules=JsonUtility.FromJson<RuleSet>(Resources.Load<TextAsset>("rts-rules").text);
            var commands=UiCommandCatalog.Load(rules);
            CollectionAssert.AreEqual(new[]{"depot","barracks","refinery","turret","engineeringbay","armory"},commands.Buildings.Select(d=>d.Id));
            Assert.That(commands.TrainKey,Is.EqualTo(KeyCode.Q));
            Assert.That(commands.BuildShortcuts.Single(p=>p.Key=="barracks").Value,Is.EqualTo(KeyCode.B));
            Assert.That(commands.TrainingFor("command").Single().MineralCost,Is.EqualTo(rules.Definitions.Single(d=>d.Id=="worker").MineralCost));
        }

        [TestCase("unknown")]
        [TestCase("duplicate-key")]
        [TestCase("invalid-key")]
        public void MalformedBindingsFailAtTheResourceBoundary(string kind)
        {
            var layout=new UiCommandLayout {TrainKey="Q"};
            if(kind=="unknown")layout.BuildOrder=new[]{"missing"};
            if(kind=="invalid-key")layout.TrainKey="None";
            if(kind=="duplicate-key")layout.BuildShortcuts=new[]{new UiCommandLayout.Shortcut {DefinitionId="depot",Key="Q"}};
            Assert.That(()=>new UiCommandCatalog(SimulationFixture.Rules(),layout),Throws.ArgumentException);
        }

        [Test]
        public void ShortcutCaptionsFollowTheSameLayoutAsInputInsteadOfCopiedTranslationKeys()
        {
            var rules=SimulationFixture.Rules();
            var commands=new UiCommandCatalog(rules,new UiCommandLayout {TrainKey="F3",
                BuildShortcuts=new[]{new UiCommandLayout.Shortcut {DefinitionId="barracks",Key="K"}}});
            Assert.That(commands.BuildCaption(rules.Definitions.Single(d=>d.Id=="barracks")),Does.EndWith("[K]"));
            Assert.That(commands.TrainCaption(rules.Definitions.Single(d=>d.Id=="worker")),Does.EndWith("[F3]"));
        }

        [TestCase("Research producer unavailable","Требуется: Producer")]
        [TestCase("Research producer incomplete","Здание ещё строится")]
        [TestCase("Upgrade already completed","Изучено")]
        [TestCase("Upgrade already in progress","Уже исследуется в другом здании")]
        [TestCase("Producer busy","Здание занято")]
        [TestCase("Prerequisite missing","Требуется: Prerequisite")]
        [TestCase("Upgrade prerequisite missing","Требуется предыдущий уровень")]
        [TestCase("Insufficient resources","Недостаточно минералов или веспена")]
        [TestCase("Match is not running","Матч завершён")]
        [TestCase("future-reason","Приказ недоступен")]
        public void ResearchReasonsUseResourceFormats(string reason,string expected)
        {
            var upgrade=new UpgradeDefinition {ProducerId="producer",PrerequisiteId="prerequisite"};
            Assert.That(RuntimeText.ResearchReason(reason,upgrade,id=>id=="producer"?"Producer":"Prerequisite"),Is.EqualTo(expected));
        }
    }
}
