using System;
using System.Collections.Generic;

public enum ResourceType
{
    None,
    Ore,
    Fuel,
    Carbon,
    Food,
    TradeGoods
}

public enum GameTurnPhase
{
    Production,
    TradeAndBuild,
    Flight
}

public enum FoundingPhase
{
    RollForStartingPlayer,
    PlaceFirstColony,
    PlaceSecondColony,
    PlaceThirdColony,
    SelectSpaceport,
    SelectStartingShip,
    SelectStartingShipPoint,
    SelectFlagshipUpgrade,
    Complete
}

public enum StarshipType
{
    ColonyShip,
    TradeShip
}

public enum FlagshipUpgrade
{
    Drive,
    Cannon,
    CargoModule
}

public sealed class ProductionRollResult
{
    public int FirstDie { get; private set; }
    public int SecondDie { get; private set; }
    public int Total { get; private set; }

    public ProductionRollResult(int firstDie, int secondDie)
    {
        if (firstDie < 1 || firstDie > 6)
        {
            throw new ArgumentOutOfRangeException("firstDie");
        }
        if (secondDie < 1 || secondDie > 6)
        {
            throw new ArgumentOutOfRangeException("secondDie");
        }

        FirstDie = firstDie;
        SecondDie = secondDie;
        Total = firstDie + secondDie;
    }
}

public enum FlightImpulseCore
{
    Black,
    Blue,
    Yellow,
    Red
}

public class FlagshipRollResult
{
    public FlightImpulseCore FirstCore { get; private set; }
    public FlightImpulseCore SecondCore { get; private set; }
    public int BaseSpeed { get; private set; }
    public int DriveCount { get; private set; }
    public int Speed { get; private set; }
    public bool RequiresEncounter { get; private set; }

    public FlagshipRollResult(
        FlightImpulseCore firstCore,
        FlightImpulseCore secondCore,
        int driveCount)
    {
        if (driveCount < 0)
        {
            throw new ArgumentOutOfRangeException("driveCount");
        }
        if (firstCore == secondCore && firstCore != FlightImpulseCore.Yellow)
        {
            throw new ArgumentException("Only yellow flight impulse cores have two copies.");
        }

        int firstValue = GetCoreValue(firstCore);
        int secondValue = GetCoreValue(secondCore);
        FirstCore = firstCore;
        SecondCore = secondCore;
        DriveCount = driveCount;
        RequiresEncounter = firstCore == FlightImpulseCore.Black ||
            secondCore == FlightImpulseCore.Black;
        BaseSpeed = RequiresEncounter
            ? 3
            : firstValue + secondValue;
        Speed = BaseSpeed + DriveCount;
    }

    private static int GetCoreValue(FlightImpulseCore core)
    {
        switch (core)
        {
            case FlightImpulseCore.Black:
                return 0;
            case FlightImpulseCore.Blue:
                return 1;
            case FlightImpulseCore.Yellow:
                return 2;
            case FlightImpulseCore.Red:
                return 3;
            default:
                throw new ArgumentOutOfRangeException("core");
        }
    }
}

public enum EncounterCardType
{
    TraderGift,
    TraderMarauder,
    MarauderDemand,
    MarauderTrade,
    MarauderRaidOffer,
    MarauderAttack,
    Rescue,
    SpaceDistortion,
    Drifter,
    Event
}

public enum EncounterCardPhase
{
    ReadPrompt,
    SelectOutcome,
    ApplyOutcome
}

public enum EncounterInteractionType
{
    Donation,
    Choice,
    Event
}

public enum EncounterOpponentRole
{
    None,
    Right,
    Left,
    SecondRight,
    SecondLeft
}

public enum EncounterComparisonType
{
    None,
    Combat,
    Speed,
    FlagshipRoll
}

[System.Serializable]
public sealed class EncounterOutcomeEffect
{
    public int HalfMedals { get; set; }
    public string ResourceReward { get; set; }
    public string ResourceCost { get; set; }
    public bool BlockOneShip { get; set; }
    public bool LoseUpgrade { get; set; }
    public bool GainUpgrade { get; set; }
    public bool GainTradeShip { get; set; }
    public bool SpaceJump { get; set; }
    public bool ReshuffleDeck { get; set; }
    public bool RedrawCard { get; set; }
}

public sealed class EncounterCardOutcome
{
    private readonly string rawCondition;
    private readonly string rawEffectSummary;

    public LocalizedString LocalizedCondition { get; private set; }
    public LocalizedString LocalizedEffectSummary { get; private set; }
    public EncounterOutcomeEffect Effect { get; private set; }

    public string Condition
    {
        get => LocalizedCondition != null ? LocalizedCondition.GetText() : rawCondition;
    }

    public string EffectSummary
    {
        get => LocalizedEffectSummary != null ? LocalizedEffectSummary.GetText() : rawEffectSummary;
    }

    public EncounterCardOutcome(string condition, string effectSummary)
        : this(condition, effectSummary, new LocalizedString(condition, condition), new LocalizedString(effectSummary, effectSummary), null)
    {
    }

    public EncounterCardOutcome(
        string condition,
        string effectSummary,
        LocalizedString localizedCondition,
        LocalizedString localizedEffectSummary,
        EncounterOutcomeEffect effect = null)
    {
        rawCondition = condition ?? string.Empty;
        rawEffectSummary = effectSummary ?? string.Empty;
        LocalizedCondition = localizedCondition ?? new LocalizedString(rawCondition, rawCondition);
        LocalizedEffectSummary = localizedEffectSummary ?? new LocalizedString(rawEffectSummary, rawEffectSummary);
        Effect = effect ?? new EncounterOutcomeEffect();
    }
}

public sealed class EncounterCard
{
    private readonly EncounterCardOutcome[] outcomes;
    private readonly string rawSituation;
    private readonly string rawPlayerChoice;

    public int Number { get; private set; }
    public EncounterCardType Type { get; private set; }
    public int CharacterIndex { get; private set; }
    public LocalizedString Title { get; private set; }
    public EncounterInteractionType InteractionType { get; private set; }
    public EncounterOpponentRole OpponentRole { get; private set; }
    public EncounterComparisonType ComparisonType { get; private set; }

    public LocalizedString LocalizedSituation { get; private set; }
    public LocalizedString LocalizedPlayerChoice { get; private set; }

    public string Situation
    {
        get => LocalizedSituation != null ? LocalizedSituation.GetText() : rawSituation;
    }

    public string PlayerChoice
    {
        get => LocalizedPlayerChoice != null ? LocalizedPlayerChoice.GetText() : rawPlayerChoice;
    }

    public bool RefreshesEncounterDeck { get; private set; }
    public IList<EncounterCardOutcome> Outcomes { get; private set; }

    public EncounterCard(
        int number,
        EncounterCardType type,
        string situation,
        string playerChoice,
        params EncounterCardOutcome[] outcomes)
        : this(number, type, situation, playerChoice,
               new LocalizedString(situation, situation),
               new LocalizedString(playerChoice, playerChoice),
               null,
               EncounterInteractionType.Choice,
               EncounterOpponentRole.None,
               EncounterComparisonType.None,
               0,
               outcomes)
    {
    }

    public EncounterCard(
        int number,
        EncounterCardType type,
        string situation,
        string playerChoice,
        LocalizedString localizedSituation,
        LocalizedString localizedPlayerChoice,
        params EncounterCardOutcome[] outcomes)
        : this(number, type, situation, playerChoice,
               localizedSituation,
               localizedPlayerChoice,
               null,
               EncounterInteractionType.Choice,
               EncounterOpponentRole.None,
               EncounterComparisonType.None,
               0,
               outcomes)
    {
    }

    public EncounterCard(
        int number,
        EncounterCardType type,
        string situation,
        string playerChoice,
        LocalizedString localizedSituation,
        LocalizedString localizedPlayerChoice,
        LocalizedString localizedTitle,
        EncounterInteractionType interactionType,
        EncounterOpponentRole opponentRole,
        EncounterComparisonType comparisonType,
        params EncounterCardOutcome[] outcomes)
        : this(number, type, situation, playerChoice,
               localizedSituation,
               localizedPlayerChoice,
               localizedTitle,
               interactionType,
               opponentRole,
               comparisonType,
               0,
               outcomes)
    {
    }

    public EncounterCard(
        int number,
        EncounterCardType type,
        string situation,
        string playerChoice,
        LocalizedString localizedSituation,
        LocalizedString localizedPlayerChoice,
        LocalizedString localizedTitle,
        EncounterInteractionType interactionType,
        EncounterOpponentRole opponentRole,
        EncounterComparisonType comparisonType,
        int characterIndex,
        params EncounterCardOutcome[] outcomes)
    {
        if (number < 1 || number > 32)
        {
            throw new ArgumentOutOfRangeException("number");
        }
        if (string.IsNullOrEmpty(situation) && (localizedSituation == null || string.IsNullOrEmpty(localizedSituation.GetText())))
        {
            throw new ArgumentException("An encounter card needs a situation.", "situation");
        }
        if (outcomes == null || outcomes.Length == 0)
        {
            throw new ArgumentException("An encounter card needs at least one outcome.", "outcomes");
        }

        Number = number;
        Type = type;
        CharacterIndex = characterIndex;
        Title = localizedTitle ?? new LocalizedString($"Card {number}", $"Karte {number}");
        InteractionType = interactionType;
        OpponentRole = opponentRole;
        ComparisonType = comparisonType;
        rawSituation = situation ?? string.Empty;
        rawPlayerChoice = playerChoice ?? string.Empty;
        LocalizedSituation = localizedSituation ?? new LocalizedString(rawSituation, rawSituation);
        LocalizedPlayerChoice = localizedPlayerChoice ?? new LocalizedString(rawPlayerChoice, rawPlayerChoice);
        RefreshesEncounterDeck = type == EncounterCardType.Event;
        this.outcomes = (EncounterCardOutcome[])outcomes.Clone();
        Outcomes = Array.AsReadOnly(this.outcomes);
    }
}

public sealed class EncounterCardDeck
{
    private readonly List<EncounterCard> drawPile;
    private readonly List<EncounterCard> discardPile;
    private readonly System.Random random;

    public EncounterCard CurrentCard { get; private set; }
    public int RemainingCount
    {
        get { return drawPile.Count; }
    }

    public EncounterCardDeck(IList<EncounterCard> cards, int seed)
    {
        if (cards == null || cards.Count == 0)
        {
            throw new ArgumentException("The encounter deck must contain cards.", "cards");
        }

        random = new System.Random(seed);
        drawPile = new List<EncounterCard>(cards);
        discardPile = new List<EncounterCard>();
        Shuffle(drawPile);
    }

    public EncounterCard DrawNext()
    {
        if (CurrentCard != null)
        {
            throw new InvalidOperationException("Resolve the current encounter card before drawing another.");
        }
        if (drawPile.Count == 0)
        {
            if (discardPile.Count == 0)
            {
                throw new InvalidOperationException("There are no encounter cards available to draw.");
            }

            drawPile.AddRange(discardPile);
            discardPile.Clear();
            Shuffle(drawPile);
        }

        int cardIndex = drawPile.Count - 1;
        CurrentCard = drawPile[cardIndex];
        drawPile.RemoveAt(cardIndex);
        return CurrentCard;
    }

    public void ForceCurrentCard(EncounterCard card)
    {
        if (CurrentCard != null)
        {
            discardPile.Add(CurrentCard);
        }

        CurrentCard = card;
        if (card != null)
        {
            drawPile.Remove(card);
            discardPile.Remove(card);
        }
    }

    public EncounterCard CompleteCurrentCard()
    {
        if (CurrentCard == null)
        {
            throw new InvalidOperationException("There is no encounter card to complete.");
        }

        EncounterCard completedCard = CurrentCard;
        CurrentCard = null;
        if (completedCard.RefreshesEncounterDeck)
        {
            drawPile.Clear();
            discardPile.Clear();
            drawPile.AddRange(EncounterCardCatalog.Cards);
            Shuffle(drawPile);
            return DrawNext();
        }

        discardPile.Add(completedCard);
        return null;
    }

    private void Shuffle(List<EncounterCard> cardsToShuffle)
    {
        for (int i = cardsToShuffle.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            EncounterCard card = cardsToShuffle[i];
            cardsToShuffle[i] = cardsToShuffle[swapIndex];
            cardsToShuffle[swapIndex] = card;
        }
    }
}

public static class EncounterCardCatalog
{
    private static IList<EncounterCard> activeCards;

    public static IList<EncounterCard> DefaultCards => Cards;

    public static IList<EncounterCard> Cards
    {
        get
        {
            if (activeCards == null || activeCards.Count == 0)
            {
                activeCards = EncounterCardDataLoader.LoadCards();
            }
            return activeCards;
        }
        set
        {
            activeCards = value;
        }
    }

    public static EncounterCard GetByNumber(int number)
    {
        IList<EncounterCard> currentCards = Cards;
        if (number < 1 || number > currentCards.Count)
        {
            throw new ArgumentOutOfRangeException("number");
        }

        EncounterCard card = currentCards[number - 1];
        if (card.Number != number)
        {
            foreach (var c in currentCards)
            {
                if (c.Number == number) return c;
            }
            throw new InvalidOperationException("Encounter card #" + number + " could not be found.");
        }

        return card;
    }
}

public static class GameRules
{
    private static readonly ResourceType[] ResourceTypes =
    {
        ResourceType.Ore,
        ResourceType.Fuel,
        ResourceType.Carbon,
        ResourceType.Food,
        ResourceType.TradeGoods
    };

    public static IList<ResourceType> Resources
    {
        get { return Array.AsReadOnly(ResourceTypes); }
    }

    public static bool IsResource(ResourceType resource)
    {
        for (int i = 0; i < ResourceTypes.Length; i++)
        {
            if (ResourceTypes[i] == resource)
            {
                return true;
            }
        }

        return false;
    }

    // --- Building Costs ---
    public static readonly Dictionary<ResourceType, int> ColonyShipCost = new Dictionary<ResourceType, int>
    {
        { ResourceType.Ore, 1 },
        { ResourceType.Fuel, 1 },
        { ResourceType.Carbon, 1 },
        { ResourceType.Food, 1 }
    };

    public static readonly Dictionary<ResourceType, int> TradeShipCost = new Dictionary<ResourceType, int>
    {
        { ResourceType.Ore, 1 },
        { ResourceType.Fuel, 1 },
        { ResourceType.TradeGoods, 2 }
    };

    public static readonly Dictionary<ResourceType, int> BoosterCost = new Dictionary<ResourceType, int>
    {
        { ResourceType.Fuel, 2 }
    };

    public static readonly Dictionary<ResourceType, int> CannonCost = new Dictionary<ResourceType, int>
    {
        { ResourceType.Carbon, 2 }
    };

    public static readonly Dictionary<ResourceType, int> FreightPodCost = new Dictionary<ResourceType, int>
    {
        { ResourceType.Ore, 2 }
    };

    public static readonly Dictionary<ResourceType, int> SpaceportCost = new Dictionary<ResourceType, int>
    {
        { ResourceType.Carbon, 3 },
        { ResourceType.Food, 2 }
    };

    // --- Component Supply Limits per Player (Game rules & material) ---
    public const int MaxTransportShipsPerPlayer = 3;
    public const int MaxColoniesPerPlayer = 9;
    public const int MaxSpaceportsPerPlayer = 3;
    public const int MaxTradeStationsPerPlayer = 7;
    public const int MaxBoostersPerPlayer = 6;
    public const int MaxCannonsPerPlayer = 6;
    public const int MaxFreightPodsPerPlayer = 5;

    public static IDictionary<ResourceType, int> GetUpgradeCost(FlagshipUpgrade upgrade)
    {
        switch (upgrade)
        {
            case FlagshipUpgrade.Drive:
                return BoosterCost;
            case FlagshipUpgrade.Cannon:
                return CannonCost;
            case FlagshipUpgrade.CargoModule:
                return FreightPodCost;
            default:
                throw new ArgumentOutOfRangeException("upgrade", upgrade, "Unknown flagship upgrade.");
        }
    }

    public static int GetSupplyTradeRate(ResourceType offered)
    {
        return offered == ResourceType.TradeGoods ? 2 : 3;
    }
}

public enum GameBoardVariant
{
    Legacy,
    Revised
}

public sealed class GameBoardSpace
{
    private readonly ResourceType[] planetResources;

    public int Q { get; private set; }
    public int R { get; private set; }
    public bool IsPassable { get; private set; }
    public SystemTypes SystemType { get; private set; }
    public SystemKind SystemKind { get; private set; }
    public bool IsColonyPoint { get; private set; }
    public bool IsColonySite => IsColonyPoint;
    public bool IsTradePoint { get; private set; }
    public bool IsDockingPoint => IsTradePoint;
    public int QuadrantStarCount { get; private set; }
    public IList<ResourceType> PlanetResources { get; private set; }
    public IList<int> PlanetProductionNumbers { get; private set; }
    public bool ArePlanetProductionNumbersRevealed { get; private set; }

    internal GameBoardSpace(Tri tri)
    {
        Q = tri.Q;
        R = tri.R;
        IsPassable = tri.IsPassable;
        SystemType = tri.SystemType;
        SystemKind = tri.SystemKind;
        IsColonyPoint = tri.IsColonyPoint;
        IsTradePoint = tri.IsTradePoint;
        QuadrantStarCount = SystemType == SystemTypes.OneStar ? 1 :
            SystemType == SystemTypes.TwoStars ? 2 : 0;
        planetResources = tri.PlanetResources;
        PlanetResources = Array.AsReadOnly(planetResources);
        PlanetProductionNumbers = Array.AsReadOnly(tri.PlanetProductionNumbers);
        ArePlanetProductionNumbersRevealed = tri.ArePlanetProductionNumbersRevealed;
    }

    internal void RevealPlanetProductionNumbers()
    {
        ArePlanetProductionNumbersRevealed = true;
    }
}

public sealed class GameBoardModel
{
    private readonly GameBoardSpace[] spaces;
    private readonly Dictionary<Tri, GameBoardSpace> spaceByTri;

    public GameBoardVariant Variant { get; private set; }
    public int Columns { get; private set; }
    public int Rows { get; private set; }
    public IList<GameBoardSpace> Spaces { get; private set; }

    internal GameBoardModel(GameBoardVariant variant, Tri[,] tris)
    {
        if (tris == null)
        {
            throw new ArgumentNullException("tris");
        }

        Variant = variant;
        Columns = tris.GetLength(0);
        Rows = tris.GetLength(1);
        spaces = new GameBoardSpace[Columns * Rows];
        spaceByTri = new Dictionary<Tri, GameBoardSpace>(spaces.Length);

        int index = 0;
        for (int q = 0; q < Columns; q++)
        {
            for (int r = 0; r < Rows; r++)
            {
                GameBoardSpace space = new GameBoardSpace(tris[q, r]);
                spaces[index++] = space;
                spaceByTri.Add(tris[q, r], space);
            }
        }

        Spaces = Array.AsReadOnly(spaces);
    }

    internal void RevealPlanetProductionNumbers(Tri tri)
    {
        if (tri != null &&
            spaceByTri.TryGetValue(tri, out GameBoardSpace space) &&
            space.SystemKind == SystemKind.Planets)
        {
            space.RevealPlanetProductionNumbers();
        }
    }

    public GameBoardSpace GetSpaceAt(int q, int r)
    {
        if (q < 0 || q >= Columns || r < 0 || r >= Rows)
        {
            return null;
        }

        return spaces[q * Rows + r];
    }
}
