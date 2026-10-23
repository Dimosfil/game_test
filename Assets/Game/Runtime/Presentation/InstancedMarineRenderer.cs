using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColonyRTS.Presentation
{
    /// <summary>Logical authored geometry, independent of physical renderer implementation.</summary>
    public sealed class ArtRenderInventory
    {
        public int LogicalRendererCount, ActiveLogicalRendererCount;
        public long SummedVertices, SummedTriangles;
        readonly HashSet<Mesh> meshes=new HashSet<Mesh>();
        readonly HashSet<Material> materials=new HashSet<Material>();
        public int UniqueMeshCount=>meshes.Count;
        public int SharedMaterialCount=>materials.Count;

        internal void Add(Mesh mesh,Material material,bool active)
        {
            if(mesh==null)return;
            LogicalRendererCount++;if(active)ActiveLogicalRendererCount++;
            SummedVertices+=mesh.vertexCount;
            for(int submesh=0;submesh<mesh.subMeshCount;submesh++)SummedTriangles+=(long)mesh.GetIndexCount(submesh)/3;
            meshes.Add(mesh);if(material!=null)materials.Add(material);
        }
        internal static void Accumulate(GameObject root,ArtRenderInventory inventory)
        {
            if(inventory==null)throw new ArgumentNullException(nameof(inventory));
            if(root==null)return;
            var compact=root.GetComponent<InstancedArtInstance>();
            if(compact!=null){compact.Accumulate(inventory);return;}
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter=renderer.GetComponent<MeshFilter>();
                if(filter!=null)inventory.Add(filter.sharedMesh,renderer.sharedMaterial,renderer.enabled&&renderer.gameObject.activeInHierarchy);
            }
        }
    }

    internal static class MarineMatrixMath
    {
        // The fast path is restricted to affine matrices. Bounded coefficients
        // also guarantee finite intermediates through the four-composition
        // root/body/tool/flash/part chain: 1e20 * (4 * 1024)^4 < float.MaxValue.
        // Exceptional/projective inputs retain Unity's full operator semantics.
        internal static bool IsBoundedAffine(in Matrix4x4 value,float bound)
        {
            return value.m30==0f&&value.m31==0f&&value.m32==0f&&value.m33==1f
                &&Within(value.m00,bound)&&Within(value.m01,bound)&&Within(value.m02,bound)&&Within(value.m03,bound)
                &&Within(value.m10,bound)&&Within(value.m11,bound)&&Within(value.m12,bound)&&Within(value.m13,bound)
                &&Within(value.m20,bound)&&Within(value.m21,bound)&&Within(value.m22,bound)&&Within(value.m23,bound);
        }
        static bool Within(float value,float bound)=>value>=-bound&&value<=bound;
        internal static Matrix4x4 ComposeAffine(in Matrix4x4 a,in Matrix4x4 b)
        {
            Matrix4x4 result;
            // Keep the operator's left-to-right sum order; omit only affine
            // bottom-row zero products, without regrouping the transform graph.
            result.m00=a.m00*b.m00+a.m01*b.m10+a.m02*b.m20;
            result.m01=a.m00*b.m01+a.m01*b.m11+a.m02*b.m21;
            result.m02=a.m00*b.m02+a.m01*b.m12+a.m02*b.m22;
            result.m03=a.m00*b.m03+a.m01*b.m13+a.m02*b.m23+a.m03;
            result.m10=a.m10*b.m00+a.m11*b.m10+a.m12*b.m20;
            result.m11=a.m10*b.m01+a.m11*b.m11+a.m12*b.m21;
            result.m12=a.m10*b.m02+a.m11*b.m12+a.m12*b.m22;
            result.m13=a.m10*b.m03+a.m11*b.m13+a.m12*b.m23+a.m13;
            result.m20=a.m20*b.m00+a.m21*b.m10+a.m22*b.m20;
            result.m21=a.m20*b.m01+a.m21*b.m11+a.m22*b.m21;
            result.m22=a.m20*b.m02+a.m21*b.m12+a.m22*b.m22;
            result.m23=a.m20*b.m03+a.m21*b.m13+a.m22*b.m23+a.m23;
            result.m30=0f;result.m31=0f;result.m32=0f;result.m33=1f;
            return result;
        }
    }

    // A prototype is captured from the existing authoring, then the temporary
    // hierarchy is discarded. Explicit mesh leases outlive that hierarchy and
    // expire with the last compact instance; no hidden renderer pool is retained.
    internal sealed class MarineRenderPrototype
    {
        internal struct PoseFrame
        {
            internal bool Valid,Flash,AffineSafe;
            internal float Time;
            internal Matrix4x4 Left,Right,Body,Tool;
        }
        internal struct Part
        {
            internal Mesh Mesh;
            internal Material Material;
            internal ArtSharedMeshCache.Entry Lease;
            internal Matrix4x4 Local;
            internal int Node;
            internal bool IdentityLocal;
        }
        internal readonly int Owner;
        internal readonly Part[] Parts;
        internal readonly Vector3 BodyOrigin, LeftOrigin, RightOrigin, ToolOrigin;
        internal readonly Matrix4x4 FlashLocal;
        internal readonly bool AffinePartsSafe;
        readonly Matrix4x4 idleLeft,idleRight,idleTool;
        readonly PoseFrame[] poseFrames=new PoseFrame[4];
        internal int References;

        internal MarineRenderPrototype(int owner)
        {
            Owner=owner;
            var template=ProceduralArt.CreateForProfile("marine",owner,null,ProceduralMeshMode.Shared);
            try
            {
                var rig=template.GetComponent<ArtMotion>();
                BodyOrigin=rig.Body.localPosition;LeftOrigin=rig.LeftLeg.localPosition;
                RightOrigin=rig.RightLeg.localPosition;ToolOrigin=rig.Tool.localPosition;
                // These are the exact zero-angle TRS values used by the legacy
                // movement/recoil cessation paths, not substitute Translate poses.
                var zeroRotation=Quaternion.Euler(0f,0f,0f);
                idleLeft=Matrix4x4.TRS(LeftOrigin,zeroRotation,Vector3.one);
                idleRight=Matrix4x4.TRS(RightOrigin,zeroRotation,Vector3.one);
                idleTool=Matrix4x4.TRS(ToolOrigin,zeroRotation,Vector3.one);
                FlashLocal=Matrix4x4.TRS(rig.Flash.transform.localPosition,rig.Flash.transform.localRotation,rig.Flash.transform.localScale);
                var renderers=template.GetComponentsInChildren<MeshRenderer>(true);
                Parts=new Part[renderers.Length];
                bool affinePartsSafe=MarineMatrixMath.IsBoundedAffine(in FlashLocal,1024f);
                for(int i=0;i<renderers.Length;i++)
                {
                    var renderer=renderers[i];var node=template.transform;int kind=0;
                    if(renderer.transform.IsChildOf(rig.Flash.transform)){node=rig.Flash.transform;kind=5;}
                    else if(renderer.transform.IsChildOf(rig.Tool)){node=rig.Tool;kind=4;}
                    else if(renderer.transform.IsChildOf(rig.Body)){node=rig.Body;kind=3;}
                    else if(renderer.transform.IsChildOf(rig.LeftLeg)){node=rig.LeftLeg;kind=1;}
                    else if(renderer.transform.IsChildOf(rig.RightLeg)){node=rig.RightLeg;kind=2;}
                    var lease=renderer.GetComponent<ArtMeshLifetime>().SharedLease;
                    ArtSharedMeshCache.Retain(lease);
                    var local=renderer.transform.parent==node
                        ?Matrix4x4.TRS(renderer.transform.localPosition,renderer.transform.localRotation,renderer.transform.localScale)
                        :node.worldToLocalMatrix*renderer.localToWorldMatrix;
                    Parts[i]=new Part{Mesh=lease.Mesh,Material=renderer.sharedMaterial,Lease=lease,Node=kind,
                        Local=local,IdentityLocal=local.Equals(Matrix4x4.identity)};
                    affinePartsSafe&=MarineMatrixMath.IsBoundedAffine(in local,1024f);
                }
                AffinePartsSafe=affinePartsSafe;
            }
            catch
            {
                if(Parts!=null)foreach(var part in Parts)ArtSharedMeshCache.Release(part.Lease);
                throw;
            }
            finally
            {
                template.SetActive(false);
                if(Application.isPlaying)UnityEngine.Object.Destroy(template);else UnityEngine.Object.DestroyImmediate(template);
            }
        }
        internal ref readonly PoseFrame GetPose(bool moving,bool attacking,float time)
        {
            ref var frame=ref poseFrames[(moving?1:0)|(attacking?2:0)];
            if(frame.Valid&&frame.Time==time)return ref frame;
            float stride=moving?Mathf.Sin(time*15f):0f;
            frame.Left=moving?Matrix4x4.TRS(LeftOrigin,Quaternion.Euler(stride*23f,0,0),Vector3.one):idleLeft;
            frame.Right=moving?Matrix4x4.TRS(RightOrigin,Quaternion.Euler(-stride*23f,0,0),Vector3.one):idleRight;
            frame.Body=Matrix4x4.TRS(BodyOrigin+Vector3.up*(moving?Mathf.Abs(stride)*.035f:Mathf.Sin(time*2.5f)*.009f),
                Quaternion.Euler(moving?5f:0f,0,moving?stride*3f:0f),Vector3.one);
            frame.Tool=attacking?Matrix4x4.TRS(ToolOrigin,Quaternion.Euler(-9f+Mathf.Sin(time*34f)*4f,0,0),Vector3.one):idleTool;
            frame.Flash=attacking&&Mathf.Repeat(time*11f,1f)<.44f;
            frame.AffineSafe=MarineMatrixMath.IsBoundedAffine(in frame.Left,1024f)
                &&MarineMatrixMath.IsBoundedAffine(in frame.Right,1024f)
                &&MarineMatrixMath.IsBoundedAffine(in frame.Body,1024f)
                &&MarineMatrixMath.IsBoundedAffine(in frame.Tool,1024f);
            frame.Time=time;frame.Valid=true;
            return ref frame;
        }
        internal void ReleaseMeshes(){foreach(var part in Parts)ArtSharedMeshCache.Release(part.Lease);}
    }

    /// <summary>One observable entity anchor; animation is matrix state rather than native child transforms.</summary>
    [ExecuteAlways]
    public sealed class InstancedArtInstance : MonoBehaviour
    {
        internal MarineRenderPrototype Prototype;
        readonly Matrix4x4[] pose=new Matrix4x4[6];
        readonly Matrix4x4[] worldPose=new Matrix4x4[6];
        bool flash,wasMoving,wasAttacking,poseAffineSafe,worldAffineSafe;
        internal Transform Anchor;
        internal bool Registered;
        internal int RegisteredIndex=-1;
        internal bool CapacityReserved;
        internal InstancedMarineRenderer.Batch[] RenderBatches;
        internal InstancedMarineRenderer BatchOwner;
        internal int BatchLayer=-1;
        void Awake(){Anchor=transform;}
        internal void Initialize(MarineRenderPrototype prototype)
        {
            Prototype=prototype;Anchor=transform;Prototype.References++;
            ResetAuthoredPose();
            if(isActiveAndEnabled)InstancedMarineRenderer.Register(this);
        }
        internal void ResetAuthoredPose()
        {
            var prototype=Prototype;
            if(prototype==null)return;
            // Before the first Animate, the ordinary hierarchy has its authored pose.
            pose[0]=Matrix4x4.identity;
            pose[1]=Matrix4x4.Translate(prototype.LeftOrigin);
            pose[2]=Matrix4x4.Translate(prototype.RightOrigin);
            pose[3]=Matrix4x4.Translate(prototype.BodyOrigin);
            pose[4]=Matrix4x4.Translate(prototype.ToolOrigin);
            pose[5]=prototype.FlashLocal;
            poseAffineSafe=true;
            for(int i=1;i<pose.Length;i++)poseAffineSafe&=MarineMatrixMath.IsBoundedAffine(in pose[i],1024f);
            flash=false;wasMoving=false;wasAttacking=false;worldAffineSafe=false;
        }
        internal void Animate(bool moving,bool attacking,float time)
        {
            if(Prototype==null)return;
            ref readonly var frame=ref Prototype.GetPose(moving,attacking,time);
            if(moving||wasMoving)
            {
                pose[1]=frame.Left;pose[2]=frame.Right;
            }
            pose[3]=frame.Body;
            if(attacking||wasAttacking)pose[4]=frame.Tool;
            // Invalid animated legs/tools can only have been written while their
            // corresponding flag was true; the cessation guards above reset them.
            poseAffineSafe=frame.AffineSafe;
            flash=frame.Flash;
            wasMoving=moving;wasAttacking=attacking;
        }
        internal bool PrepareWorld(in Matrix4x4 root,bool includeInactiveFlash=true)
        {
            worldAffineSafe=poseAffineSafe&&Prototype.AffinePartsSafe&&MarineMatrixMath.IsBoundedAffine(in root,1e20f);
            worldPose[0]=root;
            if(worldAffineSafe)
            {
                for(int i=1;i<=3;i++)worldPose[i]=MarineMatrixMath.ComposeAffine(in root,in pose[i]);
                worldPose[4]=MarineMatrixMath.ComposeAffine(in worldPose[3],in pose[4]);
                if(flash||includeInactiveFlash)worldPose[5]=MarineMatrixMath.ComposeAffine(in worldPose[4],in pose[5]);
            }
            else
            {
                for(int i=1;i<=3;i++)worldPose[i]=root*pose[i];
                worldPose[4]=worldPose[3]*pose[4];
                if(flash||includeInactiveFlash)worldPose[5]=worldPose[4]*pose[5];
            }
            return worldAffineSafe;
        }
        internal Matrix4x4 PartMatrix(int index)
        {
            var part=Prototype.Parts[index];
            if(part.IdentityLocal)return worldPose[part.Node];
            return worldAffineSafe?MarineMatrixMath.ComposeAffine(in worldPose[part.Node],in part.Local):worldPose[part.Node]*part.Local;
        }
        internal bool PartVisible(int index)=>Prototype.Parts[index].Node!=5||flash;
        public bool TryGetPart(int index,out Mesh mesh,out Material material,out Matrix4x4 matrix,out bool active)
        {
            mesh=null;material=null;matrix=Matrix4x4.identity;active=false;
            if(Prototype==null||index<0||index>=Prototype.Parts.Length)return false;
            var part=Prototype.Parts[index];mesh=part.Mesh;material=part.Material;
            PrepareWorld(Anchor.localToWorldMatrix);
            matrix=PartMatrix(index);active=isActiveAndEnabled&&PartVisible(index);return true;
        }
        internal void Accumulate(ArtRenderInventory inventory)
        {
            if(Prototype==null)return;
            for(int i=0;i<Prototype.Parts.Length;i++)
                inventory.Add(Prototype.Parts[i].Mesh,Prototype.Parts[i].Material,isActiveAndEnabled&&PartVisible(i));
        }
        void OnEnable(){if(Prototype!=null)InstancedMarineRenderer.Register(this);}
        void OnDisable(){InstancedMarineRenderer.Unregister(this);}
        void OnDestroy()
        {
            InstancedMarineRenderer.Unregister(this);
            if(Prototype!=null){InstancedMarineRenderer.Release(Prototype);Prototype=null;}
        }
    }

    // Runs after BattleView's interpolation. Each batch retains grow-only capacity
    // while instances live, and submits all parts including offscreen shadow casters.
    [DefaultExecutionOrder(1000)]
    public sealed class InstancedMarineRenderer : MonoBehaviour
    {
        internal struct BatchKey : IEquatable<BatchKey>
        {
            internal Mesh Mesh;internal Material Material;internal int Layer;
            public bool Equals(BatchKey other)=>Mesh==other.Mesh&&Material==other.Material&&Layer==other.Layer;
            public override bool Equals(object other)=>other is BatchKey key&&Equals(key);
            public override int GetHashCode()=>unchecked((Mesh.GetInstanceID()*397^Material.GetInstanceID())*31+Layer);
        }
        internal sealed class Batch
        {
            internal readonly BatchKey Key;
            internal Matrix4x4[] Matrices=new Matrix4x4[64];
            internal int Count,PotentialCount;
            internal Batch(BatchKey key){Key=key;}
            internal void EnsureCapacity(int required)
            {
                if(required<=Matrices.Length)return;
                int capacity=Matrices.Length;
                while(capacity<required)capacity=checked(capacity*2);
                Array.Resize(ref Matrices,capacity);
                MatrixBufferGrowthCount++;
            }
            internal void Add(Matrix4x4 matrix)
            {
                EnsureCapacity(Count+1);
                Matrices[Count++]=matrix;
            }
        }
        static readonly Dictionary<int,MarineRenderPrototype> prototypes=new Dictionary<int,MarineRenderPrototype>();
        static readonly List<InstancedArtInstance> instances=new List<InstancedArtInstance>();
        static InstancedMarineRenderer driver;
        readonly Dictionary<BatchKey,Batch> batches=new Dictionary<BatchKey,Batch>();
        readonly List<Batch> orderedBatches=new List<Batch>();
        public static long MatrixBufferGrowthCount{get;private set;}
        public static int RegisteredInstanceCount=>instances.Count;
        public static double DiagnosticSubmitMs{get;private set;}
        public static double DiagnosticPrepareMs{get;private set;}
        public static double DiagnosticDrawMs{get;private set;}
        public static int DiagnosticSubmitFrame{get;private set;}=-1;

        internal static GameObject Create(string definition,int owner,Transform parent)
        {
            var shader=Shader.Find("Standard");
            if(definition!="marine"||!SystemInfo.supportsInstancing||shader==null||!shader.isSupported)
                return ProceduralArt.CreateForProfile(definition,owner,parent,ProceduralMeshMode.Shared);
            if(!prototypes.TryGetValue(owner,out var prototype))
            {
                prototype=new MarineRenderPrototype(owner);prototypes.Add(owner,prototype);
            }
            var root=new GameObject("Art_"+definition);
            // Activate lifecycle before attaching an inactive parent, including previews.
            var instance=root.AddComponent<InstancedArtInstance>();
            var motion=root.AddComponent<ArtMotion>();motion.Instanced=instance;
            root.transform.SetParent(parent,false);
            instance.Initialize(prototype);
            return root;
        }
        internal static void Register(InstancedArtInstance instance)
        {
            if(instance.Registered)return;
            instance.Registered=true;instance.RegisteredIndex=instances.Count;instances.Add(instance);
            if(Application.isPlaying&&driver==null)
            {
                var root=new GameObject("Compact marine rendering");
                driver=root.AddComponent<InstancedMarineRenderer>();
                DontDestroyOnLoad(root);
            }
        }
        internal static void Unregister(InstancedArtInstance instance)
        {
            if(!instance.Registered)return;
            int index=instance.RegisteredIndex,last=instances.Count-1;
            if(index<0||index>last||!ReferenceEquals(instances[index],instance))
                throw new InvalidOperationException("Compact renderer registration index is inconsistent.");
            ReleaseCapacityReservation(instance);
            var moved=instances[last];instances[index]=moved;moved.RegisteredIndex=index;
            instances.RemoveAt(last);instance.RegisteredIndex=-1;instance.Registered=false;
        }
        static void ReleaseCapacityReservation(InstancedArtInstance instance)
        {
            if(!instance.CapacityReserved)return;
            foreach(var batch in instance.RenderBatches)
            {
                if(batch.PotentialCount<=0)throw new InvalidOperationException("Compact renderer capacity reservation is inconsistent.");
                batch.PotentialCount--;
            }
            instance.CapacityReserved=false;
        }
        internal static void Release(MarineRenderPrototype prototype)
        {
            if(--prototype.References>0)return;
            prototypes.Remove(prototype.Owner);
            if(driver!=null)driver.RemoveUnusedBatches();
            prototype.ReleaseMeshes();
            if(prototypes.Count==0&&driver!=null)
            {
                var released=driver;driver=null;released.enabled=false;
                if(Application.isPlaying)Destroy(released.gameObject);else DestroyImmediate(released.gameObject);
            }
        }
        void RemoveUnusedBatches()
        {
            // Surviving owners retain their exact prepared buffers and cached
            // references. Evict before releasing meshes, while keys are valid.
            for(int i=orderedBatches.Count-1;i>=0;i--)
            {
                var batch=orderedBatches[i];bool used=false;
                foreach(var prototype in prototypes.Values)
                {
                    foreach(var part in prototype.Parts)
                        if(part.Mesh==batch.Key.Mesh&&part.Material==batch.Key.Material){used=true;break;}
                    if(used)break;
                }
                if(used)continue;
                batches.Remove(batch.Key);orderedBatches.RemoveAt(i);
            }
        }
        void LateUpdate()
        {
            var host=GameHost.Instance;
            bool profiling=host!=null&&host.DiagnosticProfilingEnabled;
            long started=profiling?System.Diagnostics.Stopwatch.GetTimestamp():0;
            try{SubmitFrame(profiling);}
            finally
            {
                if(profiling)
                {
                    DiagnosticSubmitMs=(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000.0/System.Diagnostics.Stopwatch.Frequency;
                    DiagnosticSubmitFrame=Time.frameCount;
                }
            }
        }
        Bounds PrepareFrame(out bool hasBounds)
        {
            foreach(var batch in orderedBatches)batch.Count=0;
            hasBounds=false;Bounds bounds=default;
            for(int n=0;n<instances.Count;n++)
            {
                var instance=instances[n];if(instance==null||!instance.isActiveAndEnabled)continue;
                var root=instance.Anchor.localToWorldMatrix;
                bool affineSafe=instance.PrepareWorld(in root,false);
                // Conservative full-model bounds retain offscreen shadow casters.
                float radius;
                if(affineSafe)
                {
                    float x=root.m00*root.m00+root.m10*root.m10+root.m20*root.m20;
                    float y=root.m01*root.m01+root.m11*root.m11+root.m21*root.m21;
                    float z=root.m02*root.m02+root.m12*root.m12+root.m22*root.m22;
                    radius=3f*Mathf.Sqrt(Mathf.Max(x,Mathf.Max(y,z)));
                }
                else radius=3f*Mathf.Max(((Vector3)root.GetColumn(0)).magnitude,Mathf.Max(((Vector3)root.GetColumn(1)).magnitude,((Vector3)root.GetColumn(2)).magnitude));
                var modelBounds=new Bounds(root.GetColumn(3),Vector3.one*radius*2f);
                if(hasBounds)bounds.Encapsulate(modelBounds);else{bounds=modelBounds;hasBounds=true;}
                var parts=instance.Prototype.Parts;int layer=instance.gameObject.layer;
                if(instance.BatchOwner!=this||instance.BatchLayer!=layer)
                {
                    ReleaseCapacityReservation(instance);
                    if(instance.RenderBatches==null)instance.RenderBatches=new Batch[parts.Length];
                    for(int i=0;i<parts.Length;i++)
                    {
                        var key=new BatchKey{Mesh=parts[i].Mesh,Material=parts[i].Material,Layer=layer};
                        if(!batches.TryGetValue(key,out var batch))
                        {
                            batch=new Batch(key);batches.Add(key,batch);orderedBatches.Add(batch);
                        }
                        instance.RenderBatches[i]=batch;
                    }
                    instance.BatchOwner=this;instance.BatchLayer=layer;
                }
                if(!instance.CapacityReserved)
                {
                    // Reserve every possible part of this actual observed anchor,
                    // including currently inactive flash. First combat activation
                    // must not grow a buffer that could be prepared with its model.
                    foreach(var batch in instance.RenderBatches)
                    {
                        batch.PotentialCount++;
                        batch.EnsureCapacity(batch.PotentialCount);
                    }
                    instance.CapacityReserved=true;
                }
                for(int i=0;i<parts.Length;i++)
                {
                    if(!instance.PartVisible(i))continue;
                    instance.RenderBatches[i].Add(instance.PartMatrix(i));
                }
            }
            return bounds;
        }
        void SubmitFrame(bool profiling)
        {
            if(profiling){DiagnosticPrepareMs=0;DiagnosticDrawMs=0;}
            long prepareStarted=profiling?System.Diagnostics.Stopwatch.GetTimestamp():0;
            Bounds bounds;bool hasBounds;
            try{bounds=PrepareFrame(out hasBounds);}
            finally
            {
                if(profiling)DiagnosticPrepareMs=(System.Diagnostics.Stopwatch.GetTimestamp()-prepareStarted)*1000.0/System.Diagnostics.Stopwatch.Frequency;
            }
            if(!hasBounds)return;
            long drawStarted=profiling?System.Diagnostics.Stopwatch.GetTimestamp():0;
            try
            {
                foreach(var batch in orderedBatches)
                {
                    if(batch.Count==0)continue;
                    var parameters=new RenderParams(batch.Key.Material){worldBounds=bounds,layer=batch.Key.Layer,
                        shadowCastingMode=ShadowCastingMode.On,receiveShadows=true,lightProbeUsage=LightProbeUsage.BlendProbes,
                        reflectionProbeUsage=ReflectionProbeUsage.BlendProbes,renderingLayerMask=1};
                    // Standard supplies both object-to-world and its inverse: the
                    // documented default limit is 511, not 1023. No shader alteration.
                    for(int start=0;start<batch.Count;start+=511)
                        Graphics.RenderMeshInstanced(parameters,batch.Key.Mesh,0,batch.Matrices,Mathf.Min(511,batch.Count-start),start);
                }
            }
            finally
            {
                if(profiling)DiagnosticDrawMs=(System.Diagnostics.Stopwatch.GetTimestamp()-drawStarted)*1000.0/System.Diagnostics.Stopwatch.Frequency;
            }
        }
    }
}
