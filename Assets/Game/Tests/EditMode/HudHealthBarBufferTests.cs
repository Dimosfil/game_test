using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using ColonyRTS.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace ColonyRTS.Tests
{
    [TestFixture]
    public sealed class HudHealthBarBufferTests
    {
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly Rect Viewport = new Rect(10, 20, 80, 60);
        // Values deliberately not representable by Color32: native layout must retain Float32 channels.
        static readonly Color Fill = new Color(.12345679f, .33333334f, .7654321f, .8123457f);
        HudHealthBarBatch batch;

        sealed class Geometry
        {
            public readonly List<Vector3> Positions = new List<Vector3>();
            public readonly List<Color> Colors = new List<Color>();
            public readonly List<int> Indices = new List<int>();

            public void Quad(float left, float top, float right, float bottom, Color color)
            {
                int start = Positions.Count;
                Positions.Add(new Vector3(left, top, 0));
                Positions.Add(new Vector3(right, top, 0));
                Positions.Add(new Vector3(right, bottom, 0));
                Positions.Add(new Vector3(left, bottom, 0));
                for (int i = 0; i < 4; i++) Colors.Add(color);
                Indices.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        struct RawColorBits
        {
            [FieldOffset(0)] public Color Value;
            [FieldOffset(0)] public uint R;
            [FieldOffset(4)] public uint G;
            [FieldOffset(8)] public uint B;
            [FieldOffset(12)] public uint A;
        }

        [SetUp]
        public void SetUp() => batch = new HudHealthBarBatch();

        [TearDown]
        public void TearDown()
        {
            batch?.Dispose();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ActualClippedGeometryColorsIndicesAndBoundsSurviveGrowShrinkEmptyGrow()
        {
            batch.Begin(4, 100, Viewport);
            batch.Add(new Rect(5, 15, 20, 10), new Rect(8, 19, 8, 4), Fill);
            AddInsideBar();
            batch.Add(new Rect(85, 75, 10, 10), new Rect(86, 76, 8, 8), Fill);
            batch.Add(new Rect(0, 0, 2, 2), new Rect(1, 1, 1, 1), Fill);
            var expected = new Geometry();
            // Independently specified intersections in bottom-left pixel space.
            expected.Quad(10, 80, 25, 75, Color.black);
            expected.Quad(10, 80, 16, 77, Fill);
            InsideGeometry(expected);
            expected.Quad(85, 25, 90, 20, Color.black);
            expected.Quad(86, 24, 90, 20, Fill);
            Assert.That(Upload(), Is.True);
            Assert.That(batch.BarCount, Is.EqualTo(4), "A fully clipped bar has no submitted triangles.");
            AssertGeometry(expected, new Bounds(new Vector3(50, 50, 0), new Vector3(80, 60, 0)));
            int vertices = batch.VertexCapacity, indices = batch.IndexCapacity;
            long reserves = batch.BufferReserveCount;
            long indexUploads = batch.IndexUploadCount;

            batch.Begin(1, 100, Viewport);
            AddInsideBar();
            expected = new Geometry();
            InsideGeometry(expected);
            Assert.That(Upload(), Is.True);
            var insideBounds = new Bounds(new Vector3(38, 56, 0), new Vector3(16, 8, 0));
            AssertGeometry(expected, insideBounds);
            AssertCapacity(vertices, indices, reserves);
            Assert.That(batch.IndexUploadCount, Is.EqualTo(indexUploads));
            Assert.That(Upload(), Is.True, "A repeated repaint can upload the same current prefix.");
            AssertGeometry(expected, insideBounds);
            AssertCapacity(vertices, indices, reserves);
            Assert.That(batch.IndexUploadCount, Is.EqualTo(indexUploads));

            var mesh = Mesh;
            var descriptor = mesh.GetSubMesh(0);
            batch.Begin(0, 100, Viewport);
            Assert.That(Upload(), Is.False, "Empty payload must bypass all native uploads and buffer reserve.");
            Assert.That(batch.BarCount, Is.Zero);
            Assert.That(batch.DrawCalls, Is.Zero);
            AssertCapacity(vertices, indices, reserves);
            Assert.That(batch.IndexUploadCount, Is.EqualTo(indexUploads));
            Assert.That(mesh.GetSubMesh(0).indexCount, Is.EqualTo(descriptor.indexCount),
                "An empty pass leaves the existing native mesh untouched and submits no draw.");
            AssertBounds(insideBounds, mesh.bounds);

            int largerObservedPopulation = vertices / 8 + 1;
            batch.Begin(largerObservedPopulation, 100, Viewport);
            expected = new Geometry();
            for (int i = 0; i < largerObservedPopulation; i++)
            {
                AddInsideBar();
                InsideGeometry(expected);
            }
            Assert.That(Upload(), Is.True);
            AssertGeometry(expected, insideBounds);
            Assert.That(batch.VertexCapacity, Is.GreaterThan(vertices));
            Assert.That(batch.IndexCapacity, Is.GreaterThan(indices));
            Assert.That(batch.BufferReserveCount, Is.GreaterThan(reserves));
            Assert.That(batch.IndexUploadCount, Is.GreaterThan(indexUploads));
        }

        [Test]
        public void NativeReserveFollowsActualHighWaterAndIgnoresUnusedObservedPotential()
        {
            batch.Begin(1, 100, Viewport);
            AddInsideBar();
            Assert.That(Upload(), Is.True);
            var expected = new Geometry();
            InsideGeometry(expected);
            AssertGeometry(expected, new Bounds(new Vector3(38, 56, 0), new Vector3(16, 8, 0)));
            Assert.That(batch.VertexCapacity, Is.GreaterThanOrEqualTo(8));
            Assert.That(batch.IndexCapacity, Is.GreaterThanOrEqualTo(12));
            Assert.That(Mesh.vertexCount, Is.EqualTo(batch.VertexCapacity));
            Assert.That(Mesh.GetSubMesh(0).indexCount, Is.EqualTo(12));
            Assert.That(Mesh.GetSubMesh(0).vertexCount, Is.EqualTo(8));
            int vertices = batch.VertexCapacity, indices = batch.IndexCapacity;
            long reserves = batch.BufferReserveCount;
            long indexUploads = batch.IndexUploadCount;
            Assert.That(indexUploads, Is.GreaterThan(0));
            var bounds = new Bounds(new Vector3(38, 56, 0), new Vector3(16, 8, 0));
            foreach (int potentialObservedPopulation in new[] { 129, 1, 10000, 64, 129 })
            {
                batch.Begin(potentialObservedPopulation, 100, Viewport);
                AddInsideBar();
                Assert.That(Upload(), Is.True);
                AssertGeometry(expected, bounds);
                AssertCapacity(vertices, indices, reserves);
                Assert.That(batch.IndexUploadCount, Is.EqualTo(indexUploads));
            }

            // A clipped-away fill leaves a single quad; the old longer native
            // prefix must not add a spare triangle or preserve stale geometry.
            batch.Begin(1, 100, Viewport);
            batch.Add(new Rect(5, 76, 12, 8), new Rect(0, 0, 1, 1), Fill);
            var clipped = new Geometry();
            clipped.Quad(10, 24, 17, 20, Color.black);
            var clippedBounds = new Bounds(new Vector3(13.5f, 22, 0), new Vector3(7, 4, 0));
            Assert.That(Upload(), Is.True);
            AssertGeometry(clipped, clippedBounds);
            AssertCapacity(vertices, indices, reserves);
            Assert.That(batch.IndexUploadCount, Is.EqualTo(indexUploads));

            // Capacity already fits two bars, but only one bar's index prefix
            // has been initialized: this growth must still upload new indices.
            batch.Begin(2, 100, Viewport);
            AddInsideBar();
            AddInsideBar();
            var twoBars = new Geometry();
            InsideGeometry(twoBars);
            InsideGeometry(twoBars);
            Assert.That(Upload(), Is.True);
            AssertGeometry(twoBars, bounds);
            AssertCapacity(vertices, indices, reserves);
            Assert.That(batch.IndexUploadCount, Is.GreaterThan(indexUploads),
                "A larger actual index prefix needs initialization even inside existing native capacity.");
            indexUploads = batch.IndexUploadCount;
            batch.Begin(1, 100, Viewport);
            batch.Add(new Rect(5, 76, 12, 8), new Rect(0, 0, 1, 1), Fill);
            Assert.That(Upload(), Is.True);
            AssertGeometry(clipped, clippedBounds);
            AssertCapacity(vertices, indices, reserves);
            Assert.That(batch.IndexUploadCount, Is.EqualTo(indexUploads));
            batch.Begin(2, 100, Viewport);
            AddInsideBar();
            AddInsideBar();
            Assert.That(Upload(), Is.True);
            AssertGeometry(twoBars, bounds);
            AssertCapacity(vertices, indices, reserves);
            Assert.That(batch.IndexUploadCount, Is.EqualTo(indexUploads),
                "Regrowing into a previously initialized prefix must reuse the native index data.");

            int actualPeak = System.Math.Max(vertices / 8, indices / 12) + 1;
            var peakGeometry = new Geometry();
            batch.Begin(actualPeak, 100, Viewport);
            for (int i = 0; i < actualPeak; i++)
            {
                AddInsideBar();
                InsideGeometry(peakGeometry);
            }
            Assert.That(Upload(), Is.True);
            AssertGeometry(peakGeometry, bounds);
            Assert.That(batch.VertexCapacity, Is.GreaterThan(vertices));
            Assert.That(batch.IndexCapacity, Is.GreaterThan(indices));
            Assert.That(batch.BufferReserveCount, Is.GreaterThan(reserves),
                "A genuinely larger submitted prefix must grow native buffers and report that growth.");
            Assert.That(batch.IndexUploadCount, Is.GreaterThan(indexUploads));
            vertices = batch.VertexCapacity;
            indices = batch.IndexCapacity;
            reserves = batch.BufferReserveCount;
            indexUploads = batch.IndexUploadCount;

            batch.Begin(10000, 100, Viewport);
            AddInsideBar();
            Assert.That(Upload(), Is.True);
            AssertGeometry(expected, bounds);
            AssertCapacity(vertices, indices, reserves);
            Assert.That(batch.IndexUploadCount, Is.EqualTo(indexUploads));
            batch.Begin(10000, 100, Viewport);
            Assert.That(Upload(), Is.False);
            AssertCapacity(vertices, indices, reserves);
            Assert.That(batch.IndexUploadCount, Is.EqualTo(indexUploads));
            batch.Begin(actualPeak, 100, Viewport);
            for (int i = 0; i < actualPeak; i++) AddInsideBar();
            Assert.That(Upload(), Is.True);
            AssertGeometry(peakGeometry, bounds);
            AssertCapacity(vertices, indices, reserves);
            Assert.That(batch.IndexUploadCount, Is.EqualTo(indexUploads));
        }

        [Test]
        public void EmptyAndFullyClippedFirstPayloadsDoNotAllocateNativeBufferLayouts()
        {
            batch.Begin(100, 100, Viewport);
            Assert.That(Upload(), Is.False);
            AssertCapacity(0, 0, 0);
            Assert.That(batch.IndexUploadCount, Is.Zero);
            batch.Add(new Rect(-20, -20, 3, 3), new Rect(-19, -19, 1, 1), Fill);
            Assert.That(batch.BarCount, Is.EqualTo(1));
            Assert.That(Upload(), Is.False);
            AssertCapacity(0, 0, 0);
            Assert.That(batch.IndexUploadCount, Is.Zero);
            Assert.That(Mesh.vertexCount, Is.Zero);
            Assert.That(batch.DrawCalls, Is.Zero);
        }

        [Test]
        public void ExactGeometryReusePreservesNativeBitsAndDetectsOneUlpPositionAndColorChanges()
        {
            batch.Begin(1, 100, Viewport);
            AddInsideBar();
            Assert.That(Upload(), Is.True);
            var expected = new Geometry();
            InsideGeometry(expected);
            var bounds = new Bounds(new Vector3(38, 56, 0), new Vector3(16, 8, 0));
            AssertGeometry(expected, bounds);
            AssertNativeVertexBits(expected);
            long uploads = batch.VertexUploadCount, reuses = batch.ExactGeometryReuseCount;
            long indexUploads = batch.IndexUploadCount, reserves = batch.BufferReserveCount;
            int vertexCapacity = batch.VertexCapacity, indexCapacity = batch.IndexCapacity;
            Assert.That(uploads, Is.GreaterThan(0));

            Assert.That(Upload(), Is.True, "Reusing native data must still return drawable geometry.");
            Assert.That(batch.VertexUploadCount, Is.EqualTo(uploads));
            Assert.That(batch.ExactGeometryReuseCount, Is.EqualTo(++reuses));
            AssertGeometry(expected, bounds);
            AssertNativeVertexBits(expected);

            batch.Begin(10000, 100, Viewport);
            AddInsideBar();
            batch.Add(new Rect(-20, -20, 3, 3), new Rect(-19, -19, 1, 1), Fill);
            Assert.That(Upload(), Is.True);
            Assert.That(batch.BarCount, Is.EqualTo(2));
            Assert.That(Field("uploadedBars"), Is.EqualTo(2), "Clipped-away extra bars change logical metadata, not native geometry.");
            Assert.That(batch.VertexUploadCount, Is.EqualTo(uploads));
            Assert.That(batch.ExactGeometryReuseCount, Is.EqualTo(++reuses));
            AssertGeometry(expected, bounds);
            AssertNativeVertexBits(expected);
            AssertCapacity(vertexCapacity, indexCapacity, reserves);

            batch.Begin(0, 100, Viewport);
            Assert.That(Upload(), Is.False);
            Assert.That(batch.VertexUploadCount, Is.EqualTo(uploads));
            Assert.That(batch.ExactGeometryReuseCount, Is.EqualTo(reuses));
            Assert.That(Mesh.GetSubMesh(0).indexCount, Is.EqualTo(12));
            AssertNativeVertexBits(expected);
            batch.Begin(1, 100, Viewport);
            AddInsideBar();
            Assert.That(Upload(), Is.True);
            Assert.That(batch.VertexUploadCount, Is.EqualTo(uploads));
            Assert.That(batch.ExactGeometryReuseCount, Is.EqualTo(++reuses));
            AssertGeometry(expected, bounds);
            AssertNativeVertexBits(expected);

            float left = NextFloat(30f);
            batch.Begin(1, 100, Viewport);
            batch.Add(new Rect(left, 40, 16, 8), new Rect(31, 41, 9, 6), Fill);
            expected = new Geometry();
            // One ULP at30 changes the left edge; adding16 rounds the right edge to46.
            expected.Quad(left, 60, 46, 52, Color.black);
            expected.Quad(31, 59, 40, 53, Fill);
            bounds.SetMinMax(new Vector3(left, 52, 0), new Vector3(46, 60, 0));
            Assert.That(Upload(), Is.True);
            Assert.That(batch.VertexUploadCount, Is.EqualTo(++uploads));
            Assert.That(batch.ExactGeometryReuseCount, Is.EqualTo(reuses));
            AssertGeometry(expected, bounds);
            AssertNativeVertexBits(expected);

            var changedColor = new Color(NextFloat(Fill.r), Fill.g, Fill.b, Fill.a);
            batch.Begin(1, 100, Viewport);
            batch.Add(new Rect(left, 40, 16, 8), new Rect(31, 41, 9, 6), changedColor);
            expected = new Geometry();
            expected.Quad(left, 60, 46, 52, Color.black);
            expected.Quad(31, 59, 40, 53, changedColor);
            Assert.That(Upload(), Is.True);
            Assert.That(batch.VertexUploadCount, Is.EqualTo(++uploads));
            Assert.That(batch.ExactGeometryReuseCount, Is.EqualTo(reuses));
            AssertGeometry(expected, bounds);
            AssertNativeVertexBits(expected);
            Assert.That(Upload(), Is.True);
            Assert.That(batch.VertexUploadCount, Is.EqualTo(uploads));
            Assert.That(batch.ExactGeometryReuseCount, Is.EqualTo(++reuses));
            AssertNativeVertexBits(expected);
            Assert.That(batch.IndexUploadCount, Is.EqualTo(indexUploads));
            AssertCapacity(vertexCapacity, indexCapacity, reserves);
        }

        [Test]
        public void ScreenViewportAndActualPrefixGrowthRefreshNativeGeometryBeforeExactReuse()
        {
            batch.Begin(1, 100, Viewport);
            AddInsideBar();
            Assert.That(Upload(), Is.True);
            long uploads = batch.VertexUploadCount, reuses = batch.ExactGeometryReuseCount;

            batch.Begin(1, 101, Viewport);
            AddInsideBar();
            var expected = new Geometry();
            expected.Quad(30, 61, 46, 53, Color.black);
            expected.Quad(31, 60, 40, 54, Fill);
            Assert.That(Upload(), Is.True);
            Assert.That(batch.VertexUploadCount, Is.EqualTo(++uploads));
            AssertGeometry(expected, new Bounds(new Vector3(38, 57, 0), new Vector3(16, 8, 0)));
            AssertNativeVertexBits(expected);

            var clippedViewport = new Rect(35, 20, 55, 60);
            var clippedBounds = new Bounds(new Vector3(40.5f, 57, 0), new Vector3(11, 8, 0));
            batch.Begin(1, 101, clippedViewport);
            AddInsideBar();
            expected = ClippedInsideGeometry(1);
            Assert.That(Upload(), Is.True);
            Assert.That(batch.VertexUploadCount, Is.EqualTo(++uploads));
            AssertGeometry(expected, clippedBounds);
            AssertNativeVertexBits(expected);
            int vertexCapacity = batch.VertexCapacity, indexCapacity = batch.IndexCapacity;
            long reserves = batch.BufferReserveCount;

            foreach (int count in new[] { 2, 1, 2 })
            {
                batch.Begin(count, 101, clippedViewport);
                for (int i = 0; i < count; i++) AddInsideBar();
                expected = ClippedInsideGeometry(count);
                Assert.That(Upload(), Is.True);
                Assert.That(batch.VertexUploadCount, Is.EqualTo(++uploads), "Changed actual prefix length cannot reuse the prior submesh.");
                Assert.That(batch.ExactGeometryReuseCount, Is.EqualTo(reuses));
                AssertGeometry(expected, clippedBounds);
                AssertNativeVertexBits(expected);
                AssertCapacity(vertexCapacity, indexCapacity, reserves);
            }

            int actualPeak = System.Math.Max(vertexCapacity / 8, indexCapacity / 12) + 1;
            batch.Begin(actualPeak, 101, clippedViewport);
            for (int i = 0; i < actualPeak; i++) AddInsideBar();
            expected = ClippedInsideGeometry(actualPeak);
            Assert.That(Upload(), Is.True);
            Assert.That(batch.VertexUploadCount, Is.EqualTo(++uploads));
            Assert.That(batch.VertexCapacity, Is.GreaterThan(vertexCapacity));
            Assert.That(batch.IndexCapacity, Is.GreaterThan(indexCapacity));
            Assert.That(batch.BufferReserveCount, Is.GreaterThan(reserves));
            AssertGeometry(expected, clippedBounds);
            AssertNativeVertexBits(expected);
            vertexCapacity = batch.VertexCapacity; indexCapacity = batch.IndexCapacity;
            reserves = batch.BufferReserveCount;
            Assert.That(Upload(), Is.True);
            Assert.That(batch.VertexUploadCount, Is.EqualTo(uploads));
            Assert.That(batch.ExactGeometryReuseCount, Is.EqualTo(++reuses));
            AssertGeometry(expected, clippedBounds);
            AssertNativeVertexBits(expected);
            AssertCapacity(vertexCapacity, indexCapacity, reserves);
        }

        static Geometry ClippedInsideGeometry(int bars)
        {
            var expected = new Geometry();
            for (int i = 0; i < bars; i++)
            {
                expected.Quad(35, 61, 46, 53, Color.black);
                expected.Quad(35, 60, 40, 54, Fill);
            }
            return expected;
        }

        static float NextFloat(float value) => System.BitConverter.Int32BitsToSingle(System.BitConverter.SingleToInt32Bits(value) + 1);

        void AssertNativeVertexBits(Geometry expected)
        {
            var positions = new List<Vector3>();
            var colors = new List<Color>();
            Mesh.GetVertices(positions); Mesh.GetColors(colors);
            Assert.That(positions.Count, Is.GreaterThanOrEqualTo(expected.Positions.Count));
            Assert.That(colors.Count, Is.GreaterThanOrEqualTo(expected.Colors.Count));
            for (int i = 0; i < expected.Positions.Count; i++)
            {
                for (int channel = 0; channel < 3; channel++)
                    Assert.That(System.BitConverter.SingleToInt32Bits(positions[i][channel]), Is.EqualTo(System.BitConverter.SingleToInt32Bits(expected.Positions[i][channel])), "Position bits " + i + "/" + channel);
                for (int channel = 0; channel < 4; channel++)
                    Assert.That(System.BitConverter.SingleToInt32Bits(colors[i][channel]), Is.EqualTo(System.BitConverter.SingleToInt32Bits(expected.Colors[i][channel])), "Color bits " + i + "/" + channel);
            }
        }

        [Test]
        public void RawNativeVertexWordsPreserveSignedZeroAndDistinctNanColorPayloadsAcrossReuse()
        {
            // Color is transferred as a struct, never through floating-point
            // conversion/comparison APIs that can quiet signalling NaNs on Mono.
            uint[] redPatterns = {
                0x00000000u, 0x80000000u,
                0x7fc00001u, 0x7fc00002u,
                0x7f800001u, 0x7f800002u,
                0xffc00001u, 0xff800001u
            };
            long uploads = batch.VertexUploadCount, reuses = batch.ExactGeometryReuseCount;
            int vertexCapacity = 0, indexCapacity = 0;
            long reserves = 0, indexUploads = 0;
            foreach (uint red in redPatterns)
            {
                var color = new RawColorBits { R = red, G = 0x3f000000u, B = 0x3e800000u, A = 0x3f800000u }.Value;
                batch.Begin(1, 100, Viewport);
                batch.Add(new Rect(30, 40, 16, 8), new Rect(31, 41, 9, 6), color);
                Assert.That(Upload(), Is.True);
                Assert.That(batch.VertexUploadCount, Is.EqualTo(++uploads), "Distinct raw R pattern " + red.ToString("x8"));
                Assert.That(batch.ExactGeometryReuseCount, Is.EqualTo(reuses));
                AssertRawInsideVertexWords(red);
                if (vertexCapacity == 0)
                {
                    vertexCapacity = batch.VertexCapacity; indexCapacity = batch.IndexCapacity;
                    reserves = batch.BufferReserveCount; indexUploads = batch.IndexUploadCount;
                }

                Assert.That(Upload(), Is.True);
                Assert.That(batch.VertexUploadCount, Is.EqualTo(uploads));
                Assert.That(batch.ExactGeometryReuseCount, Is.EqualTo(++reuses));
                AssertRawInsideVertexWords(red);

                batch.Begin(1, 100, Viewport);
                batch.Add(new Rect(30, 40, 16, 8), new Rect(31, 41, 9, 6), color);
                Assert.That(Upload(), Is.True);
                Assert.That(batch.VertexUploadCount, Is.EqualTo(uploads));
                Assert.That(batch.ExactGeometryReuseCount, Is.EqualTo(++reuses));
                AssertRawInsideVertexWords(red);
                AssertCapacity(vertexCapacity, indexCapacity, reserves);
                Assert.That(batch.IndexUploadCount, Is.EqualTo(indexUploads));
            }
        }

        void AssertRawInsideVertexWords(uint fillRed)
        {
            // Independent IEEE words for the finite, hand-authored clipped
            // positions: background(30,60)-(46,52), fill(31,59)-(40,53).
            int[,] positionWords = {
                { 0x41f00000, 0x42700000, 0 }, { 0x42380000, 0x42700000, 0 },
                { 0x42380000, 0x42500000, 0 }, { 0x41f00000, 0x42500000, 0 },
                { 0x41f80000, 0x426c0000, 0 }, { 0x42200000, 0x426c0000, 0 },
                { 0x42200000, 0x42540000, 0 }, { 0x41f80000, 0x42540000, 0 }
            };
            // Dispose the read lease before the next native upload: no stale
            // MeshData snapshot or copy-on-write can satisfy a later assertion.
            using (var snapshot = UnityEngine.Mesh.AcquireReadOnlyMeshData(Mesh))
            {
                var native = snapshot[0];
                Assert.That(native.GetVertexBufferStride(0), Is.EqualTo(28));
                var words = native.GetVertexData<int>(0);
                Assert.That(words.Length, Is.GreaterThanOrEqualTo(56));
                for (int vertex = 0; vertex < 8; vertex++)
                {
                    int offset = vertex * 7;
                    for (int axis = 0; axis < 3; axis++)
                        Assert.That(words[offset + axis], Is.EqualTo(positionWords[vertex, axis]), "Position raw word " + vertex + "/" + axis);
                    Assert.That(words[offset + 3], Is.EqualTo(vertex < 4 ? 0 : unchecked((int)fillRed)), "R raw word " + vertex);
                    Assert.That(words[offset + 4], Is.EqualTo(vertex < 4 ? 0 : 0x3f000000), "G raw word " + vertex);
                    Assert.That(words[offset + 5], Is.EqualTo(vertex < 4 ? 0 : 0x3e800000), "B raw word " + vertex);
                    Assert.That(words[offset + 6], Is.EqualTo(0x3f800000), "A raw word " + vertex);
                }
            }
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 }, Mesh.GetIndices(0));
            var submesh = Mesh.GetSubMesh(0);
            Assert.That(submesh.indexCount, Is.EqualTo(12));
            Assert.That(submesh.firstVertex, Is.Zero);
            Assert.That(submesh.vertexCount, Is.EqualTo(8));
            Assert.That(Field("uploadedVertices"), Is.EqualTo(8));
            Assert.That(Field("uploadedIndices"), Is.EqualTo(12));
            Assert.That(Field("uploadedBars"), Is.EqualTo(1));
            var bounds = new Bounds(new Vector3(38, 56, 0), new Vector3(16, 8, 0));
            AssertBounds(bounds, submesh.bounds);
            AssertBounds(bounds, Mesh.bounds);
        }

        [Test]
        public void RetainedLargerIndexPrefixNeverSubmitsStaleTrianglesAfterClippingEmptyOrRegrowth()
        {
            var bounds=new Bounds(new Vector3(38,56,0),new Vector3(16,8,0));
            batch.Begin(3,100,Viewport);
            var expected=new Geometry();
            for(int i=0;i<3;i++){AddInsideBar();InsideGeometry(expected);}
            Assert.That(Upload(),Is.True);
            AssertGeometry(expected,bounds);
            long initialIndexUploads=batch.IndexUploadCount;
            int vertexCapacity=batch.VertexCapacity,indexCapacity=batch.IndexCapacity;
            long reserves=batch.BufferReserveCount;

            // One surviving background quad, with the fill fully outside the viewport.
            batch.Begin(1,100,Viewport);
            batch.Add(new Rect(30,40,16,8),new Rect(0,0,2,2),Fill);
            expected=new Geometry();expected.Quad(30,60,46,52,Color.black);
            Assert.That(Upload(),Is.True);
            AssertGeometry(expected,bounds);
            CollectionAssert.AreEqual(new[]{0,1,2,0,2,3},Mesh.GetIndices(0),
                "The earlier larger prefix must not contribute any submitted triangles.");
            Assert.That(batch.IndexUploadCount,Is.EqualTo(initialIndexUploads));
            AssertCapacity(vertexCapacity,indexCapacity,reserves);

            foreach(bool fullyClipped in new[]{false,true})
            {
                batch.Begin(fullyClipped?1:0,100,Viewport);
                if(fullyClipped)batch.Add(new Rect(0,0,2,2),new Rect(1,1,1,1),Fill);
                Assert.That(Upload(),Is.False,"Empty actual geometry must submit nothing despite the cached prefix.");
                Assert.That(batch.DrawCalls,Is.Zero);
                Assert.That(batch.IndexUploadCount,Is.EqualTo(initialIndexUploads));
                AssertCapacity(vertexCapacity,indexCapacity,reserves);
                Assert.That(Mesh.GetSubMesh(0).indexCount,Is.EqualTo(6),
                    "No-upload empty passes retain native metadata; it is not permission to draw the cached mesh.");
            }

            // Regrow within the old initialized prefix, extend within spare native
            // capacity, then exceed it. In every phase the real mesh exposes only
            // the independently authored active geometry and exact index sequence.
            foreach(int bars in new[]{2,5,17})
            {
                batch.Begin(bars,100,Viewport);expected=new Geometry();
                for(int i=0;i<bars;i++){AddInsideBar();InsideGeometry(expected);}
                Assert.That(Upload(),Is.True);
                AssertGeometry(expected,bounds);
                if(bars==2)
                {
                    Assert.That(batch.IndexUploadCount,Is.EqualTo(initialIndexUploads));
                    AssertCapacity(vertexCapacity,indexCapacity,reserves);
                }
                else if(bars==5)
                {
                    Assert.That(batch.IndexUploadCount,Is.EqualTo(initialIndexUploads+1));
                    AssertCapacity(vertexCapacity,indexCapacity,reserves);
                }
                else
                {
                    Assert.That(batch.IndexUploadCount,Is.EqualTo(initialIndexUploads+2));
                    Assert.That(batch.IndexCapacity,Is.GreaterThan(indexCapacity));
                }
            }
        }

        [Test]
        public void DisposeReleasesOwnedMeshAndMaterialWithoutAffectingAnotherBatch()
        {
            var ownedMesh = Mesh;
            var ownedMaterial = (Material)Field("material");
            var neighbor = new HudHealthBarBatch();
            try
            {
                var neighborMesh = (Mesh)typeof(HudHealthBarBatch).GetField("mesh", Members).GetValue(neighbor);
                var neighborMaterial = (Material)typeof(HudHealthBarBatch).GetField("material", Members).GetValue(neighbor);
                batch.Begin(1, 100, Viewport);
                AddInsideBar();
                Assert.That(Upload(), Is.True);
                batch.Dispose();
                Assert.That(ownedMesh == null, Is.True);
                Assert.That(ownedMaterial == null, Is.True);
                Assert.That(neighborMesh != null, Is.True);
                Assert.That(neighborMaterial != null, Is.True);
                batch.Dispose();
                Assert.That(neighborMesh != null && neighborMaterial != null, Is.True,
                    "Repeated disposal must not release resources owned by another batch.");
            }
            finally { neighbor.Dispose(); }
        }

        void AddInsideBar() => batch.Add(new Rect(30, 40, 16, 8), new Rect(31, 41, 9, 6), Fill);
        static void InsideGeometry(Geometry expected)
        {
            expected.Quad(30, 60, 46, 52, Color.black);
            expected.Quad(31, 59, 40, 53, Fill);
        }
        object Field(string name) => typeof(HudHealthBarBatch).GetField(name, Members).GetValue(batch);
        Mesh Mesh => (Mesh)Field("mesh");
        bool Upload() => (bool)typeof(HudHealthBarBatch).GetMethod("UploadGeometry", Members).Invoke(batch, null);

        void AssertCapacity(int vertices, int indices, long reserves)
        {
            Assert.That(batch.VertexCapacity, Is.EqualTo(vertices));
            Assert.That(batch.IndexCapacity, Is.EqualTo(indices));
            Assert.That(batch.BufferReserveCount, Is.EqualTo(reserves));
        }
        void AssertGeometry(Geometry expected, Bounds bounds)
        {
            var mesh = Mesh;
            Assert.That(mesh.indexFormat, Is.EqualTo(IndexFormat.UInt32));
            Assert.That(mesh.GetVertexAttributeFormat(VertexAttribute.Position), Is.EqualTo(VertexAttributeFormat.Float32));
            Assert.That(mesh.GetVertexAttributeDimension(VertexAttribute.Position), Is.EqualTo(3));
            Assert.That(mesh.GetVertexAttributeFormat(VertexAttribute.Color), Is.EqualTo(VertexAttributeFormat.Float32));
            Assert.That(mesh.GetVertexAttributeDimension(VertexAttribute.Color), Is.EqualTo(4));
            Assert.That(mesh.GetVertexAttributeStream(VertexAttribute.Position), Is.Zero);
            Assert.That(mesh.GetVertexAttributeStream(VertexAttribute.Color), Is.Zero);
            Assert.That(mesh.GetVertexBufferStride(0), Is.EqualTo(28));
            var positions = new List<Vector3>();
            var colors = new List<Color>();
            mesh.GetVertices(positions);
            mesh.GetColors(colors);
            Assert.That(positions.Count, Is.EqualTo(batch.VertexCapacity));
            Assert.That(Field("uploadedVertices"), Is.EqualTo(expected.Positions.Count),
                "Logical upload diagnostics must describe the actual prefix, independently of storage capacity.");
            Assert.That(Field("uploadedIndices"), Is.EqualTo(expected.Indices.Count));
            Assert.That(Field("uploadedBars"), Is.EqualTo(batch.BarCount));
            for (int vertex = 0; vertex < expected.Positions.Count; vertex++)
            {
                Assert.That(positions[vertex], Is.EqualTo(expected.Positions[vertex]), "Position " + vertex);
                Assert.That(colors[vertex], Is.EqualTo(expected.Colors[vertex]), "Float32 color " + vertex);
            }
            CollectionAssert.AreEqual(expected.Indices, mesh.GetIndices(0));
            Assert.That(mesh.subMeshCount, Is.EqualTo(1));
            var submesh = mesh.GetSubMesh(0);
            Assert.That(submesh.topology, Is.EqualTo(MeshTopology.Triangles));
            Assert.That(submesh.indexStart, Is.Zero);
            Assert.That(submesh.baseVertex, Is.Zero);
            Assert.That(submesh.firstVertex, Is.Zero);
            Assert.That(submesh.indexCount, Is.EqualTo(expected.Indices.Count), "Spare index capacity must not be rendered.");
            Assert.That(submesh.vertexCount, Is.EqualTo(expected.Positions.Count), "Submesh describes the actual vertex prefix.");
            AssertBounds(bounds, submesh.bounds);
            AssertBounds(bounds, mesh.bounds);
        }
        static void AssertBounds(Bounds expected, Bounds actual)
        {
            Assert.That(actual.center, Is.EqualTo(expected.center));
            Assert.That(actual.size, Is.EqualTo(expected.size));
        }
    }
}
