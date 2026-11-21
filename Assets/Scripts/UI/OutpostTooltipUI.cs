using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays world-space anchored tooltips for alien outpost trade stations.
/// Projects the 3D outpost position to Canvas coordinates and keeps the tooltip in view.
/// </summary>
public class OutpostTooltipUI : MonoBehaviour
{
    private RectTransform _rect;
    private Image _badgeBg;
    private Text _titleText;
    private Text _benefitText;
    private Text _rulesText;
    private Canvas _canvas;
    private RectTransform _canvasRect;
    private Tri _currentTri;

    public static OutpostTooltipUI Create(Transform parent, Font font)
    {
        Canvas canvas = parent.GetComponentInParent<Canvas>();
        Transform rootParent = canvas != null ? canvas.transform : parent;

        Image bg = UIFactory.CreatePanel("OutpostInfoTooltip", rootParent,
            new Color(0.02f, 0.05f, 0.08f, 0.97f), false);
        GameObject root = bg.gameObject;

        OutpostTooltipUI tooltip = root.AddComponent<OutpostTooltipUI>();
        tooltip._canvas = canvas;
        tooltip._canvasRect = canvas != null ? canvas.transform as RectTransform : null;

        tooltip._rect = UIFactory.Rect(root);
        // Anchor to canvas center (0.5, 0.5) so that localPoint directly corresponds to anchoredPosition
        tooltip._rect.anchorMin = new Vector2(0.5f, 0.5f);
        tooltip._rect.anchorMax = new Vector2(0.5f, 0.5f);
        tooltip._rect.pivot = new Vector2(0.5f, 0f);
        tooltip._rect.sizeDelta = new Vector2(320f, 130f);

        Outline outline = root.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.75f, 0.95f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        // Folk Title Badge
        Image badge = UIFactory.CreatePanel("TitleBadge", root.transform,
            new Color(0.15f, 0.45f, 0.65f, 0.95f));
        tooltip._badgeBg = badge;
        RectTransform badgeRect = badge.rectTransform;
        badgeRect.anchorMin = new Vector2(0f, 1f);
        badgeRect.anchorMax = new Vector2(1f, 1f);
        badgeRect.pivot = new Vector2(0.5f, 1f);
        badgeRect.anchoredPosition = new Vector2(0f, -4f);
        badgeRect.sizeDelta = new Vector2(-12f, 26f);

        tooltip._titleText = UIFactory.CreateText(badge.transform, font, string.Empty, 13,
            Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Stretch(tooltip._titleText.rectTransform, new Vector2(6f, 0f), new Vector2(-6f, 0f));

        // Advantage/Benefit Text
        tooltip._benefitText = UIFactory.CreateText(root.transform, font, string.Empty, 11,
            new Color(0.95f, 0.98f, 0.7f), TextAnchor.UpperLeft);
        RectTransform benRect = tooltip._benefitText.rectTransform;
        benRect.anchorMin = new Vector2(0f, 0f);
        benRect.anchorMax = new Vector2(1f, 1f);
        benRect.pivot = new Vector2(0.5f, 0.5f);
        benRect.offsetMin = new Vector2(10f, 40f);
        benRect.offsetMax = new Vector2(-10f, -34f);

        // General Trade Station Rule Bonus Text
        tooltip._rulesText = UIFactory.CreateText(root.transform, font, UIStrings.OutpostFriendshipBonus.GetText(),
            10, new Color(0.65f, 0.88f, 0.95f), TextAnchor.UpperLeft);
        RectTransform rulesRect = tooltip._rulesText.rectTransform;
        rulesRect.anchorMin = new Vector2(0f, 0f);
        rulesRect.anchorMax = new Vector2(1f, 0f);
        rulesRect.pivot = new Vector2(0.5f, 0f);
        rulesRect.anchoredPosition = new Vector2(0f, 6f);
        rulesRect.sizeDelta = new Vector2(-20f, 32f);

        root.SetActive(false);
        return tooltip;
    }

    public void Show(Tri tri)
    {
        if (UIManager.IsAnyOverlayActive || UIManager.IsPointerOverUI() || UIManager.IsTriUnderHUD(tri))
        {
            Hide();
            return;
        }

        if (tri == null || tri.SystemKind != SystemKind.Trade || tri.AlienCivilization == AlienCivilization.None)
        {
            Hide();
            return;
        }

        _currentTri = tri;

        AlienCivilization civ = tri.AlienCivilization;
        _titleText.text = UIStrings.GetAlienCivilizationName(civ).ToUpper();
        _benefitText.text = UIStrings.GetAlienCivilizationBenefit(civ);
        _rulesText.text = UIStrings.OutpostFriendshipBonus.GetText();

        Color badgeColor;
        switch (civ)
        {
            case AlienCivilization.Trader:
                badgeColor = new Color(0.12f, 0.45f, 0.78f, 0.95f);
                break;
            case AlienCivilization.Scientist:
                badgeColor = new Color(0.85f, 0.65f, 0.10f, 0.95f);
                break;
            case AlienCivilization.Diplomat:
                badgeColor = new Color(0.65f, 0.20f, 0.60f, 0.95f);
                break;
            case AlienCivilization.Cultivator:
                badgeColor = new Color(0.18f, 0.62f, 0.28f, 0.95f);
                break;
            default:
                badgeColor = new Color(0.2f, 0.4f, 0.5f, 0.95f);
                break;
        }
        if (_badgeBg != null)
        {
            _badgeBg.color = badgeColor;
        }

        UpdatePosition();
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
    }

    private void UpdatePosition()
    {
        if (_currentTri == null) return;

        // Determine accurate 3D world position of the outpost
        Vector3 worldPos;
        TriMap triMap = UnityEngine.Object.FindFirstObjectByType<TriMap>();
        Transform outpostRoot = triMap != null ? triMap.GetTradeOutpostRoot(_currentTri) : null;
        if (outpostRoot != null)
        {
            worldPos = outpostRoot.position + new Vector3(0f, 0.5f, 0f);
        }
        else
        {
            worldPos = _currentTri.CenterPosition() + new Vector3(0f, 0.5f, 0f);
        }

        // Project 3D world position to screen using Main Camera (which renders the 3D scene)
        Camera mainCam = UIManager.MainCamera;
        if (mainCam == null) return;

        Vector3 screenPoint = mainCam.WorldToScreenPoint(worldPos);

        // Ensure target is in front of the camera
        if (screenPoint.z <= 0)
        {
            Hide();
            return;
        }

        // Convert screen point into Canvas local coordinates (relative to Canvas center)
        Camera uiCam = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? _canvas.worldCamera : null;
        RectTransform targetRect = _canvasRect != null ? _canvasRect : (transform.parent as RectTransform);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetRect,
            screenPoint,
            uiCam,
            out Vector2 localPoint))
        {
            Vector2 targetPos = localPoint + new Vector2(0f, 25f);

            // Clamp within Canvas viewport boundaries so tooltip is never clipped
            if (_canvasRect != null)
            {
                float halfWidth = _canvasRect.rect.width * 0.5f;
                float halfHeight = _canvasRect.rect.height * 0.5f;
                float halfTooltipW = _rect.sizeDelta.x * 0.5f;
                float tooltipH = _rect.sizeDelta.y;

                // If tooltip overflows top edge, flip it below the outpost
                if (targetPos.y + tooltipH > halfHeight - 12f)
                {
                    targetPos.y = localPoint.y - tooltipH - 25f;
                }

                // Never overlap the right sidebar (260px wide from right screen edge)
                float minX = -halfWidth + halfTooltipW + 12f;
                float maxX = halfWidth - 260f - halfTooltipW - 12f;
                targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);

                // Never overlap the bottom HUD (e.g. VP track bar, roughly bottom 105px)
                float minY = -halfHeight + 105f;
                float maxY = halfHeight - tooltipH - 12f;
                targetPos.y = Mathf.Clamp(targetPos.y, minY, maxY);
            }

            _rect.anchoredPosition = targetPos;
        }
    }

    void LateUpdate()
    {
        if (gameObject.activeSelf && _currentTri != null)
        {
            if (UIManager.IsAnyOverlayActive || UIManager.IsPointerOverUI() || UIManager.IsTriUnderHUD(_currentTri))
            {
                Hide();
                return;
            }
            UpdatePosition();
        }
    }

    public void Hide()
    {
        _currentTri = null;
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }
}
