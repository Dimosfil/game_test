using System;
using System.Collections.Generic;
using ColonyRTS.Simulation;
using UnityEngine;

namespace ColonyRTS.Presentation
{
    public sealed class BattleView : MonoBehaviour
    {
        private sealed class Visual
        {
            public GameObject Root;
            public Transform Transform;
            public ArtMotion Motion;
            public bool IsBuilding, Selected;
            public float BuildScale=float.NaN;
            public EntityState Snapshot;
            public Vector3 Target;
            public int ShotTick = -1;
            public LineRenderer Ring;
            public bool Remembered;
            public InstancedArtInstance Compact;
            public int Owner;
        }
        private sealed class CompactVisualPool
        {
            public readonly List<Visual> Available=new List<Visual>();
            public int Owned;
        }
        private GameHost host;
        private Transform entitiesRoot;
        private readonly Dictionary<int, Visual> visuals = new Dictionary<int, Visual>();
        private readonly Dictionary<int,CompactVisualPool> compactVisualPools=new Dictionary<int,CompactVisualPool>();
        private readonly HashSet<int> seen = new HashSet<int>();
        private readonly HashSet<int> selectedSnapshot = new HashSet<int>();
        private readonly Dictionary<int,EntityState> observedById = new Dictionary<int,EntityState>();
        private readonly List<int> removed = new List<int>();
        private Texture2D fogTexture;
        private Color32[] fogPixels;
        private Material fogMaterial;
        private Material lineMaterial;
        private int observedTick = -1;
        private AudioSource audioSource;
        private AudioClip shot;
        private ObservedShotPool shotPool;
        public int TracerCapacity=>shotPool?.Capacity??0;
        public int ActiveTracerCount=>shotPool?.ActiveCount??0;
        public int CreatedTracerCount=>shotPool?.CreatedCount??0;
        public long DisplayedShotCount=>shotPool?.ShownCount??0;
        public double DiagnosticLateUpdateMs { get; private set; }
        public int DiagnosticLateUpdateFrame { get; private set; }
        public long CreatedVisualCount { get; private set; }
        public long ReusedVisualCount { get; private set; }
        public int PooledCompactVisualCount { get; private set; }

        public void Initialize(GameHost owner)
        {
            host = owner;
            WorldArt.Create(host.Rules, transform);
            entitiesRoot = new GameObject("Observed Entities").transform;
            entitiesRoot.SetParent(transform, false);
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
            shotPool=new ObservedShotPool(entitiesRoot,lineMaterial);
            MakeFog();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = 0.14f;
            shot = AudioClip.Create("Procedural pulse", 1400, 1, 22050, false);
            var samples = new float[1400];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = Mathf.Sin(i * (0.32f - i * 0.00012f)) * Mathf.Exp(-i / 190f) * 0.3f;
            shot.SetData(samples, 0);
        }

        private void MakeFog()
        {
            int width = host.Rules.MapWidth, height = host.Rules.MapHeight;
            fogTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            fogTexture.filterMode = FilterMode.Bilinear;
            fogTexture.wrapMode = TextureWrapMode.Clamp;
            fogPixels = new Color32[width * height];
            fogMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = fogTexture, renderQueue = 3100 };
            var fog = new GameObject("Fog of War", typeof(MeshFilter), typeof(MeshRenderer));
            fog.transform.SetParent(transform, false);
            var mesh = new Mesh { name = "Fog surface" };
            mesh.vertices = new[] { new Vector3(0,.15f,0), new Vector3(0,.15f,height), new Vector3(width,.15f,height), new Vector3(width,.15f,0) };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            mesh.triangles = new[] { 0,1,2,0,2,3 };
            mesh.RecalculateNormals();
            fog.GetComponent<MeshFilter>().sharedMesh = mesh;
            fog.GetComponent<MeshRenderer>().sharedMaterial = fogMaterial;
            fog.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fog.GetComponent<MeshRenderer>().receiveShadows = false;
        }

        public void ResetViews()
        {
            foreach (var item in visuals.Values) RetireVisual(item);
            visuals.Clear();
            observedById.Clear();
            seen.Clear();removed.Clear();selectedSnapshot.Clear();
            shotPool?.Clear();
            observedTick = -1;
        }

        private void LateUpdate()
        {
            if(host==null||!host.DiagnosticProfilingEnabled) { UpdateViews();return; }
            long started=System.Diagnostics.Stopwatch.GetTimestamp();
            try { UpdateViews(); }
            finally
            {
                DiagnosticLateUpdateMs=(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000.0/System.Diagnostics.Stopwatch.Frequency;
                DiagnosticLateUpdateFrame=Time.frameCount;
            }
        }

        private void UpdateViews()
        {
            float animationTime=Time.time;
            // Destroy(delay) previously expired beams independently of new
            // observations and Match pause/speed. Keep the same Unity clock.
            shotPool?.Advance(animationTime);
            if (host == null || host.Observation == null) return;
            var observation = host.Observation;
            // Selection is intentionally a public mutable List. Snapshot its
            // membership here rather than introduce a second authoritative set.
            selectedSnapshot.Clear();
            var interaction=host.Interaction;
            if(interaction!=null)foreach(int id in interaction.Selection)selectedSnapshot.Add(id);
            float deltaTime=Time.unscaledDeltaTime;
            float interpolation=1-Mathf.Exp(-deltaTime*18);
            float rotationInterpolation=deltaTime*14;
            if (observedTick != observation.Tick)
            {
                observedTick = observation.Tick;
                seen.Clear();
                observedById.Clear();
                int armedPopulation=0;
                foreach(var entity in observation.Entities)
                {
                    observedById.Add(entity.Id,entity);
                    if(entity.Completed&&entity.Health>0&&host.Definition(entity.DefinitionId).Damage>0)armedPopulation++;
                }
                // Real observed actors prepare their potential beam resources
                // alongside their models. This is an estimate, never a shot cap.
                shotPool.EnsureCapacity(armedPopulation);
                foreach (var entity in observation.Entities) UpdateSnapshot(entity, false,animationTime);
                foreach (var entity in observation.RememberedBuildings) UpdateSnapshot(entity, true,animationTime);
                removed.Clear();
                foreach (var pair in visuals) if (!seen.Contains(pair.Key)) removed.Add(pair.Key);
                // Retire only after every snapshot update: killing-shot fallback
                // still needs the previous target's observed snapshot/position.
                foreach (var id in removed) { var retired=visuals[id];visuals.Remove(id);RetireVisual(retired); }
                for (int i = 0; i < fogPixels.Length; i++)
                    fogPixels[i] = new Color32(5, 11, 19, observation.Visible[i] ? (byte)0 : observation.Explored[i] ? (byte)145 : (byte)235);
                fogTexture.SetPixels32(fogPixels);
                fogTexture.Apply(false, false);
            }
            foreach (var pair in visuals)
            {
                var visual = pair.Value;
                var before = visual.Transform.position;
                if(!before.Equals(visual.Target))visual.Transform.position = Vector3.Lerp(before, visual.Target, interpolation);
                var delta = visual.Target - before;
                bool moving = delta.sqrMagnitude > 0.002f;
                if (moving && !visual.IsBuilding)
                    visual.Transform.rotation = Quaternion.Slerp(visual.Transform.rotation, Quaternion.LookRotation(delta), rotationInterpolation);
                bool attacking = observation.Tick - visual.Snapshot.LastAttackTick < 3;
                ProceduralArt.Animate(visual.Motion, moving, attacking, animationTime);
                bool selected = selectedSnapshot.Contains(pair.Key);
                if(visual.Selected!=selected){visual.Selected=selected;visual.Ring.enabled=selected;}
            }
        }

        private void UpdateSnapshot(EntityState entity, bool remembered,float animationTime)
        {
            seen.Add(entity.Id);
            if (!visuals.TryGetValue(entity.Id, out var visual))
            {
                if(entity.DefinitionId=="marine"&&compactVisualPools.TryGetValue(entity.Owner,out var pool)&&pool.Available.Count>0)
                {
                    int last=pool.Available.Count-1;visual=pool.Available[last];pool.Available.RemoveAt(last);
                    PooledCompactVisualCount--;ReusedVisualCount++;
                    visual.Transform.SetParent(entitiesRoot,false);
                    visual.Transform.localPosition=Vector3.zero;visual.Transform.localRotation=Quaternion.identity;visual.Transform.localScale=Vector3.one;
                    visual.Root.layer=0;visual.Selected=false;visual.Ring.enabled=false;
                    visual.ShotTick=-1;visual.BuildScale=float.NaN;visual.Remembered=false;
                    visual.Snapshot=null;visual.Target=Vector3.zero;
                    visual.Compact.ResetAuthoredPose();
                }
                else
                {
                    visual = new Visual { Root = ProceduralArt.Create(entity.DefinitionId, entity.Owner, entitiesRoot),Owner=entity.Owner };
                    visual.Transform=visual.Root.transform;
                    visual.Motion=visual.Root.GetComponent<ArtMotion>();
                    visual.Compact=entity.DefinitionId=="marine"?visual.Root.GetComponent<InstancedArtInstance>():null;
                    visual.IsBuilding=host.Definition(entity.DefinitionId).IsBuilding;
                    var ring = new GameObject("Selection").AddComponent<LineRenderer>();
                    ring.transform.SetParent(visual.Root.transform, false);
                    ring.useWorldSpace = false;
                    ring.sharedMaterial = lineMaterial;
                    ring.startWidth = ring.endWidth = 0.055f;
                    ring.loop = true;
                    ring.positionCount = 48;
                    float radius = Mathf.Max(0.65f, host.Definition(entity.DefinitionId).Radius / 1000f + 0.12f);
                    for (int i = 0; i < 48; i++)
                    {
                        float a = i * Mathf.PI * 2 / 48;
                        ring.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0.18f, Mathf.Sin(a) * radius));
                    }
                    ring.startColor = ring.endColor = entity.Owner == 1 ? new Color(1,.3f,.2f) : new Color(.2f,1,.85f);
                    ring.enabled = false;
                    visual.Ring = ring;
                    CreatedVisualCount++;
                    if(visual.Compact!=null)
                    {
                        if(!compactVisualPools.TryGetValue(entity.Owner,out var ownerPool))
                        { ownerPool=new CompactVisualPool();compactVisualPools.Add(entity.Owner,ownerPool); }
                        ownerPool.Owned++;
                        // Reserve retirement storage alongside actually observed roots,
                        // so a casualty/reveal transition cannot grow the pool list.
                        if(ownerPool.Available.Capacity<ownerPool.Owned)
                            ownerPool.Available.Capacity=Mathf.Max(4,Mathf.Max(ownerPool.Owned,ownerPool.Available.Capacity*2));
                    }
                }
                visual.Root.name=entity.DefinitionId+"_"+entity.Id;
                visual.Transform.position=Position(entity.Position);
                visual.Snapshot=entity;visual.Target=Position(entity.Position);
                visuals.Add(entity.Id,visual);
                if(!visual.Root.activeSelf)visual.Root.SetActive(true);
            }
            visual.Snapshot = entity;
            visual.Target = Position(entity.Position);
            if (visual.Remembered != remembered)
            {
                visual.Remembered = remembered;
                var block = new MaterialPropertyBlock();
                if (remembered) { block.SetColor("_Color", new Color(.12f,.17f,.22f)); block.SetColor("_EmissionColor", Color.black); }
                foreach (var renderer in visual.Root.GetComponentsInChildren<Renderer>())
                    if (renderer != visual.Ring) renderer.SetPropertyBlock(remembered ? block : null);
            }
            float buildScale = entity.Completed ? 1 : Mathf.Lerp(.2f, 1, 1f - entity.ConstructionRemaining / (float)Mathf.Max(1, entity.ConstructionTotal));
            if(visual.BuildScale!=buildScale)
            {
                visual.BuildScale=buildScale;
                visual.Transform.localScale=new Vector3(1,buildScale,1);
            }
            if (!remembered && entity.LastAttackTick > visual.ShotTick && observedTick - entity.LastAttackTick <= 2)
            {
                visual.ShotTick = entity.LastAttackTick;
                observedById.TryGetValue(entity.LastAttackTarget,out var target);
                Vector3? endpoint = target != null ? Position(target.Position) :
                    visuals.TryGetValue(entity.LastAttackTarget, out var lastVisual) && lastVisual.Snapshot != null && !lastVisual.Remembered
                    ? lastVisual.Target : (Vector3?)null;
                if (endpoint.HasValue)
                {
                    shotPool.Show(visual.Target + Vector3.up * .8f, endpoint.Value + Vector3.up * .65f, entity.Owner,animationTime);
                    if (host.Mode == FrontendMode.Playing && Vector3.Distance(host.GameCamera.transform.position, visual.Target) < 50)
                        audioSource.PlayOneShot(shot);
                }
            }
        }

        public static Vector3 Position(SimPoint point) => new Vector3(point.X / 1000f, 0, point.Z / 1000f);

        private void RetireVisual(Visual visual)
        {
            visual.Snapshot=null;visual.Target=Vector3.zero;
            if(visual.Root==null)return;
            visual.Root.SetActive(false);visual.Root.name="Retired observed visual";
            visual.Selected=false;visual.Ring.enabled=false;
            if(visual.Compact!=null)
            {
                compactVisualPools[visual.Owner].Available.Add(visual);
                PooledCompactVisualCount++;
            }
            else DestroyViewResource(visual.Root);
        }

        private void DestroyVisual(Visual visual)
        {
            visual.Snapshot=null;
            if(visual.Root==null)return;
            visual.Root.SetActive(false);DestroyViewResource(visual.Root);
        }

        private static void DestroyViewResource(UnityEngine.Object resource)
        {
            if(resource==null)return;
            if(Application.isPlaying)Destroy(resource);else DestroyImmediate(resource);
        }

        private void OnDestroy()
        {
            foreach(var visual in visuals.Values)DestroyVisual(visual);
            visuals.Clear();
            foreach(var pool in compactVisualPools.Values)
                foreach(var visual in pool.Available)DestroyVisual(visual);
            compactVisualPools.Clear();PooledCompactVisualCount=0;
            shotPool?.Dispose();shotPool=null;
            DestroyViewResource(fogTexture);
            DestroyViewResource(fogMaterial);
            DestroyViewResource(lineMaterial);
            DestroyViewResource(shot);
        }
    }

    /// <summary>Owns reusable beam actors; observation eligibility and audio remain with BattleView.</summary>
    public sealed class ObservedShotPool : IDisposable
    {
        sealed class Beam
        {
            internal LineRenderer Renderer;
            internal float Expires;
        }
        readonly Transform parent;
        readonly Material material;
        readonly List<Beam> slots=new List<Beam>();
        readonly List<Beam> available=new List<Beam>();
        readonly List<Beam> active=new List<Beam>();
        readonly Vector3[] endpoints=new Vector3[2];
        bool disposed;
        public int Capacity=>slots.Count;
        public int ActiveCount=>active.Count;
        public int CreatedCount{get;private set;}
        public long ShownCount{get;private set;}

        public ObservedShotPool(Transform parent,Material material)
        {
            if(parent==null)throw new ArgumentNullException(nameof(parent));
            if(material==null)throw new ArgumentNullException(nameof(material));
            this.parent=parent;this.material=material;
        }
        public void EnsureCapacity(int observedArmedPopulation)
        {
            ThrowIfDisposed();
            if(observedArmedPopulation<0)throw new ArgumentOutOfRangeException(nameof(observedArmedPopulation));
            if(slots.Capacity<observedArmedPopulation)
            {
                int reserve=Math.Max(observedArmedPopulation,Math.Max(4,slots.Capacity*2));
                slots.Capacity=reserve;available.Capacity=reserve;active.Capacity=reserve;
            }
            while(slots.Count<observedArmedPopulation)
            {
                var root=new GameObject("Observed shot");
                root.transform.SetParent(parent,false);root.SetActive(false);
                var renderer=root.AddComponent<LineRenderer>();
                renderer.sharedMaterial=material;renderer.useWorldSpace=true;
                renderer.positionCount=2;renderer.startWidth=.055f;renderer.endWidth=.015f;
                var beam=new Beam{Renderer=renderer};slots.Add(beam);available.Add(beam);CreatedCount++;
            }
        }
        /// <param name="now">Scaled Unity Time.time, independently of Match tick or speed.</param>
        public void Show(Vector3 start,Vector3 end,int owner,float now)
        {
            ThrowIfDisposed();
            if(available.Count==0)EnsureCapacity(slots.Count+1);
            int index=available.Count-1;var beam=available[index];available.RemoveAt(index);
            endpoints[0]=start;endpoints[1]=end;beam.Renderer.SetPositions(endpoints);
            beam.Renderer.startColor=beam.Renderer.endColor=owner==0?new Color(.45f,.9f,1,.9f):new Color(1,.5f,.2f,.9f);
            beam.Expires=now+.07f;active.Add(beam);beam.Renderer.gameObject.SetActive(true);ShownCount++;
        }
        public void Advance(float now)
        {
            ThrowIfDisposed();
            for(int i=active.Count-1;i>=0;i--)if(now>=active[i].Expires)Return(i);
        }
        public void Clear()
        {
            ThrowIfDisposed();
            for(int i=active.Count-1;i>=0;i--)Return(i);
        }
        void Return(int index)
        {
            var beam=active[index];beam.Renderer.gameObject.SetActive(false);
            int last=active.Count-1;active[index]=active[last];active.RemoveAt(last);available.Add(beam);
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            foreach(var beam in slots)
            {
                if(beam.Renderer==null)continue;
                var root=beam.Renderer.gameObject;root.SetActive(false);
                if(Application.isPlaying)UnityEngine.Object.Destroy(root);else UnityEngine.Object.DestroyImmediate(root);
            }
            active.Clear();available.Clear();slots.Clear();
        }
        void ThrowIfDisposed()
        {
            if(disposed)throw new ObjectDisposedException(nameof(ObservedShotPool));
        }
    }
}
