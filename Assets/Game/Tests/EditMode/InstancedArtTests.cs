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
    public sealed class InstancedArtTests
    {
        GameObject fixture;
        Scene scene;
        int initialMeshes,initialReferences,initialRegistered;

        [SetUp]
        public void SetUp()
        {
            initialMeshes=ProceduralArt.LiveSharedMeshCount;
            initialReferences=ProceduralArt.LiveSharedMeshReferences;
            initialRegistered=InstancedMarineRenderer.RegisteredInstanceCount;
            scene=EditorSceneManager.NewPreviewScene();
            fixture=new GameObject("Compact rendering contract fixture");
            SceneManager.MoveGameObjectToScene(fixture,scene);
        }

        [TearDown]
        public void TearDown()
        {
            if(fixture!=null)Object.DestroyImmediate(fixture);
            if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);
            Assert.That(ProceduralArt.LiveSharedMeshCount,Is.EqualTo(initialMeshes),"Owned compact prototypes must release every geometry entry.");
            Assert.That(ProceduralArt.LiveSharedMeshReferences,Is.EqualTo(initialReferences),"Owned compact prototypes must release every geometry lease.");
            Assert.That(InstancedMarineRenderer.RegisteredInstanceCount,Is.EqualTo(initialRegistered),"Owned anchors must leave the active registration set.");
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(0,false,false)]
        [TestCase(0,true,false)]
        [TestCase(0,false,true)]
        [TestCase(0,true,true)]
        [TestCase(1,false,false)]
        [TestCase(1,true,false)]
        [TestCase(1,false,true)]
        [TestCase(1,true,true)]
        public void EveryAuthoredPartPreservesMeshPaletteAndAnimatedWorldPose(int owner,bool moving,bool attacking)
        {
            RequireInstancing();
            fixture.transform.SetPositionAndRotation(new Vector3(7,2,-11),Quaternion.Euler(0,31,0));
            fixture.transform.localScale=new Vector3(1.2f,.9f,.8f);
            var legacy=ProceduralArt.CreateForProfile("marine",owner,fixture.transform,ProceduralMeshMode.Shared);
            var compact=Compact(owner,fixture.transform);
            foreach(var root in new[]{legacy,compact})
            {
                root.transform.localPosition=new Vector3(2,.3f,4);
                root.transform.localRotation=Quaternion.Euler(7,63,-4);
                root.transform.localScale=new Vector3(.85f,1.3f,1.1f);
            }
            var renderers=legacy.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(renderers.Length,Is.GreaterThan(10),"The fixture must contain the complete authored marine.");
            Assert.That(compact.GetComponentsInChildren<MeshRenderer>(true),Is.Empty,"Compact rendering must not retain a per-instance native hierarchy.");
            foreach(float time in new[]{0f,.1f,.37f,1f})
            {
                ProceduralArt.Animate(legacy,"marine",moving,attacking,time);
                ProceduralArt.Animate(compact,"marine",moving,attacking,time);
                AssertParts(legacy,compact,renderers,"time="+time);
            }
            // Exercise walking/recoil cessation, rather than only fresh idle rigs.
            ProceduralArt.Animate(legacy,"marine",false,false,1.1f);
            ProceduralArt.Animate(compact,"marine",false,false,1.1f);
            AssertParts(legacy,compact,renderers,"return to idle");
            ProceduralArt.Animate(legacy,"marine",false,false,1.2f);
            ProceduralArt.Animate(compact,"marine",false,false,1.2f);
            AssertParts(legacy,compact,renderers,"retained idle leg and rifle poses with continuing body bob");
            Assert.That(ProceduralArt.TryGetInstancedPart(compact,renderers.Length,out _,out _,out _,out _),Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void SamePrototypeTimeChangesCannotMutateAnotherInstancesRetainedPose(int owner)
        {
            RequireInstancing();
            fixture.transform.SetPositionAndRotation(new Vector3(-3,2,8),Quaternion.Euler(9,27,-3));
            fixture.transform.localScale=new Vector3(.8f,1.3f,1.1f);
            var legacyA=ProceduralArt.CreateForProfile("marine",owner,fixture.transform,ProceduralMeshMode.Shared);
            var compactA=Compact(owner,fixture.transform);
            var legacyB=ProceduralArt.CreateForProfile("marine",owner,fixture.transform,ProceduralMeshMode.Shared);
            var compactB=Compact(owner,fixture.transform);
            foreach(var root in new[]{legacyA,compactA})
            {
                root.transform.localPosition=new Vector3(2,.1f,-4);
                root.transform.localRotation=Quaternion.Euler(5,71,-8);
                root.transform.localScale=new Vector3(.9f,1.2f,.7f);
            }
            foreach(var root in new[]{legacyB,compactB})
            {
                root.transform.localPosition=new Vector3(-5,.4f,3);
                root.transform.localRotation=Quaternion.Euler(-7,13,4);
                root.transform.localScale=new Vector3(1.4f,.8f,1.2f);
            }
            var partsA=legacyA.GetComponentsInChildren<MeshRenderer>(true);
            var partsB=legacyB.GetComponentsInChildren<MeshRenderer>(true);
            for(int state=0;state<4;state++)
            {
                bool moving=(state&1)!=0,attacking=(state&2)!=0;
                ProceduralArt.Animate(legacyA,"marine",moving,attacking,.37f);
                ProceduralArt.Animate(compactA,"marine",moving,attacking,.37f);
                AssertParts(legacyA,compactA,partsA,"A before shared-state cache replacement "+state);
                // Both calls overwrite the same prototype/state cache entry at
                // different times. A is deliberately not animated again.
                foreach(float time in new[]{.1f,-.2f})
                {
                    ProceduralArt.Animate(legacyB,"marine",moving,attacking,time);
                    ProceduralArt.Animate(compactB,"marine",moving,attacking,time);
                    AssertParts(legacyB,compactB,partsB,"B nonmonotonic time="+time+" state="+state);
                    AssertParts(legacyA,compactA,partsA,"A retained after B time="+time+" state="+state);
                }
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        public void SharedTimeMixedStateTransitionsMatchLegacyAndKeepNeighborIndependent(int owner)
        {
            RequireInstancing();
            var legacy=ProceduralArt.CreateForProfile("marine",owner,fixture.transform,ProceduralMeshMode.Shared);
            var compact=Compact(owner,fixture.transform);
            var neighbor=Compact(owner,fixture.transform);
            var renderers=legacy.GetComponentsInChildren<MeshRenderer>(true);
            // Revisit cached idle and attacking states after movement/recoil so
            // the cessation guards must still reset the instance's prior pose.
            foreach(int state in new[]{0,1,0,2,3,2,0,3,0})
            {
                bool moving=(state&1)!=0,attacking=(state&2)!=0;
                ProceduralArt.Animate(legacy,"marine",moving,attacking,.1f);
                ProceduralArt.Animate(compact,"marine",moving,attacking,.1f);
                AssertParts(legacy,compact,renderers,"same-time transition state="+state);
                // A different state at the identical timestamp must not reuse a
                // pose by time alone or change the already-copied active flash.
                ProceduralArt.Animate(neighbor,"marine",!moving,!attacking,.1f);
                AssertParts(legacy,compact,renderers,"retained after neighbor opposite state="+state);
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        public void RandomSignedNonuniformParentTrsPreservesEveryLegacyPart(int owner)
        {
            RequireInstancing();
            var random=new System.Random(1739+owner);
            var legacy=ProceduralArt.CreateForProfile("marine",owner,fixture.transform,ProceduralMeshMode.Shared);
            var compact=Compact(owner,fixture.transform);
            var renderers=legacy.GetComponentsInChildren<MeshRenderer>(true);
            for(int sample=0;sample<32;sample++)
            {
                fixture.transform.SetPositionAndRotation(RandomVector(random,12f),Quaternion.Euler(RandomVector(random,180f)));
                fixture.transform.localScale=RandomSignedScale(random);
                var position=RandomVector(random,4f);
                var rotation=Quaternion.Euler(RandomVector(random,180f));
                var scale=RandomSignedScale(random);
                foreach(var root in new[]{legacy,compact})
                {
                    root.transform.localPosition=position;
                    root.transform.localRotation=rotation;
                    root.transform.localScale=scale;
                }
                bool moving=(sample&1)!=0,attacking=(sample&2)!=0;
                float time=(sample-12)*.137f;
                ProceduralArt.Animate(legacy,"marine",moving,attacking,time);
                ProceduralArt.Animate(compact,"marine",moving,attacking,time);
                // Nested rotations with signed, unequal scales create sheared
                // world matrices; a decomposed/reassociated TRS is not equivalent.
                AssertParts(legacy,compact,renderers,"random signed/sheared sample="+sample);
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        public void SingularTinyAndLargeFiniteRootCasesPreserveInactiveLegacyGeometry(int owner)
        {
            RequireInstancing();
            var legacy=ProceduralArt.CreateForProfile("marine",owner,fixture.transform,ProceduralMeshMode.Shared);
            var compact=Compact(owner,fixture.transform);
            var renderers=legacy.GetComponentsInChildren<MeshRenderer>(true);
            var parentScales=new[]{new Vector3(-2f,.5f,3f),new Vector3(0f,-1f,2f),new Vector3(1e-12f,-2e-12f,3e-12f),Vector3.one};
            var rootScales=new[]{new Vector3(.1f,-1.5f,2f),new Vector3(-1f,0f,.5f),new Vector3(-.1f,3f,.2f),new Vector3(-1.5f,.5f,2f)};
            for(int sample=0;sample<parentScales.Length;sample++)
            {
                fixture.transform.localPosition=Vector3.zero;
                fixture.transform.localRotation=Quaternion.Euler(11f,37f,-23f);
                fixture.transform.localScale=parentScales[sample];
                foreach(var root in new[]{legacy,compact})
                {
                    // This finite translation exceeds the documented bounded
                    // affine domain and must use the original full operator.
                    root.transform.localPosition=sample==3?new Vector3(2e21f,-3e21f,1e21f):new Vector3(-2f,.3f,4f);
                    root.transform.localRotation=Quaternion.Euler(-7f,61f,19f);
                    root.transform.localScale=rootScales[sample];
                }
                foreach(int state in new[]{3,0,2,0})
                {
                    bool moving=(state&1)!=0,attacking=(state&2)!=0;
                    ProceduralArt.Animate(legacy,"marine",moving,attacking,.37f);
                    ProceduralArt.Animate(compact,"marine",moving,attacking,.37f);
                    // AssertParts requests inactive flash matrices as well; an
                    // inactive submission optimization must not leave them stale.
                    AssertParts(legacy,compact,renderers,"edge root="+sample+" state="+state);
                }
            }
        }

        [Test]
        public void DisabledRootReappearsWithSameGeometryAndIndependentPose()
        {
            RequireInstancing();
            var first=Compact(0,fixture.transform);
            var second=Compact(0,fixture.transform);
            ProceduralArt.Animate(first,"marine",true,true,.37f);
            ProceduralArt.Animate(second,"marine",false,false,.1f);
            var initial=Inventory(first);
            Assert.That(initial.ActiveLogicalRendererCount,Is.GreaterThan(0));
            Assert.That(ProceduralArt.TryGetInstancedPart(first,0,out var shared,out _,out var firstPose,out _),Is.True);
            Assert.That(ProceduralArt.TryGetInstancedPart(second,0,out var neighborMesh,out _,out var secondPose,out _),Is.True);
            Assert.That(neighborMesh,Is.SameAs(shared));
            Assert.That(firstPose.Equals(secondPose),Is.False,"Independent moving and idle leg poses must differ.");
            first.SetActive(false);
            Assert.That(Inventory(first).ActiveLogicalRendererCount,Is.Zero);
            Assert.That(Inventory(second).ActiveLogicalRendererCount,Is.GreaterThan(0));
            Assert.That(ProceduralArt.TryGetInstancedPart(first,0,out var disabledMesh,out _,out _,out var active),Is.True);
            Assert.That(active,Is.False);Assert.That(disabledMesh,Is.SameAs(shared));
            first.SetActive(true);
            Assert.That(Inventory(first).ActiveLogicalRendererCount,Is.EqualTo(initial.ActiveLogicalRendererCount));
            Assert.That(ProceduralArt.TryGetInstancedPart(first,0,out _,out _,out var restored,out _),Is.True);
            AssertMatrix(firstPose,restored,"reactivated pose");
        }

        [Test]
        public void InactiveParentConstructionAndDestructionCannotInvalidateNeighborGeometry()
        {
            RequireInstancing();
            var survivor=Compact(0,fixture.transform);
            var referenceCount=ProceduralArt.LiveSharedMeshReferences;
            var meshCount=ProceduralArt.LiveSharedMeshCount;
            Assert.That(ProceduralArt.TryGetInstancedPart(survivor,0,out var mesh,out _,out _,out _),Is.True);
            var inactiveParent=new GameObject("Initially inactive parent");
            inactiveParent.transform.SetParent(fixture.transform,false);inactiveParent.SetActive(false);
            var inactive=Compact(0,inactiveParent.transform);
            Assert.That(Inventory(inactive).ActiveLogicalRendererCount,Is.Zero);
            Assert.That(ProceduralArt.LiveSharedMeshCount,Is.EqualTo(meshCount));
            Assert.That(ProceduralArt.LiveSharedMeshReferences,Is.EqualTo(referenceCount),"A prototype lease is shared, not reacquired per anchor.");
            Object.DestroyImmediate(inactiveParent);
            Assert.That(mesh!=null,Is.True,"Destroying a never-enabled anchor must preserve the survivor's geometry.");
            Assert.That(ProceduralArt.LiveSharedMeshReferences,Is.EqualTo(referenceCount));
            Object.DestroyImmediate(survivor);
            Assert.That(ProceduralArt.LiveSharedMeshCount,Is.EqualTo(initialMeshes));
            Assert.That(ProceduralArt.LiveSharedMeshReferences,Is.EqualTo(initialReferences));
        }

        [Test]
        public void PalettesShareGeometryAndLastOwnerCleanupPreservesOtherOwner()
        {
            RequireInstancing();
            var cyan=Compact(0,fixture.transform);var red=Compact(1,fixture.transform);
            var cyanInventory=Inventory(cyan);var redInventory=Inventory(red);
            Assert.That(cyanInventory.UniqueMeshCount,Is.EqualTo(redInventory.UniqueMeshCount));
            bool differentPalette=false;
            for(int i=0;i<cyanInventory.LogicalRendererCount;i++)
            {
                Assert.That(ProceduralArt.TryGetInstancedPart(cyan,i,out var cyanMesh,out var cyanMaterial,out _,out _),Is.True);
                Assert.That(ProceduralArt.TryGetInstancedPart(red,i,out var redMesh,out var redMaterial,out _,out _),Is.True);
                Assert.That(cyanMesh,Is.SameAs(redMesh));
                differentPalette|=cyanMaterial!=redMaterial;
            }
            Assert.That(differentPalette,Is.True,"Teams must retain their distinct authored palettes.");
            Object.DestroyImmediate(cyan);
            for(int i=0;i<redInventory.LogicalRendererCount;i++)
            {
                Assert.That(ProceduralArt.TryGetInstancedPart(red,i,out var mesh,out _,out _,out _),Is.True);
                Assert.That(mesh!=null,Is.True,"Last cyan anchor must not destroy geometry still owned by red.");
            }
            Object.DestroyImmediate(red);
            Assert.That(ProceduralArt.LiveSharedMeshReferences,Is.EqualTo(initialReferences));
        }

        [Test]
        public void RemovingMiddleThenMovedTailAnchorsPreservesSurvivorsAndReactivation()
        {
            RequireInstancing();
            var roots=new GameObject[6];
            for(int i=0;i<roots.Length;i++)roots[i]=Compact(i%2,fixture.transform);
            Assert.That(InstancedMarineRenderer.RegisteredInstanceCount,Is.EqualTo(initialRegistered+6));
            // Removing the middle moves the tail registration. Removing that
            // moved anchor next must use its updated index rather than stale data.
            roots[1].SetActive(false);roots[5].SetActive(false);
            Assert.That(InstancedMarineRenderer.RegisteredInstanceCount,Is.EqualTo(initialRegistered+4));
            Object.DestroyImmediate(roots[2]);
            Assert.That(InstancedMarineRenderer.RegisteredInstanceCount,Is.EqualTo(initialRegistered+3));
            roots[1].SetActive(true);roots[5].SetActive(true);
            Assert.That(InstancedMarineRenderer.RegisteredInstanceCount,Is.EqualTo(initialRegistered+5));
            for(int i=0;i<roots.Length;i++)
            {
                if(roots[i]==null)continue;
                ProceduralArt.Animate(roots[i],"marine",i%2==0,true,.37f);
                var inventory=Inventory(roots[i]);
                Assert.That(inventory.ActiveLogicalRendererCount,Is.EqualTo(inventory.LogicalRendererCount),"Every surviving anchor includes its active muzzle flash.");
                for(int part=0;part<inventory.LogicalRendererCount;part++)
                {
                    Assert.That(ProceduralArt.TryGetInstancedPart(roots[i],part,out var mesh,out var material,out _,out var active),Is.True);
                    Assert.That(mesh!=null&&material!=null&&active,Is.True,"Registry removal must not invalidate surviving part geometry/palettes.");
                }
            }
            // Arbitrary destruction order also exercises swap-back updates after
            // reactivation, with both owner prototypes alive until their last root.
            foreach(int i in new[]{3,1,0,5,4})Object.DestroyImmediate(roots[i]);
            Assert.That(InstancedMarineRenderer.RegisteredInstanceCount,Is.EqualTo(initialRegistered));
        }

        [Test]
        public void ParentActivationAndMixedOwnerGroupDeathPreserveNeighborRegistration()
        {
            RequireInstancing();
            var survivor=Compact(0,fixture.transform);
            var group=new GameObject("Observed group");group.transform.SetParent(fixture.transform,false);
            for(int i=0;i<7;i++)Compact(i%2,group.transform);
            Assert.That(InstancedMarineRenderer.RegisteredInstanceCount,Is.EqualTo(initialRegistered+8));
            group.SetActive(false);
            Assert.That(InstancedMarineRenderer.RegisteredInstanceCount,Is.EqualTo(initialRegistered+1));
            foreach(var child in group.GetComponentsInChildren<InstancedArtInstance>(true))
                Assert.That(Inventory(child.gameObject).ActiveLogicalRendererCount,Is.Zero);
            group.SetActive(true);
            Assert.That(InstancedMarineRenderer.RegisteredInstanceCount,Is.EqualTo(initialRegistered+8));
            Object.DestroyImmediate(group);
            Assert.That(InstancedMarineRenderer.RegisteredInstanceCount,Is.EqualTo(initialRegistered+1));
            ProceduralArt.Animate(survivor,"marine",true,true,.37f);
            var inventory=Inventory(survivor);
            Assert.That(inventory.ActiveLogicalRendererCount,Is.EqualTo(inventory.LogicalRendererCount));
            Assert.That(ProceduralArt.TryGetInstancedPart(survivor,0,out var mesh,out _,out _,out var active),Is.True);
            Assert.That(mesh!=null&&active,Is.True,"Group death must not unregister or destroy an unrelated survivor.");
        }

        [Test]
        public void NonMarineDefinitionsUseTheOrdinarySharedRendererFallback()
        {
            var worker=ProceduralArt.CreateInstanced("worker",0,fixture.transform);
            Assert.That(worker.GetComponent<InstancedArtInstance>(),Is.Null);
            Assert.That(worker.GetComponentsInChildren<MeshRenderer>(true),Is.Not.Empty);
            Assert.That(Inventory(worker).LogicalRendererCount,Is.GreaterThan(0));
        }

        static void RequireInstancing()
        {
            var shader=Shader.Find("Standard");
            if(!SystemInfo.supportsInstancing||shader==null||!shader.isSupported)
                Assert.Ignore("This graphics device does not support the compact instancing path; product uses the ordinary renderer fallback.");
        }
        static Vector3 RandomVector(System.Random random,float extent)
        {
            return new Vector3((float)(random.NextDouble()*2-1)*extent,(float)(random.NextDouble()*2-1)*extent,(float)(random.NextDouble()*2-1)*extent);
        }
        static Vector3 RandomSignedScale(System.Random random)
        {
            var value=new Vector3(.1f+(float)random.NextDouble()*2.4f,.1f+(float)random.NextDouble()*2.4f,.1f+(float)random.NextDouble()*2.4f);
            if(random.Next(2)==0)value.x=-value.x;
            if(random.Next(2)==0)value.y=-value.y;
            if(random.Next(2)==0)value.z=-value.z;
            return value;
        }
        static GameObject Compact(int owner,Transform parent)
        {
            var root=ProceduralArt.CreateInstanced("marine",owner,parent);
            Assert.That(root.GetComponent<InstancedArtInstance>(),Is.Not.Null,"A supported device must create the compact backend.");
            return root;
        }
        static ArtRenderInventory Inventory(GameObject root)
        {
            var result=new ArtRenderInventory();ProceduralArt.AccumulateRenderInventory(root,result);return result;
        }
        static void AssertParts(GameObject legacy,GameObject compact,MeshRenderer[] renderers,string context)
        {
            for(int i=0;i<renderers.Length;i++)
            {
                Assert.That(ProceduralArt.TryGetInstancedPart(compact,i,out var mesh,out var material,out var world,out var active),Is.True,context+" part "+i);
                Assert.That(mesh,Is.SameAs(renderers[i].GetComponent<MeshFilter>().sharedMesh),context+" mesh "+i);
                Assert.That(material,Is.SameAs(renderers[i].sharedMaterial),context+" material "+i);
                Assert.That(active,Is.EqualTo(renderers[i].enabled&&renderers[i].gameObject.activeInHierarchy),context+" visibility "+i);
                AssertMatrix(renderers[i].localToWorldMatrix,world,context+" matrix "+i);
            }
            var a=Inventory(legacy);var b=Inventory(compact);
            Assert.That(b.LogicalRendererCount,Is.EqualTo(a.LogicalRendererCount));
            Assert.That(b.ActiveLogicalRendererCount,Is.EqualTo(a.ActiveLogicalRendererCount));
            Assert.That(b.SummedVertices,Is.EqualTo(a.SummedVertices));
            Assert.That(b.SummedTriangles,Is.EqualTo(a.SummedTriangles));
            Assert.That(b.UniqueMeshCount,Is.EqualTo(a.UniqueMeshCount));
            Assert.That(b.SharedMaterialCount,Is.EqualTo(a.SharedMaterialCount));
        }
        static void AssertMatrix(Matrix4x4 expected,Matrix4x4 actual,string context)
        {
            for(int i=0;i<16;i++)Assert.That(actual[i],Is.EqualTo(expected[i]).Within(.00002f),context+" element "+i);
        }
    }
}
