using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Ensures the active EventSystem uses InputSystemUIInputModule if not already present.
/// </summary>
public static class InputEventSystemAdapter
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        EnsureEventSystemModule();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => EnsureEventSystemModule();
    }

    public static void EnsureEventSystemModule()
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
            if (standalone != null)
            {
                Object.Destroy(standalone);
            }

            var newModule = es.GetComponent<InputSystemUIInputModule>();
            if (newModule == null)
            {
                newModule = es.gameObject.AddComponent<InputSystemUIInputModule>();
                newModule.AssignDefaultActions();
                es.UpdateModules();
            }
        }
#endif
    }
}
