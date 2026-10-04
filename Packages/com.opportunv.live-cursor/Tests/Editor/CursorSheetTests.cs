using System.IO;
using NUnit.Framework;
using Opportunv.LiveCursor.Editor;
using UnityEngine;

namespace Opportunv.LiveCursor.Tests.Editor
{
    public sealed class CursorSheetTests
    {
        private const string Root = "Temp/LiveCursorSheetTests";
        private const string Folder = Root + "/Set";
        private const int Cell = 8;

        private static readonly Color32 _red = new(255, 0, 0, 255);
        private static readonly Color32 _green = new(0, 255, 0, 255);
        private static readonly Color32 _blue = new(0, 0, 255, 255);
        private static readonly Color32 _clear = new(0, 0, 0, 0);

        [SetUp]
        public void SetUp()
        {
            DeleteRoot();
            WriteSheet("Idle/Default/Frame_0.png", 1, 1, _red);
            WriteSheet("Idle/Default/Frame_1.png", 1, 1, _red);
            WriteSheet("Grab_2x2_3.png", 2, 2, _blue, _blue, _blue, _clear);
            WriteSheet("Default_to_Grab_3x1.png", 3, 1, _red, _green, _blue);
        }

        [TearDown]
        public void TearDown()
        {
            DeleteRoot();
        }

        [TestCase("Grab_4x2", "Grab", 4, 2, 0)]
        [TestCase("Grab_4x2_7", "Grab", 4, 2, 7)]
        [TestCase("Grab-4X2", "Grab", 4, 2, 0)]
        [TestCase("Grab4x2", "Grab", 4, 2, 0)]
        [TestCase("Default_to_Grab 6x1", "Default_to_Grab", 6, 1, 0)]
        public void TryParseFileName_ReadsLayoutSuffix(
            string fileName,
            string expectedName,
            int columns,
            int rows,
            int count)
        {
            var parsed = CursorSheetLayout.TryParseFileName(fileName, out var name, out var layout);

            Assert.That(parsed, Is.True);
            Assert.That(name, Is.EqualTo(expectedName));
            Assert.That(layout, Is.EqualTo(new CursorSheetLayout(columns, rows, count)));
        }

        [TestCase("Frame_000")]
        [TestCase("Grab")]
        [TestCase("Grab_1x1")]
        [TestCase("4x2")]
        public void TryParseFileName_RejectsOtherNames(string fileName)
        {
            Assert.That(CursorSheetLayout.TryParseFileName(fileName, out _, out _), Is.False);
        }

        [TestCase("4x2", 4, 2, 0)]
        [TestCase(" 4 x 2 : 7 ", 4, 2, 7)]
        [TestCase("4X2/7", 4, 2, 7)]
        public void TryParse_ReadsTypedLayout(string text, int columns, int rows, int count)
        {
            Assert.That(CursorSheetLayout.TryParse(text, out var layout), Is.True);
            Assert.That(layout, Is.EqualTo(new CursorSheetLayout(columns, rows, count)));
        }

        [Test]
        public void ToString_RoundTrips()
        {
            Assert.That(new CursorSheetLayout(4, 2, 7).ToString(), Is.EqualTo("4x2:7"));
            Assert.That(new CursorSheetLayout(4, 2, 8).ToString(), Is.EqualTo("4x2"));
            Assert.That(default(CursorSheetLayout).ToString(), Is.EqualTo("1"));
        }

        [Test]
        public void CellRect_CountsRowsFromTheTop()
        {
            CursorSheetLayout layout = new(2, 2, 0);

            Assert.That(layout.CellRect(0, 16, 16), Is.EqualTo(new RectInt(0, 8, 8, 8)));
            Assert.That(layout.CellRect(3, 16, 16), Is.EqualTo(new RectInt(8, 0, 8, 8)));
        }

        [Test]
        public void Fits_RejectsUnevenOrTinyCells()
        {
            Assert.That(new CursorSheetLayout(3, 1, 0).Fits(16, 8), Is.False);
            Assert.That(new CursorSheetLayout(4, 4, 0).Fits(16, 16), Is.False);
            Assert.That(new CursorSheetLayout(2, 2, 0).Fits(16, 16), Is.True);
        }

        [Test]
        public void Scan_DetectsSheetsByName()
        {
            var clips = CursorFolderScanner.Scan(Folder);

            var grab = clips.Find(clip => clip.Name == "Grab");
            Assert.That(grab, Is.Not.Null);
            Assert.That(grab.IsSheet, Is.True);
            Assert.That(grab.FrameCount, Is.EqualTo(3));
            Assert.That(grab.FrameWidth, Is.EqualTo(Cell));
            Assert.That(clips.Exists(clip => clip.Name == "Default_to_Grab"), Is.True);
        }

        [Test]
        public void FromFolder_ClassifiesSheetsAndDetectsEndpoints()
        {
            var model = CursorBuilderModel.FromFolder(Folder);

            Assert.That(model.IncludedStateNames(), Is.EquivalentTo(new[] { "Default", "Grab" }));
            Assert.That(model.Transitions.Count, Is.EqualTo(1));
            Assert.That(model.Transitions[0].Clip.FrameCount, Is.EqualTo(3));
            Assert.That(model.Transitions[0].IncludesEndpoints, Is.True);
        }

        [Test]
        public void ToDefinition_WritesSheetLayout()
        {
            var model = CursorBuilderModel.FromFolder(Folder);

            var grab = System.Array.Find(model.ToDefinition().states, state => state.name == "Grab");

            Assert.That(grab.frames.sheet, Is.EqualTo("Grab_2x2_3.png"));
            Assert.That(grab.frames.columns, Is.EqualTo(2));
            Assert.That(grab.frames.rows, Is.EqualTo(2));
            Assert.That(grab.frames.count, Is.EqualTo(3));
        }

        [Test]
        public void SetSheet_TurnsSheetOffAndRedetectsEndpoints()
        {
            var model = CursorBuilderModel.FromFolder(Folder);
            var transition = model.Transitions[0];

            model.SetSheet(transition.Clip, default);

            var frames = model.ToDefinition().transitions[0].frames;
            Assert.That(transition.IncludesEndpoints, Is.False);
            Assert.That(frames.sheet, Is.Null.Or.Empty);
            Assert.That(frames.files, Is.EqualTo(new[] { "Default_to_Grab_3x1.png" }));
        }

        [Test]
        public void DefinitionFile_RoundTripsEditedLayout()
        {
            var model = CursorBuilderModel.FromFolder(Folder);
            var grab = model.States.Find(state => state.Name == "Grab");
            model.SetSheet(grab.Clip, new(2, 2, 0));
            File.WriteAllText(model.OutputPath, CursorSetDefinitionWriter.Write(model.ToDefinition()));

            var loaded = CursorBuilderModel.FromDefinitionFile(model.OutputPath);

            var loadedGrab = loaded.States.Find(state => state.Name == "Grab");
            Assert.That(loadedGrab.Include, Is.True);
            Assert.That(loadedGrab.Clip.Sheet, Is.EqualTo(new CursorSheetLayout(2, 2, 0)));
            Assert.That(loadedGrab.Clip.FrameCount, Is.EqualTo(4));
        }

        [Test]
        public void Validate_UsesCellSizeForHotspot()
        {
            var model = CursorBuilderModel.FromFolder(Folder);
            model.Hotspot = new(10, 0);
            CursorImportReport report = new();

            model.Validate(report);

            Assert.That(report.Errors, Has.Some.Contains("outside the 8x8 canvas"));
        }

        [Test]
        public void FrameReader_ReadsSheetCells()
        {
            var clip = CursorFolderScanner.Scan(Folder).Find(candidate => candidate.Name == "Default_to_Grab");

            var middle = CursorClipFrameReader.Read(clip, 1);

            try
            {
                Assert.That(middle.width, Is.EqualTo(Cell));
                Assert.That((Color32)middle.GetPixel(0, 0), Is.EqualTo(_green));
            }
            finally
            {
                Object.DestroyImmediate(middle);
            }
        }

        private static void WriteSheet(string relativePath, int columns, int rows, params Color32[] cells)
        {
            var path = $"{Folder}/{relativePath}";
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Folder);
            var width = columns * Cell;
            var height = rows * Cell;
            Texture2D texture = new(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var row = rows - 1 - y / Cell;
                    pixels[y * width + x] = cells[row * columns + x / Cell];
                }
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
