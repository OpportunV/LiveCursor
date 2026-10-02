using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Dev.Spike.Editor
{
    public static class CursorSpikeBuilder
    {
        public const string OutputRoot = "Assets/Dev/Spike/Generated";
        private const string ArtRoot = "Assets/Dev/Art/Prism/Animations";
        private const string DataPath = "Assets/Dev/Spike/CursorSpikeData.asset";
        private const string ScenePath = "Assets/Dev/Spike/CursorSpike.unity";
        private const string PlayerPath = "Builds/Spike/LiveCursorSpike.exe";
        private const int MasterSize = 128;
        private const int MasterHotspot = 48;
        private const int FrameCount = 16;
        private static readonly int[] _sizes = { 32, 48, 64, 128 };

        private static readonly Color32[] _outlineColors =
        {
            new(255, 64, 64, 255),
            new(64, 255, 64, 255),
            new(64, 160, 255, 255),
            new(255, 220, 0, 255)
        };

        private static readonly (string Name, string Folder, float FrameDuration, bool PingPong)[] _sequences =
        {
            ("Default loop", "Idle/Default", 0.1f, false),
            ("Busy loop", "Idle/Busy", 0.1f, false),
            ("Default<->Grab 50 fps", "Transitions/DefaultToGrab", 0.02f, true),
            ("Grab<->Dragging 50 fps", "Transitions/GrabToDragging", 0.02f, true)
        };

        [MenuItem("Live Cursor Dev/Spike/Generate Assets")]
        public static void GenerateAssets()
        {
            List<string> written = new();
            for (var s = 0; s < _sizes.Length; s++)
            {
                var size = _sizes[s];
                var hotspot = MasterHotspot * size / MasterSize;
                var sizeFolder = $"{OutputRoot}/{size}";
                Directory.CreateDirectory(sizeFolder);

                var gridPath = $"{sizeFolder}/Grid.png";
                File.WriteAllBytes(gridPath, CreateGrid(size, hotspot, _outlineColors[s]).EncodeToPNG());
                written.Add(gridPath);

                if (size == MasterSize)
                {
                    continue;
                }

                foreach (var (_, folder, _, _) in _sequences)
                {
                    var sequenceFolder = $"{sizeFolder}/{folder}";
                    Directory.CreateDirectory(sequenceFolder);
                    for (var f = 0; f < FrameCount; f++)
                    {
                        var master = LoadMaster(folder, f);
                        var scaled = Downscale(master, size);
                        var path = $"{sequenceFolder}/Frame_{f:000}.png";
                        File.WriteAllBytes(path, scaled.EncodeToPNG());
                        Object.DestroyImmediate(scaled);
                        written.Add(path);
                    }
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            CreateData();
            Debug.Log($"Live Cursor spike: generated {written.Count} textures.");
        }

        [MenuItem("Live Cursor Dev/Spike/Create Scene")]
        public static void CreateScene()
        {
            var data = AssetDatabase.LoadAssetAtPath<CursorSpikeData>(DataPath);
            if (data == null)
            {
                GenerateAssets();
                data = AssetDatabase.LoadAssetAtPath<CursorSpikeData>(DataPath);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            GameObject spikeObject = new("Cursor Spike");
            var spike = spikeObject.AddComponent<CursorSpike>();
            SerializedObject serialized = new(spike);
            serialized.FindProperty("_data").objectReferenceValue = data;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        [MenuItem("Live Cursor Dev/Spike/Build Windows Player")]
        public static void BuildPlayer()
        {
            if (!File.Exists(ScenePath))
            {
                CreateScene();
            }

            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1400;
            PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.resizableWindow = true;

            BuildPlayerOptions options = new()
            {
                scenes = new[] { ScenePath },
                locationPathName = PlayerPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log(
                $"Live Cursor spike build: {report.summary.result}, {report.summary.totalErrors} errors, output {Path.GetFullPath(PlayerPath)}");
        }

        private static void CreateData()
        {
            var sizes = new SpikeSize[_sizes.Length];
            for (var s = 0; s < _sizes.Length; s++)
            {
                var size = _sizes[s];
                var hotspot = MasterHotspot * size / MasterSize;
                var sizeFolder = $"{OutputRoot}/{size}";
                var sequences = new SpikeSequence[_sequences.Length];
                for (var q = 0; q < _sequences.Length; q++)
                {
                    var (name, folder, frameDuration, pingPong) = _sequences[q];
                    var frames = new Texture2D[FrameCount];
                    for (var f = 0; f < FrameCount; f++)
                    {
                        frames[f] = size == MasterSize
                            ? LoadMaster(folder, f)
                            : AssetDatabase.LoadAssetAtPath<Texture2D>($"{sizeFolder}/{folder}/Frame_{f:000}.png");
                    }

                    sequences[q] = new(name, frameDuration, pingPong, frames);
                }

                var grid = AssetDatabase.LoadAssetAtPath<Texture2D>($"{sizeFolder}/Grid.png");
                sizes[s] = new(size, new(hotspot, hotspot), grid, sequences);
            }

            var data = AssetDatabase.LoadAssetAtPath<CursorSpikeData>(DataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<CursorSpikeData>();
                AssetDatabase.CreateAsset(data, DataPath);
            }

            data.Initialize(sizes);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }

        private static Texture2D LoadMaster(string folder, int frame)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtRoot}/{folder}/Frame_{frame:000}.png");
        }

        private static Texture2D Downscale(Texture2D source, int size)
        {
            var sourceSize = source.width;
            var input = source.GetPixels32();
            var output = new Color32[size * size];
            var scale = (float)sourceSize / size;

            for (var y = 0; y < size; y++)
            {
                var y0 = y * scale;
                var y1 = y0 + scale;
                for (var x = 0; x < size; x++)
                {
                    var x0 = x * scale;
                    var x1 = x0 + scale;
                    var r = 0f;
                    var g = 0f;
                    var b = 0f;
                    var a = 0f;
                    var weight = 0f;

                    for (var sy = (int)y0; sy < Mathf.CeilToInt(y1) && sy < sourceSize; sy++)
                    {
                        var wy = Mathf.Min(y1, sy + 1) - Mathf.Max(y0, sy);
                        for (var sx = (int)x0; sx < Mathf.CeilToInt(x1) && sx < sourceSize; sx++)
                        {
                            var wx = Mathf.Min(x1, sx + 1) - Mathf.Max(x0, sx);
                            var w = wx * wy;
                            var pixel = input[sy * sourceSize + sx];
                            var alpha = pixel.a / 255f;
                            r += pixel.r * alpha * w;
                            g += pixel.g * alpha * w;
                            b += pixel.b * alpha * w;
                            a += alpha * w;
                            weight += w;
                        }
                    }

                    if (a <= 0f)
                    {
                        output[y * size + x] = new(0, 0, 0, 0);
                        continue;
                    }

                    output[y * size + x] = new(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(r / a), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(g / a), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(b / a), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(a / weight * 255f), 0, 255));
                }
            }

            Texture2D result = new(size, size, TextureFormat.RGBA32, false);
            result.SetPixels32(output);
            result.Apply();
            return result;
        }

        private static Texture2D CreateGrid(int size, int hotspot, Color32 outline)
        {
            var pixels = new Color32[size * size];
            Color32 gridLine = new(255, 255, 255, 90);
            Color32 black = new(0, 0, 0, 255);
            Color32 red = new(255, 0, 0, 255);
            var hotspotRow = size - 1 - hotspot;

            for (var y = 0; y < size; y++)
            {
                var topY = size - 1 - y;
                for (var x = 0; x < size; x++)
                {
                    Color32 color = new(0, 0, 0, 0);
                    if (x % 8 == 0 || topY % 8 == 0)
                    {
                        color = gridLine;
                    }

                    if (x == 0 || topY == 0 || x == size - 1 || topY == size - 1)
                    {
                        color = outline;
                    }

                    var dx = Mathf.Abs(x - hotspot);
                    var dy = Mathf.Abs(y - hotspotRow);
                    if ((dx == 0 && dy >= 2 && dy <= 6) || (dy == 0 && dx >= 2 && dx <= 6))
                    {
                        color = black;
                    }

                    if (dx == 0 && dy == 0)
                    {
                        color = red;
                    }

                    pixels[y * size + x] = color;
                }
            }

            Texture2D texture = new(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}