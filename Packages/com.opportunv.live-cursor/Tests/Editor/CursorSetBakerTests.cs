using System;
using NUnit.Framework;
using Opportunv.LiveCursor.Editor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Opportunv.LiveCursor.Tests.Editor
{
    public sealed class CursorSetBakerTests
    {
        private static readonly Color32 _red = new(255, 0, 0, 255);
        private static readonly Color32 _blue = new(0, 0, 255, 255);
        private static readonly Color32 _green = new(0, 255, 0, 255);

        private InMemoryFrameLoader _loader;
        private CursorImportReport _report;
        private CursorSetBaker _baker;
        private CursorSet _set;

        [SetUp]
        public void SetUp()
        {
            _loader = new();
            _report = new();
            _baker = new(_loader, _report);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var texture in _baker.Textures)
            {
                Object.DestroyImmediate(texture);
            }

            if (_set != null)
            {
                Object.DestroyImmediate(_set);
            }

            _loader.Dispose();
        }

        [Test]
        public void Bake_BuildsStatesTransitionsSizesAndHotspots()
        {
            AddStandardFrames();

            _set = _baker.Bake(CreateStandardDefinition(), "Set");

            Assert.That(_report.Errors, Is.Empty);
            Assert.That(_report.Warnings, Is.Empty);
            Assert.That(_set.name, Is.EqualTo("Set"));
            Assert.That(_set.SizeCount, Is.EqualTo(2));
            Assert.That(_set.GetSize(0), Is.EqualTo(4));
            Assert.That(_set.GetState(0).Loop.GetFrame(0).GetHotspot(0), Is.EqualTo(new Vector2(2f, 1f)));
            Assert.That(_set.GetState(0).Loop.GetFrame(0).GetHotspot(1), Is.EqualTo(new Vector2(4f, 2f)));
            Assert.That(_set.StateCount, Is.EqualTo(2));
            Assert.That(_set.GetState(0).Loop.FrameCount, Is.EqualTo(2));
            Assert.That(_set.GetState(0).Loop.FrameDuration, Is.EqualTo(0.1f).Within(1e-6f));
            Assert.That(_set.GetState(1).LoopDelay, Is.EqualTo(0.25f).Within(1e-6f));
            Assert.That(_set.TransitionCount, Is.EqualTo(1));
            Assert.That(_set.GetTransition(0).Clip.FrameCount, Is.EqualTo(4));
            Assert.That(_set.GetTransition(0).ReverseFrameDuration, Is.EqualTo(0.05f).Within(1e-6f));
            Assert.That(_baker.Textures.Count, Is.EqualTo((2 + 1 + 4) * 2));
        }

        [Test]
        public void Bake_ProducesReadableTexturesOfEachSize()
        {
            AddStandardFrames();

            _set = _baker.Bake(CreateStandardDefinition(), "Set");

            var small = _set.GetState(0).Loop.GetFrame(0).GetTexture(0);
            var large = _set.GetState(0).Loop.GetFrame(0).GetTexture(1);
            Assert.That(small.width, Is.EqualTo(4));
            Assert.That(large.width, Is.EqualTo(8));
            Assert.That(small.isReadable, Is.True);
            Assert.That(small.format, Is.EqualTo(TextureFormat.RGBA32));
            Assert.That(small.mipmapCount, Is.EqualTo(1));
            Assert.That(small.GetPixel(1, 1), Is.EqualTo((Color)_red));
        }

        [Test]
        public void Bake_PlayerCanUseResult()
        {
            AddStandardFrames();
            _set = _baker.Bake(CreateStandardDefinition(), "Set");
            RecordingCursorOutput output = new();
            CursorPlayer player = new(output);
            player.SetSystemCursorSize(4);

            player.SetSet(_set);
            player.SetState("B");

            Assert.That(output.Last, Is.EqualTo("A>B 001 @4"));
        }

        [Test]
        public void Bake_NullDefinitionReportsErrorAndReturnsEmptySet()
        {
            _set = _baker.Bake(null, "Set");

            Assert.That(_report.HasErrors, Is.True);
            Assert.That(_set.StateCount, Is.EqualTo(0));
        }

        [Test]
        public void Bake_ReportsCanvasMismatch()
        {
            AddStandardFrames();
            _loader.Add("B", _loader.CreateFrame(16, _blue, "Wrong"));

            _set = _baker.Bake(CreateStandardDefinition(), "Set");

            Assert.That(_report.Errors, Has.Some.Contains("('Wrong') is 16x16"));
            Assert.That(_set.StateCount, Is.EqualTo(0));
        }

        [Test]
        public void Bake_ReportsNonSquareCanvas()
        {
            _loader.Add("A", new Texture2D(8, 4) { name = "Wide" });
            var definition = CreateStandardDefinition();
            definition.states = new[] { definition.states[0] };
            definition.transitions = null;

            _set = _baker.Bake(definition, "Set");

            Assert.That(_report.Errors, Has.Some.Contains("must be square"));
        }

        [Test]
        public void Bake_ReportsUnknownTransitionState()
        {
            AddStandardFrames();
            var definition = CreateStandardDefinition();
            definition.transitions[0].to = "Missing";

            _set = _baker.Bake(definition, "Set");

            Assert.That(_report.Errors, Has.Some.Contains("unknown state 'Missing'"));
        }

        [Test]
        public void Bake_ReportsDuplicateState()
        {
            AddStandardFrames();
            var definition = CreateStandardDefinition();
            definition.states[1].name = "A";

            _set = _baker.Bake(definition, "Set");

            Assert.That(_report.Errors, Has.Some.Contains("'A' is defined twice"));
        }

        [Test]
        public void Bake_ReportsHotspotOutsideCanvas()
        {
            AddStandardFrames();
            var definition = CreateStandardDefinition();
            definition.hotspot = new[] { 8, 0 };

            _set = _baker.Bake(definition, "Set");

            Assert.That(_report.Errors, Has.Some.Contains("outside the 8x8 canvas"));
        }

        [Test]
        public void Bake_UsesStateHotspotOverride()
        {
            AddStandardFrames();
            var definition = CreateStandardDefinition();
            definition.states[1].hotspot = new[] { 6, 6 };

            _set = _baker.Bake(definition, "Set");

            Assert.That(_report.Errors, Is.Empty);
            Assert.That(_set.GetState(0).Loop.GetFrame(0).GetHotspot(1), Is.EqualTo(new Vector2(4f, 2f)));
            Assert.That(_set.GetState(1).Loop.GetFrame(0).GetHotspot(1), Is.EqualTo(new Vector2(6f, 6f)));
            Assert.That(_set.GetState(1).Loop.GetFrame(0).GetHotspot(0), Is.EqualTo(new Vector2(3f, 3f)));
        }

        [Test]
        public void Bake_MovesTransitionHotspotBetweenStates()
        {
            AddStandardFrames();
            var definition = CreateStandardDefinition();
            definition.states[1].hotspot = new[] { 6, 6 };

            _set = _baker.Bake(definition, "Set");

            var clip = _set.GetTransition(0).Clip;
            Assert.That(clip.GetFrame(0).GetHotspot(1), Is.EqualTo(new Vector2(4f, 2f)));
            Assert.That(clip.GetFrame(1).GetHotspot(1), Is.EqualTo(new Vector2(5f, 3f)));
            Assert.That(clip.GetFrame(2).GetHotspot(1), Is.EqualTo(new Vector2(5f, 5f)));
            Assert.That(clip.GetFrame(3).GetHotspot(1), Is.EqualTo(new Vector2(6f, 6f)));
        }

        [Test]
        public void Bake_UsesTransitionHotspotOverride()
        {
            AddStandardFrames();
            var definition = CreateStandardDefinition();
            definition.states[1].hotspot = new[] { 6, 6 };
            definition.transitions[0].hotspot = new[] { 1, 1 };

            _set = _baker.Bake(definition, "Set");

            var clip = _set.GetTransition(0).Clip;
            for (var i = 0; i < clip.FrameCount; i++)
            {
                Assert.That(clip.GetFrame(i).GetHotspot(1), Is.EqualTo(new Vector2(1f, 1f)));
            }
        }

        [Test]
        public void Bake_ReportsStateHotspotOutsideCanvas()
        {
            AddStandardFrames();
            var definition = CreateStandardDefinition();
            definition.states[1].hotspot = new[] { 9, 0 };

            _set = _baker.Bake(definition, "Set");

            Assert.That(_report.Errors, Has.Some.Contains("State 'B': hotspot (9, 0) is outside"));
        }

        [Test]
        public void Bake_ReportsMalformedStateHotspot()
        {
            AddStandardFrames();
            var definition = CreateStandardDefinition();
            definition.states[0].hotspot = new[] { 1 };

            _set = _baker.Bake(definition, "Set");

            Assert.That(_report.Errors, Has.Some.Contains("'hotspot' must be [x, y]"));
        }

        [Test]
        public void Bake_ReportsMissingSizes()
        {
            AddStandardFrames();
            var definition = CreateStandardDefinition();
            definition.sizes = Array.Empty<int>();

            _set = _baker.Bake(definition, "Set");

            Assert.That(_report.Errors, Has.Some.Contains("'sizes'"));
        }

        [Test]
        public void Bake_WarnsWhenTransitionEndpointDiffersFromState()
        {
            AddStandardFrames();
            _loader.Add("A>B", Frame(_green), Frame(_green), Frame(_green), Frame(_blue));

            _set = _baker.Bake(CreateStandardDefinition(), "Set");

            Assert.That(_report.Errors, Is.Empty);
            Assert.That(_report.Warnings, Has.Some.Contains("first frame differs from 'A'"));
            Assert.That(_report.Warnings, Has.None.Contains("last frame differs"));
        }

        [Test]
        public void Bake_SkipsEndpointCheckWhenEndpointsAreNotIncluded()
        {
            AddStandardFrames();
            _loader.Add("A>B", Frame(_green), Frame(_green));
            var definition = CreateStandardDefinition();
            definition.transitions[0].includesEndpoints = false;

            _set = _baker.Bake(definition, "Set");

            Assert.That(_report.Warnings, Is.Empty);
        }

        [Test]
        public void Bake_WarnsAboutUpscaling()
        {
            AddStandardFrames();
            var definition = CreateStandardDefinition();
            definition.sizes = new[] { 16 };

            _set = _baker.Bake(definition, "Set");

            Assert.That(_report.Warnings, Has.Some.Contains("upscaled"));
        }

        private void AddStandardFrames()
        {
            _loader.Add("A", Frame(_red), Frame(_red));
            _loader.Add("B", Frame(_blue));
            _loader.Add("A>B", Frame(_red), Frame(_green), Frame(_green), Frame(_blue));
        }

        private Texture2D Frame(Color32 color)
        {
            return _loader.CreateFrame(8, color);
        }

        private static CursorSetDefinition CreateStandardDefinition()
        {
            return new()
            {
                sizes = new[] { 4, 8 },
                hotspot = new[] { 4, 2 },
                states = new[]
                {
                    new CursorStateDefinition
                    {
                        name = "A",
                        frames = new() { folder = "A" },
                        frameDurationMs = 100f
                    },
                    new CursorStateDefinition
                    {
                        name = "B",
                        frames = new() { folder = "B" },
                        frameDurationMs = 100f,
                        loopDelayMs = 250f
                    }
                },
                transitions = new[]
                {
                    new CursorTransitionDefinition
                    {
                        from = "A",
                        to = "B",
                        frames = new() { folder = "A>B" },
                        frameDurationMs = 20f,
                        reverseFrameDurationMs = 50f
                    }
                }
            };
        }
    }
}
