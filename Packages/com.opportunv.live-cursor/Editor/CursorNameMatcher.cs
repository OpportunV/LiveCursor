using System.Collections.Generic;
using System.Text;

namespace Opportunv.LiveCursor.Editor
{
    internal static class CursorNameMatcher
    {
        private static readonly string[] _joiners = { "to", "2", "" };

        public static string Normalize(string name)
        {
            StringBuilder builder = new(name.Length);
            foreach (var chr in name)
            {
                if (char.IsLetterOrDigit(chr))
                {
                    builder.Append(char.ToLowerInvariant(chr));
                }
            }

            return builder.ToString();
        }

        public static bool TryMatchTransition(string clipName, IReadOnlyList<string> stateNames, out string from,
            out string to)
        {
            from = null;
            to = null;
            var normalized = Normalize(clipName);
            var withoutFrom = normalized.StartsWith("from") ? normalized.Substring(4) : null;
            var hasSeparator = HasSeparator(clipName);

            foreach (var joiner in _joiners)
            {
                if (joiner.Length == 0 && !hasSeparator)
                {
                    continue;
                }

                foreach (var first in stateNames)
                {
                    var firstNormalized = Normalize(first);
                    if (firstNormalized.Length == 0)
                    {
                        continue;
                    }

                    foreach (var second in stateNames)
                    {
                        if (ReferenceEquals(first, second) || first == second)
                        {
                            continue;
                        }

                        var candidate = firstNormalized + joiner + Normalize(second);
                        if (candidate != normalized && candidate != withoutFrom)
                        {
                            continue;
                        }

                        from = first;
                        to = second;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasSeparator(string name)
        {
            foreach (var chr in name)
            {
                if (chr is '-' or '_' or ' ' or '>' or '.')
                {
                    return true;
                }
            }

            return false;
        }
    }
}
