using ColonyRTS.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace ColonyRTS.Tests
{
    public sealed class HudInputBoundaryTests
    {
        [Test]
        public void TechnologyButtonCannotAlsoTargetTheWorld()
        {
            var button=RtsInteraction.TechnologyButtonRect;
            foreach(var point in new[]{button.center,new Vector2(button.x+1,button.y+1),new Vector2(button.xMax-1,button.yMax-1)})
                Assert.That(RtsInteraction.IsWorldArea(point),Is.False,"A technology button click must not place a building, select units or issue a target order.");
            Assert.That(RtsInteraction.IsWorldArea(new Vector2(Screen.width*.5f,(62+Screen.height-RtsInteraction.HudHeight)*.5f)),Is.True);
            Assert.That(RtsInteraction.IsWorldArea(new Vector2(10,30)),Is.False);
            Assert.That(RtsInteraction.IsWorldArea(RtsInteraction.MapRect.center),Is.False);
        }
    }
}
