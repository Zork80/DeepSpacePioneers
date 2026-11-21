using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modal dialog (opened via ESC or Quit) allowing the player to change language or quit the application.
/// </summary>
public class ConfirmQuitDialogUI : MonoBehaviour
{
    private Image _dimmer;
    private Text _titleText;
    private Text _promptText;
    private Text _languageBtnText;
    private Text _quitBtnText;
    private Text _cancelBtnText;

    public bool IsActive => gameObject != null && gameObject.activeSelf;

    /// <summary>Builds the dialog under <paramref name="parent"/> (hidden initially).</summary>
    public static ConfirmQuitDialogUI Create(Transform parent, Font font)
    {
        // Fullscreen dark dimmer
        Image dimmer = UIFactory.CreatePanel("ConfirmQuitModal", parent,
            new Color(0.015f, 0.045f, 0.08f, 0.98f), true);
        GameObject root = dimmer.gameObject;
        UIFactory.SetAnchors(UIFactory.Rect(root), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440f, 0f));

        ConfirmQuitDialogUI dialog = root.AddComponent<ConfirmQuitDialogUI>();
        dialog._dimmer = dimmer;

        Outline border = root.AddComponent<Outline>();
        border.effectColor = new Color(0.95f, 0.35f, 0.25f, 0.95f);
        border.effectDistance = new Vector2(2f, -2f);

        UIFactory.AddVerticalLayout(root, 14f, 20);
        UIFactory.AddSizeFitter(root);

        dialog._titleText = UIFactory.CreateText(root.transform, font, UIStrings.QuitGame.GetText(), 18,
            new Color(1f, 0.82f, 0.3f), TextAnchor.MiddleCenter, FontStyle.Bold);

        dialog._promptText = UIFactory.CreateText(root.transform, font, UIStrings.ConfirmQuitPrompt.GetText(), 13,
            new Color(0.92f, 0.95f, 1f), TextAnchor.MiddleCenter);
        dialog._promptText.gameObject.AddComponent<LayoutElement>().minHeight = 40f;

        Button langBtn = UIFactory.CreateButton(root.transform, font, UIStrings.LanguageButton.GetText(),
            () => LocalizationManager.ToggleLanguage(), new Color(0.14f, 0.28f, 0.44f, 0.95f), 12, 34f);
        dialog._languageBtnText = langBtn.GetComponentInChildren<Text>();

        GameObject row = UIFactory.CreateElement("ButtonRow", root.transform);
        UIFactory.AddHorizontalLayout(row, 20f);
        row.AddComponent<LayoutElement>().minHeight = 38f;

        Button quitBtn = UIFactory.CreateButton(row.transform, font, UIStrings.Quit.GetText(),
            QuitApplication, new Color(0.7f, 0.22f, 0.22f, 1f), 13, 38f);
        dialog._quitBtnText = quitBtn.GetComponentInChildren<Text>();

        Button cancelBtn = UIFactory.CreateButton(row.transform, font, UIStrings.Cancel.GetText(),
            dialog.Hide, new Color(0.15f, 0.35f, 0.55f, 1f), 13, 38f);
        dialog._cancelBtnText = cancelBtn.GetComponentInChildren<Text>();

        root.SetActive(false);
        return dialog;
    }

    private void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
        RefreshLanguage();
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
    }

    private void HandleLanguageChanged(GameLanguage language)
    {
        RefreshLanguage();
    }

    public void RefreshLanguage()
    {
        if (_titleText != null) _titleText.text = UIStrings.QuitGame.GetText();
        if (_promptText != null) _promptText.text = UIStrings.ConfirmQuitPrompt.GetText();
        if (_languageBtnText != null) _languageBtnText.text = UIStrings.LanguageButton.GetText();
        if (_quitBtnText != null) _quitBtnText.text = UIStrings.Quit.GetText();
        if (_cancelBtnText != null) _cancelBtnText.text = UIStrings.Cancel.GetText();
    }

    public void Show()
    {
        if (_dimmer != null) _dimmer.raycastTarget = true;
        transform.SetAsLastSibling();
        RefreshLanguage();
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (_dimmer != null) _dimmer.raycastTarget = false;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Exits play mode in the Unity Editor or terminates the application in standalone builds.
    /// </summary>
    public static void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
