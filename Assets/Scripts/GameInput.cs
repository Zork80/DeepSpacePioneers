using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

/// <summary>
/// Centralized cross-version input abstraction for Deep Space Pioneers.
/// Supports both Unity's New Input System (UnityEngine.InputSystem) and Legacy Input Manager.
/// </summary>
public static class GameInput
{
    public static Vector3 MousePosition
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                Vector2 pos = Mouse.current.position.ReadValue();
                return new Vector3(pos.x, pos.y, 0f);
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return Vector3.zero;
#endif
        }
    }

    public static float MouseScrollDeltaY
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                // Input System scroll delta is typically in pixels/steps (e.g. 120 per notch); normalize to legacy scale (~1 per notch)
                if (Mathf.Abs(scroll) > 0.001f)
                {
                    return Mathf.Clamp(scroll / 120f, -1f, 1f);
                }
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetAxis("Mouse ScrollWheel");
#else
            return 0f;
#endif
        }
    }

    public static bool GetMouseButton(int button)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            ButtonControl btn = GetMouseButtonControl(button);
            if (btn != null) return btn.isPressed;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButton(button);
#else
        return false;
#endif
    }

    public static bool GetMouseButtonDown(int button)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            ButtonControl btn = GetMouseButtonControl(button);
            if (btn != null) return btn.wasPressedThisFrame;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(button);
#else
        return false;
#endif
    }

    public static bool GetMouseButtonUp(int button)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            ButtonControl btn = GetMouseButtonControl(button);
            if (btn != null) return btn.wasReleasedThisFrame;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonUp(button);
#else
        return false;
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private static ButtonControl GetMouseButtonControl(int button)
    {
        if (Mouse.current == null) return null;
        switch (button)
        {
            case 0: return Mouse.current.leftButton;
            case 1: return Mouse.current.rightButton;
            case 2: return Mouse.current.middleButton;
            default: return null;
        }
    }
#endif

    public static bool GetKey(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            KeyControl keyControl = GetKeyControl(key);
            if (keyControl != null) return keyControl.isPressed;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(key);
#else
        return false;
#endif
    }

    public static bool GetKeyDown(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            KeyControl keyControl = GetKeyControl(key);
            if (keyControl != null) return keyControl.wasPressedThisFrame;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(key);
#else
        return false;
#endif
    }

    public static bool GetKeyUp(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            KeyControl keyControl = GetKeyControl(key);
            if (keyControl != null) return keyControl.wasReleasedThisFrame;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyUp(key);
#else
        return false;
#endif
    }

    public static bool AnyKeyDown
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame ||
                                          Mouse.current.rightButton.wasPressedThisFrame ||
                                          Mouse.current.middleButton.wasPressedThisFrame)) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
#else
            return false;
#endif
        }
    }

    public static float GetAxisRaw(string axis)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (axis == "Horizontal")
            {
                float val = 0f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) val += 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) val -= 1f;
                return val;
            }
            if (axis == "Vertical")
            {
                float val = 0f;
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) val += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) val -= 1f;
                return val;
            }
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetAxisRaw(axis);
#else
        return 0f;
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private static KeyControl GetKeyControl(KeyCode key)
    {
        if (Keyboard.current == null) return null;
        switch (key)
        {
            case KeyCode.W: return Keyboard.current.wKey;
            case KeyCode.A: return Keyboard.current.aKey;
            case KeyCode.S: return Keyboard.current.sKey;
            case KeyCode.D: return Keyboard.current.dKey;
            case KeyCode.UpArrow: return Keyboard.current.upArrowKey;
            case KeyCode.DownArrow: return Keyboard.current.downArrowKey;
            case KeyCode.LeftArrow: return Keyboard.current.leftArrowKey;
            case KeyCode.RightArrow: return Keyboard.current.rightArrowKey;
            case KeyCode.LeftShift: return Keyboard.current.leftShiftKey;
            case KeyCode.RightShift: return Keyboard.current.rightShiftKey;
            case KeyCode.Space: return Keyboard.current.spaceKey;
            case KeyCode.LeftAlt: return Keyboard.current.leftAltKey;
            case KeyCode.RightAlt: return Keyboard.current.rightAltKey;
            case KeyCode.Escape: return Keyboard.current.escapeKey;
            case KeyCode.Tab: return Keyboard.current.tabKey;
            case KeyCode.Home: return Keyboard.current.homeKey;
            case KeyCode.Return: return Keyboard.current.enterKey;
            case KeyCode.F1: return Keyboard.current.f1Key;
            case KeyCode.F2: return Keyboard.current.f2Key;
            case KeyCode.F3: return Keyboard.current.f3Key;
            case KeyCode.F4: return Keyboard.current.f4Key;
            case KeyCode.F5: return Keyboard.current.f5Key;
            default: return null;
        }
    }
#endif
}
