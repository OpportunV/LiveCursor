using System.IO;
using UnityEditor;
using UnityEngine;

namespace Dev.Editor
{
    internal static class SampleExporter
    {
        private const string Source = "Assets/LiveCursorSamples/Demo";
        private const string Destination = "Packages/com.opportunv.live-cursor/Samples~/Demo";

        [MenuItem("Live Cursor Dev/Samples/Export Demo To Package")]
        private static void Export()
        {
            AssetDatabase.SaveAssets();
            if (Directory.Exists(Destination))
            {
                Directory.Delete(Destination, true);
            }

            CopyDirectory(Source, Destination);
            Debug.Log($"[Live Cursor Dev] Copied '{Source}' to '{Destination}'.");
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
            }

            foreach (var directory in Directory.GetDirectories(source))
            {
                CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
            }
        }
    }
}
