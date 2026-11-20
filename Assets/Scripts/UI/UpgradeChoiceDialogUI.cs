using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modal dialog in which the active player picks which flagship upgrade
/// (booster, cannon or freight pod) is removed, e.g. by an encounter card.
/// </summary>
public class UpgradeChoiceDialogUI : MonoBehaviour
{
    private Font _font;
    private Action<FlagshipUpgrade> _onChosen;

    private Text _titleText;
    private Text _promptText;
    private Transform _buttonsContainer;
    private Outline _outline;

    public bool IsActive => gameObject != null && gameObject.activeSelf;

    public static UpgradeChoiceDialogUI Create(Transform parent, Font font)
    {
        Image background = UIFactory.CreatePanel("UpgradeChoiceOverlay", parent,
            new Color(0.02f, 0.04f, 0.08f, 0.98f), true);
        GameObject root = background.gameObject;

        UIFactory.SetAnchors(UIFactory.Rect(root), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 0f));

        Outline outline = root.AddComponent<Outline>();
        outline.effectColor = new Color(0.9f, 0.35f, 0.25f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        UpgradeChoiceDialogUI dialog = root.AddComponent<UpgradeChoiceDialogUI>();
        dialog._font = font;
        dialog._outline = outline;

        UIFactory.AddVerticalLayout(root, 12f, 16);
        UIFactory.AddSizeFitter(root);

        dialog._titleText = UIFactory.CreateText(root.transform, font, string.Empty, 20,
            new Color(1f, 0.55f, 0.35f), TextAnchor.MiddleCenter, FontStyle.Bold);
        dialog._promptText = UIFactory.CreateText(root.transform, font, string.Empty, 13,
            Color.white, TextAnchor.MiddleCenter);

        GameObject btnContainer = UIFactory.CreateElement("ButtonsContainer", root.transform);
        UIFactory.AddVerticalLayout(btnContainer, 8f, 4);
        dialog._buttonsContainer = btnContainer.transform;

        UIManager.RegisterOverlayCheck(() => dialog != null && dialog.IsActive);
        root.SetActive(false);
        return dialog;
    }

    /// <summary>Opens the dialog with one button per upgrade type the player owns.</summary>
    public void Open(Player player, Action<FlagshipUpgrade> onChosen)
    {
        _onChosen = onChosen;
        _titleText.text = UIStrings.EncounterLoseUpgradeTitle.GetText();
        _titleText.color = new Color(1f, 0.55f, 0.35f);
        _promptText.text = string.Format(UIStrings.EncounterLoseUpgradePrompt.GetText(), player.PlayerName);

        if (_outline != null)
        {
            _outline.effectColor = new Color(0.9f, 0.35f, 0.25f, 0.85f);
        }

        foreach (Transform child in _buttonsContainer)
        {
            Destroy(child.gameObject);
        }

        AddOption(FlagshipUpgrade.Drive, player.FlagshipDriveCount);
        AddOption(FlagshipUpgrade.Cannon, player.FlagshipCannonCount);
        AddOption(FlagshipUpgrade.CargoModule, player.FlagshipCargoModuleCount);

        transform.SetAsLastSibling();
        gameObject.SetActive(true);
    }

    /// <summary>Opens the dialog with one button per upgrade type the player can gain/mount.</summary>
    public void OpenForGain(Player player, Action<FlagshipUpgrade> onChosen)
    {
        _onChosen = onChosen;
        _titleText.text = UIStrings.EncounterGainUpgradeTitle.GetText();
        _titleText.color = new Color(0.35f, 0.85f, 1f);
        _promptText.text = string.Format(UIStrings.EncounterGainUpgradePrompt.GetText(), player.PlayerName);

        if (_outline != null)
        {
            _outline.effectColor = new Color(0.2f, 0.6f, 0.95f, 0.85f);
        }

        foreach (Transform child in _buttonsContainer)
        {
            Destroy(child.gameObject);
        }

        AddGainOption(FlagshipUpgrade.Drive, player.AvailableBoostersCount);
        AddGainOption(FlagshipUpgrade.Cannon, player.AvailableCannonsCount);
        AddGainOption(FlagshipUpgrade.CargoModule, player.AvailableFreightPodsCount);

        transform.SetAsLastSibling();
        gameObject.SetActive(true);
    }

    private void AddGainOption(FlagshipUpgrade upgrade, int count)
    {
        if (count <= 0) return;

        string label = string.Format(UIStrings.EncounterGainUpgradeOption.GetText(),
            UIStrings.GetUpgradeSingularName(upgrade), count);
        UIFactory.CreateButton(_buttonsContainer, _font, label,
            () => Choose(upgrade), new Color(0.18f, 0.42f, 0.65f, 0.95f), 13, 36f);
    }

    private void AddOption(FlagshipUpgrade upgrade, int count)
    {
        if (count <= 0) return;

        string label = string.Format(UIStrings.EncounterLoseUpgradeOption.GetText(),
            UIStrings.GetUpgradeSingularName(upgrade), count);
        UIFactory.CreateButton(_buttonsContainer, _font, label,
            () => Choose(upgrade), new Color(0.45f, 0.18f, 0.15f, 0.95f), 13, 36f);
    }

    private void Choose(FlagshipUpgrade upgrade)
    {
        gameObject.SetActive(false);
        _onChosen?.Invoke(upgrade);
    }
}

