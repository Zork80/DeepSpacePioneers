using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Ensures the active EventSystem uses the appropriate input module (InputSystemUIInputModule when Input System is active).
/// </summary>
public static class InputEventSystemAdapter
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureEventSystemModule()
    {
#if ENABLE_INPUT_SYSTEM
        EventSystem es = EventSystem.current;
        if (es == null)
        {
            es = Object.FindAnyObjectByType<EventSystem>();
        }

        if (es != null)
        {
            var standalone = es.GetComponent<StandaloneInputModule>();
            var newModule = es.GetComponent<InputSystemUIInputModule>();

            if (newModule == null)
            {
                newModule = es.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            if (standalone != null)
            {
                standalone.enabled = false;
            }
            newModule.enabled = true;
        }
#endif
    }
}
