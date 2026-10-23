using System.Collections.Generic;
using ColonyRTS.Presentation;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ColonyRTS.Tests
{
    [TestFixture]
    public sealed class ObservedShotPoolTests
    {
        GameObject fixture;
        Material material;
        ObservedShotPool pool;
        Scene scene;

        [SetUp]
        public void SetUp()
        {
            scene=EditorSceneManager.NewPreviewScene();
            fixture=new GameObject("Observed shot pool contract fixture");
            SceneManager.MoveGameObjectToScene(fixture,scene);
            material=new Material(Shader.Find("Sprites/Default"));
            pool=new ObservedShotPool(fixture.transform,material);
        }
        [TearDown]
        public void TearDown()
        {
            pool?.Dispose();
            if(fixture!=null)Object.DestroyImmediate(fixture);
            if(material!=null)Object.DestroyImmediate(material);
            if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void CapacityPreparationCreatesOnlyInactiveResourcesAndNoShots()
        {
            pool.EnsureCapacity(8);
            Assert.That(pool.Capacity,Is.EqualTo(8));
            Assert.That(pool.CreatedCount,Is.EqualTo(8));
            Assert.That(pool.ActiveCount,Is.Zero);Assert.That(pool.ShownCount,Is.Zero);
            var renderers=fixture.GetComponentsInChildren<LineRenderer>(true);
            Assert.That(renderers.Length,Is.EqualTo(8));
            foreach(var renderer in renderers)Assert.That(renderer.gameObject.activeInHierarchy,Is.False);
            pool.EnsureCapacity(3);
            Assert.That(pool.CreatedCount,Is.EqualTo(8),"Capacity preparation must neither recreate resources nor emit fake shots.");
        }

        [TestCase(0)]
        [TestCase(1)]
        public void PooledBeamPreservesTheActualLegacyLineRendererParameters(int owner)
        {
            fixture.transform.SetPositionAndRotation(new Vector3(9,3,7),Quaternion.Euler(7,31,-5));
            fixture.transform.localScale=new Vector3(1.2f,.8f,1.4f);
            var start=new Vector3(2,.8f,3);var end=new Vector3(7,.65f,9);
            pool.Show(start,end,owner,0);
            var actual=OnlyActive();
            var legacy=new GameObject("Legacy observed shot reference").AddComponent<LineRenderer>();
            legacy.transform.SetParent(fixture.transform,false);legacy.sharedMaterial=material;
            legacy.positionCount=2;legacy.SetPositions(new[]{start,end});
            legacy.startWidth=.055f;legacy.endWidth=.015f;
            legacy.startColor=legacy.endColor=owner==0?new Color(.45f,.9f,1,.9f):new Color(1,.5f,.2f,.9f);
            Assert.That(actual.sharedMaterial,Is.SameAs(legacy.sharedMaterial));
            Assert.That(actual.useWorldSpace,Is.EqualTo(legacy.useWorldSpace));
            Assert.That(actual.positionCount,Is.EqualTo(legacy.positionCount));
            Assert.That(actual.GetPosition(0),Is.EqualTo(legacy.GetPosition(0)));
            Assert.That(actual.GetPosition(1),Is.EqualTo(legacy.GetPosition(1)));
            Assert.That(actual.startWidth,Is.EqualTo(legacy.startWidth));Assert.That(actual.endWidth,Is.EqualTo(legacy.endWidth));
            Assert.That(actual.startColor,Is.EqualTo(legacy.startColor));Assert.That(actual.endColor,Is.EqualTo(legacy.endColor));
            Assert.That(actual.loop,Is.EqualTo(legacy.loop));Assert.That(actual.alignment,Is.EqualTo(legacy.alignment));
            Assert.That(actual.textureMode,Is.EqualTo(legacy.textureMode));
            Assert.That(actual.numCornerVertices,Is.EqualTo(legacy.numCornerVertices));Assert.That(actual.numCapVertices,Is.EqualTo(legacy.numCapVertices));
            Assert.That(actual.shadowCastingMode,Is.EqualTo(legacy.shadowCastingMode));Assert.That(actual.receiveShadows,Is.EqualTo(legacy.receiveShadows));
        }

        [Test]
        public void BeamExpiresAtPointZeroSevenOnTheSuppliedUnityClock()
        {
            pool.Show(Vector3.zero,Vector3.one,0,0);
            for(int frame=0;frame<20;frame++)pool.Advance(0);
            Assert.That(pool.ActiveCount,Is.EqualTo(1),"Frame/tick count must not replace scaled Unity time.");
            pool.Advance(.069f);Assert.That(pool.ActiveCount,Is.EqualTo(1));
            pool.Advance(.07f);Assert.That(pool.ActiveCount,Is.Zero);
            foreach(var renderer in fixture.GetComponentsInChildren<LineRenderer>(true))Assert.That(renderer.gameObject.activeInHierarchy,Is.False);
        }

        [Test]
        public void ExpirationReleasesOnlyTheDueBeamAndReuseGetsFreshDataAndDeadline()
        {
            pool.EnsureCapacity(2);
            pool.Show(Vector3.zero,Vector3.one,0,0);
            pool.Show(Vector3.right,Vector3.up,1,.02f);
            pool.Advance(.07f);Assert.That(pool.ActiveCount,Is.EqualTo(1));
            Assert.That(OnlyActive().startColor,Is.EqualTo(LegacyNativeColor(1)));
            pool.Advance(.1f);Assert.That(pool.ActiveCount,Is.Zero);
            pool.Show(new Vector3(8,2,4),new Vector3(3,5,7),0,1);
            var reused=OnlyActive();
            Assert.That(reused.GetPosition(0),Is.EqualTo(new Vector3(8,2,4)));
            Assert.That(reused.GetPosition(1),Is.EqualTo(new Vector3(3,5,7)));
            Assert.That(reused.startColor,Is.EqualTo(LegacyNativeColor(0)));
            pool.Advance(1.069f);Assert.That(pool.ActiveCount,Is.EqualTo(1));
            pool.Advance(1.071f);Assert.That(pool.ActiveCount,Is.Zero);
            Assert.That(pool.CreatedCount,Is.EqualTo(2));
        }

        [Test]
        public void AnEightHundredShotBurstGrowsRatherThanDroppingAnyBeams()
        {
            pool.EnsureCapacity(4);
            for(int i=0;i<800;i++)pool.Show(new Vector3(i,0,0),new Vector3(i,1,1),i%2,0);
            Assert.That(pool.ShownCount,Is.EqualTo(800));Assert.That(pool.ActiveCount,Is.EqualTo(800));
            Assert.That(pool.Capacity,Is.EqualTo(800));Assert.That(pool.CreatedCount,Is.EqualTo(800));
            var sources=new HashSet<int>();
            foreach(var renderer in fixture.GetComponentsInChildren<LineRenderer>())sources.Add((int)renderer.GetPosition(0).x);
            Assert.That(sources.Count,Is.EqualTo(800),"Each real shot needs its own independent live beam.");
            pool.Advance(.07f);Assert.That(pool.ActiveCount,Is.Zero);
        }

        [Test]
        public void WarmBurstsReuseTheSameActorsWithoutNewNativeObjects()
        {
            pool.EnsureCapacity(12);
            var originals=new HashSet<LineRenderer>(fixture.GetComponentsInChildren<LineRenderer>(true));
            for(int burst=0;burst<5;burst++)
            {
                float now=burst;
                for(int i=0;i<12;i++)pool.Show(new Vector3(i,0,burst),new Vector3(i,1,burst),i%2,now);
                Assert.That(pool.ActiveCount,Is.EqualTo(12));
                foreach(var renderer in fixture.GetComponentsInChildren<LineRenderer>())Assert.That(originals.Contains(renderer),Is.True);
                pool.Advance(now+.071f);
                Assert.That(pool.ActiveCount,Is.Zero);Assert.That(pool.CreatedCount,Is.EqualTo(12));
            }
            Assert.That(pool.ShownCount,Is.EqualTo(60));
        }

        [Test]
        public void ResetClearsActiveBeamsWithoutLosingReusableResources()
        {
            pool.EnsureCapacity(3);
            pool.Show(Vector3.zero,Vector3.one,0,0);pool.Show(Vector3.one,Vector3.up,1,0);
            pool.Clear();Assert.That(pool.ActiveCount,Is.Zero);Assert.That(pool.Capacity,Is.EqualTo(3));
            Assert.That(fixture.GetComponentsInChildren<LineRenderer>(),Is.Empty);
            pool.Show(Vector3.right,Vector3.forward,1,.01f);
            Assert.That(pool.ActiveCount,Is.EqualTo(1));Assert.That(pool.CreatedCount,Is.EqualTo(3));
        }

        [Test]
        public void DisposeDestroysOwnedActorsAndPreservesTheBorrowedMaterial()
        {
            pool.EnsureCapacity(3);pool.Show(Vector3.zero,Vector3.one,0,0);
            var renderers=fixture.GetComponentsInChildren<LineRenderer>(true);
            pool.Dispose();pool.Dispose();
            foreach(var renderer in renderers)Assert.That(renderer==null,Is.True);
            Assert.That(fixture.GetComponentsInChildren<LineRenderer>(true),Is.Empty);
            Assert.That(material!=null,Is.True,"The view, rather than the pool, owns its shared material.");
            Assert.That(pool.Capacity,Is.Zero);Assert.That(pool.ActiveCount,Is.Zero);
        }

        Color LegacyNativeColor(int owner)
        {
            var root=new GameObject("Legacy native color reference");
            root.transform.SetParent(fixture.transform,false);root.SetActive(false);
            try
            {
                var reference=root.AddComponent<LineRenderer>();
                reference.startColor=reference.endColor=owner==0?new Color(.45f,.9f,1,.9f):new Color(1,.5f,.2f,.9f);
                // LineRenderer's native gradient storage quantizes components.
                // Compare exact getters from identical authored colors, not a
                // tolerance against the unquantized managed input value.
                return reference.startColor;
            }
            finally{Object.DestroyImmediate(root);}
        }

        LineRenderer OnlyActive()
        {
            var renderers=fixture.GetComponentsInChildren<LineRenderer>();
            Assert.That(renderers.Length,Is.EqualTo(1));return renderers[0];
        }
    }
}
