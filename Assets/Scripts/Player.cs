using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Player
{
    private readonly Dictionary<ResourceType, int> resources;

    public Player( string name )
    {
        PlayerName = name; 

        units = new HashSet<Unit>();
        colonies = new HashSet<Colony>();
        tradeStations = new HashSet<TradeStation>();
        resources = new Dictionary<ResourceType, int>();

        foreach (ResourceType resource in GameRules.Resources)
        {
            resources.Add(resource, 0);
        }
    }

    public int PlayerNumber { get; set; }

    private string customPlayerName;

    public string PlayerName
    {
        get
        {
            if (PlayerNumber > 0 && (string.IsNullOrEmpty(customPlayerName) || customPlayerName.StartsWith("Player ") || customPlayerName.StartsWith("Spieler ")))
            {
                return string.Format(UIStrings.DefaultPlayerName.GetText(), PlayerNumber);
            }
            if (customPlayerName == "Erkundungstest" || customPlayerName == "Discovery Test")
            {
                return UIStrings.DiscoveryTestPlayerName.GetText();
            }
            return customPlayerName;
        }
        set
        {
            customPlayerName = value;
        }
    }

    public enum PlayerType { LOCAL, AI, REMOTE };
    public PlayerType Type = PlayerType.LOCAL;

    public enum AiDifficulty { Easy, Normal, Hard };
    public AiDifficulty Difficulty = AiDifficulty.Normal;

    private HashSet<Unit> units;
    private HashSet<Colony> colonies;
    private HashSet<TradeStation> tradeStations;

    public Unit[] Units {
        get { return units.ToArray(); }
    }

    public Colony[] Colonies {
        get { return colonies.ToArray(); }
    }

    public TradeStation[] TradeStations {
        get { return tradeStations.ToArray(); }
    }

    // --- Component Supply & Board Tracking ---
    public int ColonyShipsOnBoard => units.Count(u => u.ShipType == StarshipType.ColonyShip);
    public int TradeShipsOnBoard => units.Count(u => u.ShipType == StarshipType.TradeShip);
    public int ActiveTransportShips => ColonyShipsOnBoard + TradeShipsOnBoard;
    public int AvailableTransportShips => Mathf.Max(0, GameRules.MaxTransportShipsPerPlayer - ActiveTransportShips);

    /// <summary>Trade ships captured from marauders/encounters, held in reserve until placed at a spaceport.</summary>
    public int CapturedTradeShipsCount { get; set; } = 0;

    // Total colonies on the board (both regular colonies and colonies modified into spaceports).
    // Spaceports are modified colonies; modifying a colony does not reduce the number of deployed colonies!
    public int? NetworkVictoryPoints { get; set; } = null;
    public int? NetworkRegularColoniesCount { get; set; } = null;
    public int? NetworkBuiltSpaceportsCount { get; set; } = null;
    public int? NetworkBuiltTradeStationsCount { get; set; } = null;
    public int? NetworkTotalResources { get; set; } = null;

    public void ResetNetworkOverrides()
    {
        NetworkVictoryPoints = null;
        NetworkRegularColoniesCount = null;
        NetworkBuiltSpaceportsCount = null;
        NetworkBuiltTradeStationsCount = null;
        NetworkTotalResources = null;
    }

    public int TotalColoniesCount => (NetworkRegularColoniesCount.HasValue && NetworkBuiltSpaceportsCount.HasValue)
        ? (NetworkRegularColoniesCount.Value + NetworkBuiltSpaceportsCount.Value)
        : colonies.Count;
    public int BuiltColoniesCount => TotalColoniesCount;
    public int RegularColoniesCount => NetworkRegularColoniesCount ?? colonies.Count(c => !c.IsSpaceport);

    // Total deployed colony pieces: on board (including spaceports) + loaded on active colony ships
    public int DeployedColoniesCount => TotalColoniesCount + ColonyShipsOnBoard;
    public int AvailableColoniesCount => Mathf.Max(0, GameRules.MaxColoniesPerPlayer - DeployedColoniesCount);

    public int BuiltSpaceportsCount => NetworkBuiltSpaceportsCount ?? colonies.Count(c => c.IsSpaceport);
    public int AvailableSpaceportsCount => Mathf.Max(0, GameRules.MaxSpaceportsPerPlayer - BuiltSpaceportsCount);

    public int BuiltTradeStationsCount => NetworkBuiltTradeStationsCount ?? tradeStations.Count;
    public int AvailableTradeStationsCount => Mathf.Max(0, GameRules.MaxTradeStationsPerPlayer - (BuiltTradeStationsCount + TradeShipsOnBoard));

    public int AvailableBoostersCount => Mathf.Max(0, GameRules.MaxBoostersPerPlayer - FlagshipDriveCount);
    public int AvailableCannonsCount => Mathf.Max(0, GameRules.MaxCannonsPerPlayer - FlagshipCannonCount);
    public int AvailableFreightPodsCount => Mathf.Max(0, GameRules.MaxFreightPodsPerPlayer - FlagshipCargoModuleCount);

    public int FlagshipDriveCount { get; private set; }
    public int FlagshipCannonCount { get; private set; }
    public int FlagshipCargoModuleCount { get; private set; }
    public int FlagshipFreightPodCount => FlagshipCargoModuleCount;
    public int HalfMedalCount { get; set; }
    public int HalfMedals => HalfMedalCount;

    // --- Friendship Cards ---
    private readonly List<FriendshipCard> friendshipCards = new List<FriendshipCard>();
    public IReadOnlyList<FriendshipCard> FriendshipCards => friendshipCards.AsReadOnly();

    public bool HasFriendshipCard(FriendshipCardId id)
    {
        return friendshipCards.Any(c => c.Id == id);
    }

    public int CountFriendshipCards(FriendshipCardId id)
    {
        return friendshipCards.Count(c => c.Id == id);
    }

    public void AddFriendshipCard(FriendshipCard card)
    {
        if (card != null && !friendshipCards.Contains(card))
        {
            friendshipCards.Add(card);
            FriendshipCardsChanged?.Invoke();
            UpgradesChanged?.Invoke();
        }
    }

    /// <summary>
    /// Synchronizes friendship cards received from network observation state.
    /// </summary>
    public void SyncFriendshipCardsFromDTO(string[] cardIds)
    {
        if (cardIds == null || cardIds.Length == 0)
        {
            if (friendshipCards.Count > 0)
            {
                friendshipCards.Clear();
                FriendshipCardsChanged?.Invoke();
                UpgradesChanged?.Invoke();
            }
            return;
        }

        bool changed = friendshipCards.Count != cardIds.Length;
        if (!changed)
        {
            for (int i = 0; i < cardIds.Length; i++)
            {
                if (!System.Enum.TryParse(cardIds[i], out FriendshipCardId cid) || friendshipCards[i].Id != cid)
                {
                    changed = true;
                    break;
                }
            }
        }

        if (!changed)
        {
            return;
        }

        friendshipCards.Clear();
        for (int i = 0; i < cardIds.Length; i++)
        {
            if (System.Enum.TryParse(cardIds[i], out FriendshipCardId cid))
            {
                FriendshipCard card = FriendshipCard.GetCardById(cid);
                if (card != null)
                {
                    friendshipCards.Add(card);
                }
            }
        }

        FriendshipCardsChanged?.Invoke();
        UpgradesChanged?.Invoke();
    }

    public event System.Action FriendshipCardsChanged;

    /// <summary>
    /// Bonus speed provided by Friendship Cards (Scholars: Khor-Nem, Hir-Sto, Asa-Bar, Hir-Akas).
    /// </summary>
    public int FriendshipSpeedBonus
    {
        get
        {
            int bonus = 0;
            if (HasFriendshipCard(FriendshipCardId.ScientistUpgradeCannonAndDrive1)) bonus += 1;
            if (HasFriendshipCard(FriendshipCardId.ScientistUpgradeCannonAndDrive2)) bonus += 1;
            if (HasFriendshipCard(FriendshipCardId.ScientistUpgradeCannonAndDrive3)) bonus += 1;
            if (HasFriendshipCard(FriendshipCardId.ScientistUpgradeDrives2)) bonus += 2;
            return bonus;
        }
    }

    /// <summary>
    /// Bonus cannon combat strength provided by Friendship Cards (Scholars: Khor-Nem, Hir-Sto, Asa-Bar, Kha-Star).
    /// </summary>
    public int FriendshipCannonBonus
    {
        get
        {
            int bonus = 0;
            if (HasFriendshipCard(FriendshipCardId.ScientistUpgradeCannonAndDrive1)) bonus += 1;
            if (HasFriendshipCard(FriendshipCardId.ScientistUpgradeCannonAndDrive2)) bonus += 1;
            if (HasFriendshipCard(FriendshipCardId.ScientistUpgradeCannonAndDrive3)) bonus += 1;
            if (HasFriendshipCard(FriendshipCardId.ScientistUpgradeCannons2)) bonus += 2;
            return bonus;
        }
    }

    /// <summary>
    /// Effective boosters count for flight rolls and speed checks (Flagship drives + Friendship cards).
    /// </summary>
    public int EffectiveDriveCount => FlagshipDriveCount + FriendshipSpeedBonus;

    /// <summary>
    /// Effective cannons count for combat encounters (Flagship cannons + Friendship cards).
    /// </summary>
    public int EffectiveCannonCount => FlagshipCannonCount + FriendshipCannonBonus;

    /// <summary>
    /// Max hand cards before having to discard half on a 7-roll. Default is 7; 12 if player has Tribute Discount.
    /// </summary>
    public int SafeCardLimitOnSeven => HasFriendshipCard(FriendshipCardId.DiplomatTributeDiscount) ? 12 : 7;

    // Turn tracking for once-per-turn friendship card abilities
    public bool HasUsedBoughtFameThisTurn { get; set; } = false;
    public bool HasUsedRichHelpsPoorThisTurn { get; set; } = false;
    public bool HasUsedGoods11TradeThisTurn { get; set; } = false;

    // --- Victory Points Breakdown ---
    // • Reine Kolonien zählen jeweils 1 Siegpunkt.
    public int ColonyVictoryPoints => RegularColoniesCount;
    // • Raumhäfen (modifizierte Kolonien) zählen jeweils 2 Siegpunkte.
    public int SpaceportVictoryPoints => BuiltSpaceportsCount * 2;
    // • Zwei halbe Medaillen zählen jeweils 1 Siegpunkt.
    public int MedalVictoryPoints => HalfMedalCount / 2;
    // • Freundschafts-Aufsteller zählen jeweils 2 Siegpunkte (wird über TradeStations bzw. Sonderkarten geführt).
    public int FriendshipVictoryPoints { get; set; } = 0;
    // • Korsarenbasis (Marauder Base) & Eisplaneten-Sondermarker zählen jeweils 1 Siegpunkt.
    public int IcePlanetTokensCount { get; private set; }
    public int MarauderBaseTokensCount { get; private set; }
    private int _bonusSpecialVictoryPoints = 0;
    public int OutpostDefeatedVictoryPoints
    {
        get => IcePlanetTokensCount + MarauderBaseTokensCount + _bonusSpecialVictoryPoints;
        set => _bonusSpecialVictoryPoints = value;
    }

    // Total Victory Points
    public int VictoryPoints => NetworkVictoryPoints ?? (
        ColonyVictoryPoints +
        SpaceportVictoryPoints +
        MedalVictoryPoints +
        FriendshipVictoryPoints +
        OutpostDefeatedVictoryPoints);

    public Material Material {get; set;}
    public Color PlayerColor => Material != null ? Material.color : Color.gray;

    public event System.Action ResourcesChanged;
    public event System.Action UpgradesChanged;

    public void NotifyUpgradesChanged()
    {
        UpgradesChanged?.Invoke();
    }

    public void AddIcePlanetToken()
    {
        IcePlanetTokensCount++;
        NotifyUpgradesChanged();
    }

    public void AddMarauderBaseToken()
    {
        MarauderBaseTokensCount++;
        NotifyUpgradesChanged();
    }

    public void AddFlagshipDrive()
    {
        FlagshipDriveCount++;
        NotifyUpgradesChanged();
    }

    public void AddFlagshipCannon()
    {
        FlagshipCannonCount++;
        NotifyUpgradesChanged();
    }

    public void AddFlagshipCargoModule()
    {
        FlagshipCargoModuleCount++;
        NotifyUpgradesChanged();
    }

    public void AddHalfMedal(int count = 1)
    {
        HalfMedalCount += Mathf.Max(0, count);
        UpgradesChanged?.Invoke();
    }

    public void SyncUpgradesFromDTO(AgentBridge.FlagshipDTO fs)
    {
        if (fs == null) return;
        FlagshipDriveCount = fs.boosters;
        FlagshipCannonCount = fs.cannons;
        FlagshipCargoModuleCount = fs.freightPods;
        UpgradesChanged?.Invoke();
    }

    public void SyncResourcesFromBundle(AgentBridge.ResourceBundleDTO bundle)
    {
        if (bundle == null) return;
        resources[ResourceType.Ore] = bundle.ore;
        resources[ResourceType.Fuel] = bundle.fuel;
        resources[ResourceType.Carbon] = bundle.carbon;
        resources[ResourceType.Food] = bundle.food;
        resources[ResourceType.TradeGoods] = bundle.tradeGoods;
        NetworkTotalResources = bundle.total;
        ResourcesChanged?.Invoke();
    }

    public void RemoveHalfMedal(int count = 1)
    {
        HalfMedalCount = Mathf.Max(0, HalfMedalCount - count);
        UpgradesChanged?.Invoke();
    }

    public bool RemoveFlagshipDrive()
    {
        if (FlagshipDriveCount > 0)
        {
            FlagshipDriveCount--;
            UpgradesChanged?.Invoke();
            return true;
        }
        return false;
    }

    public bool RemoveFlagshipCannon()
    {
        if (FlagshipCannonCount > 0)
        {
            FlagshipCannonCount--;
            UpgradesChanged?.Invoke();
            return true;
        }
        return false;
    }

    public bool RemoveFlagshipCargoModule()
    {
        if (FlagshipCargoModuleCount > 0)
        {
            FlagshipCargoModuleCount--;
            UpgradesChanged?.Invoke();
            return true;
        }
        return false;
    }

    public int TotalFlagshipUpgrades => FlagshipDriveCount + FlagshipCannonCount + FlagshipCargoModuleCount;

    public bool RemoveRandomUpgrade()
    {
        List<FlagshipUpgrade> available = new List<FlagshipUpgrade>();
        if (FlagshipDriveCount > 0) available.Add(FlagshipUpgrade.Drive);
        if (FlagshipCannonCount > 0) available.Add(FlagshipUpgrade.Cannon);
        if (FlagshipCargoModuleCount > 0) available.Add(FlagshipUpgrade.CargoModule);

        if (available.Count == 0) return false;

        FlagshipUpgrade chosen = available[UnityEngine.Random.Range(0, available.Count)];
        switch (chosen)
        {
            case FlagshipUpgrade.Drive: RemoveFlagshipDrive(); break;
            case FlagshipUpgrade.Cannon: RemoveFlagshipCannon(); break;
            case FlagshipUpgrade.CargoModule: RemoveFlagshipCargoModule(); break;
        }
        return true;
    }

    public int GetResourceCount(ResourceType resource)
    {
        return GameRules.IsResource(resource) ? resources[resource] : 0;
    }

    public int TotalResources
    {
        get
        {
            if (Type != PlayerType.LOCAL && NetworkTotalResources.HasValue)
            {
                return NetworkTotalResources.Value;
            }
            int total = 0;
            foreach (ResourceType res in GameRules.Resources)
            {
                total += GetResourceCount(res);
            }
            return total;
        }
    }

    public int TotalResourceCount => TotalResources;
    public int DiscardLimitOnSeven => HasFriendshipCard(FriendshipCardId.DiplomatTributeDiscount) ? 12 : 7;
    public bool MustDiscardOnSeven => TotalResources > DiscardLimitOnSeven;
    public int DiscardCountOnSeven => MustDiscardOnSeven ? TotalResources / 2 : 0;

    /// <summary>
    /// Draws a random resource card from this player's hand (e.g. when an opponent rolls a 7 or steals a card).
    /// Deducts 1 card of the chosen resource from the hand and returns its type.
    /// Returns ResourceType.None if the player has no resource cards.
    /// </summary>
    public ResourceType DrawRandomResourceCard()
    {
        if (TotalResources <= 0)
        {
            return ResourceType.None;
        }

        List<ResourceType> pool = new List<ResourceType>();
        foreach (ResourceType r in GameRules.Resources)
        {
            int count = GetResourceCount(r);
            for (int i = 0; i < count; i++)
            {
                pool.Add(r);
            }
        }

        if (pool.Count == 0) return ResourceType.None;

        ResourceType chosen = pool[UnityEngine.Random.Range(0, pool.Count)];
        RemoveResource(chosen, 1);
        return chosen;
    }

    /// <summary>
    /// Discards a set of resource cards from this player's hand (e.g. when rolling a 7 with > 7 cards).
    /// </summary>
    public bool DiscardResources(IDictionary<ResourceType, int> cardsToDiscard)
    {
        if (cardsToDiscard == null) return false;
        return SpendResources(cardsToDiscard);
    }

    public void AddResource(ResourceType resource, int amount = 1)
    {
        if (!GameRules.IsResource(resource))
        {
            throw new System.ArgumentOutOfRangeException("resource", resource, "Unknown Deep Space Pioneers resource.");
        }
        if (amount < 0)
        {
            throw new System.ArgumentOutOfRangeException("amount", amount, "Resource amount cannot be negative.");
        }
        if (amount == 0)
        {
            return;
        }

        resources[resource] += amount;
        ResourcesChanged?.Invoke();
    }

    public bool RemoveResource(ResourceType resource, int amount = 1)
    {
        if (!GameRules.IsResource(resource))
        {
            throw new System.ArgumentOutOfRangeException("resource", resource, "Unknown Deep Space Pioneers resource.");
        }
        if (amount < 0)
        {
            throw new System.ArgumentOutOfRangeException("amount", amount, "Resource amount cannot be negative.");
        }
        if (amount == 0)
        {
            return true;
        }
        if (resources[resource] < amount)
        {
            return false;
        }

        resources[resource] -= amount;
        ResourcesChanged?.Invoke();
        return true;
    }

    public bool HasResources(IDictionary<ResourceType, int> costs)
    {
        if (costs == null)
        {
            return false;
        }

        foreach (KeyValuePair<ResourceType, int> cost in costs)
        {
            if (cost.Value <= 0) continue;
            if (!GameRules.IsResource(cost.Key) || GetResourceCount(cost.Key) < cost.Value)
            {
                return false;
            }
        }

        return true;
    }

    public bool SpendResources(IDictionary<ResourceType, int> costs)
    {
        if (costs == null)
        {
            throw new System.ArgumentNullException("costs");
        }

        foreach (KeyValuePair<ResourceType, int> cost in costs)
        {
            if (cost.Value <= 0) continue;
            if (!GameRules.IsResource(cost.Key) ||
                GetResourceCount(cost.Key) < cost.Value)
            {
                return false;
            }
        }

        foreach (KeyValuePair<ResourceType, int> cost in costs)
        {
            if (cost.Value > 0)
            {
                resources[cost.Key] -= cost.Value;
            }
        }

        ResourcesChanged?.Invoke();
        return true;
    }

    public int GetSupplyTradeRate(ResourceType offered)
    {
        // Merchant 1:1 Trade Goods card
        if (offered == ResourceType.TradeGoods && HasFriendshipCard(FriendshipCardId.TraderTradeGoods11) && !HasUsedGoods11TradeThisTurn)
        {
            return 1;
        }

        // Merchant 2:1 cards for specific resources
        if (offered == ResourceType.Ore && HasFriendshipCard(FriendshipCardId.TraderTradeOre21)) return 2;
        if (offered == ResourceType.Fuel && HasFriendshipCard(FriendshipCardId.TraderTradeFuel21)) return 2;
        if (offered == ResourceType.Carbon && HasFriendshipCard(FriendshipCardId.TraderTradeCarbon21)) return 2;
        if (offered == ResourceType.Food && HasFriendshipCard(FriendshipCardId.TraderTradeFood21)) return 2;

        return GameRules.GetSupplyTradeRate(offered);
    }

    public bool CanTradeWithSupply(ResourceType offered, ResourceType requested)
    {
        if (!GameRules.IsResource(offered) || !GameRules.IsResource(requested) || offered == requested)
        {
            return false;
        }

        int rate = GetSupplyTradeRate(offered);
        return GetResourceCount(offered) >= rate;
    }

    public bool ExecuteSupplyTrade(ResourceType offered, ResourceType requested)
    {
        if (!CanTradeWithSupply(offered, requested))
        {
            return false;
        }

        int rate = GetSupplyTradeRate(offered);
        if (offered == ResourceType.TradeGoods && rate == 1)
        {
            HasUsedGoods11TradeThisTurn = true;
        }

        resources[offered] -= rate;
        resources[requested] += 1;
        ResourcesChanged?.Invoke();
        return true;
    }

    public static bool ExecutePlayerTrade(
        Player playerA,
        IDictionary<ResourceType, int> offerA,
        Player playerB,
        IDictionary<ResourceType, int> offerB)
    {
        if (playerA == null || playerB == null || playerA == playerB)
        {
            return false;
        }
        if (offerA == null || offerB == null)
        {
            return false;
        }

        int totalA = offerA.Where(kv => GameRules.IsResource(kv.Key) && kv.Value > 0).Sum(kv => kv.Value);
        int totalB = offerB.Where(kv => GameRules.IsResource(kv.Key) && kv.Value > 0).Sum(kv => kv.Value);

        // Trade rules: trades must be at least 1 card for 1 card.
        if (totalA < 1 || totalB < 1)
        {
            return false;
        }

        if (!playerA.HasResources(offerA) || !playerB.HasResources(offerB))
        {
            return false;
        }

        foreach (KeyValuePair<ResourceType, int> item in offerA)
        {
            if (item.Value > 0 && GameRules.IsResource(item.Key))
            {
                playerA.resources[item.Key] -= item.Value;
                playerB.resources[item.Key] += item.Value;
            }
        }

        foreach (KeyValuePair<ResourceType, int> item in offerB)
        {
            if (item.Value > 0 && GameRules.IsResource(item.Key))
            {
                playerB.resources[item.Key] -= item.Value;
                playerA.resources[item.Key] += item.Value;
            }
        }

        playerA.ResourcesChanged?.Invoke();
        playerB.ResourcesChanged?.Invoke();
        return true;
    }

    public void AddUnit( Unit u )
    {
        units.Add(u);
        u.OnObjectDestroyed += OnUnitDestroyed;
        UpgradesChanged?.Invoke();
    }

    public void OnUnitDestroyed( MapObject mo )
    {
        units.Remove( (Unit)mo );
        UpgradesChanged?.Invoke();
    }

    public void AddColony ( Colony c )
    {
        if (colonies.Contains(c)) return;
        colonies.Add(c);
        c.OnObjectDestroyed += OnColonyDestroyed;
        UpgradesChanged?.Invoke();
    }

    public bool RemoveColony ( Colony c )
    {
        if (colonies.Remove(c))
        {
            c.OnObjectDestroyed -= OnColonyDestroyed;
            UpgradesChanged?.Invoke();
            return true;
        }
        return false;
    }

    public void OnColonyDestroyed( MapObject mo )
    {
        colonies.Remove( (Colony)mo );
        UpgradesChanged?.Invoke();
    }


    public void AddTradeStation ( TradeStation t )
    {
        tradeStations.Add(t);
        t.OnObjectDestroyed += OnTradeStationDestroyed;
        UpgradesChanged?.Invoke();
    }

    public void OnTradeStationDestroyed( MapObject mo )
    {
        tradeStations.Remove( (TradeStation)mo );
        UpgradesChanged?.Invoke();
    }
}
