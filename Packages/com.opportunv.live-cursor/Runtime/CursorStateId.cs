using System;

namespace Opportunv.LiveCursor
{
    /// <summary>Identifies a cursor state by name. Create one per state and reuse it, for example as a static readonly
    /// field; generated state constants provide these for you.</summary>
    public readonly struct CursorStateId : IEquatable<CursorStateId>
    {
        /// <summary>A hash of the name, used for comparisons.</summary>
        public int Hash => _hash;

        /// <summary>The state name.</summary>
        public string Name => _name ?? string.Empty;

        /// <summary>Whether the id was created from a non-empty name.</summary>
        public bool IsValid => _hash != 0;

        private const uint FnvOffsetBasis = 2166136261;
        private const uint FnvPrime = 16777619;

        private readonly int _hash;
        private readonly string _name;

        /// <summary>Creates an id for the state named <paramref name="name"/>.</summary>
        public CursorStateId(string name)
        {
            _name = name;
            _hash = ComputeHash(name);
        }

        /// <summary>Whether both ids name the same state.</summary>
        public bool Equals(CursorStateId other)
        {
            return _hash == other._hash;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is CursorStateId other && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return _hash;
        }

        /// <summary>Returns the state name.</summary>
        public override string ToString()
        {
            return Name;
        }

        /// <summary>Whether both ids name the same state.</summary>
        public static bool operator ==(CursorStateId left, CursorStateId right)
        {
            return left._hash == right._hash;
        }

        /// <summary>Whether the ids name different states.</summary>
        public static bool operator !=(CursorStateId left, CursorStateId right)
        {
            return left._hash != right._hash;
        }

        private static int ComputeHash(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return 0;
            }

            var hash = FnvOffsetBasis;
            foreach (var chr in name)
            {
                hash ^= chr;
                hash *= FnvPrime;
            }

            return hash == 0 ? 1 : unchecked((int)hash);
        }
    }
}