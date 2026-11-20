using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight toast popup. Created once under the canvas; use <see cref="Show"/> from anywhere.
/// </summary>
public class ToastNotificationUI : MonoBehaviour
{
    private static readonly Color WarningText = new Color(1f, 0.92f, 0.65f);
    private static readonly Color WarningBackground = new Color(0.16f, 0.05f, 0.02f, 0.96f);
    private static readonly Color WarningAccent = new Color(1f, 0.72f, 0.2f, 1f);
    private static readonly Color InfoText = new Color(0.6f, 0.95f, 1f);
    private static readonly Color InfoBackground = new Color(0.02f, 0.1f, 0.16f, 0.96f);
    private static readonly Color InfoAccent = new Color(0.2f, 0.8f, 1f, 1f);

    public static ToastNotificationUI Instance { get; private set; }

    private Text _text;
    private Image _background;
    private Image _accentStrip;
    private Coroutine _hideCoroutine;

    /// <summary>Builds the toast UI under <paramref name="parent"/> and returns its controller.</summary>
    public static ToastNotificationUI Create(Transform parent, Font font)
    {
        Image background = UIFactory.CreatePanel("NotificationToast", parent, WarningBackground);
        GameObject root = background.gameObject;
        UIFactory.SetAnchors(UIFactory.Rect(root), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(740f, 46f));

        ToastNotificationUI toast = root.AddComponent<ToastNotificationUI>();
        toast._background = background;

        Image strip = UIFactory.CreatePanel("ToastAccentStrip", root.transform, WarningAccent);
        UIFactory.SetAnchors(strip.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(0f, 0.5f), Vector2.zero, new Vector2(5f, 0f));
        toast._accentStrip = strip;

        toast._text = UIFactory.CreateText(root.transform, font, string.Empty, 14, WarningText,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Stretch(toast._text.rectTransform, new Vector2(18f, 0f), new Vector2(-18f, 0f));

        Instance = toast;
        root.SetActive(false);
        return toast;
    }

    public static void Show(string message, bool isWarning = true, float duration = 4.5f)
    {
        if (Instance != null)
        {
            Instance.Display(message, isWarning, duration);
        }
    }

    public void Display(string message, bool isWarning = true, float duration = 4.5f)
    {
        if (string.IsNullOrEmpty(message) || _text == null)
        {
            return;
        }

        _text.text = message;
        _text.color = isWarning ? WarningText : InfoText;
        _background.color = isWarning ? WarningBackground : InfoBackground;
        _accentStrip.color = isWarning ? WarningAccent : InfoAccent;

        transform.SetAsLastSibling();
        gameObject.SetActive(true);

        if (_hideCoroutine != null)
        {
            StopCoroutine(_hideCoroutine);
        }
        _hideCoroutine = StartCoroutine(HideAfter(duration));
    }

    private IEnumerator HideAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        gameObject.SetActive(false);
        _hideCoroutine = null;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}

