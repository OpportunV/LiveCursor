using System;

namespace Opportunv.LiveCursor
{
    /// <summary>A handle to a state request made with <see cref="CursorPlayer.Request(CursorStateId, int, bool)"/>.
    /// Dispose it, or call <see cref="Release"/>, to remove the request.</summary>
    public readonly struct CursorRequest : IDisposable
    {
        /// <summary>Gets a value indicating whether the request is still active.</summary>
        public bool IsActive => _player != null && _player.IsRequestActive(_id);

        private readonly CursorPlayer _player;
        private readonly int _id;

        internal CursorRequest(CursorPlayer player, int id)
        {
            _player = player;
            _id = id;
        }

        /// <summary>Removes the request. Pass <paramref name="immediate"/> for a click-driven change, so the first
        /// frame shows at once. Releasing an inactive or default handle does nothing.</summary>
        public void Release(bool immediate = false)
        {
            _player?.Release(_id, immediate);
        }

        /// <summary>Removes the request.</summary>
        public void Dispose()
        {
            Release();
        }
    }
}
