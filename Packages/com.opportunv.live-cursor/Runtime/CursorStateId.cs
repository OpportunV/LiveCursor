using System;

namespace Opportunv.LiveCursor
{
    public readonly struct CursorStateId : IEquatable<CursorStateId>
    {
        public int Hash => _hash;

        public string Name => _name ?? string.Empty;

        public bool IsValid => _hash != 0;

        private const uint FnvOffsetBasis = 2166136261;
        private const uint FnvPrime = 16777619;

        private readonly int _hash;
        private readonly string _name;

        public CursorStateId(string name)
        {
            _name = name;
            _hash = ComputeHash(name);
        }

        public bool Equals(CursorStateId other)
        {
            return _hash == other._hash;
        }

        public override bool Equals(object obj)
        {
            return obj is CursorStateId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _hash;
        }

        public override string ToString()
        {
            return Name;
        }

        public static bool operator ==(CursorStateId left, CursorStateId right)
        {
            return left._hash == right._hash;
        }

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