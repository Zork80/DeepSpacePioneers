using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays small hover tooltips for build action costs.
/// </summary>
public class CostTooltipUI : MonoBehaviour
{
    private Text _text;
    private RectTransform _rect;
    private RectTransform _canvasRect;
    private RectTransform _anchor;

    public static CostTooltipUI Create(Transform parent, Font font)
    {
        Image bg = UIFactory.CreatePanel("CostTooltip", parent,
            new Color(0.04f, 0.08f, 0.12f, 0.98f), false);
        GameObject root = bg.gameObject;

        CostTooltipUI tooltip = root.AddComponent<CostTooltipUI>();
        Canvas canvas = parent.GetComponentInParent<Canvas>();
        tooltip._canvasRect = canvas != null ? canvas.transform as RectTransform : parent as RectTransform;

        tooltip._rect = UIFactory.Rect(root);
        tooltip._rect.anchorMin = new Vector2(0.5f, 0.5f);
        tooltip._rect.anchorMax = new Vector2(0.5f, 0.5f);
        tooltip._rect.pivot = new Vector2(0.5f, 0f);
        tooltip._rect.sizeDelta = new Vector2(200f, 26f);

        LayoutElement le = root.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        Outline outline = root.AddComponent<Outline>();
        outline.effectColor = new Color(0.25f, 0.65f, 0.85f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        tooltip._text = UIFactory.CreateText(root.transform, font, string.Empty, 11,
            new Color(0.9f, 0.96f, 1f), TextAnchor.MiddleCenter);
        UIFactory.Stretch(tooltip._text.rectTransform, new Vector2(6f, 2f), new Vector2(-6f, -2f));

        root.SetActive(false);
        return tooltip;
    }

    public void Show(string costDescription, RectTransform buttonRect, Transform buildPanel = null)
    {
        if (UIManager.IsAnyOverlayActive)
        {
            Hide();
            return;
        }

        if (_text == null || buttonRect == null) return;

        _anchor = buttonRect;
        _text.text = costDescription;
        float preferredWidth = Mathf.Max(160f, _text.preferredWidth + 18f);
        _rect.sizeDelta = new Vector2(preferredWidth, 26f);

        if (_canvasRect != null)
        {
            Vector3[] corners = new Vector3[4];
            buttonRect.GetWorldCorners(corners);
            Vector3 topCenter = (corners[1] + corners[2]) * 0.5f;

            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, topCenter);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPos, null, out Vector2 localPoint))
            {
                _rect.anchorMin = new Vector2(0.5f, 0.5f);
                _rect.anchorMax = new Vector2(0.5f, 0.5f);
                _rect.pivot = new Vector2(0.5f, 0f);
                _rect.anchoredPosition = new Vector2(localPoint.x, localPoint.y + 4f);
            }
        }

        transform.SetAsLastSibling();
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        _anchor = null;
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    // The anchor button can be hidden or destroyed while the pointer is still over it
    // (no PointerExit is sent then), so the tooltip has to watch its anchor itself.
    private void LateUpdate()
    {
        if (_anchor == null || !_anchor.gameObject.activeInHierarchy)
        {
            Hide();
        }
    }
}
