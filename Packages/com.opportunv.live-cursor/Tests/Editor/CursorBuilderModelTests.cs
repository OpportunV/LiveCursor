using System.IO;
using NUnit.Framework;
using Opportunv.LiveCursor.Editor;
using UnityEngine;

namespace Opportunv.LiveCursor.Tests.Editor
{
    public sealed class CursorBuilderModelTests
    {
        private const string Root = "Temp/LiveCursorBuilderTests";
        private const string Folder = Root + "/Set";

        private static readonly Color32 _red = new(255, 0, 0, 255);
        private static readonly Color32 _green = new(0, 255, 0, 255);
        private static readonly Color32 _blue = new(0, 0, 255, 255);

        [SetUp]
        public void SetUp()
        {
            DeleteRoot();
            WritePng("Idle/Default/Frame_0.png", _red);
            WritePng("Idle/Default/Frame_1.png", _red);
            WritePng("Idle/Grab/Frame_0.png", _blue);
            WritePng("Idle/Grab/Frame_1.png", _blue);
            WritePng("Transitions/Default_to_Grab/Frame_0.png", _red);
            WritePng("Transitions/Default_to_Grab/Frame_1.png", _green);
            WritePng("Transitions/Default_to_Grab/Frame_2.png", _blue);
            WritePng("Transitions/GrabToBusy/Frame_0.png", _green);
            WritePng("Transitions/GrabToBusy/Frame_1.png", _green);
            WritePng("Transitions/GrabToBusy/Frame_2.png", _green);
            WritePng("Statics/Busy.png", _green);
            WritePng("Statics/Blocked.png", _green);
        }

        [TearDown]
        public void TearDown()
        {
            DeleteRoot();
        }

        [Test]
        public void FromFolder_ClassifiesStatesAndTransitions()
        {
            var model = CursorBuilderModel.FromFolder(Folder);

            Assert.That(model.IncludedStateNames(), Is.EquivalentTo(new[] { "Default", "Grab", "Busy", "Blocked" }));
            Assert.That(model.Transitions.Count, Is.EqualTo(2));
            var defaultToGrab = model.Transitions.Find(transition => transition.From == "Default");
            Assert.That(defaultToGrab.To, Is.EqualTo("Grab"));
            Assert.That(defaultToGrab.Clip.FramePaths.Count, Is.EqualTo(3));
            Assert.That(model.OutputPath, Is.EqualTo($"{Folder}/Set.cursorset"));
        }

        [Test]
        public void FromFolder_DetectsMatchingEndpoints()
        {
            var model = CursorBuilderModel.FromFolder(Folder);

            var defaultToGrab = model.Transitions.Find(transition => transition.From == "Default");
            var grabToBusy = model.Transitions.Find(transition => transition.From == "Grab");
            Assert.That(defaultToGrab.IncludesEndpoints, Is.True);
            Assert.That(grabToBusy.IncludesEndpoints, Is.False);
        }

        [Test]
        public void FromFolder_UsesDefaultDurations()
        {
            var model = CursorBuilderModel.FromFolder(Folder);

            Assert.That(model.States[0].FrameDurationMs, Is.EqualTo(CursorBuilderModel.DefaultStateFrameMs));
            Assert.That(model.Transitions[0].FrameDurationMs, Is.EqualTo(CursorBuilderModel.DefaultTransitionFrameMs));
        }

        [Test]
        public void ToDefinition_UsesPathsRelativeToOutputFile()
        {
            var model = CursorBuilderModel.FromFolder(Folder);

            var definition = model.ToDefinition();

            var defaultState = System.Array.Find(definition.states, state => state.name == "Default");
            var busyState = System.Array.Find(definition.states, state => state.name == "Busy");
            Assert.That(defaultState.frames.folder, Is.EqualTo("Idle/Default"));
            Assert.That(busyState.frames.files, Is.EqualTo(new[] { "Statics/Busy.png" }));
            Assert.That(definition.code, Is.Null);
        }

        [Test]
        public void ToDefinition_SkipsExcludedRows()
        {
            var model = CursorBuilderModel.FromFolder(Folder);
            model.States.Find(state => state.Name == "Blocked").Include = false;
            model.Transitions[0].Include = false;

            var definition = model.ToDefinition();

            Assert.That(definition.states.Length, Is.EqualTo(3));
            Assert.That(definition.transitions.Length, Is.EqualTo(1));
        }

        [Test]
        public void DefinitionFile_RoundTripsEdits()
        {
            var model = CursorBuilderModel.FromFolder(Folder);
            var grab = model.States.Find(state => state.Name == "Grab");
            grab.Name = "Hover";
            grab.LoopDelayMs = 250f;
            foreach (var transition in model.Transitions)
            {
                transition.From = transition.From == "Grab" ? "Hover" : transition.From;
                transition.To = transition.To == "Grab" ? "Hover" : transition.To;
            }

            model.States.Find(state => state.Name == "Blocked").Include = false;
            model.Hotspot = new(3, 1);
            model.GenerateCode = true;
            model.ClassName = "GameCursors";
            model.Namespace = "Game.UI";
            model.CodePath = $"{Folder}/Code/GameCursors.cs";
            File.WriteAllText(model.OutputPath, CursorSetDefinitionWriter.Write(model.ToDefinition()));

            var loaded = CursorBuilderModel.FromDefinitionFile(model.OutputPath);

            var hover = loaded.States.Find(state => state.Name == "Hover");
            Assert.That(hover, Is.Not.Null);
            Assert.That(hover.Include, Is.True);
            Assert.That(hover.LoopDelayMs, Is.EqualTo(250f));
            Assert.That(loaded.States.Find(state => state.Name == "Blocked").Include, Is.False);
            Assert.That(loaded.Transitions.Find(transition => transition.To == "Hover"), Is.Not.Null);
            Assert.That(loaded.Hotspot, Is.EqualTo(new Vector2Int(3, 1)));
            Assert.That(loaded.GenerateCode, Is.True);
            Assert.That(loaded.ClassName, Is.EqualTo("GameCursors"));
            Assert.That(loaded.Namespace, Is.EqualTo("Game.UI"));
            Assert.That(loaded.CodePath, Is.EqualTo($"{Folder}/Code/GameCursors.cs"));
        }

        [Test]
        public void DefinitionFile_IgnoredWhenNothingMatches()
        {
            var path = $"{Folder}/Set.cursorset";
            File.WriteAllText(path,
                "{ \"states\": [ { \"name\": \"Default\", \"frames\": { \"folder\": \"Nowhere\" } } ] }");

            var model = CursorBuilderModel.FromDefinitionFile(path);

            Assert.That(model.OutputPath, Is.EqualTo(path));
            Assert.That(model.IncludedStateNames().Count, Is.EqualTo(4));
        }

        [Test]
        public void RenameClass_RenamesMatchingFile()
        {
            var model = CursorBuilderModel.FromFolder(Folder);

            model.RenameClass("PrismStates");

            Assert.That(model.ClassName, Is.EqualTo("PrismStates"));
            Assert.That(model.CodePath, Is.EqualTo($"{Folder}/PrismStates.cs"));
        }

        [Test]
        public void RenameClass_KeepsCustomFileName()
        {
            var model = CursorBuilderModel.FromFolder(Folder);
            model.CodePath = $"{Folder}/Cursors.cs";

            model.RenameClass("PrismStates");

            Assert.That(model.CodePath, Is.EqualTo($"{Folder}/Cursors.cs"));
        }

        [Test]
        public void Validate_ReportsHotspotOutsideCanvas()
        {
            var model = CursorBuilderModel.FromFolder(Folder);
            model.Hotspot = new(10, 0);
            CursorImportReport report = new();

            model.Validate(report);

            Assert.That(report.Errors, Has.Some.Contains("outside the 4x4 canvas"));
        }

        [Test]
        public void Writer_OutputParsesBackToSameDefinition()
        {
            var model = CursorBuilderModel.FromFolder(Folder);
            var definition = model.ToDefinition();

            var parsed = JsonUtility.FromJson<CursorSetDefinition>(CursorSetDefinitionWriter.Write(definition));

            Assert.That(parsed.sizes, Is.EqualTo(definition.sizes));
            Assert.That(parsed.states.Length, Is.EqualTo(definition.states.Length));
            Assert.That(parsed.states[0].frames.folder, Is.EqualTo(definition.states[0].frames.folder));
            Assert.That(parsed.transitions[0].includesEndpoints, Is.EqualTo(definition.transitions[0].includesEndpoints));
            Assert.That(parsed.transitions[0].frameDurationMs, Is.EqualTo(definition.transitions[0].frameDurationMs));
        }

        private static void WritePng(string relativePath, Color32 color)
        {
            var path = $"{Folder}/{relativePath}";
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Folder);
            Texture2D texture = new(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color32[16];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        private static void DeleteRoot()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, true);
            }
        }
    }
}
