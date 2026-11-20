using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modal dialog for discarding half of resource cards when a 7 is rolled.
/// Features resource steppers and validates that exact required discard count is chosen.
/// </summary>
public class DiscardCardsDialogUI : MonoBehaviour
{
    private Font _font;
    private Player _player;
    private int _requiredCount;
    private Action _onDiscardComplete;
    private string _customTitle;
    private string _customPrompt;
    private Action<Dictionary<ResourceType, int>> _onDiscardWithSelection;

    private Text _titleText;
    private Text _promptText;
    private Text _countStatusText;
    private Button _confirmButton;
    private Transform _steppersContainer;

    private readonly Dictionary<ResourceType, int> _selectedDiscards = new Dictionary<ResourceType, int>();
    private readonly Dictionary<ResourceType, Text> _stepperValueTexts = new Dictionary<ResourceType, Text>();

    public bool IsActive => gameObject != null && gameObject.activeSelf;
    public IReadOnlyDictionary<ResourceType, int> SelectedDiscards => _selectedDiscards;
    public Player CurrentDiscardPlayer => _player;
    public int RequiredDiscardCount => _requiredCount;

    public bool TrySubmitExternalDiscard(Dictionary<ResourceType, int> discards)
    {
        if (!IsActive || _player == null || discards == null) return false;
        int total = 0;
        foreach (var kvp in discards)
        {
            if (kvp.Value < 0 || _player.GetResourceCount(kvp.Key) < kvp.Value) return false;
            total += kvp.Value;
        }
        if (total != _requiredCount) return false;

        _player.DiscardResources(discards);
        gameObject.SetActive(false);
        _onDiscardWithSelection?.Invoke(new Dictionary<ResourceType, int>(discards));
        _onDiscardComplete?.Invoke();
        return true;
    }

    public static DiscardCardsDialogUI Create(Transform parent, Font font)
    {
        Image background = UIFactory.CreatePanel("DiscardCardsOverlay", parent,
            new Color(0.02f, 0.04f, 0.07f, 0.98f), true);
        GameObject root = background.gameObject;

        RectTransform rect = UIFactory.Rect(root);
        UIFactory.SetAnchors(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 0f));

        Outline outline = root.AddComponent<Outline>();
        outline.effectColor = new Color(0.85f, 0.25f, 0.25f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        DiscardCardsDialogUI dialog = root.AddComponent<DiscardCardsDialogUI>();
        dialog._font = font;

        UIFactory.AddVerticalLayout(root, 10f, 16);
        UIFactory.AddSizeFitter(root);

        // Title
        dialog._titleText = UIFactory.CreateText(root.transform, font,
            UIStrings.SevenRollDiscardTitle.GetText(), 20,
            new Color(1f, 0.35f, 0.35f), TextAnchor.MiddleCenter, FontStyle.Bold);

        // Prompt
        dialog._promptText = UIFactory.CreateText(root.transform, font,
            string.Empty, 13, Color.white, TextAnchor.MiddleCenter);

        // Steppers Container
        GameObject steppersObj = UIFactory.CreateElement("SteppersContainer", root.transform);
        UIFactory.AddVerticalLayout(steppersObj, 6f, 8);
        dialog._steppersContainer = steppersObj.transform;

        // Count status
        dialog._countStatusText = UIFactory.CreateText(root.transform, font,
            string.Empty, 14, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter, FontStyle.Bold);

        // Confirm button
        dialog._confirmButton = UIFactory.CreateButton(root.transform, font,
            UIStrings.SevenRollDiscardConfirm.GetText(), dialog.OnConfirmClicked,
            new Color(0.75f, 0.22f, 0.22f), 13, 36f);

        UIManager.RegisterOverlayCheck(() => dialog != null && dialog.IsActive);
        root.SetActive(false);
        return dialog;
    }

    public void Open(
        Player player,
        int requiredCount,
        Action onComplete,
        string customTitle = null,
        string customPrompt = null,
        Action<Dictionary<ResourceType, int>> onDiscardWithSelection = null)
    {
        _player = player;
        _requiredCount = requiredCount;
        _onDiscardComplete = onComplete;
        _customTitle = customTitle;
        _customPrompt = customPrompt;
        _onDiscardWithSelection = onDiscardWithSelection;
        _selectedDiscards.Clear();
        _stepperValueTexts.Clear();

        if (_titleText != null)
        {
            _titleText.text = !string.IsNullOrEmpty(customTitle)
                ? customTitle
                : UIStrings.SevenRollDiscardTitle.GetText();
        }

        foreach (ResourceType r in GameRules.Resources)
        {
            _selectedDiscards[r] = 0;
        }

        RebuildSteppers();
        UpdateUI();
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
    }

    private void RebuildSteppers()
    {
        foreach (Transform child in _steppersContainer)
        {
            Destroy(child.gameObject);
        }
        _stepperValueTexts.Clear();

        foreach (ResourceType res in GameRules.Resources)
        {
            int inHand = _player != null ? _player.GetResourceCount(res) : 0;
            if (inHand <= 0) continue; // Only show resources player actually owns

            GameObject row = UIFactory.CreateElement("Row_" + res, _steppersContainer);
            UIFactory.AddHorizontalLayout(row, 8f);
            row.AddComponent<LayoutElement>().minHeight = 32f;

            // Resource name & count in hand
            string nameLabel = string.Format("{0} ({1}):", UIStrings.GetResourceName(res), inHand);
            Text label = UIFactory.CreateText(row.transform, _font, nameLabel, 13, Color.white, TextAnchor.MiddleLeft);
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            // Minus button
            ResourceType resCapture = res;
            Button minusBtn = UIFactory.CreateButton(row.transform, _font, "-", () => AdjustDiscard(resCapture, -1),
                new Color(0.2f, 0.25f, 0.3f), 14, 30f);
            minusBtn.GetComponent<LayoutElement>().preferredWidth = 32f;

            // Selected count text
            Text valText = UIFactory.CreateText(row.transform, _font, "0", 14,
                Color.yellow, TextAnchor.MiddleCenter, FontStyle.Bold);
            valText.gameObject.AddComponent<LayoutElement>().minWidth = 32f;
            _stepperValueTexts[res] = valText;

            // Plus button
            Button plusBtn = UIFactory.CreateButton(row.transform, _font, "+", () => AdjustDiscard(resCapture, 1),
                new Color(0.2f, 0.25f, 0.3f), 14, 30f);
            plusBtn.GetComponent<LayoutElement>().preferredWidth = 32f;
        }
    }

    private void AdjustDiscard(ResourceType res, int delta)
    {
        int current = _selectedDiscards[res];
        int inHand = _player != null ? _player.GetResourceCount(res) : 0;
        int currentTotal = GetTotalSelected();

        int next = current + delta;
        if (next < 0) return;
        if (next > inHand) return;
        if (delta > 0 && currentTotal >= _requiredCount) return;

        _selectedDiscards[res] = next;
        if (_stepperValueTexts.TryGetValue(res, out Text txt))
        {
            txt.text = next.ToString();
        }

        UpdateUI();
    }

    private int GetTotalSelected()
    {
        int sum = 0;
        foreach (var kvp in _selectedDiscards)
        {
            sum += kvp.Value;
        }
        return sum;
    }

    private void UpdateUI()
    {
        if (_player == null) return;

        if (_player.Type != Player.PlayerType.LOCAL)
        {
            _promptText.text = $"{_player.PlayerName} wählt Handkarten zum Abwerfen ({_requiredCount} Karten)...";
        }
        else if (!string.IsNullOrEmpty(_customPrompt))
        {
            _promptText.text = _customPrompt;
        }
        else
        {
            _promptText.text = string.Format(UIStrings.SevenRollDiscardPrompt.GetText(),
                _player.PlayerName, _player.TotalResources, _player.DiscardLimitOnSeven, _requiredCount);
        }

        int totalSelected = GetTotalSelected();
        _countStatusText.text = string.Format(UIStrings.SevenRollDiscardCount.GetText(), totalSelected, _requiredCount);

        bool isValid = totalSelected == _requiredCount && _player.Type == Player.PlayerType.LOCAL;
        _confirmButton.interactable = isValid;
        _confirmButton.image.color = isValid
            ? new Color(0.75f, 0.22f, 0.22f, 1f)
            : new Color(0.3f, 0.3f, 0.3f, 0.5f);
    }

    private void OnConfirmClicked()
    {
        if (GetTotalSelected() != _requiredCount || _player == null) return;

        var chosen = new Dictionary<ResourceType, int>(_selectedDiscards);
        _player.DiscardResources(_selectedDiscards);
        gameObject.SetActive(false);
        _onDiscardWithSelection?.Invoke(chosen);
        _onDiscardComplete?.Invoke();
    }
}
