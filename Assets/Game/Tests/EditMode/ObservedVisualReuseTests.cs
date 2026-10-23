using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    public sealed class ObservedVisualReuseTests
    {
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        GameObject fixture, viewObject;
        Scene scene;
        GameHost host;
        BattleView view;
        Transform actors;
        int initialMeshes, initialReferences, initialRegistered;

        struct Part
        {
            public Mesh Mesh;
            public Material Material;
            public Matrix4x4 Matrix;
            public bool Active;
        }

        [SetUp]
        public void SetUp()
        {
            initialMeshes = ProceduralArt.LiveSharedMeshCount;
            initialReferences = ProceduralArt.LiveSharedMeshReferences;
            initialRegistered = InstancedMarineRenderer.RegisteredInstanceCount;
            scene = EditorSceneManager.NewPreviewScene();
            fixture = new GameObject("Observed visual lifetime fixture");
            SceneManager.MoveGameObjectToScene(fixture, scene);

            // Do not execute the product startup, create a Match, or touch the open scene.
            var hostObject = Child("Inactive presentation host", fixture.transform);
            hostObject.SetActive(false);
            host = hostObject.AddComponent<GameHost>();
            var rules = SimulationFixture.Rules();
            SetProperty(host, "Rules", rules);
            var catalog = (Dictionary<string, EntityDefinition>)Field(host, "displayCatalog");
            foreach (var definition in rules.Definitions) catalog.Add(definition.Id, definition);

            viewObject = Child("Observed presentation", fixture.transform);
            view = viewObject.AddComponent<BattleView>();
            actors = Child("Observed entities", viewObject.transform).transform;
            var material = new Material(Shader.Find("Sprites/Default"));
            Set(view, "host", host);
            Set(view, "entitiesRoot", actors);
            Set(view, "lineMaterial", material);
            Set(view, "shotPool", new ObservedShotPool(actors, material));
            Set(view, "fogPixels", new Color32[4]);
            Set(view, "fogTexture", new Texture2D(2, 2, TextureFormat.RGBA32, false));
        }

        [TearDown]
        public void TearDown()
        {
            if (fixture != null) Object.DestroyImmediate(fixture);
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            Assert.That(ProceduralArt.LiveSharedMeshCount, Is.EqualTo(initialMeshes));
            Assert.That(ProceduralArt.LiveSharedMeshReferences, Is.EqualTo(initialReferences),
                "Destroying a view must release active and retired prototype leases exactly once.");
            Assert.That(InstancedMarineRenderer.RegisteredInstanceCount, Is.EqualTo(initialRegistered));
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(0)]
        [TestCase(1)]
        public void ReusedMarineRestoresFreshAuthoredPoseTransformSelectionAndShotHistory(int owner)
        {
            RequireInstancing();
            var oldEntity = Entity(10, owner);
            Snapshot(oldEntity);
            object oldVisual = Visual(10);
            var oldRoot = Root(oldVisual);
            ProceduralArt.Animate(oldRoot, "marine", true, true, .1f);
            oldRoot.transform.SetPositionAndRotation(new Vector3(37, 2, -8), Quaternion.Euler(7, 81, -4));
            oldRoot.transform.localScale = new Vector3(.8f, .3f, 1.4f);
            oldRoot.layer = 7;
            Set(oldVisual, "Selected", true);
            Set(oldVisual, "ShotTick", 100);
            ((LineRenderer)Field(oldVisual, "Ring")).enabled = true;
            Publish(1);

            Assert.That(oldRoot.activeSelf, Is.False);
            var next = Entity(11, owner);
            next.Position = SimPoint.World(12, 9);
            Snapshot(next, 5);
            var reused = Root(Visual(11));
            Assert.That(reused, Is.SameAs(oldRoot));
            Assert.That(view.CreatedVisualCount, Is.EqualTo(1));
            Assert.That(view.ReusedVisualCount, Is.EqualTo(1));
            Assert.That(view.PooledCompactVisualCount, Is.Zero);
            Assert.That(reused.activeInHierarchy, Is.True);
            Assert.That(InstancedMarineRenderer.RegisteredInstanceCount, Is.EqualTo(initialRegistered + 1));
            Assert.That(Field(Visual(11), "Selected"), Is.EqualTo(false));
            Assert.That(((LineRenderer)Field(Visual(11), "Ring")).enabled, Is.False);
            Assert.That(Field(Visual(11), "ShotTick"), Is.EqualTo(-1));
            Assert.That(reused.name, Is.EqualTo("marine_11"));

            var fresh = ProceduralArt.CreateInstanced("marine", owner, actors);
            fresh.transform.position = BattleView.Position(next.Position);
            AssertParts(Parts(fresh), Parts(reused), "authored pose before first animation");
            Assert.That(reused.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(reused.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(reused.layer, Is.Zero, "A reused actor must restore the fresh observed root layer.");

            // A lower tick in a new session must still display the newly observed shot.
            var target = Entity(900, 1 - owner);
            ((Dictionary<int, EntityState>)Field(view, "observedById")).Add(target.Id, target);
            next.LastAttackTick = 5;
            next.LastAttackTarget = target.Id;
            Snapshot(next, 5);
            Assert.That(view.DisplayedShotCount, Is.EqualTo(1));
            Snapshot(next, 5);
            Assert.That(view.DisplayedShotCount, Is.EqualTo(1), "One observation must not replay the same shot.");

            foreach (int state in new[] { 0, 3, 0 })
            {
                bool moving = (state & 1) != 0, attacking = (state & 2) != 0;
                ProceduralArt.Animate(fresh, "marine", moving, attacking, .37f);
                ProceduralArt.Animate(reused, "marine", moving, attacking, .37f);
                AssertParts(Parts(fresh), Parts(reused), "animation after reuse state=" + state);
            }
        }

        [Test]
        public void FogWithdrawalRemovesBindingAndRegistrationWithoutUpdatingRetiredActor()
        {
            RequireInstancing();
            var entity = Entity(20, 0);
            Publish(0, entity);
            object retired = Visual(entity.Id);
            var root = Root(retired);
            long created = view.CreatedVisualCount;
            Publish(1);
            Assert.That(Visuals.Contains(entity.Id), Is.False);
            Assert.That(Field(retired, "Snapshot"), Is.Null);
            Assert.That(root.activeSelf, Is.False);
            Assert.That(Parts(root).All(part => !part.Active), Is.True);
            Assert.That(InstancedMarineRenderer.RegisteredInstanceCount, Is.EqualTo(initialRegistered));
            Assert.That(view.PooledCompactVisualCount, Is.EqualTo(1));
            var before = Parts(root);
            var position = root.transform.position;
            entity.Position = SimPoint.World(100, 100);
            entity.LastAttackTick = 2;
            Publish(2);
            Assert.That(root.transform.position, Is.EqualTo(position));
            AssertParts(before, Parts(root), "hidden retired actor receives no publication updates");
            Assert.That(view.CreatedVisualCount, Is.EqualTo(created));
            Assert.That(view.DisplayedShotCount, Is.Zero);
            Assert.That(view.PooledCompactVisualCount, Is.EqualTo(1));
        }

        [Test]
        public void ReuseKeepsOwnerPoolsAndPalettesSeparate()
        {
            RequireInstancing();
            Snapshot(Entity(30, 0));
            var blue = Root(Visual(30));
            var blueRingColor = ((LineRenderer)Field(Visual(30), "Ring")).startColor;
            Publish(1);
            Snapshot(Entity(31, 1));
            var red = Root(Visual(31));
            var redRingColor = ((LineRenderer)Field(Visual(31), "Ring")).startColor;
            Assert.That(red, Is.Not.SameAs(blue));
            Assert.That(view.CreatedVisualCount, Is.EqualTo(2));
            Assert.That(view.ReusedVisualCount, Is.Zero);
            Assert.That(Parts(blue).Zip(Parts(red), (a, b) => a.Material != b.Material).Any(different => different), Is.True,
                "The fixture must exercise different owner palettes.");
            Publish(2);
            Snapshot(Entity(32, 0));
            Snapshot(Entity(33, 1));
            Assert.That(Root(Visual(32)), Is.SameAs(blue));
            Assert.That(Root(Visual(33)), Is.SameAs(red));
            Assert.That(view.ReusedVisualCount, Is.EqualTo(2));
            Assert.That(view.PooledCompactVisualCount, Is.Zero);
            Assert.That(((LineRenderer)Field(Visual(32), "Ring")).startColor, Is.EqualTo(blueRingColor));
            Assert.That(((LineRenderer)Field(Visual(33), "Ring")).startColor, Is.EqualTo(redRingColor));
        }

        [Test]
        public void NoncompactWorkerIsDestroyedInsteadOfEnteringMarinePool()
        {
            Snapshot(Entity(40, 0, "worker"));
            var root = Root(Visual(40));
            Assert.That(root.GetComponent<InstancedArtInstance>(), Is.Null);
            int identity = root.GetInstanceID();
            Publish(1);
            Assert.That(root == null, Is.True);
            Assert.That(view.PooledCompactVisualCount, Is.Zero);
            Snapshot(Entity(41, 0, "worker"));
            Assert.That(Root(Visual(41)).GetInstanceID(), Is.Not.EqualTo(identity));
            Assert.That(view.CreatedVisualCount, Is.EqualTo(2));
            Assert.That(view.ReusedVisualCount, Is.Zero);
        }

        [Test]
        public void ResetRetainsOnlyObservedResourcesAndFinalCleanupReleasesAllLeases()
        {
            RequireInstancing();
            var first = Entity(50, 0);
            var neighbor = Entity(51, 0);
            var enemy = Entity(52, 1);
            Publish(0, first, neighbor, enemy);
            var neighborRoot = Root(Visual(51));
            Publish(1, neighbor);
            ProceduralArt.Animate(neighborRoot, "marine", true, true, .1f);
            var neighborPose = Parts(neighborRoot);
            Snapshot(Entity(53, 0));
            AssertParts(neighborPose, Parts(neighborRoot), "reusing a neighbor's prototype must preserve this actor");
            long roots = view.CreatedVisualCount;
            int references = ProceduralArt.LiveSharedMeshReferences;
            view.ResetViews();
            Assert.That(Visuals.Count, Is.Zero);
            Assert.That(view.PooledCompactVisualCount, Is.EqualTo(3));
            Assert.That(InstancedMarineRenderer.RegisteredInstanceCount, Is.EqualTo(initialRegistered));
            Assert.That(ProceduralArt.LiveSharedMeshReferences, Is.EqualTo(references),
                "Reset keeps existing compact resources for a later observed session.");
            view.ResetViews();
            Assert.That(view.PooledCompactVisualCount, Is.EqualTo(3), "Repeated reset must not retire a root twice.");
            Snapshot(Entity(54, 0));
            Assert.That(view.CreatedVisualCount, Is.EqualTo(roots));
            Assert.That(view.PooledCompactVisualCount, Is.EqualTo(2));
            Object.DestroyImmediate(viewObject);
            Assert.That(ProceduralArt.LiveSharedMeshCount, Is.EqualTo(initialMeshes));
            Assert.That(ProceduralArt.LiveSharedMeshReferences, Is.EqualTo(initialReferences));
            Assert.That(InstancedMarineRenderer.RegisteredInstanceCount, Is.EqualTo(initialRegistered));
        }

        [Test]
        public void KillingShotUsesPreviousObservedTargetBeforeItsVisualIsRetired()
        {
            RequireInstancing();
            var attacker = Entity(60, 0);
            var target = Entity(61, 1);
            Publish(0, attacker, target);
            var targetRoot = Root(Visual(target.Id));
            attacker = Entity(60, 0);
            attacker.LastAttackTick = 1;
            attacker.LastAttackTarget = target.Id;
            Publish(1, attacker);
            Assert.That(view.DisplayedShotCount, Is.EqualTo(1),
                "Lethal-shot fallback needs the previous detached target until all new snapshots are updated.");
            Assert.That(Visuals.Contains(target.Id), Is.False);
            Assert.That(targetRoot.activeSelf, Is.False);
            Assert.That(view.PooledCompactVisualCount, Is.EqualTo(1));
        }

        IDictionary Visuals => (IDictionary)Field(view, "visuals");
        object Visual(int id) => Visuals[id];
        static GameObject Root(object visual) => (GameObject)Field(visual, "Root");

        void Snapshot(EntityState entity, int tick = 0)
        {
            Set(view, "observedTick", tick);
            typeof(BattleView).GetMethod("UpdateSnapshot", Members).Invoke(view, new object[] { entity, false, .1f });
        }

        void Publish(int tick, params EntityState[] entities)
        {
            SetProperty(host, "Observation", new PlayerObservation
            {
                Tick = tick, Visible = new bool[4], Explored = new bool[4],
                Entities = new List<EntityState>(entities)
            });
            typeof(BattleView).GetMethod("UpdateViews", Members).Invoke(view, null);
        }

        static EntityState Entity(int id, int owner, string definition = "marine") => new EntityState
        {
            Id = id, Owner = owner, DefinitionId = definition, Completed = true,
            Health = definition == "marine" ? 40 : 45, LastAttackTick = -100,
            Position = SimPoint.World(4 + id % 7, 6 + id % 5)
        };

        static Part[] Parts(GameObject root)
        {
            var result = new List<Part>();
            for (int i = 0; ProceduralArt.TryGetInstancedPart(root, i, out var mesh, out var material, out var matrix, out var active); i++)
                result.Add(new Part { Mesh = mesh, Material = material, Matrix = matrix, Active = active });
            Assert.That(result.Count, Is.GreaterThan(10), "Exercise the complete authored compact marine.");
            return result.ToArray();
        }

        static void AssertParts(Part[] expected, Part[] actual, string context)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length), context);
            for (int part = 0; part < expected.Length; part++)
            {
                Assert.That(actual[part].Mesh, Is.SameAs(expected[part].Mesh), context + " mesh " + part);
                Assert.That(actual[part].Material, Is.SameAs(expected[part].Material), context + " palette " + part);
                Assert.That(actual[part].Active, Is.EqualTo(expected[part].Active), context + " active " + part);
                for (int element = 0; element < 16; element++)
                    Assert.That(actual[part].Matrix[element], Is.EqualTo(expected[part].Matrix[element]).Within(.00002f),
                        context + " part " + part + " matrix element " + element);
            }
        }

        static object Field(object target, string name) => target.GetType().GetField(name, Members).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Members).SetValue(target, value);
        static void SetProperty(object target, string name, object value) => target.GetType().GetProperty(name, Members).SetValue(target, value);
        static GameObject Child(string name, Transform parent)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }
        static void RequireInstancing()
        {
            var shader = Shader.Find("Standard");
            if (!SystemInfo.supportsInstancing || shader == null || !shader.isSupported)
                Assert.Ignore("This graphics device uses the ordinary renderer fallback rather than compact marines.");
        }
    }
}
