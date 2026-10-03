using UnityEngine;
using UnityEngine.EventSystems;

namespace Opportunv.LiveCursor.Samples
{
    [RequireComponent(typeof(EventSystem))]
    public sealed class DemoInputModule : MonoBehaviour
    {
        private void Awake()
        {
            if (GetComponent<BaseInputModule>())
            {
                return;
            }

#if ENABLE_INPUT_SYSTEM
            const string inputSystemModule = "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem";
            var type = System.Type.GetType(inputSystemModule);
            if (type != null)
            {
                gameObject.AddComponent(type);
                return;
            }
#endif
            gameObject.AddComponent<StandaloneInputModule>();
        }
    }
}
