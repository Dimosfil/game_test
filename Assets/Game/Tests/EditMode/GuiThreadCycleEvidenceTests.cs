using System;
using System.Reflection;
using ColonyRTS.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace ColonyRTS.Tests
{
    public sealed class GuiThreadCycleEvidenceTests
    {
        const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Static;
        delegate void Pair(ref BattleHud.GuiThreadCycleEvidence evidence,bool startAvailable,ulong start,bool endAvailable,ulong end,bool completed);
        static readonly Pair Accumulate=(Pair)Delegate.CreateDelegate(typeof(Pair),
            typeof(BattleHud).GetMethod("AccumulateGuiThreadCyclePair",Hidden));

        [Test]
        public void SuccessfulZeroIsAvailableAndDistinctFromMissingRead()
        {
            var measured=new BattleHud.GuiThreadCycleEvidence { Calls=1 };
            Accumulate(ref measured,true,17,true,17,true);
            Assert.That(measured.Available,Is.True);
            Assert.That(measured.Cycles,Is.Zero);
            Assert.That(measured.SuccessfulPairs,Is.EqualTo(1));
            var missing=new BattleHud.GuiThreadCycleEvidence { Calls=1 };
            Accumulate(ref missing,false,0,true,17,true);
            Assert.That(missing.Available,Is.False);
            Assert.That(missing.SuccessfulPairs,Is.Zero);
            Assert.That(missing.Completed,Is.True);
        }

        [TestCase(false,true)]
        [TestCase(true,false)]
        [TestCase(false,false)]
        public void MissingEndpointNeverBecomesSuccessfulPair(bool start,bool end)
        {
            var e=new BattleHud.GuiThreadCycleEvidence { Calls=1 };
            Accumulate(ref e,start,10,end,30,true);
            Assert.That(e.Available,Is.False);
            Assert.That(e.SuccessfulPairs,Is.Zero);
            Assert.That(e.Cycles,Is.Zero);
            Assert.That(e.Completed,Is.True);
        }

        [Test]
        public void RegressionAndOverflowPreserveEarlierSumAndInvalidateAggregate()
        {
            var e=new BattleHud.GuiThreadCycleEvidence { Calls=1 };
            Accumulate(ref e,true,10,true,20,true);
            e.Calls++;
            Accumulate(ref e,true,30,true,29,true);
            Assert.That(e.Cycles,Is.EqualTo(10UL));
            Assert.That(e.SuccessfulPairs,Is.EqualTo(1));
            Assert.That(e.Available,Is.False);
            var overflow=new BattleHud.GuiThreadCycleEvidence { Calls=2,SuccessfulPairs=1,Cycles=ulong.MaxValue-2 };
            Accumulate(ref overflow,true,0,true,3,true);
            Assert.That(overflow.Cycles,Is.EqualTo(ulong.MaxValue-2));
            Assert.That(overflow.SuccessfulPairs,Is.EqualTo(1));
            Assert.That(overflow.Available,Is.False);
        }

        [Test]
        public void RepeatedCallbacksAggregateAndEarlierFailureRemainsUnavailable()
        {
            var e=new BattleHud.GuiThreadCycleEvidence { Calls=1 };
            Accumulate(ref e,false,0,true,20,true);
            e.Calls++;
            Accumulate(ref e,true,25,true,40,true);
            Assert.That(e.Cycles,Is.EqualTo(15UL));
            Assert.That(e.SuccessfulPairs,Is.EqualTo(1));
            Assert.That(e.Available,Is.False);
            Assert.That(e.Completed,Is.True);
            Assert.That(e.CompletedCalls,Is.EqualTo(2));
        }

        [Test]
        public void ThrownScopeCanRetainValidPartialCyclesWithoutCompletedClaim()
        {
            var e=new BattleHud.GuiThreadCycleEvidence { Calls=1 };
            Accumulate(ref e,true,10,true,25,false);
            e.Calls++;
            Accumulate(ref e,true,30,true,40,true);
            Assert.That(e.Cycles,Is.EqualTo(25UL));
            Assert.That(e.Available,Is.True);
            Assert.That(e.Completed,Is.False);
            Assert.That(e.CompletedCalls,Is.EqualTo(1));
        }

        [Test]
        public void DisabledRealScopeNeverAttemptsNativeRead()
        {
            var go=new GameObject("Disabled GUI cycle scope fixture");
            go.SetActive(false);
            try
            {
                var hud=go.AddComponent<BattleHud>();
                var flags=BindingFlags.NonPublic|BindingFlags.Instance;
                var begin=typeof(BattleHud).GetMethod("BeginGuiThreadCycles",flags);
                var end=typeof(BattleHud).GetMethod("EndGuiThreadCycles",flags);
                var args=new object[] { new BattleHud.GuiThreadCycleEvidence() };
                object start=begin.Invoke(hud,args);
                var endArgs=new[] { args[0],start,(object)true };
                end.Invoke(hud,endArgs);
                var e=(BattleHud.GuiThreadCycleEvidence)endArgs[0];
                Assert.That(hud.DiagnosticGuiThreadCycleReadAttempts,Is.Zero);
                Assert.That(hud.DiagnosticGuiThreadCycleSuccessfulReads,Is.Zero);
                Assert.That(e.Calls,Is.Zero);
                Assert.That(e.Cycles,Is.Zero);
                Assert.That(e.Available,Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
