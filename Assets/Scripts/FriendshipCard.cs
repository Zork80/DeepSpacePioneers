using System;
using System.Collections.Generic;

/// <summary>
/// Identification type for each specific friendship card (20 unique cards across 4 civilizations).
/// </summary>
public enum FriendshipCardId
{
    // Diplomat - 5 cards
    DiplomatTributeDiscount,       // Tribut-Ermäßigung (1x): Max 12 cards before discard on 7
    DiplomatBoughtFame1,           // Gekaufter Ruhm (1st copy): Pay 1 TradeGoods -> 1 Half-Medal (1/turn)
    DiplomatBoughtFame2,           // Gekaufter Ruhm (2nd copy): Same benefit
    DiplomatRichHelpsPoor,         // Reich hilft Arm (1x): Steal 1 card from up to 2 players with more VP
    DiplomatGalacticAid,           // Galaktische Hilfe (1x): If roll (non-7) gave 0 resources, pick 1 resource

    // Cultivator - 5 cards
    CultivatorProductionOre,       // +1 Ore when at least 1 colony/spaceport produces Ore
    CultivatorProductionFuel,      // +1 Fuel
    CultivatorProductionCarbon,    // +1 Carbon
    CultivatorProductionFood,      // +1 Food
    CultivatorProductionGoods,     // +1 TradeGoods

    // Scientist - 5 cards
    ScientistUpgradeCannonAndDrive1, // +1 Speed, +1 Cannon power
    ScientistUpgradeCannonAndDrive2, // +1 Speed, +1 Cannon power
    ScientistUpgradeCannonAndDrive3, // +1 Speed, +1 Cannon power
    ScientistUpgradeCannons2,        // +2 Cannon power
    ScientistUpgradeDrives2,         // +2 Speed

    // Trader - 5 cards
    TraderTradeOre21,              // 2:1 Trade for Ore
    TraderTradeFuel21,             // 2:1 Trade for Fuel
    TraderTradeCarbon21,           // 2:1 Trade for Carbon
    TraderTradeFood21,             // 2:1 Trade for Food
    TraderTradeGoods11             // 1:1 Trade for TradeGoods (1/turn)
}

/// <summary>
/// Definition of a single Friendship Card.
/// </summary>
[System.Serializable]
public class FriendshipCard
{
    public FriendshipCardId Id { get; private set; }
    public AlienCivilization Folk { get; private set; }
    public string NameDe { get; private set; }
    public string NameEn { get; private set; }
    public string DescriptionDe { get; private set; }
    public string DescriptionEn { get; private set; }

    public FriendshipCard(FriendshipCardId id, AlienCivilization folk, string nameDe, string nameEn, string descDe, string descEn)
    {
        Id = id;
        Folk = folk;
        NameDe = nameDe;
        NameEn = nameEn;
        DescriptionDe = descDe;
        DescriptionEn = descEn;
    }

    public string GetName()
    {
        return LocalizationManager.CurrentLanguage == GameLanguage.German ? NameDe : NameEn;
    }

    public string GetDescription()
    {
        return LocalizationManager.CurrentLanguage == GameLanguage.German ? DescriptionDe : DescriptionEn;
    }

    /// <summary>
    /// Returns a concise one-line summary of the card's benefit for sidebar and HUD displays.
    /// </summary>
    public string GetShortBenefit()
    {
        bool de = LocalizationManager.CurrentLanguage == GameLanguage.German;
        switch (Id)
        {
            case FriendshipCardId.DiplomatTributeDiscount:
                return de ? "Erst ab >12 Handkarten abgeben" : "Discard only if >12 cards";
            case FriendshipCardId.DiplomatBoughtFame1:
            case FriendshipCardId.DiplomatBoughtFame2:
                return de ? "1 Handelsware -> 1/2 Medaille" : "1 Goods -> 1/2 Medal";
            case FriendshipCardId.DiplomatRichHelpsPoor:
                return de ? "1 Karte von reicheren Spielern" : "Draw 1 card from richer players";
            case FriendshipCardId.DiplomatGalacticAid:
                return de ? "Bei 0 Ertrag 1 Rohstoff wählen" : "Take 1 resource if roll yielded 0";
            case FriendshipCardId.CultivatorProductionOre:
                return de ? "+1 Erz bei Erz-Ertrag" : "+1 Ore on Ore production";
            case FriendshipCardId.CultivatorProductionFuel:
                return de ? "+1 Treibstoff bei Treibstoff-Ertrag" : "+1 Fuel on Fuel production";
            case FriendshipCardId.CultivatorProductionCarbon:
                return de ? "+1 Carbon bei Carbon-Ertrag" : "+1 Carbon on Carbon production";
            case FriendshipCardId.CultivatorProductionFood:
                return de ? "+1 Nahrung bei Nahrungs-Ertrag" : "+1 Food on Food production";
            case FriendshipCardId.CultivatorProductionGoods:
                return de ? "+1 Handelsware bei Handelswaren-Ertrag" : "+1 Goods on Goods production";
            case FriendshipCardId.ScientistUpgradeCannonAndDrive1:
            case FriendshipCardId.ScientistUpgradeCannonAndDrive2:
            case FriendshipCardId.ScientistUpgradeCannonAndDrive3:
                return de ? "+1 Flugweite, +1 Kanone" : "+1 Speed, +1 Cannon";
            case FriendshipCardId.ScientistUpgradeCannons2:
                return de ? "+2 Bordkanonen" : "+2 Cannons";
            case FriendshipCardId.ScientistUpgradeDrives2:
                return de ? "+2 Flugweite" : "+2 Speed";
            case FriendshipCardId.TraderTradeOre21:
                return de ? "2:1 Vorratshandel für Erz" : "2:1 Supply Trade for Ore";
            case FriendshipCardId.TraderTradeFuel21:
                return de ? "2:1 Vorratshandel für Treibstoff" : "2:1 Supply Trade for Fuel";
            case FriendshipCardId.TraderTradeCarbon21:
                return de ? "2:1 Vorratshandel für Carbon" : "2:1 Supply Trade for Carbon";
            case FriendshipCardId.TraderTradeFood21:
                return de ? "2:1 Vorratshandel für Nahrung" : "2:1 Supply Trade for Food";
            case FriendshipCardId.TraderTradeGoods11:
                return de ? "1:1 Handel für Handelsware (1x/Zug)" : "1:1 Trade for Goods (1x/turn)";
            default:
                return string.Empty;
        }
    }

    /// <summary>
    /// Generates the full 5-card deck for an alien civilization.
    /// </summary>
    public static List<FriendshipCard> CreateDeckForCivilization(AlienCivilization civ)
    {
        List<FriendshipCard> deck = new List<FriendshipCard>();
        switch (civ)
        {
            case AlienCivilization.Diplomat:
                deck.Add(new FriendshipCard(
                    FriendshipCardId.DiplomatTributeDiscount,
                    AlienCivilization.Diplomat,
                    "Tribut-Ermäßigung",
                    "Tribute Reduction",
                    "Bei einer gewürfelten 7 musst du nur Rohstoffe abgeben, wenn du mehr als 12 Handkarten besitzt.",
                    "On a rolled 7, you only discard cards if you hold more than 12 resource cards."));
                deck.Add(new FriendshipCard(
                    FriendshipCardId.DiplomatBoughtFame1,
                    AlienCivilization.Diplomat,
                    "Gekaufter Ruhm",
                    "Purchased Fame",
                    "In deiner Handels- und Bauphase darfst du einmalig 1 Handelsware abgeben, um 1 halbe Medaille zu kaufen.",
                    "In your Trade & Build phase, you may spend 1 Trade Good to purchase 1 half-medal (once per turn)."));
                deck.Add(new FriendshipCard(
                    FriendshipCardId.DiplomatBoughtFame2,
                    AlienCivilization.Diplomat,
                    "Gekaufter Ruhm",
                    "Purchased Fame",
                    "In deiner Handels- und Bauphase darfst du einmalig 1 Handelsware abgeben, um 1 halbe Medaille zu kaufen.",
                    "In your Trade & Build phase, you may spend 1 Trade Good to purchase 1 half-medal (once per turn)."));
                deck.Add(new FriendshipCard(
                    FriendshipCardId.DiplomatRichHelpsPoor,
                    AlienCivilization.Diplomat,
                    "Reich hilft Arm",
                    "Rich Helps Poor",
                    "In deiner Handels- und Bauphase darfst du von bis zu 2 Spielern mit mehr Siegpunkten je 1 Rohstoffkarte ziehen.",
                    "In your Trade & Build phase, draw 1 card each from up to 2 players who have more Victory Points than you."));
                deck.Add(new FriendshipCard(
                    FriendshipCardId.DiplomatGalacticAid,
                    AlienCivilization.Diplomat,
                    "Galaktische Hilfe",
                    "Galactic Aid",
                    "Wenn du bei einem Ertragswurf leer ausgehst (keine 7), nimmst du sofort 1 beliebigen Rohstoff aus dem Vorrat.",
                    "If a production roll (not 7) yields no resources for you, immediately take 1 resource of choice from supply."));
                break;

            case AlienCivilization.Cultivator:
                deck.Add(new FriendshipCard(
                    FriendshipCardId.CultivatorProductionOre,
                    AlienCivilization.Cultivator,
                    "Produktions-Erhöhung: Erz",
                    "Production Bonus: Ore",
                    "Produziert mindestens eine Kolonie/Raumhafen Erz, erhältst du +1 zusätzliches Erz.",
                    "Whenever at least one of your colonies/spaceports produces Ore, receive +1 extra Ore."));
                deck.Add(new FriendshipCard(
                    FriendshipCardId.CultivatorProductionFuel,
                    AlienCivilization.Cultivator,
                    "Produktions-Erhöhung: Treibstoff",
                    "Production Bonus: Fuel",
                    "Produziert mindestens eine Kolonie/Raumhafen Treibstoff, erhältst du +1 zusätzlichen Treibstoff.",
                    "Whenever at least one of your colonies/spaceports produces Fuel, receive +1 extra Fuel."));
                deck.Add(new FriendshipCard(
                    FriendshipCardId.CultivatorProductionCarbon,
                    AlienCivilization.Cultivator,
                    "Produktions-Erhöhung: Carbon",
                    "Production Bonus: Carbon",
                    "Produziert mindestens eine Kolonie/Raumhafen Carbon, erhältst du +1 zusätzliches Carbon.",
                    "Whenever at least one of your colonies/spaceports produces Carbon, receive +1 extra Carbon."));
                deck.Add(new FriendshipCard(
                    FriendshipCardId.CultivatorProductionFood,
                    AlienCivilization.Cultivator,
                    "Produktions-Erhöhung: Nahrung",
                    "Production Bonus: Food",
                    "Produziert mindestens eine Kolonie/Raumhafen Nahrung, erhältst du +1 zusätzliche Nahrung.",
                    "Whenever at least one of your colonies/spaceports produces Food, receive +1 extra Food."));
                deck.Add(new FriendshipCard(
                    FriendshipCardId.CultivatorProductionGoods,
                    AlienCivilization.Cultivator,
                    "Produktions-Erhöhung: Handelsware",
                    "Production Bonus: Trade Goods",
                    "Produziert mindestens eine Kolonie/Raumhafen Handelsware, erhältst du +1 zusätzliche Handelsware.",
                    "Whenever at least one of your colonies/spaceports produces Trade Goods, receive +1 extra Trade Good."));
                break;

            case AlienCivilization.Scientist:
                deck.Add(new FriendshipCard(
                    FriendshipCardId.ScientistUpgradeCannonAndDrive1,
                    AlienCivilization.Scientist,
                    "Ausbau-Erhöhung: Bordkanone & Antrieb",
                    "Upgrade Bonus: Cannon & Booster",
                    "Geschwindigkeit und Kampfstärke deiner Raumschiffe sind um jeweils +1 erhöht.",
                    "Increases ship flight speed by +1 and cannon combat strength by +1."));
                deck.Add(new FriendshipCard(
                    FriendshipCardId.ScientistUpgradeCannonAndDrive2,
                    AlienCivilization.Scientist,
                    "Ausbau-Erhöhung: Bordkanone & Antrieb",
                    "Upgrade Bonus: Cannon & Booster",
                    "Geschwindigkeit und Kampfstärke deiner Raumschiffe sind um jeweils +1 erhöht.",
                    "Increases ship flight speed by +1 and cannon combat strength by +1."));
                deck.Add(new FriendshipCard(
                    FriendshipCardId.ScientistUpgradeCannonAndDrive3,
                    AlienCivilization.Scientist,
                    "Ausbau-Erhöhung: Bordkanone & Antrieb",
                    "Upgrade Bonus: Cannon & Booster",
                    "Geschwindigkeit und Kampfstärke deiner Raumschiffe sind um jeweils +1 erhöht.",
                    "Increases ship flight speed by +1 and cannon combat strength by +1."));
                deck.Add(new FriendshipCard(
                    FriendshipCardId.ScientistUpgradeCannons2,
                    AlienCivilization.Scientist,
                    "Ausbau-Erhöhung: Bordkanonen",
                    "Upgrade Bonus: Cannons",
                    "Die Kampfstärke deiner Raumschiffe ist um +2 erhöht.",
                    "Increases ship cannon combat strength by +2."));
                deck.Add(new FriendshipCard(
                    FriendshipCardId.ScientistUpgradeDrives2,
                    AlienCivilization.Scientist,
                    "Ausbau-Erhöhung: Antriebe",
                    "Upgrade Bonus: Boosters",
                    "Die Fluggeschwindigkeit deiner Raumschiffe ist um +2 erhöht.",
                    "Increases ship flight speed by +2."));
                break;

            case AlienCivilization.Trader:
                deck.Add(new FriendshipCard(
                    FriendshipCardId.TraderTradeOre21,
                    AlienCivilization.Trader,
                    "Tauschkurs 2:1 Erz",
                    "Exchange Rate 2:1 Ore",
                    "Tausche in der Handelsphase Erz im Verhältnis 2:1 mit dem Vorrat.",
                    "Trade Ore at 2:1 rate with the supply during Trade & Build phase."));
                deck.Add(new FriendshipCard(
                    FriendshipCardId.TraderTradeFuel21,
                    AlienCivilization.Trader,
                    "Tauschkurs 2:1 Treibstoff",
                    "Exchange Rate 2:1 Fuel",
                    "Tausche in der Handelsphase Treibstoff im Verhältnis 2:1 mit dem Vorrat.",
                    "Trade Fuel at 2:1 rate with the supply during Trade & Build phase."));
                deck.Add(new FriendshipCard(
                    FriendshipCardId.TraderTradeCarbon21,
                    AlienCivilization.Trader,
                    "Tauschkurs 2:1 Carbon",
                    "Exchange Rate 2:1 Carbon",
                    "Tausche in der Handelsphase Carbon im Verhältnis 2:1 mit dem Vorrat.",
                    "Trade Carbon at 2:1 rate with the supply during Trade & Build phase."));
                deck.Add(new FriendshipCard(
                    FriendshipCardId.TraderTradeFood21,
                    AlienCivilization.Trader,
                    "Tauschkurs 2:1 Nahrung",
                    "Exchange Rate 2:1 Food",
                    "Tausche in der Handelsphase Nahrung im Verhältnis 2:1 mit dem Vorrat.",
                    "Trade Food at 2:1 rate with the supply during Trade & Build phase."));
                deck.Add(new FriendshipCard(
                    FriendshipCardId.TraderTradeGoods11,
                    AlienCivilization.Trader,
                    "Tauschkurs 1:1 Handelsware",
                    "Exchange Rate 1:1 Trade Goods",
                    "Einmal pro Spielzug darfst du 1 Handelsware 1:1 gegen einen beliebigen Rohstoff tauschen (weitere Handelswaren regulär 2:1).",
                    "Once per turn, you may trade 1 Trade Good at 1:1 for any resource with the supply (further Goods at regular 2:1)."));
                break;
        }

        return deck;
    }

    private static Dictionary<FriendshipCardId, FriendshipCard> _allCardsCache;

    /// <summary>
    /// Returns the static definition of a friendship card by its identifier.
    /// </summary>
    public static FriendshipCard GetCardById(FriendshipCardId id)
    {
        if (_allCardsCache == null)
        {
            _allCardsCache = new Dictionary<FriendshipCardId, FriendshipCard>();
            AlienCivilization[] civs = new AlienCivilization[]
            {
                AlienCivilization.Diplomat,
                AlienCivilization.Cultivator,
                AlienCivilization.Scientist,
                AlienCivilization.Trader
            };
            foreach (AlienCivilization civ in civs)
            {
                List<FriendshipCard> deck = CreateDeckForCivilization(civ);
                foreach (FriendshipCard card in deck)
                {
                    _allCardsCache[card.Id] = card;
                }
            }
        }

        _allCardsCache.TryGetValue(id, out FriendshipCard c);
        return c;
    }
}

