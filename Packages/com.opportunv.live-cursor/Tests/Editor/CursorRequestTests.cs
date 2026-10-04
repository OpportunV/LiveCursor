using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using static Opportunv.LiveCursor.Tests.Editor.TestCursorSetBuilder;
using Is = NUnit.Framework.Is;

namespace Opportunv.LiveCursor.Tests.Editor
{
    public sealed class CursorRequestTests
    {
        private const float LoopFrame = 0.125f;
        private const float TransitionFrame = 0.0625f;

        private static readonly CursorStateId _default = new("Default");
        private static readonly CursorStateId _grab = new("Grab");
        private static readonly CursorStateId _dragging = new("Dragging");
        private static readonly CursorStateId _busy = new("Busy");

        private TestCursorSetBuilder _builder;
        private RecordingCursorOutput _output;
        private CursorPlayer _player;

        [SetUp]
        public void SetUp()
        {
            _builder = new();
            _output = new();
            _player = new(_output);
            _player.SetSet(_builder
                .AddState("Default", 4, LoopFrame)
                .AddState("Grab", 4, LoopFrame)
                .AddState("Dragging", 4, LoopFrame)
                .AddState("Busy", 1, LoopFrame)
                .AddTransition("Default", "Grab", 6, TransitionFrame)
                .Build());
        }

        [TearDown]
        public void TearDown()
        {
            _builder.Dispose();
        }

        [Test]
        public void Request_OverridesBaseState()
        {
            var request = _player.Request(_grab);

            Assert.That(request.IsActive, Is.True);
            Assert.That(_player.TargetState, Is.EqualTo(_grab));
            Assert.That(_player.BaseState, Is.EqualTo(_default));
        }

        [Test]
        public void Request_ShowsFirstTransitionFrameAtOnce()
        {
            _player.Request(_grab, immediate: true);

            Assert.That(_output.Last, Is.EqualTo(FrameName("Default>Grab", 1)));
        }

        [Test]
        public void Release_ReturnsToBaseState()
        {
            var request = _player.Request(_busy);
            _player.Tick(1f);

            request.Dispose();
            _player.Tick(1f);

            Assert.That(request.IsActive, Is.False);
            Assert.That(_player.CurrentState, Is.EqualTo(_default));
            Assert.That(_player.ActiveRequestCount, Is.EqualTo(0));
        }

        [Test]
        public void HigherPriorityWins()
        {
            _player.Request(_grab);
            var dragging = _player.Request(_dragging, 5);
            _player.Request(_busy);

            Assert.That(_player.TargetState, Is.EqualTo(_dragging));

            dragging.Dispose();

            Assert.That(_player.TargetState, Is.EqualTo(_busy));
        }

        [Test]
        public void NewestRequestWinsTies()
        {
            var grab = _player.Request(_grab);
            var busy = _player.Request(_busy);

            Assert.That(_player.TargetState, Is.EqualTo(_busy));

            busy.Dispose();

            Assert.That(_player.TargetState, Is.EqualTo(_grab));

            grab.Dispose();
        }

        [Test]
        public void ReleasingHiddenRequestKeepsTarget()
        {
            var grab = _player.Request(_grab);
            _player.Request(_busy, 1);
            _player.Tick(1f);
            var applied = _output.ApplyCount;

            grab.Dispose();
            _player.Tick(LoopFrame);

            Assert.That(_player.CurrentState, Is.EqualTo(_busy));
            Assert.That(_output.ApplyCount, Is.EqualTo(applied));
        }

        [Test]
        public void SetState_WhileRequested_ChangesBaseOnly()
        {
            var request = _player.Request(_grab);

            _player.SetState(_busy);

            Assert.That(_player.TargetState, Is.EqualTo(_grab));
            Assert.That(_player.BaseState, Is.EqualTo(_busy));

            request.Dispose();

            Assert.That(_player.TargetState, Is.EqualTo(_busy));
        }

        [Test]
        public void ReleaseAllRequests_ReturnsToBaseState()
        {
            _player.Request(_grab);
            _player.Request(_busy, 3);

            _player.ReleaseAllRequests();
            _player.Tick(1f);

            Assert.That(_player.ActiveRequestCount, Is.EqualTo(0));
            Assert.That(_player.CurrentState, Is.EqualTo(_default));
        }

        [Test]
        public void DefaultAndReleasedHandlesAreSafe()
        {
            CursorRequest none = default;
            var request = _player.Request(_grab);

            none.Dispose();
            request.Dispose();
            request.Dispose();

            Assert.That(none.IsActive, Is.False);
            Assert.That(_player.ActiveRequestCount, Is.EqualTo(0));
        }

        [Test]
        public void ReleasingStaleHandleDoesNotTouchNewerRequest()
        {
            var first = _player.Request(_grab);
            first.Dispose();
            var second = _player.Request(_busy);

            first.Dispose();

            Assert.That(second.IsActive, Is.True);
            Assert.That(_player.TargetState, Is.EqualTo(_busy));
        }

        [Test]
        public void RequestBeforeSet_IsUsedWhenSetArrives()
        {
            CursorPlayer player = new(new RecordingCursorOutput());
            player.Request(_grab);

            player.SetSet(_builder.AddState("Default", 1, LoopFrame).AddState("Grab", 1, LoopFrame).Build("Late"));

            Assert.That(player.CurrentState, Is.EqualTo(_grab));
            Assert.That(player.BaseState, Is.EqualTo(_default));
        }

        [Test]
        public void Requests_DoNotAllocate()
        {
            _player.Request(_grab).Dispose();
            _player.Tick(1f);

            Assert.That(
                () =>
                {
                    var hover = _player.Request(_grab);
                    _player.Tick(TransitionFrame);
                    var press = _player.Request(_dragging, 1, true);
                    _player.Tick(TransitionFrame);
                    press.Release(true);
                    hover.Dispose();
                    _player.Tick(1f);
                },
                Is.Not.AllocatingGCMemory());
        }
    }
}
