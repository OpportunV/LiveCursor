using UnityEngine;

namespace Opportunv.LiveCursor
{
    public sealed class CursorStateNameAttribute : PropertyAttribute
    {
        public bool AllowNone { get; }

        public CursorStateNameAttribute(bool allowNone = false)
        {
            AllowNone = allowNone;
        }
    }
}
