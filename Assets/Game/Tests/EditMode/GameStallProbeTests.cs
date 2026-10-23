using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using ColonyRTS.Presentation;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColonyRTS.Tests
{
    public sealed class GameStallProbeTests
    {
        const BindingFlags Members=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        static readonly Type ProbeType=typeof(BattleHud).Assembly.GetType("ColonyRTS.Diagnostics.WindowsGameStallProbe",true);
        GameObject fixture;
        Component probe;
        Scene scene;
        IntPtr reader,view;

        [DllImport("kernel32.dll",ExactSpelling=true,SetLastError=true)]
        static extern IntPtr OpenFileMappingW(uint access,bool inherit,[MarshalAs(UnmanagedType.LPWStr)]string name);
        [DllImport("kernel32.dll",ExactSpelling=true,SetLastError=true)]
        static extern IntPtr MapViewOfFile(IntPtr handle,uint access,uint high,uint low,UIntPtr bytes);
        [DllImport("kernel32.dll",ExactSpelling=true)]static extern bool UnmapViewOfFile(IntPtr address);
        [DllImport("kernel32.dll",ExactSpelling=true)]static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll",ExactSpelling=true)]static extern uint GetCurrentThreadId();

        [SetUp] public void SetUp()
        {
            scene=EditorSceneManager.NewPreviewScene();
            fixture=new GameObject("Owned game-stall mapping contract fixture");
            SceneManager.MoveGameObjectToScene(fixture,scene);
            fixture.SetActive(false);
            probe=fixture.AddComponent(ProbeType);
        }
        [TearDown] public void TearDown()
        {
            if(probe!=null)((IDisposable)probe).Dispose();
            if(view!=IntPtr.Zero){UnmapViewOfFile(view);view=IntPtr.Zero;}
            if(reader!=IntPtr.Zero){CloseHandle(reader);reader=IntPtr.Zero;}
            if(fixture!=null)UnityEngine.Object.DestroyImmediate(fixture);
            if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);
        }
        object Call(Component target,string name,params object[] args)=>ProbeType.GetMethod(name,Members).Invoke(target,args);
        object Get(string name)=>ProbeType.GetProperty(name,Members).GetValue(probe);
        void Pulse(int stage)
        {
            var method=ProbeType.GetMethod("Heartbeat",Members);
            method.Invoke(probe,new[] { Enum.ToObject(method.GetParameters()[0].ParameterType,stage) });
        }
        string InitializeAndRead()
        {
            string name="Local\\ColonyRTS.GameStall."+Guid.NewGuid().ToString("N");
            Assert.That(Call(probe,"Initialize",name),Is.True,Get("FailureReason") as string);
            reader=OpenFileMappingW(0x0004,false,name);
            Assert.That(reader,Is.Not.EqualTo(IntPtr.Zero));
            view=MapViewOfFile(reader,0x0004,0,0,new UIntPtr(80));
            Assert.That(view,Is.Not.EqualTo(IntPtr.Zero));
            return name;
        }
        int Int(int offset)=>Marshal.ReadInt32(view,offset);
        long Long(int offset)=>Marshal.ReadInt64(view,offset);
        void Stable()
        {
            Assert.That(Int(24)&1,Is.Zero,"A finished publish must leave an even sequence.");
            Assert.That(Int(0),Is.EqualTo(0x53535452));
            Assert.That(Int(4),Is.EqualTo(2));
            Assert.That(Int(60),Is.Zero);
            Assert.That(Int(76),Is.Zero);
        }
        void AnchorCleared()
        {
            Assert.That(Long(64),Is.Zero);
            Assert.That(Int(72),Is.EqualTo(-1));
        }

        [Test] public void UninitializedAndInactiveCallbacksDoNotPublish()
        {
            Pulse(2);Call(probe,"EndSample");
            Assert.That(Get("Available"),Is.False);
            Assert.That(Get("HeartbeatCount"),Is.EqualTo(0L));
            Assert.That(ProbeType.GetField("mapping",Members).GetValue(probe),Is.EqualTo(IntPtr.Zero));
            InitializeAndRead();AnchorCleared();
            int sequence=Int(24);long qpc=Long(48);
            Pulse(2);Call(probe,"EndSample");
            Assert.That(Int(24),Is.EqualTo(sequence));
            Assert.That(Long(48),Is.EqualTo(qpc));
            Assert.That(Get("HeartbeatCount"),Is.EqualTo(0L));
        }

        [Test] public void MappingPublishesExactCurrentIdentityAndRawClock()
        {
            InitializeAndRead();Stable();AnchorCleared();
            using(var process=Process.GetCurrentProcess())
            {
                Assert.That(Int(8),Is.EqualTo(process.Id));
                Assert.That(Long(16),Is.EqualTo(process.StartTime.ToUniversalTime().ToFileTimeUtc()));
            }
            Assert.That(unchecked((uint)Int(12)),Is.EqualTo(GetCurrentThreadId()));
            Assert.That(Long(40),Is.GreaterThan(0));
            Assert.That(Long(48),Is.GreaterThan(0));
            Assert.That(Get("ProcessStartFileTimeText"),Is.EqualTo(Long(16).ToString(System.Globalization.CultureInfo.InvariantCulture)));
            Assert.That(Int(28),Is.Zero);Assert.That(Int(36),Is.Zero);Assert.That(Int(56),Is.Zero);
        }

        [Test] public void BeginPulseEndAndNextEpochRefreshOnePrivateMapping()
        {
            string name=InitializeAndRead();
            Call(probe,"BeginSample");Stable();AnchorCleared();
            Assert.That(Int(28),Is.EqualTo(1));Assert.That(Int(36),Is.EqualTo(1));Assert.That(Int(56),Is.EqualTo(1));
            Assert.That(Int(32),Is.EqualTo(Time.frameCount));
            long before=Long(48);int sequence=Int(24);
            Pulse(2);Stable();
            Assert.That(Int(36),Is.EqualTo(2));Assert.That(Int(24),Is.GreaterThan(sequence));Assert.That(Long(48),Is.GreaterThanOrEqualTo(before));
            var duplicate=Assert.Throws<TargetInvocationException>(()=>Call(probe,"BeginSample"));
            Assert.That(duplicate.InnerException,Is.TypeOf<InvalidOperationException>());
            Pulse(4);
            Assert.That(Long(64),Is.GreaterThan(0));
            Call(probe,"EndSample");Stable();AnchorCleared();
            Assert.That(Int(28),Is.Zero);Assert.That(Int(36),Is.EqualTo(5));
            Call(probe,"BeginSample");Stable();AnchorCleared();
            Assert.That(Int(56),Is.EqualTo(2));Assert.That(Get("MapName"),Is.EqualTo(name));
        }

        [Test] public void OnlyActiveReadReplacesTheAnchorAcrossOtherBoundaryHeartbeats()
        {
            InitializeAndRead();int inactiveSequence=Int(24);
            Pulse(4);AnchorCleared();
            Assert.That(Int(24),Is.EqualTo(inactiveSequence));
            Call(probe,"BeginSample");
            Pulse(2);Stable();AnchorCleared();
            Pulse(3);Stable();AnchorCleared();
            Pulse(4);Stable();
            long readQpc=Long(64);int readFrame=Int(72);
            Assert.That(readQpc,Is.EqualTo(Long(48)).And.GreaterThan(0));
            Assert.That(readFrame,Is.EqualTo(Int(32)).And.EqualTo(Time.frameCount));
            foreach(int stage in new[]{2,3})
            {
                Pulse(stage);Stable();
                Assert.That(Long(48),Is.GreaterThanOrEqualTo(readQpc));
                Assert.That(Long(64),Is.EqualTo(readQpc));
                Assert.That(Int(72),Is.EqualTo(readFrame));
            }
            Pulse(4);Stable();
            Assert.That(Long(64),Is.EqualTo(Long(48)).And.GreaterThanOrEqualTo(readQpc));
            Assert.That(Int(72),Is.EqualTo(Int(32)));
        }

        [Test] public void ExistingNonceIsRejectedWithoutChangingOriginalWriter()
        {
            string name=InitializeAndRead();int sequence=Int(24);
            var other=new GameObject("Rejected map collision fixture");other.SetActive(false);
            SceneManager.MoveGameObjectToScene(other,scene);
            var second=other.AddComponent(ProbeType);
            try
            {
                Assert.That(Call(second,"Initialize",name),Is.False);
                Assert.That(ProbeType.GetProperty("FailureReason",Members).GetValue(second).ToString(),Does.Contain("already exists"));
                Assert.That(Int(24),Is.EqualTo(sequence));Assert.That(Get("Available"),Is.True);
            }
            finally { ((IDisposable)second).Dispose();UnityEngine.Object.DestroyImmediate(other); }
        }

        [Test] public void InvalidMapNameFailsBeforeCreatingAnObject()
        {
            var error=Assert.Throws<TargetInvocationException>(()=>Call(probe,"Initialize","Global\\OtherApplication"));
            Assert.That(error.InnerException,Is.TypeOf<ArgumentException>());
            Assert.That(ProbeType.GetField("mapping",Members).GetValue(probe),Is.EqualTo(IntPtr.Zero));
            Assert.That(Get("Available"),Is.False);
        }

        [Test] public void ClockRegressionDisablesInsteadOfPublishingAnArmedFalseInterval()
        {
            InitializeAndRead();Call(probe,"BeginSample");Pulse(4);
            ProbeType.GetField("lastQpc",Members).SetValue(probe,long.MaxValue);
            Pulse(2);Stable();AnchorCleared();
            Assert.That(Get("Available"),Is.False);Assert.That(Get("SampleActive"),Is.False);
            Assert.That(Int(28),Is.Zero);Assert.That(Int(36),Is.EqualTo(6));
            Assert.That(Get("FailureReason").ToString(),Does.Contain("regressed"));
        }

        [Test] public void DisposeDisarmsRetainedReaderAndNeverReinitializes()
        {
            InitializeAndRead();Call(probe,"BeginSample");Pulse(4);
            ((IDisposable)probe).Dispose();Stable();AnchorCleared();
            Assert.That(Int(28),Is.Zero);Assert.That(Int(36),Is.EqualTo(6));
            int sequence=Int(24);Pulse(2);Call(probe,"BeginSample");
            Assert.That(Int(24),Is.EqualTo(sequence));Assert.That(Get("Available"),Is.False);
            var error=Assert.Throws<TargetInvocationException>(()=>Call(probe,"Initialize","Local\\ColonyRTS.GameStall."+Guid.NewGuid().ToString("N")));
            Assert.That(error.InnerException,Is.TypeOf<InvalidOperationException>());
        }

        [Test] public void SymbolRequestLookupRejectsAnActiveSampleBeforeAnyPathAccess()
        {
            InitializeAndRead();Call(probe,"BeginSample");
            var consumer=ProbeType.Assembly.GetType("ColonyRTS.Diagnostics.GameStallSymbolRequests",true);
            var resolve=consumer.GetMethod("Resolve",BindingFlags.Static|BindingFlags.NonPublic);
            // Null directory would fail Path.Combine if file work were reached.
            var error=Assert.Throws<TargetInvocationException>(()=>resolve.Invoke(null,new object[]{null,probe}));
            Assert.That(error.InnerException,Is.TypeOf<InvalidOperationException>());
            Assert.That(Int(28),Is.EqualTo(1));
        }
    }
}
