using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Ensures the active EventSystem uses InputSystemUIInputModule, enables its action assets,
/// prevents focus drop, and logs diagnostic click data.
/// </summary>
public class InputEventSystemAdapter : MonoBehaviour
{
    private void Awake()
    {
        EnsureSetup();
        Debug.LogWarning($"[InputEventSystemAdapter] Awake on '{gameObject.name}'. EventSystem={EventSystem.current?.name}");
    }

    private void Start()
    {
        EnsureSetup();
        Debug.LogWarning($"[InputEventSystemAdapter] Start on '{gameObject.name}'. ActiveModule={EventSystem.current?.currentInputModule?.GetType().Name}");
    }

    private void OnEnable()
    {
        EnsureSetup();
    }

    public void EnsureSetup()
    {
#if ENABLE_INPUT_SYSTEM
        // Ensure background input is enabled so UI events are never dropped due to window focus
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;

        var es = GetComponent<EventSystem>() ?? EventSystem.current;
        if (es != null)
        {
            var standalone = es.GetComponent<StandaloneInputModule>();
            if (standalone != null)
            {
                Destroy(standalone);
            }

            var module = es.GetComponent<InputSystemUIInputModule>();
            if (module == null)
            {
                module = es.gameObject.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
            }

            if (module.actionsAsset != null && !module.actionsAsset.enabled)
            {
                module.actionsAsset.Enable();
                Debug.LogWarning($"[InputEventSystemAdapter] Explicitly enabled actionsAsset '{module.actionsAsset.name}'");
            }

            es.UpdateModules();
        }
#endif
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.leftButton.wasReleasedThisFrame))
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

            Debug.LogWarning($"[InputEventSystemAdapter] Click (down={Mouse.current.leftButton.wasPressedThisFrame}, up={Mouse.current.leftButton.wasReleasedThisFrame}) at {mousePos}. ES={es?.name}, isFocused={es?.isFocused}, Module={curModule?.GetType().Name}, Hits: {hitInfo}");
        }
#endif
    }
}
