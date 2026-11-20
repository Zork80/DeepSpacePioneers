using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modal dialog allowing the active player to choose an opponent to steal a hidden resource card from.
/// </summary>
public class StealCardDialogUI : MonoBehaviour
{
    private Font _font;
    private Action<Player> _onTargetChosen;

    private Text _titleText;
    private Text _promptText;
    private Transform _buttonsContainer;
    private Button _dismissButton;

    public bool IsActive => gameObject != null && gameObject.activeSelf;
    private List<Player> _candidateTargets;
    public IReadOnlyList<Player> CandidateTargets => _candidateTargets != null ? _candidateTargets.AsReadOnly() : null;

    public bool TrySelectTarget(Player target)
    {
        if (!IsActive) return false;
        CloseWithTarget(target);
        return true;
    }

    public static StealCardDialogUI Create(Transform parent, Font font)
    {
        Image background = UIFactory.CreatePanel("StealCardOverlay", parent,
            new Color(0.02f, 0.04f, 0.08f, 0.98f), true);
        GameObject root = background.gameObject;

        RectTransform rect = UIFactory.Rect(root);
        UIFactory.SetAnchors(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 0f));

        Outline outline = root.AddComponent<Outline>();
        outline.effectColor = new Color(0.9f, 0.6f, 0.2f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        StealCardDialogUI dialog = root.AddComponent<StealCardDialogUI>();
        dialog._font = font;

        UIFactory.AddVerticalLayout(root, 12f, 16);
        UIFactory.AddSizeFitter(root);

        // Title
        dialog._titleText = UIFactory.CreateText(root.transform, font,
            UIStrings.SevenRollStealTitle.GetText(), 20,
            new Color(1f, 0.75f, 0.25f), TextAnchor.MiddleCenter, FontStyle.Bold);

        // Prompt
        dialog._promptText = UIFactory.CreateText(root.transform, font,
            UIStrings.SevenRollStealPrompt.GetText(), 13, Color.white, TextAnchor.MiddleCenter);

        // Buttons Container
        GameObject btnContainer = UIFactory.CreateElement("ButtonsContainer", root.transform);
        UIFactory.AddVerticalLayout(btnContainer, 8f, 4);
        dialog._buttonsContainer = btnContainer.transform;

        // Dismiss button (shown when no targets available)
        dialog._dismissButton = UIFactory.CreateButton(root.transform, font, "OK",
            () => dialog.CloseWithTarget(null),
            new Color(0.25f, 0.4f, 0.6f), 13, 34f);
        dialog._dismissButton.gameObject.SetActive(false);

        UIManager.RegisterOverlayCheck(() => dialog != null && dialog.IsActive);
        root.SetActive(false);
        return dialog;
    }

    public void Open(Player activePlayer, List<Player> candidateTargets, Action<Player> onTargetChosen)
    {
        _candidateTargets = candidateTargets;
        _onTargetChosen = onTargetChosen;

        foreach (Transform child in _buttonsContainer)
        {
            Destroy(child.gameObject);
        }

        if (candidateTargets == null || candidateTargets.Count == 0)
        {
            _promptText.text = UIStrings.SevenRollStealNoTargets.GetText();
            _dismissButton.gameObject.SetActive(true);
        }
        else
        {
            if (activePlayer != null && activePlayer.Type != Player.PlayerType.LOCAL)
            {
                _promptText.text = $"{activePlayer.PlayerName} wählt einen Mitspieler zum Stehlen...";
                _dismissButton.gameObject.SetActive(false);
            }
            else
            {
                _promptText.text = UIStrings.SevenRollStealPrompt.GetText();
                _dismissButton.gameObject.SetActive(false);

                foreach (Player target in candidateTargets)
                {
                    Player targetCapture = target;
                    Color btnBg = target.PlayerColor * 0.45f;
                    btnBg.a = 0.95f;

                    string label = string.Format("{0} ({1})",
                        target.PlayerName,
                        string.Format(UIStrings.SevenRollHandCardsCount.GetText(), target.TotalResources));

                    UIFactory.CreateButton(_buttonsContainer, _font, label,
                        () => CloseWithTarget(targetCapture),
                        btnBg, 13, 36f);
                }
            }
        }

        gameObject.SetActive(true);
    }

    private void CloseWithTarget(Player target)
    {
        gameObject.SetActive(false);
        _onTargetChosen?.Invoke(target);
    }
}

