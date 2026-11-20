using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modal dialog displaying the overview of building costs.
/// Built with Auto-Layout.
/// </summary>
public class BuildingCostsDialogUI : MonoBehaviour
{
    private Text _titleText;
    private Text _contentText;
    private Text _closeButtonLabel;

    public static BuildingCostsDialogUI Create(Transform parent, Font font)
    {
        Image bg = UIFactory.CreatePanel("BuildingCostsOverviewModal", parent,
            new Color(0.015f, 0.045f, 0.075f, 0.98f), true);
        GameObject root = bg.gameObject;

        RectTransform rect = UIFactory.Rect(root);
        UIFactory.SetAnchors(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(50f, 0f), new Vector2(460f, 0f));

        Outline outline = root.AddComponent<Outline>();
        outline.effectColor = new Color(0.2f, 0.65f, 0.85f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        BuildingCostsDialogUI dialog = root.AddComponent<BuildingCostsDialogUI>();

        UIFactory.AddVerticalLayout(root, 12f, 16);
        UIFactory.AddSizeFitter(root);

        // Title
        dialog._titleText = UIFactory.CreateText(root.transform, font,
            UIStrings.BuildingCostsOverviewTitle.GetText(), 18,
            new Color(0.38f, 0.82f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);

        // Content
        dialog._contentText = UIFactory.CreateText(root.transform, font,
            string.Empty, 13, new Color(0.92f, 0.96f, 1f), TextAnchor.UpperLeft);
        dialog._contentText.lineSpacing = 1.3f;

        // Close button row
        GameObject botRow = UIFactory.CreateElement("BottomRow", root.transform);
        HorizontalLayoutGroup botLayout = UIFactory.AddHorizontalLayout(botRow, 0f);
        botLayout.childAlignment = TextAnchor.MiddleCenter;
        botLayout.childForceExpandWidth = false;

        Button closeBtn = UIFactory.CreateButton(botRow.transform, font,
            UIStrings.Close.GetText(), dialog.Close, new Color(0.12f, 0.35f, 0.55f, 1f), 13, 34f);
        EncounterDialogUI.SetPreferredSize(closeBtn, 160f, 34f);
        dialog._closeButtonLabel = closeBtn.GetComponentInChildren<Text>();

        root.SetActive(false);
        return dialog;
    }

    public void Toggle()
    {
        bool newState = !gameObject.activeSelf;
        if (newState)
        {
            RefreshContent();
            transform.SetAsLastSibling();
        }
        gameObject.SetActive(newState);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    public void UpdateLanguage()
    {
        if (_titleText != null)
        {
            _titleText.text = UIStrings.BuildingCostsOverviewTitle.GetText();
        }
        if (_closeButtonLabel != null)
        {
            _closeButtonLabel.text = UIStrings.Close.GetText();
        }
        if (gameObject.activeSelf)
        {
            RefreshContent();
        }
    }

    private void RefreshContent()
    {
        if (_contentText != null)
        {
            _contentText.text = GetBuildingCostsOverviewText();
        }
    }

    private static string GetBuildingCostsOverviewText()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("• " + UIStrings.ColonyShip.GetText() + ":\n   " + UIStrings.FormatCost(GameRules.ColonyShipCost));
        sb.AppendLine("• " + UIStrings.TradeShip.GetText() + ":\n   " + UIStrings.FormatCost(GameRules.TradeShipCost));
        sb.AppendLine("• " + UIStrings.Spaceport.GetText() + ":\n   " + UIStrings.FormatCost(GameRules.SpaceportCost));
        sb.AppendLine("• " + UIStrings.Booster.GetText() + ":\n   " + UIStrings.FormatCost(GameRules.BoosterCost));
        sb.AppendLine("• " + UIStrings.Cannon.GetText() + ":\n   " + UIStrings.FormatCost(GameRules.CannonCost));
        sb.Append("• " + UIStrings.FreightPod.GetText() + ":\n   " + UIStrings.FormatCost(GameRules.FreightPodCost));
        return sb.ToString();
    }
}

