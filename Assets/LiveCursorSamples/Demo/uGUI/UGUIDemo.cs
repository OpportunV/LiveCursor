using UnityEngine;
using UnityEngine.UI;

namespace Opportunv.LiveCursor.Samples
{
    public sealed class UGUIDemo : MonoBehaviour
    {
        [SerializeField] private CursorAnimator _cursor;
        [SerializeField] private CursorSet[] _skins;
        [SerializeField] private Text _status;
        [SerializeField] private float _taskSeconds = 3f;

        private float _taskRemaining;

        public void SetSkin(int index)
        {
            if (index >= 0 && index < _skins.Length)
            {
                _cursor.SetCursorSet(_skins[index]);
            }
        }

        public void RunTask()
        {
            _taskRemaining = _taskSeconds;
            _cursor.SetState(DemoCursorStates.Busy);
        }

        public void ToggleIdle()
        {
            _cursor.IdleEnabled = !_cursor.IdleEnabled;
        }

        private void Update()
        {
            if (_taskRemaining > 0f)
            {
                _taskRemaining -= Time.unscaledDeltaTime;
                if (_taskRemaining <= 0f)
                {
                    _cursor.SetState(DemoCursorStates.Default);
                }
            }

            if (_status)
            {
                _status.text = DemoStatus.Describe(_cursor);
            }
        }
    }
}
