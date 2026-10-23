using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using ColonyRTS.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace ColonyRTS.Tests
{
    public sealed class GameStallSymbolRequestTests
    {
        const BindingFlags Members=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        static readonly Assembly Runtime=typeof(BattleHud).Assembly;
        static readonly Type Requests=Runtime.GetType("ColonyRTS.Diagnostics.GameStallSymbolRequests",true);
        static readonly Type Batch=Runtime.GetType("ColonyRTS.Diagnostics.GameStallJitRequestBatch",true);
        static readonly Type Symbols=Runtime.GetType("ColonyRTS.Diagnostics.WindowsGameStallSymbols",true);
        const string Map="Local\\ColonyRTS.GameStall.0123456789abcdef0123456789abcdef";
        const string Json="{\"ProtocolVersion\":1,\"Pid\":42,\"CreationFileTime\":\"134372167199628225\",\"MapName\":\"Local\\\\ColonyRTS.GameStall.0123456789abcdef0123456789abcdef\",\"Requests\":[{\"CaptureIndex\":1,\"SampleEpoch\":2,\"ReadAnchorFrame\":70,\"ReadAnchorQpc\":1000000,\"HeartbeatFrame\":71,\"DirectRip\":\"0x1234\"}]}";
        [DllImport("kernel32.dll",ExactSpelling=true)]static extern uint GetCurrentThreadId();

        static object Parse(string json)=>JsonUtility.FromJson(json,Batch);
        static string Validate(object batch)=>Requests.GetMethod("Validate",Members).Invoke(null,new object[]{batch,42u,"134372167199628225",Map,2u}) as string;
        static object Resolver(uint pid,uint tid)=>Activator.CreateInstance(Symbols,Members,null,new object[]{pid,tid},null);
        static object Lookup(object resolver,ulong pc)=>Symbols.GetMethod("Lookup",Members).Invoke(resolver,new object[]{pc});
        static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,Members).GetValue(value);

        [Test] public void ValidRequestKeepsLosslessIdentityAndBothFrameIds()
        {
            var batch=Parse(Json);
            Assert.That(Validate(batch),Is.Null);
            Assert.That(Field<string>(batch,"CreationFileTime"),Is.EqualTo("134372167199628225"));
        }
        [TestCase("\"Pid\":42","\"Pid\":43")]
        [TestCase("134372167199628225","134372167199628224")]
        [TestCase("0123456789abcdef0123456789abcdef","1123456789abcdef0123456789abcdef")]
        [TestCase("\"ProtocolVersion\":1","\"ProtocolVersion\":2")]
        [TestCase("\"SampleEpoch\":2","\"SampleEpoch\":3")]
        [TestCase("\"SampleEpoch\":2","\"SampleEpoch\":0")]
        [TestCase("\"ReadAnchorQpc\":1000000","\"ReadAnchorQpc\":0")]
        [TestCase("\"HeartbeatFrame\":71","\"HeartbeatFrame\":72")]
        [TestCase("\"HeartbeatFrame\":71","\"HeartbeatFrame\":69")]
        [TestCase("0x1234","0x0")]
        [TestCase("0x1234","0xffffffffffffffff")]
        [TestCase("0x1234","0x12zz")]
        public void OtherIdentityOrInvalidWindowNeverReachesMono(string from,string to)
        {
            Assert.That(Json.Contains(from),Is.True);
            Assert.That(Validate(Parse(Json.Replace(from,to))),Is.Not.Null);
        }
        [Test] public void DuplicateOrExcessRequestsAreRejected()
        {
            var batch=Parse(Json);var items=(Array)Batch.GetField("Requests",Members).GetValue(batch);
            var duplicate=Array.CreateInstance(items.GetType().GetElementType(),2);
            duplicate.SetValue(items.GetValue(0),0);duplicate.SetValue(items.GetValue(0),1);
            Batch.GetField("Requests",Members).SetValue(batch,duplicate);
            Assert.That(Validate(batch),Does.Contain("Duplicate"));
            Batch.GetField("Requests",Members).SetValue(batch,Array.CreateInstance(items.GetType().GetElementType(),3));
            Assert.That(Validate(batch),Does.Contain("one or two"));
        }
        [Test] public void ResolverConstructionIsLazyAndWrongIdentityIsUnavailable()
        {
            using(var process=Process.GetCurrentProcess())
            {
                var resolver=Resolver((uint)process.Id+1,GetCurrentThreadId());
                Assert.That(Symbols.GetProperty("NativeAvailable",Members).GetValue(resolver),Is.False);
                var result=Lookup(resolver,0x1234);
                Assert.That(Field<bool>(result,"Available"),Is.False);
                Assert.That(Field<string>(result,"FailureReason"),Does.Contain("identity"));
            }
        }
        [Test] public void ActualOwnFixtureResolvesOnlyInsideItsJitRange()
        {
            var fixture=Requests.GetMethod("LookupFixture",Members);
            Assert.That(fixture.Invoke(null,new object[]{17}),Is.EqualTo(17^0x5a5a));
            ulong pc=unchecked((ulong)fixture.MethodHandle.GetFunctionPointer().ToInt64());
            using(var process=Process.GetCurrentProcess())
            {
                var resolver=Resolver((uint)process.Id,GetCurrentThreadId());
                var result=Lookup(resolver,pc);
                Assert.That(Field<bool>(result,"Available"),Is.True,Field<string>(result,"FailureReason"));
                Assert.That(Field<string>(result,"MethodName"),Does.Contain("GameStallSymbolRequests:LookupFixture"));
                ulong start=Field<ulong>(result,"CodeStart");int size=Field<int>(result,"CodeSize");
                Assert.That(size,Is.GreaterThan(0));Assert.That(pc,Is.GreaterThanOrEqualTo(start));
                Assert.That(pc-start,Is.LessThan((ulong)size));
                Assert.That(Field<ulong>(result,"Offset"),Is.EqualTo(pc-start));
                Assert.That(Field<string>(result,"PcHex"),Is.EqualTo("0x"+pc.ToString("x")));
                var zero=Lookup(resolver,0);
                Assert.That(Field<bool>(zero,"Available"),Is.False);
                Assert.That(Field<string>(zero,"FailureReason"),Does.Contain("PC outside"));
            }
        }
    }
}
