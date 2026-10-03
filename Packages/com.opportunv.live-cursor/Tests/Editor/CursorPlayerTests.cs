using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using static Opportunv.LiveCursor.Tests.Editor.TestCursorSetBuilder;
using Is = NUnit.Framework.Is;

namespace Opportunv.LiveCursor.Tests.Editor
{
    public sealed class CursorPlayerTests
    {
        private const float LoopFrame = 0.125f;
        private const float TransitionFrame = 0.0625f;
        private const float SlowReverseFrame = 0.125f;

        private static readonly CursorStateId _default = new("Default");
        private static readonly CursorStateId _grab = new("Grab");
        private static readonly CursorStateId _dragging = new("Dragging");
        private static readonly CursorStateId _busy = new("Busy");

        private TestCursorSetBuilder _builder;
        private RecordingCursorOutput _output;
        private CursorPlayer _player;
        private List<CursorStateId> _entered;

        [SetUp]
        public void SetUp()
        {
            _builder = new();
            _output = new();
            _player = new(_output);
            _entered = new(64);
            _player.StateEntered += _entered.Add;
        }

        [TearDown]
        public void TearDown()
        {
            _builder.Dispose();
        }

        [Test]
        public void SetSet_ShowsFirstLoopFrameOfFirstState()
        {
            _player.SetSet(CreateStandardSet());

            Assert.That(_output.Last, Is.EqualTo(FrameName("Default", 0)));
            Assert.That(_player.CurrentState, Is.EqualTo(_default));
            Assert.That(_entered, Is.EqualTo(new[] { _default }));
        }

        [Test]
        public void SetSet_UsesStateRequestedBeforehand()
        {
            _player.SetState(_grab);
            _player.SetSet(CreateStandardSet());

            Assert.That(_output.Last, Is.EqualTo(FrameName("Grab", 0)));
            Assert.That(_player.CurrentState, Is.EqualTo(_grab));
        }

        [Test]
        public void SetSet_KeepsStateWhenSwitchingSets()
        {
            _player.SetSet(CreateStandardSet("First"));
            _player.SetState(_grab, true);
            _player.SetSet(CreateStandardSet("Second"));

            Assert.That(_player.CurrentState, Is.EqualTo(_grab));
            Assert.That(_player.IsTransitioning, Is.False);
        }

        [Test]
        public void SetSet_FallsBackToFirstStateWhenStateIsMissing()
        {
            _player.SetSet(CreateStandardSet());
            _player.SetState(_busy);
            var other = _builder.AddState("Default", 1, LoopFrame).AddState("Grab", 1, LoopFrame).Build("Other");

            _player.SetSet(other);

            Assert.That(_player.CurrentState, Is.EqualTo(_default));
            Assert.That(_player.BaseState, Is.EqualTo(_busy));
        }

        [Test]
        public void SetSet_Null_ClearsOutput()
        {
            _player.SetSet(CreateStandardSet());
            _player.SetSet(null);

            Assert.That(_output.ClearCount, Is.EqualTo(1));
            Assert.That(_player.CurrentState.IsValid, Is.False);
        }

        [Test]
        public void Loop_AdvancesEveryFrameDuration()
        {
            _player.SetSet(CreateStandardSet());

            _player.Tick(LoopFrame / 2f);
            Assert.That(_output.Last, Is.EqualTo(FrameName("Default", 0)));

            _player.Tick(LoopFrame / 2f);
            Assert.That(_output.Last, Is.EqualTo(FrameName("Default", 1)));

            _player.Tick(LoopFrame * 2f);
            Assert.That(_output.Last, Is.EqualTo(FrameName("Default", 3)));

            _player.Tick(LoopFrame);
            Assert.That(_output.Last, Is.EqualTo(FrameName("Default", 0)));
        }

        [Test]
        public void Loop_WaitsForLoopDelayOnFirstFrame()
        {
            var set = _builder.AddState("Default", 4, LoopFrame, 0.5f).Build();
            _player.SetSet(set);

            _player.Tick(0.25f);
            Assert.That(_player.FrameIndex, Is.EqualTo(0));

            _player.Tick(0.25f);
            Assert.That(_player.FrameIndex, Is.EqualTo(0));

            _player.Tick(LoopFrame);
            Assert.That(_player.FrameIndex, Is.EqualTo(1));
        }

        [Test]
        public void Loop_SingleFrameStateNeverReapplies()
        {
            _player.SetSet(CreateStandardSet());
            _player.SetState(_busy);
            var count = _output.ApplyCount;

            _player.Tick(10f);

            Assert.That(_output.ApplyCount, Is.EqualTo(count));
        }

        [Test]
        public void Idle_Disabled_HoldsFirstFrame()
        {
            _player.SetSet(CreateStandardSet());
            _player.Tick(LoopFrame * 2f);

            _player.IdleEnabled = false;
            Assert.That(_output.Last, Is.EqualTo(FrameName("Default", 0)));

            _player.Tick(LoopFrame * 3f);
            Assert.That(_output.Last, Is.EqualTo(FrameName("Default", 0)));

            _player.IdleEnabled = true;
            _player.Tick(LoopFrame);
            Assert.That(_output.Last, Is.EqualTo(FrameName("Default", 1)));
        }

        [Test]
        public void Idle_SuppressedUntilEveryTokenIsReleased()
        {
            _player.SetSet(CreateStandardSet());
            object aiming = new();
            object dialog = new();

            _player.SuppressIdle(aiming);
            _player.SuppressIdle(dialog);
            _player.Tick(LoopFrame * 2f);
            Assert.That(_player.FrameIndex, Is.EqualTo(0));

            _player.ReleaseIdle(aiming);
            _player.Tick(LoopFrame * 2f);
            Assert.That(_player.FrameIndex, Is.EqualTo(0));

            _player.ReleaseIdle(dialog);
            _player.Tick(LoopFrame);
            Assert.That(_player.FrameIndex, Is.EqualTo(1));
        }

        [Test]
        public void Idle_SuppressingTwiceWithSameTokenNeedsOneRelease()
        {
            _player.SetSet(CreateStandardSet());
            object token = new();

            _player.SuppressIdle(token);
            _player.SuppressIdle(token);
            _player.ReleaseIdle(token);

            Assert.That(_player.IsIdleSuppressed, Is.False);
        }

        [Test]
        public void SetState_ShowsFirstInBetweenWithoutTicking()
        {
            _player.SetSet(CreateStandardSet());

            _player.SetState(_grab);

            Assert.That(_output.Last, Is.EqualTo(FrameName("Default>Grab", 1)));
            Assert.That(_player.IsTransitioning, Is.True);
            Assert.That(_player.CurrentState, Is.EqualTo(_default));
            Assert.That(_player.TargetState, Is.EqualTo(_grab));
        }

        [Test]
        public void SetState_SameStateDoesNothing()
        {
            _player.SetSet(CreateStandardSet());
            var count = _output.ApplyCount;

            _player.SetState(_default);

            Assert.That(_output.ApplyCount, Is.EqualTo(count));
            Assert.That(_player.IsTransitioning, Is.False);
        }

        [Test]
        public void SetState_UnknownStateIsIgnored()
        {
            _player.SetSet(CreateStandardSet());
            LogAssert.Expect(LogType.Warning, new Regex("no state 'Unknown'"));

            _player.SetState("Unknown");

            Assert.That(_player.CurrentState, Is.EqualTo(_default));
            Assert.That(_player.IsTransitioning, Is.False);
        }

        [Test]
        public void Transition_PlaysInBetweensThenEntersDestinationLoop()
        {
            _player.SetSet(CreateStandardSet());
            _entered.Clear();
            _player.SetState(_grab);

            _player.Tick(TransitionFrame);
            Assert.That(_output.Last, Is.EqualTo(FrameName("Default>Grab", 2)));
            _player.Tick(TransitionFrame);
            _player.Tick(TransitionFrame);
            Assert.That(_output.Last, Is.EqualTo(FrameName("Default>Grab", 4)));
            Assert.That(_entered, Is.Empty);

            _player.Tick(TransitionFrame);
            Assert.That(_output.Last, Is.EqualTo(FrameName("Grab", 0)));
            Assert.That(_player.IsTransitioning, Is.False);
            Assert.That(_player.CurrentState, Is.EqualTo(_grab));
            Assert.That(_entered, Is.EqualTo(new[] { _grab }));
        }

        [Test]
        public void Transition_WithoutEndpointsPlaysEveryFrame()
        {
            var set = _builder
                .AddState("Default", 1, LoopFrame)
                .AddState("Grab", 1, LoopFrame)
                .AddTransition("Default", "Grab", 3, TransitionFrame, false)
                .Build();
            _player.SetSet(set);

            _player.SetState(_grab);
            Assert.That(_output.Last, Is.EqualTo(FrameName("Default>Grab", 0)));

            _player.Tick(TransitionFrame * 2f);
            Assert.That(_output.Last, Is.EqualTo(FrameName("Default>Grab", 2)));

            _player.Tick(TransitionFrame);
            Assert.That(_output.Last, Is.EqualTo(FrameName("Grab", 0)));
        }

        [Test]
        public void Transition_CarriesLeftoverTimeIntoDestinationLoop()
        {
            _player.SetSet(CreateStandardSet());
            _player.SetState(_grab);

            _player.Tick(TransitionFrame * 4f + LoopFrame);

            Assert.That(_output.Last, Is.EqualTo(FrameName("Grab", 1)));
        }

        [Test]
        public void Transition_OppositeDirectionPlaysAuthoredClipBackwards()
        {
            _player.SetSet(CreateStandardSet());
            _player.SetState(_grab, true);
            _player.SetState(_grab, true);
            Assert.That(_player.CurrentState, Is.EqualTo(_grab));

            _player.SetState(_default);
            Assert.That(_output.Last, Is.EqualTo(FrameName("Default>Grab", 4)));

            _player.Tick(TransitionFrame);
            Assert.That(_output.Last, Is.EqualTo(FrameName("Default>Grab", 3)));

            _player.Tick(TransitionFrame * 3f);
            Assert.That(_output.Last, Is.EqualTo(FrameName("Default", 0)));
            Assert.That(_player.CurrentState, Is.EqualTo(_default));
        }

        [Test]
        public void Transition_ReverseUsesItsOwnFrameDuration()
        {
            _player.SetSet(CreateStandardSet());
            EnterImmediately(_grab);
            EnterImmediately(_dragging);

            _player.SetState(_grab);
            Assert.That(_output.Last, Is.EqualTo(FrameName("Grab>Dragging", 4)));

            _player.Tick(TransitionFrame);
            Assert.That(_output.Last, Is.EqualTo(FrameName("Grab>Dragging", 4)));

            _player.Tick(SlowReverseFrame - TransitionFrame);
            Assert.That(_output.Last, Is.EqualTo(FrameName("Grab>Dragging", 3)));
        }

        [Test]
        public void Transition_NonReversibleWithoutOppositeClipSnaps()
        {
            var set = _builder
                .AddState("Default", 1, LoopFrame)
                .AddState("Grab", 1, LoopFrame)
                .AddTransition("Default", "Grab", 6, TransitionFrame, reversible: false)
                .Build();
            _player.SetSet(set);
            EnterImmediately(_grab);

            _player.SetState(_default);

            Assert.That(_output.Last, Is.EqualTo(FrameName("Default", 0)));
            Assert.That(_player.IsTransitioning, Is.False);
        }

        [Test]
        public void Transition_MissingClipSnapsToState()
        {
            _player.SetSet(CreateStandardSet());

            _player.SetState(_busy);

            Assert.That(_output.Last, Is.EqualTo(FrameName("Busy", 0)));
            Assert.That(_player.CurrentState, Is.EqualTo(_busy));
            Assert.That(_player.IsTransitioning, Is.False);
        }

        [Test]
        public void Reversal_ContinuesBackwardsFromCurrentFrame()
        {
            _player.SetSet(CreateStandardSet());
            _entered.Clear();
            _player.SetState(_grab);
            _player.Tick(TransitionFrame * 2f);
            Assert.That(_output.Last, Is.EqualTo(FrameName("Default>Grab", 3)));

            _player.SetState(_default);
            Assert.That(_output.Last, Is.EqualTo(FrameName("Default>Grab", 3)));

            _player.Tick(TransitionFrame);
            Assert.That(_output.Last, Is.EqualTo(FrameName("Default>Grab", 2)));

            _player.Tick(TransitionFrame * 2f);
            Assert.That(_output.Last, Is.EqualTo(FrameName("Default", 0)));
            Assert.That(_entered, Is.EqualTo(new[] { _default }));
        }

        [Test]
        public void Reversal_ImmediateStepsAtOnce()
        {
            _player.SetSet(CreateStandardSet());
            _player.SetState(_grab);
            _player.Tick(TransitionFrame * 2f);

            _player.SetState(_default, true);

            Assert.That(_output.Last, Is.EqualTo(FrameName("Default>Grab", 2)));
        }

        [Test]
        public void Reversal_CanTurnAroundRepeatedly()
        {
            _player.SetSet(CreateStandardSet());
            _player.SetState(_grab);
            _player.Tick(TransitionFrame);
            _player.SetState(_default);
            _player.SetState(_grab);
            _player.Tick(TransitionFrame);

            Assert.That(_output.Last, Is.EqualTo(FrameName("Default>Grab", 3)));
            Assert.That(_player.TargetState, Is.EqualTo(_grab));
        }

        [Test]
        public void Reversal_NonReversibleFinishesThenPlaysOppositeClip()
        {
            var set = _builder
                .AddState("Default", 1, LoopFrame)
                .AddState("Grab", 1, LoopFrame)
                .AddTransition("Default", "Grab", 6, TransitionFrame, reversible: false)
                .AddTransition("Grab", "Default", 6, TransitionFrame, reversible: false)
                .Build();
            _player.SetSet(set);
            _player.SetState(_grab);
            _player.Tick(TransitionFrame);

            _player.SetState(_default);
            Assert.That(_output.Last, Is.EqualTo(FrameName("Default>Grab", 2)));

            _player.Tick(TransitionFrame * 3f);
            Assert.That(_output.Last, Is.EqualTo(FrameName("Grab>Default", 1)));
            Assert.That(_player.CurrentState, Is.EqualTo(_grab));
            Assert.That(_player.TargetState, Is.EqualTo(_default));
        }

        [Test]
        public void ThirdState_WaitsForCurrentTransitionToFinish()
        {
            _player.SetSet(CreateStandardSet());
            _entered.Clear();
            _player.SetState(_grab);
            _player.Tick(TransitionFrame);

            _player.SetState(_dragging);
            Assert.That(_output.Last, Is.EqualTo(FrameName("Default>Grab", 2)));
            Assert.That(_player.TargetState, Is.EqualTo(_dragging));

            _player.Tick(TransitionFrame * 3f);
            Assert.That(_output.Last, Is.EqualTo(FrameName("Grab>Dragging", 1)));
            Assert.That(_entered, Is.EqualTo(new[] { _grab }));

            _player.Tick(TransitionFrame * 4f);
            Assert.That(_output.Last, Is.EqualTo(FrameName("Dragging", 0)));
            Assert.That(_entered, Is.EqualTo(new[] { _grab, _dragging }));
        }

        [Test]
        public void ThirdState_ImmediateSkipsToDestinationAndStartsNextTransition()
        {
            _player.SetSet(CreateStandardSet());
            _player.SetState(_grab);
            _player.Tick(TransitionFrame);

            _player.SetState(_dragging, true);

            Assert.That(_output.Last, Is.EqualTo(FrameName("Grab>Dragging", 1)));
            Assert.That(_player.CurrentState, Is.EqualTo(_grab));
            Assert.That(_player.TargetState, Is.EqualTo(_dragging));
        }

        [Test]
        public void ThirdState_RequestingDestinationAgainCancelsQueue()
        {
            _player.SetSet(CreateStandardSet());
            _player.SetState(_grab);
            _player.SetState(_dragging);

            _player.SetState(_grab);
            _player.Tick(TransitionFrame * 4f);

            Assert.That(_output.Last, Is.EqualTo(FrameName("Grab", 0)));
            Assert.That(_player.IsTransitioning, Is.False);
        }

        [Test]
        public void ThirdState_ReversalCancelsQueue()
        {
            _player.SetSet(CreateStandardSet());
            _player.SetState(_grab);
            _player.Tick(TransitionFrame);
            _player.SetState(_dragging);

            _player.SetState(_default);
            _player.Tick(TransitionFrame * 2f);

            Assert.That(_output.Last, Is.EqualTo(FrameName("Default", 0)));
            Assert.That(_player.IsTransitioning, Is.False);
        }

        [Test]
        public void Immediate_ToCurrentDestinationFinishesTransition()
        {
            _player.SetSet(CreateStandardSet());
            _player.SetState(_grab);

            _player.SetState(_grab, true);

            Assert.That(_output.Last, Is.EqualTo(FrameName("Grab", 0)));
            Assert.That(_player.IsTransitioning, Is.False);
        }

        [Test]
        public void Size_PicksSmallestSizeCoveringSystemCursor()
        {
            var set = _builder.WithSizes(32, 48, 64).AddState("Default", 1, LoopFrame).Build();

            Assert.That(set.GetSize(set.FindSizeIndex(32)), Is.EqualTo(32));
            Assert.That(set.GetSize(set.FindSizeIndex(40)), Is.EqualTo(48));
            Assert.That(set.GetSize(set.FindSizeIndex(64)), Is.EqualTo(64));
            Assert.That(set.GetSize(set.FindSizeIndex(96)), Is.EqualTo(64));
            Assert.That(set.GetSize(set.FindSizeIndex(0)), Is.EqualTo(64));
        }

        [Test]
        public void Size_ChangeReappliesCurrentFrameAndHotspot()
        {
            var set = _builder.WithSizes(32, 48).AddState("Default", 2, LoopFrame).Build();
            _player.SetSystemCursorSize(32);
            _player.SetSet(set);
            Assert.That(_output.Last, Is.EqualTo(FrameName("Default", 0)));
            Assert.That(_output.LastHotspot, Is.EqualTo(new Vector2(8f, 8f)));

            _player.SetSystemCursorSize(48);

            Assert.That(_output.Last, Is.EqualTo(FrameName("Default", 0, 48)));
            Assert.That(_output.LastHotspot, Is.EqualTo(new Vector2(12f, 12f)));
            Assert.That(_player.CursorSize, Is.EqualTo(48));
        }

        [Test]
        public void Apply_SkipsUnchangedFrames()
        {
            _player.SetSet(CreateStandardSet());
            var count = _output.ApplyCount;

            _player.Tick(LoopFrame / 4f);
            _player.Tick(LoopFrame / 4f);

            Assert.That(_output.ApplyCount, Is.EqualTo(count));
        }

        [Test]
        public void Refresh_ReappliesCurrentFrame()
        {
            _player.SetSet(CreateStandardSet());
            var count = _output.ApplyCount;

            _player.Refresh();

            Assert.That(_output.ApplyCount, Is.EqualTo(count + 1));
        }

        [Test]
        public void Warm_AppliesEveryFrameThenRestoresCurrent()
        {
            _player.SetSet(CreateStandardSet());
            var count = _output.ApplyCount;

            _player.Warm();

            Assert.That(_output.ApplyCount, Is.EqualTo(count + 4 + 4 + 4 + 1 + 6 + 6 + 1));
            Assert.That(_output.Last, Is.EqualTo(FrameName("Default", 0)));
        }

        [Test]
        public void Tick_HugeDeltaTerminates()
        {
            _player.SetSet(CreateStandardSet());
            _player.SetState(_grab);

            _player.Tick(100000f);

            Assert.That(_player.CurrentState, Is.EqualTo(_grab));
        }

        [Test]
        public void Tick_BeforeSetDoesNothing()
        {
            _player.Tick(1f);

            Assert.That(_output.ApplyCount, Is.EqualTo(0));
        }

        [Test]
        public void Playback_DoesNotAllocate()
        {
            _player.SetSet(CreateStandardSet());
            _player.Tick(LoopFrame);
            _player.SetState(_grab);
            _player.Tick(TransitionFrame);
            _player.SetState(_default);
            _player.Tick(1f);

            Assert.That(() =>
            {
                _player.Tick(LoopFrame);
                _player.SetState(_grab);
                _player.Tick(TransitionFrame);
                _player.SetState(_default);
                _player.Tick(TransitionFrame);
                _player.SetState(_grab, true);
                _player.Tick(1f);
                _player.SetState(_default);
                _player.Tick(1f);
            }, Is.Not.AllocatingGCMemory());
        }

        private CursorSet CreateStandardSet(string name = "TestSet")
        {
            return _builder
                .AddState("Default", 4, LoopFrame)
                .AddState("Grab", 4, LoopFrame)
                .AddState("Dragging", 4, LoopFrame)
                .AddState("Busy", 1, LoopFrame)
                .AddTransition("Default", "Grab", 6, TransitionFrame)
                .AddTransition("Grab", "Dragging", 6, TransitionFrame, reverseFrameDuration: SlowReverseFrame)
                .Build(name);
        }

        private void EnterImmediately(CursorStateId state)
        {
            _player.SetState(state, true);
            _player.SetState(state, true);
        }
    }
}
