using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Ensures the active EventSystem uses InputSystemUIInputModule and logs diagnostic data on click.
/// </summary>
public class InputEventSystemAdapter : MonoBehaviour
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
        // Ensure background input is enabled so UI events are never dropped due to window focus
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;

        EventSystem es = EventSystem.current;
        if (es == null)
        {
            es = Object.FindAnyObjectByType<EventSystem>();
        }

        if (es != null)
        {
            if (es.GetComponent<InputEventSystemAdapter>() == null)
            {
                es.gameObject.AddComponent<InputEventSystemAdapter>();
            }

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
            newModule.actionsAsset?.Enable();
        }
#endif
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            var es = EventSystem.current;
            var curModule = es != null ? es.currentInputModule : null;
            Vector2 mousePos = Mouse.current.position.ReadValue();

            var pointerData = new PointerEventData(es)
            {
                position = mousePos
            };
            var results = new List<RaycastResult>();
            if (es != null)
            {
                es.RaycastAll(pointerData, results);
            }

            string hitInfo = results.Count > 0 
                ? string.Join(" -> ", results.ConvertAll(r => $"{r.gameObject.name}(cam={r.module?.eventCamera?.name}, depth={r.depth})"))
                : "NONE";

            Debug.LogWarning($"[InputEventSystemAdapter] LeftClick at {mousePos}. ES={es?.name}, isFocused={es?.isFocused}, Module={curModule?.GetType().Name}(enabled={curModule?.enabled}), Hits: {hitInfo}");
        }
#endif
    }
}
