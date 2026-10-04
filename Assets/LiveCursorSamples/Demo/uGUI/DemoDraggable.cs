using UnityEngine;
using UnityEngine.EventSystems;

namespace Opportunv.LiveCursor.Samples
{
    public sealed class DemoDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private CursorAnimator _cursor;
        [SerializeField] private int _priority = 100;

        private CursorRequest _drag;
        private Plane _plane;
        private Vector3 _offset;

        public void Configure(CursorAnimator cursor)
        {
            _cursor = cursor;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _plane = new(Vector3.up, transform.position);
            if (TryHit(eventData, out var point))
            {
                _offset = transform.position - point;
            }

            if (_cursor)
            {
                _drag = _cursor.Request(DemoCursorStates.Grabbing, _priority, true);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (TryHit(eventData, out var point))
            {
                transform.position = point + _offset;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _drag.Release(true);
        }

        private void OnDisable()
        {
            _drag.Release();
        }

        private bool TryHit(PointerEventData eventData, out Vector3 point)
        {
            var eventCamera = eventData.pressEventCamera ? eventData.pressEventCamera : Camera.main;
            if (!eventCamera)
            {
                point = default;
                return false;
            }

            var ray = eventCamera.ScreenPointToRay(eventData.position);
            if (_plane.Raycast(ray, out var distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }

            point = default;
            return false;
        }
    }
}
