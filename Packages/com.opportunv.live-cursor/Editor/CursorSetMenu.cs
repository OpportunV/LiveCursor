using UnityEditor;

namespace Opportunv.LiveCursor.Editor
{
    internal static class CursorSetMenu
    {
        private const string Template = @"{
  ""sizes"": [32, 48, 64],
  ""hotspot"": [0, 0],
  ""states"": [
    {
      ""name"": ""Default"",
      ""frames"": { ""folder"": ""Default"" },
      ""frameDurationMs"": 100
    }
  ],
  ""transitions"": []
}
";

        [MenuItem("Assets/Create/Live Cursor/Cursor Set", priority = 200)]
        private static void CreateCursorSet()
        {
            ProjectWindowUtil.CreateAssetWithContent($"New Cursor Set.{CursorSetImporter.Extension}", Template);
        }
    }
}
