using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays a world-space anchored Sci-Fi tooltip when hovering over a star system.
/// Details all 3 planets, their resource types, dice roll probabilities (pips),
/// and hazard statuses (Ice Planet requirements, Marauder Base defense, player readiness).
/// </summary>
public class PlanetTooltipUI : MonoBehaviour
{
    private RectTransform _rect;
    private Text _titleText;
    private Text _statusBadgeText;
    private Image _statusBadgeBg;
    private Canvas _canvas;
    private RectTransform _canvasRect;
    private Tri _currentTri;

    // 3 Planet row references
    private readonly GameObject[] _planetRows = new GameObject[3];
    private readonly Image[] _planetColorBars = new Image[3];
    private readonly Text[] _planetTitleTexts = new Text[3];
    private readonly Text[] _planetDetailTexts = new Text[3];
    private readonly Text[] _planetHazardTexts = new Text[3];
    private readonly RectTransform[] _planetRowRects = new RectTransform[3];

    public static PlanetTooltipUI Create(Transform parent, Font font)
    {
        Canvas canvas = parent.GetComponentInParent<Canvas>();
        Transform rootParent = canvas != null ? canvas.transform : parent;

        Image bg = UIFactory.CreatePanel("PlanetInfoTooltip", rootParent,
            new Color(0.02f, 0.05f, 0.09f, 0.96f), false);
        GameObject root = bg.gameObject;

        PlanetTooltipUI tooltip = root.AddComponent<PlanetTooltipUI>();
        tooltip._canvas = canvas;
        tooltip._canvasRect = canvas != null ? canvas.transform as RectTransform : null;

        tooltip._rect = UIFactory.Rect(root);
        tooltip._rect.anchorMin = new Vector2(0.5f, 0.5f);
        tooltip._rect.anchorMax = new Vector2(0.5f, 0.5f);
        tooltip._rect.pivot = new Vector2(0.5f, 0f);
        tooltip._rect.sizeDelta = new Vector2(360f, 225f);

        Outline outline = root.AddComponent<Outline>();
        outline.effectColor = new Color(0.2f, 0.7f, 0.95f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        // Header Panel
        Image header = UIFactory.CreatePanel("Header", root.transform,
            new Color(0.08f, 0.16f, 0.28f, 0.95f));
        RectTransform headerRect = header.rectTransform;
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -4f);
        headerRect.sizeDelta = new Vector2(-12f, 28f);

        tooltip._titleText = UIFactory.CreateText(header.transform, font,
            UIStrings.PlanetTooltipSystemTitle.GetText(), 12,
            Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
        tooltip._titleText.rectTransform.anchorMin = new Vector2(0f, 0f);
        tooltip._titleText.rectTransform.anchorMax = new Vector2(0.6f, 1f);
        tooltip._titleText.rectTransform.offsetMin = new Vector2(8f, 0f);
        tooltip._titleText.rectTransform.offsetMax = new Vector2(0f, 0f);

        Image badge = UIFactory.CreatePanel("StatusBadge", header.transform,
            new Color(0.15f, 0.5f, 0.75f, 0.95f));
        tooltip._statusBadgeBg = badge;
        RectTransform badgeRect = badge.rectTransform;
        badgeRect.anchorMin = new Vector2(0.6f, 0.12f);
        badgeRect.anchorMax = new Vector2(1f, 0.88f);
        badgeRect.offsetMin = Vector2.zero;
        badgeRect.offsetMax = new Vector2(-6f, 0f);

        tooltip._statusBadgeText = UIFactory.CreateText(badge.transform, font, string.Empty, 10,
            Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Stretch(tooltip._statusBadgeText.rectTransform, Vector2.zero, Vector2.zero);

        // 3 Planet Rows
        float startY = -38f;
        float rowHeight = 58f;
        for (int i = 0; i < 3; i++)
        {
            GameObject row = UIFactory.CreateElement("PlanetRow_" + i, root.transform);
            RectTransform rowRect = UIFactory.Rect(row);
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, startY - (i * rowHeight));
            rowRect.sizeDelta = new Vector2(-14f, rowHeight - 4f);
            tooltip._planetRowRects[i] = rowRect;

            // Left color bar indicating resource
            Image bar = UIFactory.CreatePanel("ColorBar", row.transform, Color.gray);
            RectTransform barRect = bar.rectTransform;
            barRect.anchorMin = new Vector2(0f, 0.1f);
            barRect.anchorMax = new Vector2(0f, 0.9f);
            barRect.pivot = new Vector2(0f, 0.5f);
            barRect.anchoredPosition = new Vector2(4f, 0f);
            barRect.sizeDelta = new Vector2(4f, 0f);
            tooltip._planetColorBars[i] = bar;

            // Title: "Planet 1 · Ore"
            Text pTitle = UIFactory.CreateText(row.transform, font, string.Empty, 11,
                Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
            RectTransform ptRect = pTitle.rectTransform;
            ptRect.anchorMin = new Vector2(0f, 1f);
            ptRect.anchorMax = new Vector2(1f, 1f);
            ptRect.pivot = new Vector2(0f, 1f);
            ptRect.anchoredPosition = new Vector2(14f, -1f);
            ptRect.sizeDelta = new Vector2(-18f, 16f);
            tooltip._planetTitleTexts[i] = pTitle;

            // Detail: "Roll 8 · ••••• (13.9% chance)"
            Text pDetail = UIFactory.CreateText(row.transform, font, string.Empty, 10,
                new Color(0.78f, 0.92f, 1f), TextAnchor.UpperLeft);
            RectTransform pdRect = pDetail.rectTransform;
            pdRect.anchorMin = new Vector2(0f, 1f);
            pdRect.anchorMax = new Vector2(1f, 1f);
            pdRect.pivot = new Vector2(0f, 1f);
            pdRect.anchoredPosition = new Vector2(14f, -17f);
            pdRect.sizeDelta = new Vector2(-18f, 16f);
            tooltip._planetDetailTexts[i] = pDetail;

            // Hazard info: "❄ ICE PLANET: Requires 2 Pods (You have: 3/2 - Ready)"
            Text pHazard = UIFactory.CreateText(row.transform, font, string.Empty, 9,
                new Color(1f, 0.85f, 0.5f), TextAnchor.UpperLeft);
            RectTransform phRect = pHazard.rectTransform;
            phRect.anchorMin = new Vector2(0f, 1f);
            phRect.anchorMax = new Vector2(1f, 1f);
            phRect.pivot = new Vector2(0f, 1f);
            phRect.anchoredPosition = new Vector2(14f, -33f);
            phRect.sizeDelta = new Vector2(-18f, 20f);
            tooltip._planetHazardTexts[i] = pHazard;

            tooltip._planetRows[i] = row;
        }

        root.SetActive(false);
        return tooltip;
    }

    public void Show(Tri tri, TriMap triMap)
    {
        if (UIManager.IsAnyOverlayActive || UIManager.IsPointerOverUI() || UIManager.IsTriUnderHUD(tri))
        {
            Hide();
            return;
        }

        if (tri == null || tri.SystemKind != SystemKind.Planets)
        {
            Hide();
            return;
        }

        _currentTri = tri;
        Player player = triMap != null ? triMap.CurrentPlayer : null;

        // Title
        string sysName = tri.SystemType != SystemTypes.None
            ? tri.SystemType.ToString().ToUpper() + "-SYSTEM"
            : UIStrings.PlanetTooltipSystemTitle.GetText();
        _titleText.text = sysName;

        bool hasHazard = false;
        ResourceType[] resources = tri.PlanetResources;
        int[] numbers = tri.PlanetProductionNumbers;
        bool revealed = tri.ArePlanetProductionNumbersRevealed;

        float currentY = -38f;
        for (int i = 0; i < 3; i++)
        {
            ResourceType res = (resources != null && i < resources.Length) ? resources[i] : ResourceType.None;
            Color resColor = GetResourceColor(res);
            _planetColorBars[i].color = resColor;

            string resName = UIStrings.GetResourceName(res);
            _planetTitleTexts[i].text = string.Format("Planet {0} · {1}", i + 1, resName);
            _planetTitleTexts[i].color = resColor;

            int num = (numbers != null && i < numbers.Length) ? numbers[i] : 0;
            if (!revealed)
            {
                _planetDetailTexts[i].text = UIStrings.PlanetTooltipUndiscovered.GetText();
                _planetDetailTexts[i].color = new Color(0.65f, 0.6f, 0.95f);
            }
            else
            {
                int pips = GetPipsCount(num);
                string pipsStr = GetPipsString(num);
                float chance = GetRollChance(num);
                _planetDetailTexts[i].text = string.Format(
                    UIStrings.PlanetTooltipRollFormat.GetText(),
                    num, pipsStr, chance.ToString("F1"));

                if (num == 6 || num == 8)
                {
                    _planetDetailTexts[i].color = new Color(1f, 0.4f, 0.3f); // Red-Orange highlight
                }
                else
                {
                    _planetDetailTexts[i].color = new Color(0.75f, 0.92f, 1f);
                }
            }

            // Hazard check - ONLY visible if the system is revealed!
            PlanetSpecialType specialType = tri.GetPlanetSpecialType(i);
            int req = tri.GetPlanetSpecialRequirement(i);
            bool isResolved = tri.IsPlanetSpecialResolved(i);
            bool showHazardLine = false;

            if (revealed && specialType == PlanetSpecialType.IcePlanet && !isResolved)
            {
                hasHazard = true;
                showHazardLine = true;
                int playerPods = player != null ? player.FlagshipFreightPodCount : 0;
                bool ready = playerPods >= req;
                string readyLabel = ready
                    ? UIStrings.PlanetTooltipReady.GetText()
                    : UIStrings.PlanetTooltipNotReady.GetText();
                string statusColor = ready ? "#4ade80" : "#f87171";

                _planetHazardTexts[i].text = string.Format(
                    "❄ {0}\n{1}: <color={2}>{3}/{4} ({5})</color>",
                    string.Format(UIStrings.PlanetTooltipIceHazard.GetText(), req),
                    UIStrings.FreightPod.GetText(),
                    statusColor,
                    playerPods, req, readyLabel);
                _planetHazardTexts[i].gameObject.SetActive(true);
            }
            else if (revealed && specialType == PlanetSpecialType.MarauderBase && !isResolved)
            {
                hasHazard = true;
                showHazardLine = true;
                int playerCannons = player != null ? player.EffectiveCannonCount : 0;
                bool ready = playerCannons >= req;
                string readyLabel = ready
                    ? UIStrings.PlanetTooltipReady.GetText()
                    : UIStrings.PlanetTooltipNotReady.GetText();
                string statusColor = ready ? "#4ade80" : "#f87171";

                _planetHazardTexts[i].text = string.Format(
                    "☠ {0}\n{1}: <color={2}>{3}/{4} ({5})</color>",
                    string.Format(UIStrings.PlanetTooltipMarauderHazard.GetText(), req),
                    UIStrings.Cannon.GetText(),
                    statusColor,
                    playerCannons, req, readyLabel);
                _planetHazardTexts[i].gameObject.SetActive(true);
            }
            else if (revealed && specialType != PlanetSpecialType.Normal && isResolved)
            {
                showHazardLine = true;
                _planetHazardTexts[i].text = UIStrings.PlanetTooltipSpecialResolved.GetText();
                _planetHazardTexts[i].color = new Color(1f, 0.85f, 0.2f);
                _planetHazardTexts[i].gameObject.SetActive(true);
            }
            else
            {
                _planetHazardTexts[i].gameObject.SetActive(false);
            }

            float rowHeight = showHazardLine ? 56f : 36f;
            if (_planetRowRects[i] != null)
            {
                _planetRowRects[i].anchoredPosition = new Vector2(0f, currentY);
                _planetRowRects[i].sizeDelta = new Vector2(-14f, rowHeight - 2f);
            }
            currentY -= rowHeight;
        }

        if (!revealed)
        {
            _statusBadgeText.text = UIStrings.PlanetTooltipUndiscovered.GetText();
            _statusBadgeBg.color = new Color(0.38f, 0.32f, 0.65f, 0.95f);
        }
        else if (hasHazard)
        {
            _statusBadgeText.text = UIStrings.PlanetTooltipHazardZone.GetText();
            _statusBadgeBg.color = new Color(0.85f, 0.2f, 0.2f, 0.95f);
        }
        else
        {
            _statusBadgeText.text = UIStrings.PlanetTooltipSecureZone.GetText();
            _statusBadgeBg.color = new Color(0.15f, 0.55f, 0.75f, 0.95f);
        }

        float totalHeight = Mathf.Abs(currentY) + 10f;
        if (_rect != null)
        {
            _rect.sizeDelta = new Vector2(360f, totalHeight);
        }

        UpdatePosition();
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
    }

    private void UpdatePosition()
    {
        if (_currentTri == null) return;

        Vector3 worldPos = _currentTri.CenterPosition() + new Vector3(0f, 0.45f, 0f);

        Camera mainCam = UIManager.MainCamera;
        if (mainCam == null) return;

        Vector3 screenPoint = mainCam.WorldToScreenPoint(worldPos);
        if (screenPoint.z <= 0)
        {
            Hide();
            return;
        }

        Camera uiCam = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? _canvas.worldCamera : null;
        RectTransform targetRect = _canvasRect != null ? _canvasRect : (transform.parent as RectTransform);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetRect,
            screenPoint,
            uiCam,
            out Vector2 localPoint))
        {
            Vector2 targetPos = localPoint + new Vector2(0f, 25f);

            if (_canvasRect != null)
            {
                float halfWidth = _canvasRect.rect.width * 0.5f;
                float halfHeight = _canvasRect.rect.height * 0.5f;
                float halfTooltipW = _rect.sizeDelta.x * 0.5f;
                float tooltipH = _rect.sizeDelta.y;

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

    private static Color GetResourceColor(ResourceType resource)
    {
        switch (resource)
        {
            case ResourceType.Ore: return new Color(0.95f, 0.28f, 0.25f);       // Red (Rot / Erzplanet)
            case ResourceType.Fuel: return new Color(1.0f, 0.72f, 0.15f);       // Golden Orange (Orange / Treibstoffplanet)
            case ResourceType.Carbon: return new Color(0.28f, 0.65f, 0.98f);     // Cyan/Blue (Blau / Carbonplanet)
            case ResourceType.Food: return new Color(0.25f, 0.85f, 0.30f);       // Green (Grün / Nahrungsplanet)
            case ResourceType.TradeGoods: return new Color(0.82f, 0.48f, 0.95f); // Purple / Violet (Lila gestreift / Handelsware)
            default: return new Color(0.7f, 0.7f, 0.7f);
        }
    }

    private static int GetPipsCount(int number)
    {
        switch (number)
        {
            case 2: case 12: return 1;
            case 3: case 11: return 2;
            case 4: case 10: return 3;
            case 5: case 9:  return 4;
            case 6: case 8:  return 5;
            default: return 0;
        }
    }

    private static string GetPipsString(int number)
    {
        int count = GetPipsCount(number);
        switch (count)
        {
            case 1: return "\u2022";
            case 2: return "\u2022\u2022";
            case 3: return "\u2022\u2022\u2022";
            case 4: return "\u2022\u2022\u2022\u2022";
            case 5: return "\u2022\u2022\u2022\u2022\u2022";
            default: return string.Empty;
        }
    }

    private static float GetRollChance(int number)
    {
        switch (number)
        {
            case 2: case 12: return 2.8f;
            case 3: case 11: return 5.6f;
            case 4: case 10: return 8.3f;
            case 5: case 9:  return 11.1f;
            case 6: case 8:  return 13.9f;
            default: return 0f;
        }
    }
}

