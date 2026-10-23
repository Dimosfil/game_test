using System;
using System.Runtime.InteropServices;

namespace ColonyRTS.Diagnostics
{
    /// <summary>Opt-in native CPU observations; neither measurement identifies scheduler preemption.</summary>
    internal static class WindowsCpuProbe
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        static readonly IntPtr CurrentThread = new IntPtr(-2);
        static readonly IntPtr CurrentProcess = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential)]
        struct NativeFileTime
        {
            public uint Low, High;
            public ulong Value => ((ulong)High << 32) | Low;
        }

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool QueryThreadCycleTime(IntPtr thread, out ulong cycles);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetProcessTimes(IntPtr process, out NativeFileTime creation,
            out NativeFileTime exit, out NativeFileTime kernel, out NativeFileTime user);
#endif

        // Call on the main thread. Cycle counts are not nanoseconds; process CPU time
        // sums all process threads and can advance faster than wall-clock time.
        internal static bool TrySample(out ulong mainThreadCycles, out ulong processTime100ns)
        {
            mainThreadCycles = 0;
            processTime100ns = 0;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                if (!QueryThreadCycleTime(CurrentThread, out var cycles) ||
                    !GetProcessTimes(CurrentProcess, out _, out _, out var kernel, out var user)) return false;
                mainThreadCycles = cycles;
                processTime100ns = kernel.Value + user.Value;
                return true;
            }
            catch (DllNotFoundException) { return false; }
            catch (EntryPointNotFoundException) { return false; }
#else
            return false;
#endif
        }

        // Thread-only sampling for bounded GUI scopes. Do not couple availability
        // or observer cost to an unrelated all-process GetProcessTimes query.
        internal static bool TrySampleThreadCycles(out ulong cycles)
        {
            cycles = 0;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                if (!QueryThreadCycleTime(CurrentThread, out var sampled)) return false;
                cycles = sampled;
                return true;
            }
            catch (DllNotFoundException) { return false; }
            catch (EntryPointNotFoundException) { return false; }
#else
            return false;
#endif
        }
    }
}
