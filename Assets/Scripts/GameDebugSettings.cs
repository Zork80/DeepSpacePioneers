using System;
using UnityEngine;

/// <summary>
/// Central configuration for all debugging, visualization, diagnostics, and board generation switches.
/// Editable directly in the Unity Inspector on the TriMap GameObject or accessible globally via GameDebugSettings.Active.
/// </summary>
[Serializable]
public class GameDebugSettings
{
    /// <summary>
    /// Global reference to the currently active debug settings instance.
    /// </summary>
    public static GameDebugSettings Active { get; internal set; }

    [Header("Board Seed & Randomization")]
    [Tooltip("If checked, uses the fixed seed below for deterministic board generation. If unchecked, a random seed is generated each run.")]
    public bool UseFixedBoardSeed = false;

    [Tooltip("Fixed seed value used when UseFixedBoardSeed is enabled.")]
    public int FixedBoardSeed = 20261004;

    [Tooltip("The actual seed used during the last board generation.")]
    [SerializeField]
    private int lastGeneratedSeed = 20261004;

    public int LastGeneratedSeed
    {
        get => lastGeneratedSeed;
        internal set => lastGeneratedSeed = value;
    }

    [Header("Visual Debug Overlays")]
    [Tooltip("Display coordinate labels (Q, R) on all board intersections.")]
    public bool ShowCoordinateLabels = false;

    [Tooltip("Display debug quadrant and home system markers (*, * * for variable quadrants, α, β, γ, δ for home systems).")]
    public bool ShowSystemDebugLabels = false;

    [Tooltip("Highlight the 9 outer discovery intersections around planetary systems with marker rings.")]
    public bool HighlightPlanetDiscoveryPoints = false;

    [Tooltip("Draw discovery intersection gizmo wire spheres in the Scene view.")]
    public bool DrawDiscoveryGizmos = false;

    [Header("Diagnostics & Logging")]
    [Tooltip("Enable verbose [PlanetDiscovery] diagnostic logs in the Unity console.")]
    public bool PlanetDiscoveryDiagnostics = false;

    [Tooltip("Log player turn transitions and roll summaries to the console.")]
    public bool LogTurnTransitions = true;

    [Header("Test Modes")]
    [Tooltip("Starts a deterministic one-move planet-discovery test instead of the regular founding phase.")]
    public bool PlanetDiscoveryTestMode = false;

    [Header("Player Configuration")]
    [Tooltip("Show the game setup start screen on launch to configure players and seed.")]
    public bool ShowGameSetupOnLaunch = true;

    [Tooltip("If checked, only Player 1 is human (LOCAL) and all other players are automated AI.")]
    public bool SingleHumanPlayerMode = true;

    [Tooltip("Artificial delay in seconds for AI actions to make turns readable.")]
    public float AiActionDelaySeconds = 0.5f;

    [Header("Localization")]
    [Tooltip("Active language for encounter cards and in-game texts.")]
    public GameLanguage Language = GameLanguage.German;

    [Header("Agent Bridge")]
    [Tooltip("Enable agent bridge system (in-memory state observation and network dispatching).")]
    public bool EnableAgentBridge = true;

    [Tooltip("Enable continuous disk file I/O (state.json, action.json, result.json). By default false (in-memory only); automatically enabled via CLI flags like -bridge, -enable-bridge, or -bridge-dir:<path>.")]
    public bool EnableFileBridge = false;

    [Tooltip("Directory path (relative to project root or absolute) for agent bridge communication files.")]
    public string AgentBridgeDirectory = "agent_bridge";

    [Header("Debug Controls")]
    [Tooltip("Enable runtime keyboard shortcuts (F1: toggle coordinates, F2: toggle discovery markers, F3: toggle discovery gizmos, F4: toggle language).")]
    public bool EnableKeyboardShortcuts = true;

    /// <summary>
    /// Resolves the seed to be used for the current board generation.
    /// Returns FixedBoardSeed if UseFixedBoardSeed is true; otherwise generates a new random seed.
    /// </summary>
    public int ResolveBoardSeed(out bool wasFixed)
    {
        wasFixed = UseFixedBoardSeed;
        if (UseFixedBoardSeed)
        {
            lastGeneratedSeed = FixedBoardSeed;
        }
        else
        {
            lastGeneratedSeed = UnityEngine.Random.Range(1, int.MaxValue);
        }
        return lastGeneratedSeed;
    }
}

