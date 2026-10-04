using Opportunv.LiveCursor.UGUI;
using UnityEngine;

namespace Opportunv.LiveCursor.Samples
{
    public sealed class DemoWorld : MonoBehaviour
    {
        [SerializeField] private CursorAnimator _cursor;

        private static readonly Color _floor = new(0.32f, 0.36f, 0.44f);
        private static readonly Color _crate = new(0.95f, 0.62f, 0.25f);
        private static readonly Color _target = new(0.35f, 0.8f, 0.95f);
        private static readonly Color _locked = new(0.85f, 0.3f, 0.3f);

        private void Start()
        {
            var floor = Spawn(PrimitiveType.Plane, "Floor", Vector3.zero, new(2f, 1f, 1.4f), _floor);
            floor.GetComponent<Collider>().enabled = false;

            for (var i = 0; i < 3; i++)
            {
                var crate = Spawn(
                    PrimitiveType.Cube,
                    $"Crate {i + 1}",
                    new(-3f + i * 2.2f, 0.5f, -0.5f + i * 0.6f),
                    Vector3.one,
                    _crate);
                crate.AddComponent<CursorHover>().Configure(_cursor, DemoCursorStates.Grab, DemoCursorStates.Grabbing);
                crate.AddComponent<DemoDraggable>().Configure(_cursor);
            }

            var target = Spawn(PrimitiveType.Sphere, "Target", new(-1.5f, 1f, 3f), Vector3.one * 1.4f, _target);
            target.AddComponent<CursorHover>().Configure(_cursor, DemoCursorStates.Crosshair);

            var locked = Spawn(PrimitiveType.Cylinder, "Locked", new(2.5f, 1f, 3f), new(1f, 1f, 1f), _locked);
            locked.AddComponent<CursorHover>().Configure(_cursor, DemoCursorStates.Blocked);
        }

        private GameObject Spawn(PrimitiveType type, string objectName, Vector3 position, Vector3 scale, Color color)
        {
            var spawned = GameObject.CreatePrimitive(type);
            spawned.name = objectName;
            spawned.transform.SetParent(transform, false);
            spawned.transform.localPosition = position;
            spawned.transform.localScale = scale;
            spawned.GetComponent<Renderer>().material.color = color;
            return spawned;
        }
    }
}
