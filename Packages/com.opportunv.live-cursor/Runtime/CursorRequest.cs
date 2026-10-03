using System;

namespace Opportunv.LiveCursor
{
    public readonly struct CursorRequest : IDisposable
    {
        public bool IsActive => _player != null && _player.IsRequestActive(_id);

        private readonly CursorPlayer _player;
        private readonly int _id;

        internal CursorRequest(CursorPlayer player, int id)
        {
            _player = player;
            _id = id;
        }

        public void Release(bool immediate = false)
        {
            _player?.Release(_id, immediate);
        }

        public void Dispose()
        {
            Release();
        }
    }
}
