using ColonyRTS.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace ColonyRTS.Tests
{
    public sealed class HudIconAtlasTests
    {
        [Test]
        public void GeneratedAtlasIsAnImportedFullResolutionReadOnlyHudResource()
        {
            // This checks the actual authored/imported asset boundary, not a synthetic texture
            // or a second implementation of the consumer's UV arithmetic.
            var atlas = Resources.Load<Texture2D>("UI/command-icons");
            Assert.That(atlas, Is.Not.Null, "The generated command atlas must be included as a runtime Resources asset.");
            Assert.That(atlas.width, Is.EqualTo(1536), "Import downscaling would lose the authored 256px icon cells.");
            Assert.That(atlas.height, Is.EqualTo(1024));
            Assert.That(atlas.mipmapCount, Is.EqualTo(1), "Mip chains can bleed neighboring atlas cells into small command icons.");
            Assert.That(atlas.isReadable, Is.False, "Rendering the atlas must not retain an unnecessary CPU pixel copy.");
            Assert.That(atlas.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(atlas.filterMode, Is.EqualTo(FilterMode.Bilinear));
            HudIconAtlas.Initialize();
            Assert.That(HudIconAtlas.IsReady, Is.True, "The real imported asset must pass the public HUD bootstrap gate.");
        }
    }
}
