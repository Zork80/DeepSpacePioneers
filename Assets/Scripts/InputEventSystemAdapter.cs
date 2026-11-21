using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Ensures the active EventSystem uses InputSystemUIInputModule, enables its action assets,
/// and prevents focus loss from dropping UI pointer events.
/// </summary>
public class InputEventSystemAdapter : MonoBehaviour
{
    private void Awake()
    {
        EnsureSetup();
    }

    private void OnEnable()
    {
        EnsureSetup();
    }

    public void EnsureSetup()
    {
#if ENABLE_INPUT_SYSTEM
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
            }

            es.UpdateModules();
        }
#endif
    }
}
