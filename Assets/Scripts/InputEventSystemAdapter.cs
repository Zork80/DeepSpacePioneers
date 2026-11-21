using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Ensures the active EventSystem uses the appropriate input module:
/// - Replaces StandaloneInputModule with InputSystemUIInputModule.
/// - Assigns default UI actions (Click, Point, Submit, Navigate, etc.) and ensures all action maps are enabled.
/// - Calls EventSystem.UpdateModules() so the EventSystem registers the module immediately.
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
            var newModule = es.GetComponent<InputSystemUIInputModule>();

            if (newModule == null)
            {
                newModule = es.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            if (standalone != null)
            {
                Object.Destroy(standalone);
            }

            newModule.AssignDefaultActions();
            newModule.actionsAsset?.Enable();
            newModule.enabled = false;
            newModule.enabled = true;

            es.UpdateModules();

            Debug.Log($"[InputEventSystemAdapter] EventSystem upgraded with InputSystemUIInputModule. ActionsAsset={newModule.actionsAsset != null}, enabled={newModule.enabled}");
        }
#endif
    }
}
