using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace ColonyRTS.Diagnostics
{
    [Serializable] internal sealed class GameStallJitRequest
    {
        public int CaptureIndex,ReadAnchorFrame,HeartbeatFrame;
        public uint SampleEpoch;
        public long ReadAnchorQpc;
        public string DirectRip;
    }
    [Serializable] internal sealed class GameStallJitRequestBatch
    {
        public int ProtocolVersion;
        public uint Pid;
        public string CreationFileTime,MapName;
        public GameStallJitRequest[] Requests;
    }
    [Serializable] internal sealed class GameStallJitResolution
    {
        public GameStallJitRequest Request;
        public GameStallSymbolEvidence Symbol;
        public bool LookupClockAvailable;
        public long LookupQpcBefore,LookupQpcAfter,NativeQpcFrequency;
    }
    [Serializable] internal sealed class GameStallJitReport
    {
        public string Status,FailureReason,LookupUtc;
        public string Semantics="Post-run lookup in the current own-game Mono domain. Captured PC may have changed JIT lifetime; this is not a capture-time managed stack or execution-duration measurement.";
        public bool TimedSampleActive,NativeAvailable,NativeFixtureMatched;
        public int RequestProtocolVersion=1;
        public uint Pid,MainTid,LastSampleEpoch;
        public string CreationFileTime,MapName;
        public GameStallSymbolEvidence Fixture;
        public List<GameStallJitResolution> Resolutions=new List<GameStallJitResolution>();
    }

    // One bounded read after all trial counters and the untimed volley capture.
    // No file access, Mono lookup or native initialization occurs in timed frames.
    internal static class GameStallSymbolRequests
    {
        internal const int MaximumRequestBytes=16384;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [DllImport("kernel32.dll",ExactSpelling=true)]
        [return:MarshalAs(UnmanagedType.Bool)]static extern bool QueryPerformanceCounter(out long value);
        [DllImport("kernel32.dll",ExactSpelling=true)]
        [return:MarshalAs(UnmanagedType.Bool)]static extern bool QueryPerformanceFrequency(out long value);
#endif
        internal static string Validate(GameStallJitRequestBatch batch,uint pid,string creation,string map,uint maximumEpoch)
        {
            if(batch==null||batch.ProtocolVersion!=1||batch.Pid!=pid||
                !string.Equals(batch.CreationFileTime,creation,StringComparison.Ordinal)||
                !string.Equals(batch.MapName,map,StringComparison.Ordinal))return "Request protocol or own-game identity mismatch.";
            if(batch.Requests==null||batch.Requests.Length<1||batch.Requests.Length>2)return "Expected one or two capture requests.";
            int seen=0;
            foreach(var request in batch.Requests)
            {
                if(request==null||request.CaptureIndex<1||request.CaptureIndex>2)return "Invalid capture index.";
                int bit=1<<request.CaptureIndex;
                if((seen&bit)!=0)return "Duplicate capture index.";
                seen|=bit;
                long frameDelta=(long)request.HeartbeatFrame-request.ReadAnchorFrame;
                if(request.SampleEpoch==0||request.SampleEpoch>maximumEpoch||request.ReadAnchorQpc<=0||
                    request.ReadAnchorFrame<0||frameDelta<0||frameDelta>1)return "Invalid capture epoch or Read-anchor window.";
                if(!TryPc(request.DirectRip,out _))return "Invalid captured PC.";
            }
            return null;
        }
        static bool TryPc(string text,out ulong pc)
        {
            pc=0;
            return text!=null&&text.StartsWith("0x",StringComparison.Ordinal)&&text.Length>2&&text.Length<=18&&
                ulong.TryParse(text.Substring(2),NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture,out pc)&&pc>0&&pc<=long.MaxValue;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int LookupFixture(int seed)=>seed^0x5a5a;

        internal static GameStallJitReport Resolve(string evidenceDirectory,WindowsGameStallProbe probe)
        {
            if(probe==null||probe.SampleActive)throw new InvalidOperationException("JIT capture lookup requires an inactive owned probe.");
            var report=new GameStallJitReport {
                Status="request-unavailable",LookupUtc=DateTime.UtcNow.ToString("O",CultureInfo.InvariantCulture),
                TimedSampleActive=probe.SampleActive,Pid=probe.ProcessId,MainTid=probe.MainThreadId,
                CreationFileTime=probe.ProcessStartFileTimeText,MapName=probe.MapName,LastSampleEpoch=probe.SampleEpoch };
            string input=Path.Combine(evidenceDirectory,"dumper","jit-requests.json");
            try
            {
                GameStallJitRequestBatch batch;
                // Delete sharing permits the helper's atomic replacement while
                // this reader finishes a coherent, already opened old version.
                using(var stream=new FileStream(input,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete))
                {
                    if(stream.Length<=0||stream.Length>MaximumRequestBytes)throw new InvalidDataException("Capture request exceeds the bounded size.");
                    var bytes=new byte[(int)stream.Length];int read=0;
                    while(read<bytes.Length)
                    {
                        int n=stream.Read(bytes,read,bytes.Length-read);
                        if(n==0)throw new InvalidDataException("Incomplete atomic capture request.");
                        read+=n;
                    }
                    batch=JsonUtility.FromJson<GameStallJitRequestBatch>(new UTF8Encoding(false,true).GetString(bytes));
                }
                string invalid=Validate(batch,probe.ProcessId,probe.ProcessStartFileTimeText,probe.MapName,probe.SampleEpoch);
                if(invalid!=null){report.Status="request-rejected";report.FailureReason=invalid;return Save(evidenceDirectory,report);}
                var resolver=new WindowsGameStallSymbols(probe.ProcessId,probe.MainThreadId);
                int fixtureValue=LookupFixture(17);
                var method=typeof(GameStallSymbolRequests).GetMethod(nameof(LookupFixture),BindingFlags.Static|BindingFlags.NonPublic);
                report.Fixture=resolver.Lookup(unchecked((ulong)method.MethodHandle.GetFunctionPointer().ToInt64()));
                report.NativeFixtureMatched=fixtureValue==(17^0x5a5a)&&report.Fixture.Available&&
                    report.Fixture.MethodName.Contains(typeof(GameStallSymbolRequests).FullName)&&report.Fixture.MethodName.Contains(nameof(LookupFixture));
                foreach(var request in batch.Requests)
                {
                    TryPc(request.DirectRip,out var pc);
                    var entry=new GameStallJitResolution{Request=request,LookupQpcBefore=-1,LookupQpcAfter=-1,NativeQpcFrequency=-1};
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                    long before=-1;
                    bool clock=QueryPerformanceFrequency(out var frequency)&&frequency>0&&QueryPerformanceCounter(out before);
                    if(clock){entry.NativeQpcFrequency=frequency;entry.LookupQpcBefore=before;}
#endif
                    entry.Symbol=resolver.Lookup(pc);
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                    if(clock&&QueryPerformanceCounter(out var after)&&after>=entry.LookupQpcBefore)
                    {entry.LookupQpcAfter=after;entry.LookupClockAvailable=true;}
#endif
                    report.Resolutions.Add(entry);
                }
                report.NativeAvailable=resolver.NativeAvailable;
                report.Status="completed-post-run-lookups";
                report.FailureReason=resolver.FailureReason;
            }
            catch(FileNotFoundException){report.FailureReason="Capture requests were not published before the final untimed lookup.";}
            catch(DirectoryNotFoundException){report.FailureReason="Capture request directory unavailable.";}
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is ArgumentException||e is DecoderFallbackException)
            {report.Status="request-unavailable";report.FailureReason=e.GetType().Name+": "+e.Message;}
            return Save(evidenceDirectory,report);
        }
        static GameStallJitReport Save(string directory,GameStallJitReport report)
        {
            var bytes=new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(report,true));
            using(var output=new FileStream(Path.Combine(directory,"game-stall-jit-symbols.json"),FileMode.CreateNew,FileAccess.Write,FileShare.Read))
                output.Write(bytes,0,bytes.Length);
            return report;
        }
    }
}
