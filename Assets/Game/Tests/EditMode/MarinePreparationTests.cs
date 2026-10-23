using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
    public sealed class MarinePreparationTests
    {
        const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
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
            fixture=new GameObject("Marine preparation contract fixture");
            SceneManager.MoveGameObjectToScene(fixture,scene);
        }

        [TearDown]
        public void TearDown()
        {
            if(fixture!=null)Object.DestroyImmediate(fixture);
            if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);
            Assert.That(ProceduralArt.LiveSharedMeshCount,Is.EqualTo(initialMeshes));
            Assert.That(ProceduralArt.LiveSharedMeshReferences,Is.EqualTo(initialReferences));
            Assert.That(InstancedMarineRenderer.RegisteredInstanceCount,Is.EqualTo(initialRegistered));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ActualPreparationPreservesLegacyGeometryBoundsAndEveryEligiblePart()
        {
            var shader=Shader.Find("Standard");
            if(!SystemInfo.supportsInstancing||shader==null||!shader.isSupported)
                Assert.Ignore("Compact rendering is unavailable on this graphics device.");
            var driver=fixture.AddComponent<InstancedMarineRenderer>();
            // Retain any pre-existing registered Editor actors without changing them.
            Bounds initialBounds=Prepare(driver,out bool initialHasBounds);
            var initial=ReadBatches(driver);
            var legacy=new GameObject[2];var compact=new GameObject[2];
            var renderers=new MeshRenderer[2][];
            for(int owner=0;owner<2;owner++)
            {
                legacy[owner]=ProceduralArt.CreateForProfile("marine",owner,fixture.transform,ProceduralMeshMode.Shared);
                compact[owner]=ProceduralArt.CreateInstanced("marine",owner,fixture.transform);
                renderers[owner]=legacy[owner].GetComponentsInChildren<MeshRenderer>(true);
            }
            var random=new System.Random(6021);
            for(int sample=0;sample<32;sample++)
            {
                fixture.transform.SetPositionAndRotation(Vector(random,12f),Quaternion.Euler(Vector(random,180f)));
                fixture.transform.localScale=SignedScale(random);
                var expected=Clone(initial);
                bool hasBounds=initialHasBounds;Bounds expectedBounds=initialBounds;
                for(int owner=0;owner<2;owner++)
                {
                    var position=Vector(random,4f);var rotation=Quaternion.Euler(Vector(random,180f));
                    var scale=sample==0?new Vector3(0,-1,2):sample==1?new Vector3(1e-12f,-2e-12f,3e-12f):SignedScale(random);
                    bool enabled=sample%7!=6||owner==1;
                    foreach(var root in new[]{legacy[owner],compact[owner]})
                    {
                        root.SetActive(enabled);root.layer=(sample+owner)%3;
                        root.transform.localPosition=position;root.transform.localRotation=rotation;root.transform.localScale=scale;
                        ProceduralArt.Animate(root,"marine",(sample&1)!=0,(sample&2)!=0,sample%2==0?.37f:.41f);
                    }
                    if(!enabled)continue;
                    // Independent legacy root-column oracle retains the former
                    // bounds contract, while vertices below check actual geometry.
                    var rootMatrix=legacy[owner].transform.localToWorldMatrix;
                    float radius=3f*Mathf.Max(((Vector3)rootMatrix.GetColumn(0)).magnitude,
                        Mathf.Max(((Vector3)rootMatrix.GetColumn(1)).magnitude,((Vector3)rootMatrix.GetColumn(2)).magnitude));
                    var modelBounds=new Bounds(rootMatrix.GetColumn(3),Vector3.one*radius*2f);
                    if(hasBounds)expectedBounds.Encapsulate(modelBounds);else{expectedBounds=modelBounds;hasBounds=true;}
                    foreach(var renderer in renderers[owner])
                    {
                        if(!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
                        var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                        Add(expected,Key(mesh,renderer.sharedMaterial,compact[owner].layer),renderer.localToWorldMatrix);
                    }
                }
                Bounds actualBounds=Prepare(driver,out bool actualHasBounds);
                Assert.That(actualHasBounds,Is.EqualTo(hasBounds),"bounds presence sample "+sample);
                AssertVector(expectedBounds.center,actualBounds.center,"bounds center sample "+sample);
                AssertVector(expectedBounds.extents,actualBounds.extents,"bounds extents sample "+sample);
                var actual=ReadBatches(driver);
                Assert.That(actual.Keys,Is.EquivalentTo(expected.Keys),"batch eligibility sample "+sample);
                foreach(var pair in expected)
                {
                    var unmatched=new List<Matrix4x4>(actual[pair.Key]);
                    Assert.That(unmatched.Count,Is.EqualTo(pair.Value.Count),"eligible part count "+pair.Key+" sample "+sample);
                    foreach(var matrix in pair.Value)
                    {
                        int match=unmatched.FindIndex(candidate=>Near(matrix,candidate));
                        Assert.That(match,Is.GreaterThanOrEqualTo(0),"legacy world matrix missing "+pair.Key+" sample "+sample);
                        unmatched.RemoveAt(match);
                    }
                }
                // The full conservative bounds must include every submitted
                // authored vertex, including sheared and reflected geometry.
                var padded=actualBounds;padded.Expand(.0001f);
                foreach(var group in renderers)foreach(var renderer in group)
                {
                    if(!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
                    foreach(var vertex in renderer.GetComponent<MeshFilter>().sharedMesh.vertices)
                        Assert.That(padded.Contains(renderer.localToWorldMatrix.MultiplyPoint3x4(vertex)),Is.True,
                            "submitted legacy geometry clipped by bounds, sample "+sample);
                }
            }
        }

        static Bounds Prepare(InstancedMarineRenderer driver,out bool hasBounds)
        {
            object[] arguments={false};
            var result=(Bounds)typeof(InstancedMarineRenderer).GetMethod("PrepareFrame",Fields).Invoke(driver,arguments);
            hasBounds=(bool)arguments[0];return result;
        }
        static Dictionary<string,List<Matrix4x4>> ReadBatches(InstancedMarineRenderer driver)
        {
            var result=new Dictionary<string,List<Matrix4x4>>();
            foreach(object batch in (IEnumerable)typeof(InstancedMarineRenderer).GetField("orderedBatches",Fields).GetValue(driver))
            {
                var type=batch.GetType();int count=(int)type.GetField("Count",Fields).GetValue(batch);
                if(count==0)continue;
                object key=type.GetField("Key",Fields).GetValue(batch);var keyType=key.GetType();
                string name=Key((Mesh)keyType.GetField("Mesh",Fields).GetValue(key),
                    (Material)keyType.GetField("Material",Fields).GetValue(key),(int)keyType.GetField("Layer",Fields).GetValue(key));
                var matrices=(Matrix4x4[])type.GetField("Matrices",Fields).GetValue(batch);
                for(int i=0;i<count;i++)Add(result,name,matrices[i]);
            }
            return result;
        }
        static Dictionary<string,List<Matrix4x4>> Clone(Dictionary<string,List<Matrix4x4>> source)
        {
            var result=new Dictionary<string,List<Matrix4x4>>();
            foreach(var pair in source)result.Add(pair.Key,new List<Matrix4x4>(pair.Value));return result;
        }
        static string Key(Mesh mesh,Material material,int layer)=>mesh.GetInstanceID()+":"+material.GetInstanceID()+":"+layer;
        static void Add(Dictionary<string,List<Matrix4x4>> target,string key,Matrix4x4 matrix)
        {
            if(!target.TryGetValue(key,out var values)){values=new List<Matrix4x4>();target.Add(key,values);}values.Add(matrix);
        }
        static bool Near(Matrix4x4 a,Matrix4x4 b)
        {
            for(int i=0;i<16;i++)if(Mathf.Abs(a[i]-b[i])>.00005f)return false;return true;
        }
        static void AssertVector(Vector3 expected,Vector3 actual,string context)
        {
            for(int i=0;i<3;i++)Assert.That(actual[i],Is.EqualTo(expected[i]).Within(.00005f),context+" axis "+i);
        }
        static Vector3 Vector(System.Random random,float extent)=>new Vector3(
            (float)(random.NextDouble()*2-1)*extent,(float)(random.NextDouble()*2-1)*extent,(float)(random.NextDouble()*2-1)*extent);
        static Vector3 SignedScale(System.Random random)
        {
            var scale=new Vector3(.1f+(float)random.NextDouble()*2,.1f+(float)random.NextDouble()*2,.1f+(float)random.NextDouble()*2);
            for(int i=0;i<3;i++)if(random.Next(2)==0)scale[i]=-scale[i];return scale;
        }
    }
}
