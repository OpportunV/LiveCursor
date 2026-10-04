using NUnit.Framework;
using Opportunv.LiveCursor.Editor;

namespace Opportunv.LiveCursor.Tests.Editor
{
    public sealed class CursorStateCodeGeneratorTests
    {
        [TestCase("Default", "Default")]
        [TestCase("grab hand", "GrabHand")]
        [TestCase("busy-loop", "BusyLoop")]
        [TestCase("2x", "_2x")]
        [TestCase("class", "Class")]
        [TestCase("!!!", "_")]
        public void ToIdentifier_MakesValidIdentifiers(string name, string expected)
        {
            Assert.That(CursorStateCodeGenerator.ToIdentifier(name), Is.EqualTo(expected));
        }

        [Test]
        public void Generate_WritesFieldPerStateInsideNamespace()
        {
            var code = CursorStateCodeGenerator.Generate(
                "CursorStates",
                "Game.UI",
                new[] { "Default", "grab hand" },
                new[] { "Prism.cursorset" });

            Assert.That(code, Does.Contain("namespace Game.UI\n{"));
            Assert.That(code, Does.Contain("    public static class CursorStates\n"));
            Assert.That(
                code,
                Does.Contain("        public static readonly CursorStateId Default = new(\"Default\");\n"));
            Assert.That(
                code,
                Does.Contain("        public static readonly CursorStateId GrabHand = new(\"grab hand\");\n"));
            Assert.That(code, Does.Contain("Prism.cursorset"));
        }

        [Test]
        public void Generate_WithoutNamespaceHasNoIndent()
        {
            var code = CursorStateCodeGenerator.Generate(
                "CursorStates",
                string.Empty,
                new[] { "Default" },
                new[] { "Set.cursorset" });

            Assert.That(code, Does.Not.Contain("namespace"));
            Assert.That(code, Does.Contain("\npublic static class CursorStates\n{\n"));
        }

        [Test]
        public void Generate_DeduplicatesClashingIdentifiers()
        {
            var code = CursorStateCodeGenerator.Generate(
                "CursorStates",
                string.Empty,
                new[] { "grab-hand", "Grab Hand" },
                new[] { "Set.cursorset" });

            Assert.That(code, Does.Contain("GrabHand = new(\"grab-hand\")"));
            Assert.That(code, Does.Contain("GrabHand2 = new(\"Grab Hand\")"));
        }

        [Test]
        public void Generate_ListsAllStates()
        {
            var code = CursorStateCodeGenerator.Generate(
                "CursorStates",
                string.Empty,
                new[] { "Default", "All" },
                new[] { "Set.cursorset" });

            Assert.That(code, Does.Contain("All2 = new(\"All\")"));
            Assert.That(
                code,
                Does.Contain("IReadOnlyList<CursorStateId> All = new[]\n    {\n        Default,\n        All2\n    };\n"));
        }

        [Test]
        public void Generate_RenamesAllListWhenClassIsNamedAll()
        {
            var code = CursorStateCodeGenerator.Generate(
                "All",
                string.Empty,
                new[] { "Default" },
                new[] { "Set.cursorset" });

            Assert.That(
                code,
                Does.Contain("IReadOnlyList<CursorStateId> AllStates = new[]\n    {\n        Default\n    };\n"));
        }

        [Test]
        public void Target_ReportsStatesMissingFromSet()
        {
            CursorStateCodeTarget target = new("Assets/CursorStates.cs", "CursorStates", null);
            target.Add("Prism.cursorset", Definition("Default", "Busy", "Grab"));
            target.Add("Shard.cursorset", Definition("Default", "Grab"));

            Assert.That(target.StateNames, Is.EqualTo(new[] { "Default", "Busy", "Grab" }));
            Assert.That(target.MissingStates(0), Is.Empty);
            Assert.That(target.MissingStates(1), Is.EqualTo(new[] { "Busy" }));
        }

        [TestCase("Game.UI", true)]
        [TestCase("Game..UI", false)]
        [TestCase("1Game", false)]
        [TestCase("class", false)]
        public void IsValidNamespace(string value, bool expected)
        {
            Assert.That(CursorStateCodeGenerator.IsValidNamespace(value), Is.EqualTo(expected));
        }

        private static CursorSetDefinition Definition(params string[] stateNames)
        {
            var states = new CursorStateDefinition[stateNames.Length];
            for (var i = 0; i < stateNames.Length; i++)
            {
                states[i] = new() { name = stateNames[i] };
            }

            return new() { states = states };
        }
    }
}
