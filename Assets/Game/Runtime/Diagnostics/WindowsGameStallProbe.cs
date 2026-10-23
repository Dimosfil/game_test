using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace ColonyRTS.Diagnostics
{
    internal enum GameStallStage
    {
        None=0, SampleBegin=1, EarlyUpdate=2, EndOfFrame=3,
        ReadSample=4, SampleEnd=5, Disabled=6
    }

    // Explicit diagnostic only. The external helper reads this own-process
    // heartbeat; no work, map or native calls exist without adding this component.
    [DisallowMultipleComponent]
    internal sealed class WindowsGameStallProbe : MonoBehaviour, IDisposable
    {
        internal const int MappingBytes=80;
        internal const uint Magic=0x53535452, ProtocolVersion=2;
        internal const string MappingPrefix="Local\\ColonyRTS.GameStall.";
        IntPtr mapping,view;
        bool initialized,available,active,disposed;
        uint sequence,epoch,processId,mainThreadId;
        long processStartFileTime,frequency,lastQpc;
        // Raw native QPC of the latest active ReadSample, retained across Early/EOF.
        // This is an ongoing Read-to-poll diagnostic anchor, not a Present boundary.
        long lastReadSampleQpc;
        int lastReadSampleFrame=-1;

        internal bool Available=>available;
        internal bool SampleActive=>active;
        internal string MapName { get; private set; }
        internal string FailureReason { get; private set; }
        internal uint ProcessId=>processId;
        internal uint MainThreadId=>mainThreadId;
        internal long ProcessStartFileTime=>processStartFileTime;
        internal string ProcessStartFileTimeText=>processStartFileTime.ToString(CultureInfo.InvariantCulture);
        internal long NativeQpcFrequency=>frequency;
        internal uint SampleEpoch=>epoch;
        internal long HeartbeatCount { get; private set; }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [StructLayout(LayoutKind.Sequential)]
        struct NativeFileTime
        {
            public uint Low,High;
            public long Value=>unchecked((long)(((ulong)High<<32)|Low));
        }
        [DllImport("kernel32.dll",ExactSpelling=true,SetLastError=true)]
        static extern IntPtr CreateFileMappingW(IntPtr file,IntPtr attributes,uint protect,
            uint maximumSizeHigh,uint maximumSizeLow,[MarshalAs(UnmanagedType.LPWStr)]string name);
        [DllImport("kernel32.dll",ExactSpelling=true,SetLastError=true)]
        static extern IntPtr MapViewOfFile(IntPtr mapping,uint access,uint offsetHigh,uint offsetLow,UIntPtr bytes);
        [DllImport("kernel32.dll",ExactSpelling=true,SetLastError=true)]
        [return:MarshalAs(UnmanagedType.Bool)]static extern bool UnmapViewOfFile(IntPtr address);
        [DllImport("kernel32.dll",ExactSpelling=true,SetLastError=true)]
        [return:MarshalAs(UnmanagedType.Bool)]static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll",ExactSpelling=true)]
        [return:MarshalAs(UnmanagedType.Bool)]static extern bool QueryPerformanceCounter(out long value);
        [DllImport("kernel32.dll",ExactSpelling=true)]
        [return:MarshalAs(UnmanagedType.Bool)]static extern bool QueryPerformanceFrequency(out long value);
        [DllImport("kernel32.dll",ExactSpelling=true)]static extern uint GetCurrentProcessId();
        [DllImport("kernel32.dll",ExactSpelling=true)]static extern uint GetCurrentThreadId();
        [DllImport("kernel32.dll",ExactSpelling=true)]
        [return:MarshalAs(UnmanagedType.Bool)]static extern bool GetProcessTimes(IntPtr process,
            out NativeFileTime creation,out NativeFileTime exit,out NativeFileTime kernel,out NativeFileTime user);
#endif

        internal bool Initialize(string mapName)
        {
            if(initialized||disposed)throw new InvalidOperationException("Stall probe initialization is single-use.");
            if(mapName==null||!mapName.StartsWith(MappingPrefix,StringComparison.Ordinal)||
                !Guid.TryParseExact(mapName.Substring(MappingPrefix.Length),"N",out _))
                throw new ArgumentException("Explicit local game-stall GUID mapping required.",nameof(mapName));
            initialized=true;MapName=mapName;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if(IntPtr.Size!=8){FailureReason="Windows64 required.";return false;}
            try
            {
                processId=GetCurrentProcessId();mainThreadId=GetCurrentThreadId();
                if(!GetProcessTimes(new IntPtr(-1),out var creation,out _,out _,out _)||creation.Value<=0||
                    !QueryPerformanceFrequency(out frequency)||frequency<=0||
                    !QueryPerformanceCounter(out lastQpc)||lastQpc<=0||processId==0||mainThreadId==0)
                {FailureReason="Native identity or clock unavailable.";return false;}
                processStartFileTime=creation.Value;
                mapping=CreateFileMappingW(new IntPtr(-1),IntPtr.Zero,0x04,0,MappingBytes,mapName);
                int createError=Marshal.GetLastWin32Error();
                if(mapping==IntPtr.Zero){FailureReason="CreateFileMapping failed: "+createError;return false;}
                if(createError==183)
                {FailureReason="Mapping already exists; nonce reuse rejected.";ReleaseHandles();return false;}
                view=MapViewOfFile(mapping,0x0006,0,0,new UIntPtr(MappingBytes));
                if(view==IntPtr.Zero)
                {FailureReason="MapViewOfFile failed: "+Marshal.GetLastWin32Error();ReleaseHandles();return false;}
                // New page-file mapping begins zero-filled. Publish identity and
                // body in one seqlock transaction so partial initialization is not valid.
                BeginWrite();
                Marshal.WriteInt32(view,0,unchecked((int)Magic));
                Marshal.WriteInt32(view,4,unchecked((int)ProtocolVersion));
                Marshal.WriteInt32(view,8,unchecked((int)processId));
                Marshal.WriteInt32(view,12,unchecked((int)mainThreadId));
                Marshal.WriteInt64(view,16,processStartFileTime);
                Marshal.WriteInt64(view,40,frequency);
                Marshal.WriteInt32(view,60,0);
                Marshal.WriteInt32(view,76,0);
                WriteBody(false,GameStallStage.None,lastQpc,Time.frameCount);
                EndWrite();available=true;
                return true;
            }
            catch(DllNotFoundException){FailureReason="Windows native API unavailable.";ReleaseHandles();return false;}
            catch(EntryPointNotFoundException){FailureReason="Windows native entry point unavailable.";ReleaseHandles();return false;}
#else
            FailureReason="Windows diagnostic unavailable on this platform.";
            return false;
#endif
        }

        internal void BeginSample()
        {
            if(!available)return;
            if(active)throw new InvalidOperationException("Stall sample already active.");
            epoch=unchecked(epoch+1);
            if(epoch==0){DisableProtocol("Sample epoch exhausted.");return;}
            active=true;
            Heartbeat(GameStallStage.SampleBegin);
        }

        internal void EndSample()
        {
            if(!available||!active)return;
            active=false;
            Publish(GameStallStage.SampleEnd);
        }

        internal void Heartbeat(GameStallStage stage)
        {
            if(!available||!active)return;
            Publish(stage);
        }

        void Publish(GameStallStage stage)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if(!QueryPerformanceCounter(out var qpc)||qpc<lastQpc)
            {DisableProtocol("Native heartbeat clock failed or regressed.");return;}
            lastQpc=qpc;
            int frame=Time.frameCount;
            BeginWrite();WriteBody(active,stage,qpc,frame);EndWrite();
            HeartbeatCount++;
#endif
        }

        void DisableProtocol(string reason)
        {
            active=false;FailureReason=reason;
            if(view!=IntPtr.Zero)
            {BeginWrite();WriteBody(false,GameStallStage.Disabled,lastQpc,Time.frameCount);EndWrite();}
            available=false;
        }

        void BeginWrite()
        {
            sequence=unchecked(sequence+1);
            Marshal.WriteInt32(view,24,unchecked((int)sequence));
            Thread.MemoryBarrier();
        }
        void EndWrite()
        {
            Thread.MemoryBarrier();sequence=unchecked(sequence+1);
            Marshal.WriteInt32(view,24,unchecked((int)sequence));
        }
        void WriteBody(bool sampleActive,GameStallStage stage,long qpc,int frame)
        {
            if(!sampleActive||stage==GameStallStage.SampleBegin)
            {lastReadSampleQpc=0;lastReadSampleFrame=-1;}
            else if(stage==GameStallStage.ReadSample)
            {lastReadSampleQpc=qpc;lastReadSampleFrame=frame;}
            Marshal.WriteInt32(view,28,sampleActive?1:0);
            Marshal.WriteInt32(view,32,frame);
            Marshal.WriteInt32(view,36,(int)stage);
            Marshal.WriteInt64(view,48,qpc);
            Marshal.WriteInt32(view,56,unchecked((int)epoch));
            Marshal.WriteInt64(view,64,lastReadSampleQpc);
            Marshal.WriteInt32(view,72,lastReadSampleFrame);
        }

        void OnDisable(){Dispose();}
        void OnDestroy(){Dispose();}
        public void Dispose()
        {
            if(disposed)return;
            disposed=true;
            if(view!=IntPtr.Zero)DisableProtocol(FailureReason);
            active=false;available=false;
            ReleaseHandles();
        }
        void ReleaseHandles()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if(view!=IntPtr.Zero){var old=view;view=IntPtr.Zero;UnmapViewOfFile(old);}
            if(mapping!=IntPtr.Zero){var old=mapping;mapping=IntPtr.Zero;CloseHandle(old);}
#endif
        }
    }
}
