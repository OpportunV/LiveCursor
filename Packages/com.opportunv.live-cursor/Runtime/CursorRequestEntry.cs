namespace Opportunv.LiveCursor
{
    internal readonly struct CursorRequestEntry
    {
        public int Id { get; }

        public CursorStateId State { get; }

        public int Priority { get; }

        public CursorRequestEntry(int id, CursorStateId state, int priority)
        {
            Id = id;
            State = state;
            Priority = priority;
        }
    }
}
