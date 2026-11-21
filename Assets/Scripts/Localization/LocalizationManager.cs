using System;
using UnityEngine;

/// <summary>
/// Static manager tracking the current active game language, persisting user preference,
/// and broadcasting language change events.
/// </summary>
public static class LocalizationManager
{
    private const string LanguagePrefKey = "DeepSpacePioneers_Language";
    private static GameLanguage currentLanguage = GameLanguage.German;

    public static GameLanguage CurrentLanguage
    {
        get => currentLanguage;
        set => SetLanguage(value);
    }

    public static event Action<GameLanguage> OnLanguageChanged;

    public static bool HasSavedLanguage()
    {
        return PlayerPrefs.HasKey(LanguagePrefKey);
    }

    public static GameLanguage LoadSavedLanguage(GameLanguage fallback)
    {
        if (!PlayerPrefs.HasKey(LanguagePrefKey))
        {
            return fallback;
        }

        int saved = PlayerPrefs.GetInt(LanguagePrefKey, (int)fallback);
        return Enum.IsDefined(typeof(GameLanguage), saved) ? (GameLanguage)saved : fallback;
    }

    public static void SetLanguage(GameLanguage language)
    {
        if (GameDebugSettings.Active != null)
        {
            GameDebugSettings.Active.Language = language;
        }

        PlayerPrefs.SetInt(LanguagePrefKey, (int)language);
        PlayerPrefs.Save();

        if (currentLanguage != language)
        {
            currentLanguage = language;
            OnLanguageChanged?.Invoke(language);
        }
    }

    public static void ToggleLanguage()
    {
        GameLanguage next = currentLanguage == GameLanguage.English
            ? GameLanguage.German
            : GameLanguage.English;
        SetLanguage(next);
    }
}
