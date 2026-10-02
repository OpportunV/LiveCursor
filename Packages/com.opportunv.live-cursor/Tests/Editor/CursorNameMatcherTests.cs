using NUnit.Framework;
using Opportunv.LiveCursor.Editor;

namespace Opportunv.LiveCursor.Tests.Editor
{
    public sealed class CursorNameMatcherTests
    {
        private static readonly string[] _states = { "Default", "Grab", "Dragging", "Blocked" };

        [TestCase("DefaultToGrab", "Default", "Grab")]
        [TestCase("Default_to_Grab", "Default", "Grab")]
        [TestCase("default-to-grab", "Default", "Grab")]
        [TestCase("Default To Grab", "Default", "Grab")]
        [TestCase("FromDefaultToGrab", "Default", "Grab")]
        [TestCase("Default2Grab", "Default", "Grab")]
        [TestCase("Default-Grab", "Default", "Grab")]
        [TestCase("Default_Grab", "Default", "Grab")]
        [TestCase("Default -> Grab", "Default", "Grab")]
        [TestCase("GrabToDragging", "Grab", "Dragging")]
        public void Matches(string clipName, string expectedFrom, string expectedTo)
        {
            var matched = CursorNameMatcher.TryMatchTransition(clipName, _states, out var from, out var to);

            Assert.That(matched, Is.True);
            Assert.That(from, Is.EqualTo(expectedFrom));
            Assert.That(to, Is.EqualTo(expectedTo));
        }

        [TestCase("DefaultGrab")]
        [TestCase("Default")]
        [TestCase("Tooltip")]
        [TestCase("DefaultToDefault")]
        [TestCase("DefaultToHover")]
        public void DoesNotMatch(string clipName)
        {
            Assert.That(CursorNameMatcher.TryMatchTransition(clipName, _states, out _, out _), Is.False);
        }
    }
}
