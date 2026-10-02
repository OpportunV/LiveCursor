using System;
using System.Collections.Generic;

namespace Opportunv.LiveCursor.Editor
{
    internal sealed class NaturalStringComparer : IComparer<string>
    {
        public static NaturalStringComparer Instance { get; } = new();

        public int Compare(string left, string right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            var i = 0;
            var j = 0;
            while (i < left.Length && j < right.Length)
            {
                if (char.IsDigit(left[i]) && char.IsDigit(right[j]))
                {
                    var leftStart = i;
                    var rightStart = j;
                    while (i < left.Length && char.IsDigit(left[i]))
                    {
                        i++;
                    }

                    while (j < right.Length && char.IsDigit(right[j]))
                    {
                        j++;
                    }

                    var leftNumber = left.Substring(leftStart, i - leftStart).TrimStart('0');
                    var rightNumber = right.Substring(rightStart, j - rightStart).TrimStart('0');
                    if (leftNumber.Length != rightNumber.Length)
                    {
                        return leftNumber.Length.CompareTo(rightNumber.Length);
                    }

                    var numberComparison = string.CompareOrdinal(leftNumber, rightNumber);
                    if (numberComparison != 0)
                    {
                        return numberComparison;
                    }

                    continue;
                }

                var charComparison = char.ToUpperInvariant(left[i]).CompareTo(char.ToUpperInvariant(right[j]));
                if (charComparison != 0)
                {
                    return charComparison;
                }

                i++;
                j++;
            }

            var lengthComparison = (left.Length - i).CompareTo(right.Length - j);
            return lengthComparison != 0 ? lengthComparison : string.Compare(left, right, StringComparison.Ordinal);
        }
    }
}
