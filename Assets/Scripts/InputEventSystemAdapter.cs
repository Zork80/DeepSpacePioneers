using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Ensures the active EventSystem uses the appropriate input module:
/// - In hybrid mode (activeInputHandler: Both), keeps StandaloneInputModule active so standard UI buttons
///   and raycasts work natively without conflicts.
/// - In modern-only mode (activeInputHandler: Input System Package (New)), attaches InputSystemUIInputModule,
///   assigns default actions, and calls EventSystem.UpdateModules() so the EventSystem registers it.
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
#if !ENABLE_LEGACY_INPUT_MANAGER
        // In exclusive New Input System mode, StandaloneInputModule is not supported by Unity.
        // We must switch to InputSystemUIInputModule and update EventSystem.
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

            newModule.AssignDefaultActions();
            newModule.enabled = true;

            if (standalone != null)
            {
                Object.Destroy(standalone);
            }

            es.UpdateModules();
        }
#else
        // In hybrid mode (activeInputHandler: Both), keep StandaloneInputModule active so existing UI buttons
        // work natively without action binding overhead, while GameInput uses the New Input System.
        EventSystem es = EventSystem.current;
        if (es == null)
        {
            es = Object.FindAnyObjectByType<EventSystem>();
        }

        if (es != null)
        {
            var standalone = es.GetComponent<StandaloneInputModule>();
            if (standalone != null && !standalone.enabled)
            {
                standalone.enabled = true;
            }
            var newModule = es.GetComponent<InputSystemUIInputModule>();
            if (newModule != null)
            {
                Object.Destroy(newModule);
            }
            es.UpdateModules();
        }
#endif
#endif
    }
}
