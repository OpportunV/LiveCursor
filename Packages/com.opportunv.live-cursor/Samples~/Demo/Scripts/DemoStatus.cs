namespace Opportunv.LiveCursor.Samples
{
    public static class DemoStatus
    {
        public static string Describe(CursorAnimator cursor)
        {
            if (!cursor || !cursor.CursorSet)
            {
                return "No cursor set.";
            }

            var player = cursor.Player;
            var state = player.IsTransitioning
                ? $"{player.CurrentState.Name} → {player.TargetState.Name}"
                : player.CurrentState.Name;
            return $"Set: {cursor.CursorSet.name}   State: {state}   Base: {player.BaseState.Name}   " +
                   $"Requests: {player.ActiveRequestCount}   Size: {player.CursorSize} px";
        }
    }
}
