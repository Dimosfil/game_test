using System.Collections.Generic;
using System.Linq;
using ColonyRTS.Presentation;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ColonyRTS.Tests
{
    [TestFixture]
    public sealed class RenderCacheTests
    {
        private GameObject fixtureRoot;
        private readonly List<Scene> isolatedScenes = new List<Scene>();
        private Scene originalActiveScene;
        private int initialMeshCount;
        private int initialReferences;

        [SetUp]
        public void SetUp()
        {
            initialMeshCount = ProceduralArt.LiveSharedMeshCount;
            initialReferences = ProceduralArt.LiveSharedMeshReferences;
            originalActiveScene = SceneManager.GetActiveScene();
            // Preview scenes do not require saving or replacing the Test Runner's
            // untitled scene, and never become the user's active scene.
            var fixtureScene = EditorSceneManager.NewPreviewScene();
            isolatedScenes.Add(fixtureScene);
            fixtureRoot = new GameObject("Render cache contract fixture");
            SceneManager.MoveGameObjectToScene(fixtureRoot, fixtureScene);
        }

        [TearDown]
        public void TearDown()
        {
            if (fixtureRoot != null) Object.DestroyImmediate(fixtureRoot);
            foreach (var scene in isolatedScenes)
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            isolatedScenes.Clear();
            if (originalActiveScene.IsValid() && originalActiveScene.isLoaded && SceneManager.GetActiveScene() != originalActiveScene)
                SceneManager.SetActiveScene(originalActiveScene);
            Assert.That(ProceduralArt.LiveSharedMeshCount, Is.EqualTo(initialMeshCount), "Destroying all test instances must release their geometry entries.");
            Assert.That(ProceduralArt.LiveSharedMeshReferences, Is.EqualTo(initialReferences), "Every acquired renderer lease must be released.");
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase("marine")]
        [TestCase("worker")]
        [TestCase("turret")]
        [TestCase("command")]
        [TestCase("engineeringbay")]
        [TestCase("armory")]
        public void IdenticalModelsShareGeometryWithoutGrowingWarmCache(string definition)
        {
            var first = Create(definition, 0);
            var firstFilters = Filters(first);
            Assert.That(firstFilters, Is.Not.Empty);
            var warmCount = ProceduralArt.LiveSharedMeshCount;
            var warmReferences = ProceduralArt.LiveSharedMeshReferences;
            var second = Create(definition, 0);
            var secondFilters = Filters(second);
            Assert.That(secondFilters.Keys, Is.EquivalentTo(firstFilters.Keys));
            foreach (var pair in firstFilters)
            {
                AssertValidMesh(pair.Value.sharedMesh);
                Assert.That(secondFilters[pair.Key].sharedMesh, Is.SameAs(pair.Value.sharedMesh), "Same geometry must use the same Unity Mesh: " + pair.Key);
                Assert.That(secondFilters[pair.Key].GetComponent<MeshRenderer>().sharedMaterial,
                    Is.SameAs(pair.Value.GetComponent<MeshRenderer>().sharedMaterial));
            }
            Assert.That(ProceduralArt.LiveSharedMeshCount, Is.EqualTo(warmCount));
            Assert.That(ProceduralArt.LiveSharedMeshReferences, Is.EqualTo(warmReferences + secondFilters.Count));
        }

        [Test]
        public void OwnerPalettesKeepTheirColorsWhileGeometryIsSharedAcrossOwners()
        {
            var blue = Create("marine", 0);
            var count = ProceduralArt.LiveSharedMeshCount;
            var red = Create("marine", 1);
            var neutral = Create("marine", -1);
            Assert.That(ProceduralArt.LiveSharedMeshCount, Is.EqualTo(count), "Palette differences alone cannot duplicate geometry.");
            AssertSharedGeometry(blue, red);
            AssertSharedGeometry(blue, neutral);
            var blueTeam = MaterialWithColor(blue, new Color(.08f, .68f, .86f));
            var redTeam = MaterialWithColor(red, new Color(.85f, .18f, .12f));
            var neutralTeam = MaterialWithColor(neutral, new Color(.54f, .56f, .54f));
            Assert.That(blueTeam, Is.Not.Null);
            Assert.That(redTeam, Is.Not.Null);
            Assert.That(neutralTeam, Is.Not.Null);
            Assert.That(redTeam, Is.Not.SameAs(blueTeam));
            Assert.That(neutralTeam, Is.Not.SameAs(blueTeam));
            Assert.That(blue.GetComponentsInChildren<Renderer>(true).All(renderer => renderer.sharedMaterials.All(material => material.enableInstancing)), Is.True);
            Assert.That(red.GetComponentsInChildren<Renderer>(true).All(renderer => renderer.sharedMaterials.All(material => material.enableInstancing)), Is.True);
        }

        [Test]
        public void AnimationAndInstanceTransformsCannotMutateAnotherInstance()
        {
            var first = Create("marine", 0);
            var second = Create("marine", 0);
            first.transform.localPosition = new Vector3(3, 0, 4);
            second.transform.localPosition = new Vector3(8, 0, 4);
            var firstBefore = Poses(first);
            var secondBefore = Poses(second);
            var geometryBefore = Filters(first).ToDictionary(pair => pair.Key, pair => pair.Value.sharedMesh);
            var boundsBefore = geometryBefore.ToDictionary(pair => pair.Key, pair => pair.Value.bounds);
            Assert.That(first.GetComponent<ArtMotion>(), Is.Not.SameAs(second.GetComponent<ArtMotion>()));
            ProceduralArt.Animate(first, "marine", true, true, .37f);
            Assert.That(first.GetComponentsInChildren<Transform>(true).Any(transform => transform != first.transform && !firstBefore[transform].Matches(transform)), Is.True,
                "The test must exercise a changing rig pose, not merely the root transform.");
            Assert.That(first.transform.localPosition, Is.EqualTo(new Vector3(3, 0, 4)));
            foreach (var pair in secondBefore) Assert.That(pair.Value.Matches(pair.Key), Is.True, "Animating the first rig changed the second: " + pair.Key.name);
            first.transform.localScale = Vector3.one * 2;
            Assert.That(second.transform.localScale, Is.EqualTo(Vector3.one));
            foreach (var pair in Filters(first))
            {
                Assert.That(pair.Value.sharedMesh, Is.SameAs(geometryBefore[pair.Key]));
                Assert.That(pair.Value.sharedMesh.bounds, Is.EqualTo(boundsBefore[pair.Key]), "Instance animation cannot rewrite shared geometry.");
            }
            AssertSharedGeometry(first, second);
        }

        [Test]
        public void DestroyingOneInstancePreservesSurvivorAndAllowsNewInstance()
        {
            var first = Create("worker", 0);
            var survivor = Create("worker", 0);
            var sharedCount = ProceduralArt.LiveSharedMeshCount;
            var references = ProceduralArt.LiveSharedMeshReferences;
            var firstRendererCount = Filters(first).Count;
            Object.DestroyImmediate(first);
            Assert.That(ProceduralArt.LiveSharedMeshCount, Is.EqualTo(sharedCount));
            Assert.That(ProceduralArt.LiveSharedMeshReferences, Is.EqualTo(references - firstRendererCount));
            foreach (var filter in Filters(survivor).Values) AssertValidMesh(filter.sharedMesh);
            ProceduralArt.Animate(survivor, "worker", true, true, .4f);
            var replacement = Create("worker", 0);
            AssertSharedGeometry(survivor, replacement);
            foreach (var filter in Filters(replacement).Values) AssertValidMesh(filter.sharedMesh);
        }

        [Test]
        public void RepeatedParentResetReturnsAllLeaseCountersToBaseline()
        {
            for (var cycle = 0; cycle < 5; cycle++)
            {
                var parent = new GameObject("Temporary observed-entity group " + cycle);
                parent.transform.SetParent(fixtureRoot.transform, false);
                var first = Create("marine", 0, parent.transform);
                var warmCount = ProceduralArt.LiveSharedMeshCount;
                for (var instance = 1; instance < 8; instance++)
                {
                    var next = Create("marine", instance % 3 - 1, parent.transform);
                    AssertSharedGeometry(first, next);
                    Assert.That(ProceduralArt.LiveSharedMeshCount, Is.EqualTo(warmCount));
                }
                Object.DestroyImmediate(parent);
                Assert.That(ProceduralArt.LiveSharedMeshCount, Is.EqualTo(initialMeshCount), "Reset cycle leaked geometry entries: " + cycle);
                Assert.That(ProceduralArt.LiveSharedMeshReferences, Is.EqualTo(initialReferences), "Reset cycle leaked renderer leases: " + cycle);
            }
        }

        [TestCase(ProceduralMeshMode.Shared)]
        [TestCase(ProceduralMeshMode.Unique)]
        public void InstanceCreatedUnderInactiveParentReleasesItsGeometrySafely(ProceduralMeshMode mode)
        {
            var survivor = Create("marine", 0);
            var survivorCount = ProceduralArt.LiveSharedMeshCount;
            var survivorReferences = ProceduralArt.LiveSharedMeshReferences;
            var inactiveParent = new GameObject("Initially inactive art group");
            inactiveParent.transform.SetParent(fixtureRoot.transform, false);
            inactiveParent.SetActive(false);
            var inactive = ProceduralArt.CreateForProfile("marine", 0, inactiveParent.transform, mode);
            Assert.That(inactive.activeInHierarchy, Is.False);
            var ownedMeshes = Filters(inactive).Values.Select(filter => filter.sharedMesh).Distinct().ToArray();
            foreach (var mesh in ownedMeshes) AssertValidMesh(mesh);
            if (mode == ProceduralMeshMode.Shared) AssertSharedGeometry(survivor, inactive);
            Object.DestroyImmediate(inactiveParent);
            Assert.That(ProceduralArt.LiveSharedMeshCount, Is.EqualTo(survivorCount));
            Assert.That(ProceduralArt.LiveSharedMeshReferences, Is.EqualTo(survivorReferences),
                "An instance created under an inactive parent cannot strand a native geometry lease.");
            if (mode == ProceduralMeshMode.Unique)
                Assert.That(ownedMeshes.All(mesh => mesh == null), Is.True);
            foreach (var filter in Filters(survivor).Values) AssertValidMesh(filter.sharedMesh);
        }

        [Test]
        public void UnloadingIsolatedSceneReleasesOnlyItsLeasesAndKeepsSurvivorValid()
        {
            var survivor = Create("marine", 0);
            var survivorMeshCount = ProceduralArt.LiveSharedMeshCount;
            var survivorReferences = ProceduralArt.LiveSharedMeshReferences;
            var scene = EditorSceneManager.NewPreviewScene();
            isolatedScenes.Add(scene);
            var sceneRoot = new GameObject("Isolated art root");
            SceneManager.MoveGameObjectToScene(sceneRoot, scene);
            var unloaded = Create("marine", 1, sceneRoot.transform);
            Assert.That(unloaded.scene, Is.EqualTo(scene), "The test instance must belong to the scene actually being unloaded.");
            AssertSharedGeometry(survivor, unloaded);
            EditorSceneManager.ClosePreviewScene(scene);
            Assert.That(!scene.IsValid() || !scene.isLoaded, Is.True);
            Assert.That(unloaded == null, Is.True);
            Assert.That(ProceduralArt.LiveSharedMeshCount, Is.EqualTo(survivorMeshCount));
            Assert.That(ProceduralArt.LiveSharedMeshReferences, Is.EqualTo(survivorReferences));
            foreach (var filter in Filters(survivor).Values) AssertValidMesh(filter.sharedMesh);
            AssertSharedGeometry(survivor, Create("marine", 0));
        }

        [Test]
        public void UniqueProfileModeOwnsItsMeshesWithoutChangingSharedLeaseCounters()
        {
            var first = ProceduralArt.CreateForProfile("marine", 0, fixtureRoot.transform, ProceduralMeshMode.Unique);
            var second = ProceduralArt.CreateForProfile("marine", 0, fixtureRoot.transform, ProceduralMeshMode.Unique);
            var firstFilters = Filters(first);
            var secondFilters = Filters(second);
            var firstMeshes = firstFilters.Values.Select(filter => filter.sharedMesh).Distinct().ToArray();
            foreach (var pair in firstFilters)
                Assert.That(secondFilters[pair.Key].sharedMesh, Is.Not.SameAs(pair.Value.sharedMesh));
            Assert.That(ProceduralArt.LiveSharedMeshCount, Is.EqualTo(initialMeshCount));
            Assert.That(ProceduralArt.LiveSharedMeshReferences, Is.EqualTo(initialReferences));
            Object.DestroyImmediate(first);
            Assert.That(firstMeshes.All(mesh => mesh == null), Is.True, "Exclusive profiling meshes must be destroyed with their owning instance.");
            foreach (var filter in secondFilters.Values) AssertValidMesh(filter.sharedMesh);
        }

        private GameObject Create(string definition, int owner, Transform parent = null) =>
            ProceduralArt.CreateForProfile(definition, owner, parent == null ? fixtureRoot.transform : parent, ProceduralMeshMode.Shared);

        private static Dictionary<string, MeshFilter> Filters(GameObject root) => root.GetComponentsInChildren<MeshFilter>(true)
            .ToDictionary(filter => RelativeIndexPath(filter.transform, root.transform), filter => filter);

        private static string RelativeIndexPath(Transform child, Transform root)
        {
            var indices = new List<int>();
            for (var current = child; current != root; current = current.parent) indices.Add(current.GetSiblingIndex());
            indices.Reverse();
            return string.Join("/", indices);
        }

        private static void AssertSharedGeometry(GameObject first, GameObject second)
        {
            var left = Filters(first);
            var right = Filters(second);
            Assert.That(right.Keys, Is.EquivalentTo(left.Keys));
            foreach (var pair in left) Assert.That(right[pair.Key].sharedMesh, Is.SameAs(pair.Value.sharedMesh), pair.Key);
        }

        private static void AssertValidMesh(Mesh mesh)
        {
            Assert.That(mesh != null, Is.True, "A renderer references a destroyed mesh.");
            Assert.That(mesh.vertexCount, Is.GreaterThan(0));
            Assert.That(mesh.subMeshCount, Is.GreaterThan(0));
            Assert.That(mesh.GetIndexCount(0), Is.GreaterThan(0));
            Assert.That(mesh.bounds.size.sqrMagnitude, Is.GreaterThan(0));
        }

        private static Material MaterialWithColor(GameObject root, Color expected) => root.GetComponentsInChildren<Renderer>(true)
            .SelectMany(renderer => renderer.sharedMaterials).FirstOrDefault(material =>
                material != null && ((Vector4)material.color - (Vector4)expected).sqrMagnitude < .000001f);

        private sealed class Pose
        {
            private readonly Vector3 position;
            private readonly Quaternion rotation;
            private readonly Vector3 scale;
            private readonly bool active;
            public Pose(Transform transform)
            {
                position = transform.localPosition; rotation = transform.localRotation;
                scale = transform.localScale; active = transform.gameObject.activeSelf;
            }
            public bool Matches(Transform transform) => transform.localPosition == position && transform.localRotation == rotation &&
                transform.localScale == scale && transform.gameObject.activeSelf == active;
        }

        private static Dictionary<Transform, Pose> Poses(GameObject root) =>
            root.GetComponentsInChildren<Transform>(true).ToDictionary(transform => transform, transform => new Pose(transform));
    }
}
