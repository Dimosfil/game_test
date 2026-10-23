using System;
using System.Collections.Generic;
using System.Reflection;
using ColonyRTS.Presentation;
using ColonyRTS.Simulation;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ColonyRTS.Tests
{
    [TestFixture]
    public sealed class BattleHudSelectionCacheTests
    {
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        GameObject fixture;
        Scene scene;
        GameHost host;
        BattleHud hud;
        Action capture;
        EntityState thirty, ten, twenty;

        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.NewPreviewScene();
            fixture = new GameObject("HUD selection publication fixture");
            SceneManager.MoveGameObjectToScene(fixture, scene);
            fixture.SetActive(false);
            thirty = Entity(30, 90); ten = Entity(10, 80); twenty = Entity(20, 70);
            host = CreateHost("First host", Observe(7, thirty, ten, twenty));
            var hudObject = new GameObject("Inactive real BattleHud");
            hudObject.transform.SetParent(fixture.transform, false);
            hud = hudObject.AddComponent<BattleHud>();
            // Capture needs only its host. Avoid product Awake/startup, OnGUI,
            // layout controls and native health resources in these focused tests.
            SetField(hud, "host", host);
            var method = typeof(BattleHud).GetMethod("CaptureGuiSelection", Members);
            Assert.That(method, Is.Not.Null);
            capture = (Action)Delegate.CreateDelegate(typeof(Action), hud, method);
        }

        [TearDown]
        public void TearDown()
        {
            // An inactive component may never receive OnDestroy. The Initialize
            // boundary test creates this owned resource once; dispose explicitly.
            if (hud != null) (GetField(hud, "healthBars") as HudHealthBarBatch)?.Dispose();
            if (fixture != null) Object.DestroyImmediate(fixture);
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void RepeatedCallbacksKeepObservedOrderingReferencesAndPublicSelection()
        {
            int frame = Time.frameCount;
            Select(20, 10);
            capture();
            AssertSelection(new[] {20, 10}, new[] {10, 20}, ten, twenty);
            capture();
            capture();
            AssertSelection(new[] {20, 10}, new[] {10, 20}, ten, twenty);
            Assert.That(Time.frameCount, Is.EqualTo(frame), "No frame advance is needed to call the real callback body again.");
        }

        [Test]
        public void SameFrameReplacementAtSameCountImmediatelyChangesDerivedSelection()
        {
            int frame = Time.frameCount;
            Select(20, 10);
            capture();
            AssertSelection(new[] {20, 10}, new[] {10, 20}, ten, twenty);
            // Mutate the same public List object without changing its length.
            host.Interaction.Selection[0] = 30;
            capture();
            AssertSelection(new[] {30, 10}, new[] {30, 10}, thirty, ten);
            host.Interaction.Selection[1] = 20;
            capture();
            AssertSelection(new[] {30, 20}, new[] {30, 20}, thirty, twenty);
            Assert.That(Time.frameCount, Is.EqualTo(frame));
        }

        [Test]
        public void SameTickNewObservationReplacesEntityReferencesAndRemovesDeadSelection()
        {
            Select(20, 10);
            capture();
            AssertSelection(new[] {20, 10}, new[] {10, 20}, ten, twenty);
            var replacementTen = Entity(10, 35);
            var replacementThirty = Entity(30, 45);
            // Tick/selection/frame are unchanged, but the publication is new and
            // selected ID20 is no longer observed. Never retain old entity objects.
            SetProperty(host, "Observation", Observe(7, replacementThirty, replacementTen));
            capture();
            AssertSelection(new[] {20, 10}, new[] {10, 20}, replacementTen);
            Assert.That(SelectedEntities[0].Health, Is.EqualTo(35));
            Assert.That(SelectedEntities[0], Is.Not.SameAs(ten));
            var returnedTwenty = Entity(20, 55);
            var nextTen = Entity(10, 25);
            SetProperty(host, "Observation", Observe(7, returnedTwenty, nextTen));
            capture();
            AssertSelection(new[] {20, 10}, new[] {10, 20}, returnedTwenty, nextTen);
            Assert.That(SelectedEntities[0], Is.Not.SameAs(twenty));
        }

        [Test]
        public void DuplicateReorderAndGhostEditsPreserveObservedOrderWithoutDeduplicatingInput()
        {
            Select(20, 10, 10, 999);
            capture();
            AssertSelection(new[] {20, 10, 10, 999}, new[] {10, 20, 999}, ten, twenty);
            Select(10, 20, 10, 999);
            capture();
            AssertSelection(new[] {10, 20, 10, 999}, new[] {10, 20, 999}, ten, twenty);
            host.Interaction.Selection[2] = 20;
            capture();
            AssertSelection(new[] {10, 20, 20, 999}, new[] {10, 20, 999}, ten, twenty);
            host.Interaction.Selection[3] = 998;
            capture();
            AssertSelection(new[] {10, 20, 20, 998}, new[] {10, 20, 998}, ten, twenty);
            // A same-count edit after all the duplicate/ghost states must still
            // replace membership; cached caller-owned list aliases are invalid.
            host.Interaction.Selection[0] = 30;
            capture();
            AssertSelection(new[] {30, 20, 20, 998}, new[] {20, 30, 998}, thirty, twenty);
        }

        [Test]
        public void ResetSelectionEmptyCallbacksAndReappearanceUseCurrentPublicList()
        {
            Select(10, 20);
            capture();
            AssertSelection(new[] {10, 20}, new[] {10, 20}, ten, twenty);
            host.Interaction.ResetSelection();
            capture();
            AssertSelection(new int[0], new int[0]);
            capture();
            AssertSelection(new int[0], new int[0]);
            host.Interaction.Selection.Add(30);
            capture();
            AssertSelection(new[] {30}, new[] {30}, thirty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InitializeBoundaryRebindsSameOrDifferentHostWithoutStaleSelectedEntities(bool differentHost)
        {
            Select(20, 10);
            capture();
            AssertSelection(new[] {20, 10}, new[] {10, 20}, ten, twenty);
            var freshTen = Entity(10, 19);
            var freshTwenty = Entity(20, 29);
            var publication = Observe(7, freshTwenty, freshTen);
            var owner = differentHost ? CreateHost("Replacement host", publication) : host;
            SetProperty(owner, "Observation", publication);
            owner.Interaction.Selection.Clear();
            owner.Interaction.Selection.AddRange(new[] {20, 10});
            // One actual Initialize call, with empty definition catalogs: no
            // unsupported double initialization or caption dictionary pollution.
            hud.Initialize(owner);
            host = owner;
            capture();
            AssertSelection(new[] {20, 10}, new[] {10, 20}, freshTwenty, freshTen);
            Assert.That(SelectedEntities[0].Health, Is.EqualTo(29));
            Assert.That(SelectedEntities[1], Is.Not.SameAs(ten));
        }

        GameHost CreateHost(string name, PlayerObservation observation)
        {
            var hostObject = new GameObject(name);
            hostObject.transform.SetParent(fixture.transform, false);
            var owner = hostObject.AddComponent<GameHost>();
            var interaction = hostObject.AddComponent<RtsInteraction>();
            SetProperty(owner, "Interaction", interaction);
            SetProperty(owner, "Observation", observation);
            SetProperty(owner, "Rules", new RuleSet { MapName = "Selection cache fixture", Definitions = new EntityDefinition[0], Upgrades = new UpgradeDefinition[0] });
            return owner;
        }
        void Select(params int[] ids)
        {
            host.Interaction.Selection.Clear();
            host.Interaction.Selection.AddRange(ids);
        }
        static PlayerObservation Observe(int tick, params EntityState[] entities)
        {
            var observation = new PlayerObservation { Tick = tick };
            observation.Entities.AddRange(entities);
            return observation;
        }
        static EntityState Entity(int id, int health) => new EntityState { Id = id, Owner = 0, DefinitionId = "marine", Health = health };
        List<EntityState> SelectedEntities => (List<EntityState>)GetField(hud, "selectedEntities");
        void AssertSelection(int[] expectedInput, int[] expectedMembership, params EntityState[] expectedEntities)
        {
            CollectionAssert.AreEqual(expectedInput, host.Interaction.Selection, "Capture must not reorder, prune or deduplicate the public input.");
            CollectionAssert.AreEquivalent(expectedMembership, (HashSet<int>)GetField(hud, "selectedIds"));
            Assert.That(SelectedEntities.Count, Is.EqualTo(expectedEntities.Length));
            for (int i = 0; i < expectedEntities.Length; i++)
                Assert.That(SelectedEntities[i], Is.SameAs(expectedEntities[i]), "Actual observation entity/reference order at " + i);
        }
        static object GetField(object target, string name) => target.GetType().GetField(name, Members).GetValue(target);
        static void SetField(object target, string name, object value) => target.GetType().GetField(name, Members).SetValue(target, value);
        static void SetProperty(object target, string name, object value) => target.GetType().GetProperty(name, Members).GetSetMethod(true).Invoke(target, new[] {value});
    }
}
