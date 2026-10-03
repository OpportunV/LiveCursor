using UnityEngine;

namespace Opportunv.LiveCursor
{
    /// <summary>Shows a <see cref="string"/> field in the Inspector as a dropdown of the state names found in the
    /// project's cursor sets.</summary>
    public sealed class CursorStateNameAttribute : PropertyAttribute
    {
        /// <summary>Whether the dropdown offers "None", stored as an empty string.</summary>
        public bool AllowNone { get; }

        /// <summary>Marks the field; pass <c>true</c> to allow "None".</summary>
        public CursorStateNameAttribute(bool allowNone = false)
        {
            AllowNone = allowNone;
        }
    }
}
