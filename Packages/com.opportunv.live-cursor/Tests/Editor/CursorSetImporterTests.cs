using System.IO;
using NUnit.Framework;
using Opportunv.LiveCursor.Editor;
using UnityEditor;
using UnityEngine;

namespace Opportunv.LiveCursor.Tests.Editor
{
    public sealed class CursorSetImporterTests
    {
        private const string Folder = "Assets/LiveCursorImporterTests";
        private const string AssetPath = Folder + "/Test." + CursorSetImporter.Extension;

        private const string Definition = @"{
  ""sizes"": [4, 8],
  ""hotspot"": [2, 2],
  ""states"": [
    { ""name"": ""Default"", ""frames"": { ""folder"": ""Default"" }, ""frameDurationMs"": 100 },
    { ""name"": ""Busy"", ""frames"": { ""sheet"": ""Busy.png"", ""columns"": 2, ""rows"": 1 }, ""frameDurationMs"": 80 }
  ],
  ""transitions"": [
    { ""from"": ""Default"", ""to"": ""Busy"", ""frames"": { ""files"": [""Default/Frame_0.png"", ""Busy/Mid.png""] }, ""frameDurationMs"": 20, ""includesEndpoints"": false }
  ]
}";

        [SetUp]
        public void SetUp()
        {
            Directory.CreateDirectory($"{Folder}/Default");
            Directory.CreateDirectory($"{Folder}/Busy");
            WritePng($"{Folder}/Default/Frame_0.png", 8, 8, new(255, 0, 0, 255));
            WritePng($"{Folder}/Default/Frame_1.png", 8, 8, new(0, 255, 0, 255));
            WritePng($"{Folder}/Busy.png", 16, 8, new(0, 0, 255, 255));
            WritePng($"{Folder}/Busy/Mid.png", 8, 8, new(255, 255, 0, 255));
            File.WriteAllText(AssetPath, Definition);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void Import_CreatesCursorSetWithBakedFrames()
        {
            var set = AssetDatabase.LoadAssetAtPath<CursorSet>(AssetPath);

            Assert.That(set, Is.Not.Null);
            Assert.That(set.StateCount, Is.EqualTo(2));
            Assert.That(set.GetState(0).Loop.FrameCount, Is.EqualTo(2));
            Assert.That(set.GetState(1).Loop.FrameCount, Is.EqualTo(2));
            Assert.That(set.TransitionCount, Is.EqualTo(1));
            Assert.That(set.GetHotspot(0), Is.EqualTo(new Vector2(1f, 1f)));

            var texture = set.GetState(0).Loop.GetFrame(1).GetTexture(0);
            Assert.That(texture.width, Is.EqualTo(4));
            Assert.That(texture.isReadable, Is.True);
            Assert.That(texture.GetPixel(0, 0), Is.EqualTo(Color.green));
        }

        [Test]
        public void Import_StoresTexturesAsSubAssets()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(AssetPath);

            var textures = 0;
            foreach (var asset in assets)
            {
                if (asset is Texture2D)
                {
                    textures++;
                }
            }

            Assert.That(textures, Is.EqualTo((2 + 2 + 2) * 2));
        }

        [Test]
        public void Import_ReimportsWhenFrameChanges()
        {
            WritePng($"{Folder}/Default/Frame_1.png", 8, 8, new(0, 0, 0, 255));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var set = AssetDatabase.LoadAssetAtPath<CursorSet>(AssetPath);
            var texture = set.GetState(0).Loop.GetFrame(1).GetTexture(0);

            Assert.That(texture.GetPixel(0, 0), Is.EqualTo(Color.black));
        }

        private static void WritePng(string path, int width, int height, Color32 color)
        {
            Texture2D texture = new(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
    }
}
