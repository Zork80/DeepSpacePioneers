using System;
using UnityEngine;

/// <summary>
/// Serializable localizable string supporting English and German.
/// Automatically resolves to the active language via LocalizationManager.
/// </summary>
[Serializable]
public class LocalizedString
{
    [TextArea(1, 5)]
    public string English;

    [TextArea(1, 5)]
    public string German;

    public LocalizedString()
    {
        English = string.Empty;
        German = string.Empty;
    }

    public LocalizedString(string english, string german)
    {
        English = english ?? string.Empty;
        German = german ?? string.Empty;
    }

    /// <summary>
    /// Gets the text in the requested language, with fallback to the alternative if empty.
    /// </summary>
    public string GetText(GameLanguage language)
    {
        if (language == GameLanguage.German)
        {
            return !string.IsNullOrEmpty(German) ? German : (English ?? string.Empty);
        }
        return !string.IsNullOrEmpty(English) ? English : (German ?? string.Empty);
    }

    /// <summary>
    /// Gets the text in the currently active global language.
    /// </summary>
    public string GetText()
    {
        return GetText(LocalizationManager.CurrentLanguage);
    }

    public override string ToString()
    {
        return GetText();
    }

    public static implicit operator string(LocalizedString localized)
    {
        return localized != null ? localized.GetText() : string.Empty;
    }
}

