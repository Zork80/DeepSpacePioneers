using System;
using UnityEngine;

/// <summary>
/// Configuration data for a single player slot in the game setup screen.
/// </summary>
[Serializable]
public class PlayerSetupSlot
{
    public int PlayerNumber;
    public string Name;
    public Player.PlayerType Type = Player.PlayerType.LOCAL;
    public Player.AiDifficulty Difficulty = Player.AiDifficulty.Normal;
    public bool IsActive = true;
}

/// <summary>
/// Global static container storing game setup and initialization parameters across scene reloads.
/// </summary>
public static class GameSetupConfig
{
    public static bool HasPendingGameStart = false;
    public static bool UseFixedSeed = false;
    public static int Seed = 20261004;
    public static int ActivePlayerCount = 4;
    public static bool ShowSystemDebugLabels = false;

    public static PlayerSetupSlot[] Slots = new PlayerSetupSlot[4]
    {
        new PlayerSetupSlot { PlayerNumber = 1, Name = "Spieler 1", Type = Player.PlayerType.LOCAL, Difficulty = Player.AiDifficulty.Normal, IsActive = true },
        new PlayerSetupSlot { PlayerNumber = 2, Name = "Spieler 2", Type = Player.PlayerType.AI, Difficulty = Player.AiDifficulty.Normal, IsActive = true },
        new PlayerSetupSlot { PlayerNumber = 3, Name = "Spieler 3", Type = Player.PlayerType.AI, Difficulty = Player.AiDifficulty.Normal, IsActive = true },
        new PlayerSetupSlot { PlayerNumber = 4, Name = "Spieler 4", Type = Player.PlayerType.AI, Difficulty = Player.AiDifficulty.Normal, IsActive = true },
    };

    public static void ResetToDefaults()
    {
        HasPendingGameStart = false;
        UseFixedSeed = false;
        Seed = UnityEngine.Random.Range(1, int.MaxValue);
        ActivePlayerCount = 4;
        ShowSystemDebugLabels = false;
        Slots[0] = new PlayerSetupSlot { PlayerNumber = 1, Name = "Spieler 1", Type = Player.PlayerType.LOCAL, Difficulty = Player.AiDifficulty.Normal, IsActive = true };
        Slots[1] = new PlayerSetupSlot { PlayerNumber = 2, Name = "Spieler 2", Type = Player.PlayerType.AI, Difficulty = Player.AiDifficulty.Normal, IsActive = true };
        Slots[2] = new PlayerSetupSlot { PlayerNumber = 3, Name = "Spieler 3", Type = Player.PlayerType.AI, Difficulty = Player.AiDifficulty.Normal, IsActive = true };
        Slots[3] = new PlayerSetupSlot { PlayerNumber = 4, Name = "Spieler 4", Type = Player.PlayerType.AI, Difficulty = Player.AiDifficulty.Normal, IsActive = true };
    }
}

