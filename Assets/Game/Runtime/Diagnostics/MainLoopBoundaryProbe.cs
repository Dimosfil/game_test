using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace ColonyRTS.Diagnostics
{
    // Diagnostic boundaries only. EndOfFrame is a Unity coroutine boundary, not Present.
    [DefaultExecutionOrder(-32000)]
    internal sealed class MainLoopBoundaryProbe : MonoBehaviour
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [DllImport("kernel32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool QueryPerformanceCounter(out long value);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool QueryPerformanceFrequency(out long value);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        static extern uint GetCurrentProcessId();

        [DllImport("kernel32.dll", ExactSpelling = true)]
        static extern uint GetCurrentThreadId();
#endif

        // Called only at untimed diagnostic trial boundaries. Mono Stopwatch and
        // native QPC can have different epochs even when their frequencies match.
        internal static NativeClockAnchorEvidence CaptureNativeClockAnchor()
        {
            var unavailable = new NativeClockAnchorEvidence {
                Available = false, StopwatchBefore = -1, StopwatchAfter = -1,
                NativeQpc = -1, StopwatchFrequency = -1, NativeQpcFrequency = -1,
                ProcessId = -1, ThreadId = -1, Frame = -1
            };
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                // Frequency and frame reads stay outside the timing sandwich.
                if (!QueryPerformanceFrequency(out var nativeFrequency) || nativeFrequency <= 0) return unavailable;
                long stopwatchFrequency = Stopwatch.Frequency;
                int frame = Time.frameCount;
                long processId = GetCurrentProcessId();
                long threadId = GetCurrentThreadId();
                long before = Stopwatch.GetTimestamp();
                bool available = QueryPerformanceCounter(out var nativeQpc);
                long after = Stopwatch.GetTimestamp();
                if (!available) return unavailable;
                return new NativeClockAnchorEvidence {
                    Available = true, StopwatchBefore = before, StopwatchAfter = after,
                    NativeQpc = nativeQpc, StopwatchFrequency = stopwatchFrequency,
                    NativeQpcFrequency = nativeFrequency, ProcessId = processId,
                    ThreadId = threadId, Frame = frame
                };
            }
            catch (DllNotFoundException) { return unavailable; }
            catch (EntryPointNotFoundException) { return unavailable; }
#else
            return unavailable;
#endif
        }

        struct Boundary
        {
            public int Frame;
            public long Timestamp;
            public ulong Cycles;
            public bool CpuAvailable;
        }

        readonly WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();
        Boundary earlyUpdate, completedEndOfFrame;
        bool nativeWaitProbeRequested;
        WindowsGameStallProbe gameStallProbe;
        ProfilerRecorder waitForPresent, waitForRenderThread, swapChainPresent, waitForTargetFps, waitForLastPresentation;

        // C8: the unresolved pre-EOF interval needs native wait observations at the
        // diagnostic owner. Discover in this player; Editor handles prove no availability here.
        internal void EnableNativeWaitProbe()
        {
            if (nativeWaitProbeRequested) return;
            nativeWaitProbeRequested = true;
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var handle in handles)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                if (description.UnitType.ToString() != "TimeNanoseconds") continue;
                switch (description.Name)
                {
                    case "Gfx.WaitForPresentOnGfxThread": if (!waitForPresent.Valid) waitForPresent = StartNativeWaitRecorder(handle); break;
                    case "Gfx.WaitForRenderThread": if (!waitForRenderThread.Valid) waitForRenderThread = StartNativeWaitRecorder(handle); break;
                    case "IDXGISwapChain::Present": if (!swapChainPresent.Valid) swapChainPresent = StartNativeWaitRecorder(handle); break;
                    case "WaitForTargetFPS": if (!waitForTargetFps.Valid) waitForTargetFps = StartNativeWaitRecorder(handle); break;
                    case "TimeUpdate.WaitForLastPresentationAndUpdateTime": if (!waitForLastPresentation.Valid) waitForLastPresentation = StartNativeWaitRecorder(handle); break;
                }
            }
        }

        static ProfilerRecorder StartNativeWaitRecorder(ProfilerRecorderHandle handle)
        {
            // Default per-frame aggregation, explicitly all threads. Nested and
            // cross-thread marker durations must never be added as exclusive wall time.
            var options = ProfilerRecorderOptions.Default & ~ProfilerRecorderOptions.CollectOnlyOnCurrentThread;
            var recorder = new ProfilerRecorder(handle, 1, options);
            if (recorder.Valid) recorder.Start();
            return recorder;
        }

        static NativeWaitMarkerEvidence ReadNativeWaitRecorder(ProfilerRecorder recorder)
        {
            bool valid = recorder.Valid;
            bool available = valid && recorder.Count > 0;
            if (!available) return new NativeWaitMarkerEvidence { RecorderValid = valid, SampleAvailable = false, Nanoseconds = -1, Invocations = -1 };
            var sample = recorder.GetSample(0);
            return new NativeWaitMarkerEvidence { RecorderValid = true, SampleAvailable = true, Nanoseconds = sample.Value, Invocations = sample.Count };
        }

        static void DisposeNativeWaitRecorder(ref ProfilerRecorder recorder)
        {
            if (recorder.Valid) recorder.Dispose();
            recorder = default;
        }

        static Boundary Capture()
        {
            long timestamp = Stopwatch.GetTimestamp();
            bool available = WindowsCpuProbe.TrySample(out var cycles, out _);
            return new Boundary { Frame = Time.frameCount, Timestamp = timestamp, Cycles = cycles, CpuAvailable = available };
        }

        internal void AttachGameStallProbe(WindowsGameStallProbe probe)
        {
            if(gameStallProbe!=null&&gameStallProbe!=probe)
                throw new InvalidOperationException("A game-stall probe is already attached.");
            gameStallProbe=probe;
        }

        void Update()
        {
            earlyUpdate = Capture();
            if(gameStallProbe!=null)gameStallProbe.Heartbeat(GameStallStage.EarlyUpdate);
        }

        IEnumerator Start()
        {
            while (enabled)
            {
                yield return endOfFrame;
                completedEndOfFrame = Capture();
                if(gameStallProbe!=null)gameStallProbe.Heartbeat(GameStallStage.EndOfFrame);
            }
        }

        void OnDisable()
        {
            StopAllCoroutines();
            if(gameStallProbe!=null)gameStallProbe.EndSample();
            DisposeNativeWaitRecorder(ref waitForPresent);
            DisposeNativeWaitRecorder(ref waitForRenderThread);
            DisposeNativeWaitRecorder(ref swapChainPresent);
            DisposeNativeWaitRecorder(ref waitForTargetFps);
            DisposeNativeWaitRecorder(ref waitForLastPresentation);
        }

        internal MainLoopBoundaryEvidence Read(int previousSampleFrame, long previousSampleTimestamp,
            bool previousCpuAvailable, ulong previousSampleCycles, long sampleTimestamp,
            bool sampleCpuAvailable, ulong sampleCycles)
        {
            if(gameStallProbe!=null)gameStallProbe.Heartbeat(GameStallStage.ReadSample);
            var eof = completedEndOfFrame;
            var early = earlyUpdate;
            bool aligned = previousSampleFrame == Time.frameCount - 1 && eof.Frame == previousSampleFrame &&
                early.Frame == Time.frameCount && previousSampleTimestamp > 0 &&
                previousSampleTimestamp <= eof.Timestamp && eof.Timestamp <= early.Timestamp && early.Timestamp <= sampleTimestamp;
            bool cyclesValid = aligned && previousCpuAvailable && eof.CpuAvailable && early.CpuAvailable && sampleCpuAvailable &&
                previousSampleCycles <= eof.Cycles && eof.Cycles <= early.Cycles && early.Cycles <= sampleCycles;
            double millisecondsPerTick = 1000.0 / Stopwatch.Frequency;
            return new MainLoopBoundaryEvidence {
                PreviousSampleFrame = previousSampleFrame, EndOfFrameFrame = eof.Frame, EarlyUpdateFrame = early.Frame, SampleFrame = Time.frameCount,
                PreviousSampleTimestamp = previousSampleTimestamp, EndOfFrameTimestamp = eof.Timestamp,
                EarlyUpdateTimestamp = early.Timestamp, SampleTimestamp = sampleTimestamp,
                PreviousSampleCycles = previousSampleCycles, EndOfFrameCycles = eof.Cycles, EarlyUpdateCycles = early.Cycles, SampleCycles = sampleCycles,
                PreviousSampleCpuAvailable = previousCpuAvailable, EndOfFrameCpuAvailable = eof.CpuAvailable,
                EarlyUpdateCpuAvailable = early.CpuAvailable, SampleCpuAvailable = sampleCpuAvailable,
                WallSegmentsValid = aligned, CycleSegmentsValid = cyclesValid,
                PreviousSampleToEndOfFrameMs = aligned ? (eof.Timestamp - previousSampleTimestamp) * millisecondsPerTick : -1,
                EndOfFrameToEarlyUpdateMs = aligned ? (early.Timestamp - eof.Timestamp) * millisecondsPerTick : -1,
                EarlyUpdateToSampleMs = aligned ? (sampleTimestamp - early.Timestamp) * millisecondsPerTick : -1,
                PreviousSampleToEndOfFrameCycles = cyclesValid ? (long)(eof.Cycles - previousSampleCycles) : -1,
                EndOfFrameToEarlyUpdateCycles = cyclesValid ? (long)(early.Cycles - eof.Cycles) : -1,
                EarlyUpdateToSampleCycles = cyclesValid ? (long)(sampleCycles - early.Cycles) : -1,
                NativeWaitProbeRequested = nativeWaitProbeRequested,
                NativeWaitCaptureFrame = Time.frameCount,
                NativeWaitSamplesArePreviousCompletedFrame = true,
                GfxWaitForPresentOnGfxThread = ReadNativeWaitRecorder(waitForPresent),
                GfxWaitForRenderThread = ReadNativeWaitRecorder(waitForRenderThread),
                DxgiSwapChainPresent = ReadNativeWaitRecorder(swapChainPresent),
                WaitForTargetFps = ReadNativeWaitRecorder(waitForTargetFps),
                TimeUpdateWaitForLastPresentationAndUpdateTime = ReadNativeWaitRecorder(waitForLastPresentation)
            };
        }
    }

    // Raw same-process clock anchor, never normalized to a presumed shared epoch.
    // The native QPC sample occurs within [StopwatchBefore, StopwatchAfter]; that
    // bracket includes call overhead and is an uncertainty bound, not simultaneity.
    // Frequencies retain their own units. Unavailable numeric fields are -1.
    [Serializable]
    internal struct NativeClockAnchorEvidence
    {
        public bool Available;
        public long StopwatchBefore, StopwatchAfter, NativeQpc;
        public long StopwatchFrequency, NativeQpcFrequency;
        // Native identifiers are read outside the sandwich for exact ETL provenance.
        public long ProcessId, ThreadId;
        public int Frame;
    }

    [Serializable]
    internal struct MainLoopBoundaryEvidence
    {
        public int PreviousSampleFrame, EndOfFrameFrame, EarlyUpdateFrame, SampleFrame;
        public long PreviousSampleTimestamp, EndOfFrameTimestamp, EarlyUpdateTimestamp, SampleTimestamp;
        public ulong PreviousSampleCycles, EndOfFrameCycles, EarlyUpdateCycles, SampleCycles;
        public bool PreviousSampleCpuAvailable, EndOfFrameCpuAvailable, EarlyUpdateCpuAvailable, SampleCpuAvailable;
        public bool WallSegmentsValid, CycleSegmentsValid;
        public double PreviousSampleToEndOfFrameMs, EndOfFrameToEarlyUpdateMs, EarlyUpdateToSampleMs;
        public long PreviousSampleToEndOfFrameCycles, EndOfFrameToEarlyUpdateCycles, EarlyUpdateToSampleCycles;
        // CaptureFrame identifies our read, not an invented native marker frame ID.
        // Recorder samples retain previous-completed-frame semantics and all-thread scope.
        public bool NativeWaitProbeRequested, NativeWaitSamplesArePreviousCompletedFrame;
        public int NativeWaitCaptureFrame;
        public NativeWaitMarkerEvidence GfxWaitForPresentOnGfxThread, GfxWaitForRenderThread, DxgiSwapChainPresent,
            WaitForTargetFps, TimeUpdateWaitForLastPresentationAndUpdateTime;
    }

    [Serializable]
    internal struct NativeWaitMarkerEvidence
    {
        public bool RecorderValid, SampleAvailable;
        public long Nanoseconds, Invocations;
    }
}
