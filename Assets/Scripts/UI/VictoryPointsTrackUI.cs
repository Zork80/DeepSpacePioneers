using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays and updates the Victory Points track bar and standee holders.
/// </summary>
public class VictoryPointsTrackUI : MonoBehaviour
{
    private Text _badgeText;
    private Font _font;
    private readonly Dictionary<int, RectTransform> _vpSpotRects = new Dictionary<int, RectTransform>();
    private readonly List<GameObject> _markers = new List<GameObject>();
    private RectTransform _standeeContainer;

    private static readonly Color[] FallbackColors = new Color[]
    {
        new Color(0.85f, 0.2f, 0.2f, 1f),   // Player 1: Red
        new Color(0.2f, 0.5f, 0.9f, 1f),   // Player 2: Blue
        new Color(0.95f, 0.85f, 0.2f, 1f), // Player 3: Yellow
        new Color(0.2f, 0.8f, 0.3f, 1f)    // Player 4: Green
    };

    public static VictoryPointsTrackUI Create(Transform parent, Font font)
    {
        Image bg = UIFactory.CreatePanel("VictoryPointsTrackBar", parent,
            new Color(0.015f, 0.05f, 0.08f, 0.94f), true);
        GameObject root = bg.gameObject;

        RectTransform rect = UIFactory.Rect(root);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(275f, 10f);
        rect.sizeDelta = new Vector2(810f, 88f);

        UIManager.RegisterHudElement(rect);

        VictoryPointsTrackUI track = root.AddComponent<VictoryPointsTrackUI>();
        track._font = font;

        // Title Badge
        Image badgeBg = UIFactory.CreatePanel("TitleBadge", root.transform,
            new Color(0.38f, 0.58f, 0.54f, 0.95f));
        RectTransform badgeRect = badgeBg.rectTransform;
        badgeRect.anchorMin = new Vector2(0f, 1f);
        badgeRect.anchorMax = new Vector2(0f, 1f);
        badgeRect.pivot = new Vector2(0f, 0f);
        badgeRect.anchoredPosition = new Vector2(6f, 1f);
        badgeRect.sizeDelta = new Vector2(115f, 20f);

        track._badgeText = UIFactory.CreateText(badgeBg.transform, font,
            UIStrings.VictoryPointsHeading.GetText().ToUpper(), 12,
            new Color(0.03f, 0.18f, 0.18f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Stretch(track._badgeText.rectTransform, Vector2.zero, Vector2.zero);

        // Horizontal Row for spots 4 to 15
        GameObject spotsContainer = UIFactory.CreateElement("SpotsRow", root.transform);
        RectTransform spotsRowRect = UIFactory.Rect(spotsContainer);
        spotsRowRect.anchorMin = new Vector2(0f, 0.5f);
        spotsRowRect.anchorMax = new Vector2(0f, 0.5f);
        spotsRowRect.pivot = new Vector2(0f, 0.5f);
        spotsRowRect.anchoredPosition = new Vector2(16f, 12f);
        spotsRowRect.sizeDelta = new Vector2(780f, 44f);

        HorizontalLayoutGroup spotsLayout = UIFactory.AddHorizontalLayout(spotsContainer, 14f, 0, false, false);
        spotsLayout.childAlignment = TextAnchor.MiddleLeft;

        track._vpSpotRects.Clear();

        for (int vp = 4; vp <= 15; vp++)
        {
            int point = vp;

            // Extra visual spacer groups (before 8 and before 10)
            if (point == 8 || point == 10)
            {
                GameObject spacer = UIFactory.CreateElement("Spacer_" + point, spotsContainer.transform);
                LayoutElement spLayout = spacer.AddComponent<LayoutElement>();
                spLayout.preferredWidth = 8f;
                spLayout.minWidth = 8f;
            }

            Color ringColor;
            Color textColor;

            if (point == 4)
            {
                ringColor = new Color(0.75f, 0.78f, 0.82f, 0.9f);
                textColor = Color.white;
            }
            else if (point <= 7)
            {
                ringColor = new Color(0.2f, 0.85f, 0.35f, 0.95f);
                textColor = new Color(0.55f, 1f, 0.65f);
            }
            else if (point <= 9)
            {
                ringColor = new Color(0.92f, 0.82f, 0.2f, 0.95f);
                textColor = new Color(1f, 0.95f, 0.55f);
            }
            else
            {
                ringColor = new Color(0.25f, 0.82f, 0.95f, 0.95f);
                textColor = new Color(0.6f, 0.92f, 1f);
            }

            Image spotImage = UIFactory.CreatePanel("Spot_" + point, spotsContainer.transform,
                new Color(ringColor.r * 0.15f, ringColor.g * 0.15f, ringColor.b * 0.15f, 0.9f));
            RectTransform spotRect = spotImage.rectTransform;
            spotRect.sizeDelta = new Vector2(40f, 40f);
            LayoutElement spotLayout = spotImage.gameObject.AddComponent<LayoutElement>();
            spotLayout.preferredWidth = 40f;
            spotLayout.preferredHeight = 40f;
            spotLayout.minWidth = 40f;
            spotLayout.minHeight = 40f;

            Outline outline = spotImage.gameObject.AddComponent<Outline>();
            outline.effectColor = ringColor;
            outline.effectDistance = new Vector2(2f, -2f);

            Text numText = UIFactory.CreateText(spotImage.transform, font, point.ToString(), 14,
                textColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(numText.rectTransform, Vector2.zero, Vector2.zero);

            track._vpSpotRects[point] = spotRect;
        }

        // Standees bottom row container
        GameObject standeeContainer = UIFactory.CreateElement("StandeesRow", root.transform);
        track._standeeContainer = UIFactory.Rect(standeeContainer);
        track._standeeContainer.anchorMin = new Vector2(0f, 0f);
        track._standeeContainer.anchorMax = new Vector2(1f, 0f);
        track._standeeContainer.pivot = new Vector2(0.5f, 0f);
        track._standeeContainer.anchoredPosition = new Vector2(0f, 6f);
        track._standeeContainer.sizeDelta = new Vector2(-28f, 22f);

        HorizontalLayoutGroup standeeLayout = UIFactory.AddHorizontalLayout(standeeContainer, 10f, 0, true, true);
        standeeLayout.childAlignment = TextAnchor.MiddleCenter;

        return track;
    }

    public void UpdateLanguage()
    {
        if (_badgeText != null)
        {
            _badgeText.text = UIStrings.VictoryPointsHeading.GetText().ToUpper();
        }
    }

    public void Refresh(TriMap triMap)
    {
        if (triMap == null || triMap.Players == null)
        {
            return;
        }

        foreach (var marker in _markers)
        {
            if (marker != null)
            {
                Destroy(marker);
            }
        }
        _markers.Clear();

        Player[] players = triMap.Players;
        var playersByVp = new Dictionary<int, List<Player>>();

        for (int i = 0; i < players.Length; i++)
        {
            Player p = players[i];
            int vp = Mathf.Clamp(p.VictoryPoints, 4, 15);
            if (!playersByVp.ContainsKey(vp))
            {
                playersByVp[vp] = new List<Player>();
            }
            playersByVp[vp].Add(p);
        }

        // Render Player Markers on Spot Rects
        foreach (var entry in playersByVp)
        {
            int vp = entry.Key;
            List<Player> spotPlayers = entry.Value;

            if (!_vpSpotRects.TryGetValue(vp, out RectTransform spotRect))
            {
                continue;
            }

            for (int i = 0; i < spotPlayers.Count; i++)
            {
                Player p = spotPlayers[i];
                int pIndex = System.Array.IndexOf(players, p);
                Color pColor = (p.Material != null && p.Material.color != Color.clear && p.Material.color != Color.black)
                    ? p.Material.color
                    : (pIndex >= 0 && pIndex < FallbackColors.Length ? FallbackColors[pIndex] : Color.white);

                Image markerImage = UIFactory.CreatePanel("Marker_" + p.PlayerName, spotRect,
                    pColor);
                GameObject markerObj = markerImage.gameObject;
                RectTransform markerRect = markerImage.rectTransform;
                markerRect.anchorMin = new Vector2(0.5f, 0.5f);
                markerRect.anchorMax = new Vector2(0.5f, 0.5f);
                markerRect.pivot = new Vector2(0.5f, 0.5f);

                float offsetX = (i - (spotPlayers.Count - 1) * 0.5f) * 10f;
                float offsetY = (spotPlayers.Count > 1) ? ((i % 2 == 0 ? 1f : -1f) * 5f) : 0f;
                markerRect.anchoredPosition = new Vector2(offsetX, offsetY);
                markerRect.sizeDelta = new Vector2(28f, 28f);

                Outline markerOutline = markerObj.AddComponent<Outline>();
                markerOutline.effectColor = new Color(0.05f, 0.05f, 0.05f, 0.95f);
                markerOutline.effectDistance = new Vector2(2f, -2f);

                int pNum = p.PlayerNumber > 0 ? p.PlayerNumber : (pIndex + 1);
                float luminance = 0.299f * pColor.r + 0.587f * pColor.g + 0.114f * pColor.b;
                Color textColor = luminance > 0.5f ? Color.black : Color.white;

                Text lblText = UIFactory.CreateText(markerObj.transform, _font, pNum.ToString(), 12,
                    textColor, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIFactory.Stretch(lblText.rectTransform, Vector2.zero, Vector2.zero);

                _markers.Add(markerObj);
            }
        }

        // Render Friendship Standees along bottom row
        AlienCivilization[] civilizations = new AlienCivilization[]
        {
            AlienCivilization.Trader,
            AlienCivilization.Scientist,
            AlienCivilization.Diplomat,
            AlienCivilization.Cultivator
        };

        for (int c = 0; c < civilizations.Length; c++)
        {
            AlienCivilization civ = civilizations[c];
            Player holder = null;
            if (triMap.FolkStandeeHolders != null)
            {
                triMap.FolkStandeeHolders.TryGetValue(civ, out holder);
            }

            Color badgeColor = holder != null
                ? new Color(0.12f, 0.22f, 0.28f, 0.95f)
                : new Color(0.06f, 0.1f, 0.14f, 0.65f);

            Image badgeImg = UIFactory.CreatePanel("StandeeBadge_" + civ, _standeeContainer, badgeColor);
            GameObject standeeBadgeObj = badgeImg.gameObject;

            Color holderColor = new Color(0.3f, 0.35f, 0.4f, 0.8f);
            if (holder != null)
            {
                int holderIndex = System.Array.IndexOf(players, holder);
                holderColor = (holder.Material != null && holder.Material.color != Color.clear && holder.Material.color != Color.black)
                    ? holder.Material.color
                    : (holderIndex >= 0 && holderIndex < FallbackColors.Length ? FallbackColors[holderIndex] : Color.white);
            }

            Image stripImg = UIFactory.CreatePanel("HolderStrip", standeeBadgeObj.transform, holderColor);
            RectTransform stripRect = stripImg.rectTransform;
            stripRect.anchorMin = new Vector2(0f, 0f);
            stripRect.anchorMax = new Vector2(0f, 1f);
            stripRect.pivot = new Vector2(0f, 0.5f);
            stripRect.anchoredPosition = Vector2.zero;
            stripRect.sizeDelta = new Vector2(6f, 0f);

            Text badgeTxt = UIFactory.CreateText(standeeBadgeObj.transform, _font, string.Empty, 11,
                Color.white, TextAnchor.MiddleLeft);
            UIFactory.Stretch(badgeTxt.rectTransform, new Vector2(10f, 0f), new Vector2(-4f, 0f));

            string civShortName = UIStrings.GetAlienCivilizationShortName(civ);
            if (holder != null)
            {
                badgeTxt.text = string.Format("🏆 {0}: {1} (+2)", civShortName, holder.PlayerName);
                badgeTxt.fontStyle = FontStyle.Bold;
                badgeTxt.color = new Color(1f, 0.9f, 0.4f, 1f);
            }
            else
            {
                badgeTxt.text = string.Format("◇ {0}: —", civShortName);
                badgeTxt.fontStyle = FontStyle.Normal;
                badgeTxt.color = new Color(0.65f, 0.72f, 0.78f, 0.8f);
            }

            _markers.Add(standeeBadgeObj);
        }
    }

    private void OnDestroy()
    {
        UIManager.UnregisterHudElement(transform as RectTransform);
    }
}

