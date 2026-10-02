using System.Collections.Generic;
using UnityEngine;

namespace Opportunv.LiveCursor.Editor
{
    internal interface ICursorFrameLoader
    {
        public bool TryLoad(CursorFramesDefinition frames, string clipLabel, List<Texture2D> output,
            CursorImportReport report);
    }
}
