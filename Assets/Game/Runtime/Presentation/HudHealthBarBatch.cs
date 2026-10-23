using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColonyRTS.Presentation
{
    // Owns only observed world-health rectangles; no controls, selection or game rules.
    public sealed class HudHealthBarBatch : IDisposable
    {
        public const string ShaderName = "ColonyRTS/HudHealthBars";
        [StructLayout(LayoutKind.Explicit,Size=28,Pack=4)]
        struct HealthVertex
        {
            [FieldOffset(0)] public Vector3 Position;
            [FieldOffset(12)] public Color Color;
            // Integer aliases preserve the GPU's seven Float32 words, including
            // signed zero and NaN payloads, without float conversion calls.
            [FieldOffset(0)] public int XBits;
            [FieldOffset(4)] public int YBits;
            [FieldOffset(8)] public int ZBits;
            [FieldOffset(12)] public int RedBits;
            [FieldOffset(16)] public int GreenBits;
            [FieldOffset(20)] public int BlueBits;
            [FieldOffset(24)] public int AlphaBits;
            public HealthVertex(Vector3 position,Color color){this=default;Position=position;Color=color;}
        }
        static readonly VertexAttributeDescriptor[] vertexLayout={
            new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),
            new VertexAttributeDescriptor(VertexAttribute.Color,VertexAttributeFormat.Float32,4)
        };
        readonly List<HealthVertex> vertices = new List<HealthVertex>();
        readonly List<uint> indices = new List<uint>();
        readonly List<HealthVertex> committedGeometry = new List<HealthVertex>();
        bool committedGeometryValid;
        Mesh mesh;
        Material material;
        int pixelHeight;
        int uploadedVertices,uploadedIndices,uploadedBars;
        int initializedIndices;
        Vector3 geometryMin,geometryMax;
        bool hasGeometry;
        Rect viewport;
        public int BarCount { get; private set; }
        public int DrawCalls { get; private set; }
        public FrameEvidence DiagnosticFrame { get; private set; }
        public int VertexCapacity { get; private set; }
        public int IndexCapacity { get; private set; }
        public long BufferReserveCount { get; private set; }
        public long IndexUploadCount { get; private set; }
        public long VertexUploadCount { get; private set; }
        public long ExactGeometryReuseCount { get; private set; }

        [Serializable]
        public struct FrameEvidence
        {
            // Calls count attempts, including partial failures. Latest counts are
            // the current payload; FirstPrevious counts are retained uploaded data.
            public bool Recorded,Completed;
            public int Frame,RepaintCalls,UploadCalls,DrawCalls,ShrinkCalls,EmptyCalls;
            public int FirstPreviousVertices,FirstPreviousIndices,FirstPreviousBars,LatestVertices,LatestIndices,LatestBars;
            public int VertexCapacity,IndexCapacity;
            public long BufferReserveCount,IndexUploadCount;
            public long VertexUploadCount,ExactGeometryReuseCount;
            public double TotalUploadMs,TotalImmediateDrawMs,LastUploadMs,LastImmediateDrawMs;
        }

        public HudHealthBarBatch()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException("Required HUD health shader is unavailable: " + ShaderName);
            material = new Material(shader) { name = "Observed health bars", hideFlags = HideFlags.HideAndDontSave };
            mesh = new Mesh { name = "Observed health rectangles", hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
        }

        public void Begin(int maximumBars, int screenHeight, Rect worldViewport)
        {
            if (maximumBars < 0) throw new ArgumentOutOfRangeException(nameof(maximumBars));
            int vertexCapacity = checked(maximumBars * 8), indexCapacity = checked(maximumBars * 12);
            if (vertices.Capacity < vertexCapacity)
            {
                int capacity = Math.Max(vertexCapacity, Math.Max(128, vertices.Capacity * 2));
                vertices.Capacity = capacity;
                indices.Capacity = Math.Max(indexCapacity, capacity / 4 * 6);
            }
            vertices.Clear(); indices.Clear();hasGeometry=false;
            BarCount = DrawCalls = 0; pixelHeight = screenHeight; viewport = worldViewport;
        }

        public void Add(Rect background, Rect fill, Color fillColor)
        {
            AddRectangle(background, Color.black);
            AddRectangle(fill, fillColor);
            BarCount++;
        }

        void AddRectangle(Rect source, Color color)
        {
            // The old bottom HUD covered out-of-world pixels. Clip them explicitly
            // so immediate graphics preserve that order independently of GUI batching.
            float x0 = Mathf.Max(source.xMin, viewport.xMin), x1 = Mathf.Min(source.xMax, viewport.xMax);
            float y0 = Mathf.Max(source.yMin, viewport.yMin), y1 = Mathf.Min(source.yMax, viewport.yMax);
            if (x1 <= x0 || y1 <= y0) return;
            uint start = (uint)vertices.Count;
            vertices.Add(new HealthVertex(new Vector3(x0, pixelHeight - y0, 0),color));
            vertices.Add(new HealthVertex(new Vector3(x1, pixelHeight - y0, 0),color));
            vertices.Add(new HealthVertex(new Vector3(x1, pixelHeight - y1, 0),color));
            vertices.Add(new HealthVertex(new Vector3(x0, pixelHeight - y1, 0),color));
            indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
            indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
            var minimum=new Vector3(x0,pixelHeight-y1,0);
            var maximum=new Vector3(x1,pixelHeight-y0,0);
            if(hasGeometry){geometryMin=Vector3.Min(geometryMin,minimum);geometryMax=Vector3.Max(geometryMax,maximum);}
            else{geometryMin=minimum;geometryMax=maximum;hasGeometry=true;}
        }

        static int GrowCapacity(int current,int required)
        {
            int capacity=Math.Max(128,current);
            while(capacity<required)capacity=checked(capacity*2);
            return capacity;
        }

        void EnsureBuffers()
        {
            // Managed preparation may reserve for every observed potential bar;
            // native storage follows only the actual submitted geometry high-water.
            int requiredVertices=vertices.Count,requiredIndices=indices.Count;
            bool changed=false;
            if(requiredVertices>VertexCapacity)
            {
                committedGeometryValid=false;
                int capacity=GrowCapacity(VertexCapacity,requiredVertices);
                mesh.SetVertexBufferParams(capacity,vertexLayout);VertexCapacity=capacity;changed=true;
            }
            if(requiredIndices>IndexCapacity)
            {
                committedGeometryValid=false;
                int capacity=GrowCapacity(IndexCapacity,requiredIndices);
                mesh.SetIndexBufferParams(capacity,IndexFormat.UInt32);IndexCapacity=capacity;initializedIndices=0;changed=true;
            }
            if(changed)BufferReserveCount++;
            // SetIndexBufferParams establishes one submesh. Never lower
            // subMeshCount: Unity can then shrink its underlying index buffer.
        }

        bool MatchesCommittedGeometry()
        {
            if(!committedGeometryValid||vertices.Count!=committedGeometry.Count||indices.Count!=uploadedIndices)return false;
            for(int i=0;i<vertices.Count;i++)
            {
                var current=vertices[i];var previous=committedGeometry[i];
                if(current.XBits!=previous.XBits||current.YBits!=previous.YBits||current.ZBits!=previous.ZBits||
                   current.RedBits!=previous.RedBits||current.GreenBits!=previous.GreenBits||
                   current.BlueBits!=previous.BlueBits||current.AlphaBits!=previous.AlphaBits)return false;
            }
            return true;
        }

        bool UploadGeometry()
        {
            // Empty frames retain native storage and issue neither upload nor draw.
            if(indices.Count==0)return false;
            if(MatchesCommittedGeometry())
            {
                uploadedBars=BarCount;ExactGeometryReuseCount++;
                return true;
            }
            // Any partially completed native mutation must be retried, never
            // certified by the previous successful snapshot.
            committedGeometryValid=false;
            if(committedGeometry.Capacity<vertices.Count)
                committedGeometry.Capacity=GrowCapacity(committedGeometry.Capacity,vertices.Count);
            EnsureBuffers();
            // Every submitted rectangle appends the same four-vertex/six-index
            // topology, including after clipping. Retain its initialized prefix;
            // only a new actual high-water or recreated index buffer needs data.
            // Upload it before vertices; existing submitted ranges remain valid.
            if(indices.Count>initializedIndices)
            {
                mesh.SetIndexBufferData(indices,0,0,indices.Count,MeshUpdateFlags.DontRecalculateBounds);
                initializedIndices=indices.Count;IndexUploadCount++;
            }
            mesh.SetVertexBufferData(vertices,0,0,vertices.Count,0,MeshUpdateFlags.DontRecalculateBounds);
            VertexUploadCount++;
            var bounds=new Bounds();bounds.SetMinMax(geometryMin,geometryMax);
            var descriptor=new SubMeshDescriptor(0,indices.Count,MeshTopology.Triangles){
                baseVertex=0,firstVertex=0,vertexCount=vertices.Count,bounds=bounds
            };
            mesh.SetSubMesh(0,descriptor,MeshUpdateFlags.DontRecalculateBounds);
            mesh.bounds=bounds;
            uploadedIndices=indices.Count;uploadedBars=BarCount;
            uploadedVertices=vertices.Count;
            committedGeometry.Clear();committedGeometry.AddRange(vertices);
            committedGeometryValid=true;
            return true;
        }

        public void Draw()=>Draw(false);

        internal void Draw(bool profiling)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            var diagnostic=profiling?DiagnosticFrame:default;
            bool previousCompleted=true,completed=false;
            if(profiling)
            {
                if(!diagnostic.Recorded||diagnostic.Frame!=Time.frameCount)
                    diagnostic=new FrameEvidence { Recorded=true,Completed=true,Frame=Time.frameCount,
                        FirstPreviousVertices=uploadedVertices,FirstPreviousIndices=uploadedIndices,FirstPreviousBars=uploadedBars };
                previousCompleted=diagnostic.Completed;
                diagnostic.Completed=false;diagnostic.RepaintCalls++;
                diagnostic.LatestVertices=vertices.Count;diagnostic.LatestIndices=indices.Count;diagnostic.LatestBars=BarCount;
                diagnostic.LastUploadMs=diagnostic.LastImmediateDrawMs=-1;
            }
            try
            {
                if(indices.Count==0)
                {
                    if(profiling)diagnostic.EmptyCalls++;
                    completed=true;return;
                }
                // Logical payload shrink is independent of retained mesh storage.
                bool shrinking=vertices.Count<uploadedVertices;
                long uploadStarted=profiling?System.Diagnostics.Stopwatch.GetTimestamp():0;
                if(profiling){diagnostic.UploadCalls++;if(shrinking)diagnostic.ShrinkCalls++;}
                try
                {
                    UploadGeometry();
                }
                finally
                {
                    if(profiling)
                    {
                        diagnostic.LastUploadMs=(System.Diagnostics.Stopwatch.GetTimestamp()-uploadStarted)*1000.0/System.Diagnostics.Stopwatch.Frequency;
                        diagnostic.TotalUploadMs+=diagnostic.LastUploadMs;
                    }
                }
                // DrawMeshNow is immediate and uses the currently selected material pass.
                // LoadPixelMatrix uses bottom-left pixels; Add already flips GUI's Y axis.
                // This wall scope includes pass/matrix setup and PopMatrix; it is
                // part of GUI wall time, not exclusive GPU time or extra frame cost.
                long drawStarted=profiling?System.Diagnostics.Stopwatch.GetTimestamp():0;
                if(profiling)diagnostic.DrawCalls++;
                try
                {
                    GL.PushMatrix();
                    try
                    {
                        if (!material.SetPass(0)) throw new InvalidOperationException("HUD health shader pass could not be set.");
                        GL.LoadPixelMatrix();
                        Graphics.DrawMeshNow(mesh, Matrix4x4.identity);
                        DrawCalls = 1;
                    }
                    finally { GL.PopMatrix(); }
                }
                finally
                {
                    if(profiling)
                    {
                        diagnostic.LastImmediateDrawMs=(System.Diagnostics.Stopwatch.GetTimestamp()-drawStarted)*1000.0/System.Diagnostics.Stopwatch.Frequency;
                        diagnostic.TotalImmediateDrawMs+=diagnostic.LastImmediateDrawMs;
                    }
                }
                completed=true;
            }
            finally
            {
                if(profiling)
                {
                    diagnostic.Completed=previousCompleted&&completed;
                    diagnostic.VertexCapacity=VertexCapacity;diagnostic.IndexCapacity=IndexCapacity;diagnostic.BufferReserveCount=BufferReserveCount;
                    diagnostic.IndexUploadCount=IndexUploadCount;
                    diagnostic.VertexUploadCount=VertexUploadCount;diagnostic.ExactGeometryReuseCount=ExactGeometryReuseCount;
                    DiagnosticFrame=diagnostic;
                }
            }
        }

        public void Dispose()
        {
            committedGeometryValid=false;
            if (mesh != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(mesh); else UnityEngine.Object.DestroyImmediate(mesh);
                mesh = null;
            }
            if (material != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(material); else UnityEngine.Object.DestroyImmediate(material);
                material = null;
            }
        }
    }
}
