using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Central registry for tracking active UI modals, persistent HUD elements, and screen occlusion.
/// Used to suppress hover tooltips, block raycasts, etc.
/// </summary>
public static class UIManager
{
    private static readonly List<Func<bool>> _overlayActiveChecks = new List<Func<bool>>();
    private static readonly List<RectTransform> _hudRects = new List<RectTransform>();
    private static Camera _mainCamera;

    public static Camera MainCamera
    {
        get
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            return _mainCamera;
        }
    }

    public static void RegisterOverlayCheck(Func<bool> check)
    {
        if (check != null && !_overlayActiveChecks.Contains(check))
        {
            _overlayActiveChecks.Add(check);
        }
    }

    public static void UnregisterOverlayCheck(Func<bool> check)
    {
        if (check != null)
        {
            _overlayActiveChecks.Remove(check);
        }
    }

    public static void RegisterHudElement(RectTransform rectTransform)
    {
        if (rectTransform != null && !_hudRects.Contains(rectTransform))
        {
            _hudRects.Add(rectTransform);
        }
    }

    public static void UnregisterHudElement(RectTransform rectTransform)
    {
        if (rectTransform != null)
        {
            _hudRects.Remove(rectTransform);
        }
    }

    /// <summary>
    /// Returns true if any modal dialog or overlay is currently open.
    /// </summary>
    public static bool IsAnyOverlayActive
    {
        get
        {
            for (int i = 0; i < _overlayActiveChecks.Count; i++)
            {
                try
                {
                    if (_overlayActiveChecks[i] != null && _overlayActiveChecks[i].Invoke())
                    {
                        return true;
                    }
                }
                catch
                {
                    // Ignore disposed or null delegates
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Checks if a screen-space coordinate (e.g. mouse position or projected 3D world position)
    /// lies over or under any persistent HUD element or modal overlay.
    /// </summary>
    public static bool IsScreenPositionOverHUD(Vector2 screenPoint)
    {
        if (IsAnyOverlayActive) return true;

        // Persistent LeftBar sidebar check: LeftBar occupies the left 260px of the screen
        if (screenPoint.x >= 0f && screenPoint.x <= 260f)
        {
            return true;
        }

        // Check registered HUD rects (e.g. VictoryPointsTrackBar, etc.)
        for (int i = 0; i < _hudRects.Count; i++)
        {
            RectTransform rect = _hudRects[i];
            if (rect != null && rect.gameObject.activeInHierarchy)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if the pointer is currently over any UI element (EventSystem check, modal check, or HUD bounds).
    /// </summary>
    public static bool IsPointerOverUI()
    {
        if (IsAnyOverlayActive) return true;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return true;
        }

        return IsScreenPositionOverHUD(Input.mousePosition);
    }

    /// <summary>
    /// Returns true if the projected 3D position of the specified Tri tile is occluded by or located behind HUD elements.
    /// </summary>
    public static bool IsTriUnderHUD(Tri tri)
    {
        if (tri == null) return false;
        if (IsAnyOverlayActive) return true;

        Camera cam = MainCamera;
        if (cam == null) return false;

        Vector3 screenPoint = cam.WorldToScreenPoint(tri.CenterPosition());
        if (screenPoint.z <= 0f) return true; // Behind camera

        return IsScreenPositionOverHUD(screenPoint);
    }

    /// <summary>
    /// Resets the registry (e.g. on scene reload).
    /// </summary>
    public static void Clear()
    {
        _overlayActiveChecks.Clear();
        _hudRects.Clear();
        _mainCamera = null;
    }
}

