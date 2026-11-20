using System;

/// <summary>
/// Static manager tracking the current active game language and broadcasting language change events.
/// </summary>
public static class LocalizationManager
{
    private static GameLanguage currentLanguage = GameLanguage.German;

    public static GameLanguage CurrentLanguage
    {
        get => currentLanguage;
        set => SetLanguage(value);
    }

    public static event Action<GameLanguage> OnLanguageChanged;

    public static void SetLanguage(GameLanguage language)
    {
        if (currentLanguage != language)
        {
            currentLanguage = language;
            OnLanguageChanged?.Invoke(language);
        }
    }
}

