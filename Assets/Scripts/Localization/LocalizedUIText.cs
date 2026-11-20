using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Component attached to a UI Text element to automatically update its text
/// based on the active game language and react to language changes.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Text))]
public class LocalizedUIText : MonoBehaviour
{
    [SerializeField]
    private LocalizedString localizedText;

    private Text uiText;

    public LocalizedString LocalizedText
    {
        get => localizedText;
        set
        {
            localizedText = value;
            RefreshText();
        }
    }

    private void Awake()
    {
        uiText = GetComponent<Text>();
    }

    private void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
        RefreshText();
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
    }

    public void SetText(string english, string german)
    {
        localizedText = new LocalizedString(english, german);
        RefreshText();
    }

    public void SetText(LocalizedString newText)
    {
        localizedText = newText;
        RefreshText();
    }

    public void RefreshText()
    {
        if (uiText == null)
        {
            uiText = GetComponent<Text>();
        }

        if (uiText != null && localizedText != null)
        {
            uiText.text = localizedText.GetText();
        }
    }

    private void HandleLanguageChanged(GameLanguage newLanguage)
    {
        RefreshText();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (uiText == null)
        {
            uiText = GetComponent<Text>();
        }

        if (uiText != null && localizedText != null)
        {
            uiText.text = localizedText.GetText();
        }
    }
#endif
}

