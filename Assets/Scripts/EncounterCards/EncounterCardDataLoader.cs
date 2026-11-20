using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Helper for loading and serializing localizable encounter cards from assets (ScriptableObject or JSON).
/// </summary>
public static class EncounterCardDataLoader
{
    [Serializable]
    private class JsonCardWrapper
    {
        public List<JsonCardEntry> cards;
    }

    [Serializable]
    public class JsonCardEntry
    {
        public int number;
        public string type;
        public int characterIndex;
        public string interaction;
        public string opponent;
        public string comparison;
        public JsonLocalizedString title;
        public JsonLocalizedString situation;
        public JsonLocalizedString playerChoice;
        public List<JsonOutcomeEntry> outcomes;
    }

    private static EncounterCardType ParseCardType(string typeStr)
    {
        if (Enum.TryParse(typeStr, true, out EncounterCardType parsed))
        {
            return parsed;
        }
        switch (typeStr)
        {
            case "MerchantGift": return EncounterCardType.TraderGift;
            case "MerchantPirate": return EncounterCardType.TraderMarauder;
            case "PirateDemand": return EncounterCardType.MarauderDemand;
            case "PirateTrade": return EncounterCardType.MarauderTrade;
            case "PirateRaidOffer": return EncounterCardType.MarauderRaidOffer;
            case "PirateAttack": return EncounterCardType.MarauderAttack;
            case "WanderingPeople": return EncounterCardType.Drifter;
            default: return EncounterCardType.TraderGift;
        }
    }

    [Serializable]
    public class JsonOutcomeEntry
    {
        public JsonLocalizedString condition;
        public JsonLocalizedString effectSummary;
        public JsonOutcomeEffects effects;
    }

    [Serializable]
    public class JsonOutcomeEffects
    {
        public int halfMedals;
        public string resourceReward;
        public string resourceCost;
        public bool blockOneShip;
        public bool loseUpgrade;
        public bool gainUpgrade;
        public bool gainTradeShip;
        public bool spaceJump;
        public bool reshuffleDeck;
        public bool redrawCard;

        public EncounterOutcomeEffect ToOutcomeEffect()
        {
            return new EncounterOutcomeEffect
            {
                HalfMedals = halfMedals,
                ResourceReward = resourceReward ?? string.Empty,
                ResourceCost = resourceCost ?? string.Empty,
                BlockOneShip = blockOneShip,
                LoseUpgrade = loseUpgrade,
                GainUpgrade = gainUpgrade,
                GainTradeShip = gainTradeShip,
                SpaceJump = spaceJump,
                ReshuffleDeck = reshuffleDeck,
                RedrawCard = redrawCard
            };
        }
    }

    [Serializable]
    public class JsonLocalizedString
    {
        public string en;
        public string de;

        public LocalizedString ToLocalizedString()
        {
            return new LocalizedString(en, de);
        }
    }

    /// <summary>
    /// Loads the 32 encounter cards from the best available asset source.
    /// Checks assigned ScriptableObject asset first, then Resources ScriptableObject, then JSON TextAsset.
    /// </summary>
    public static IList<EncounterCard> LoadCards(EncounterCardCatalogAsset catalogAsset = null)
    {
        // 1. Try loading JSON TextAsset from Resources (primary source of truth)
        var jsonAsset = Resources.Load<TextAsset>("EncounterCards/EncounterCardsData");
        if (jsonAsset != null && !string.IsNullOrEmpty(jsonAsset.text))
        {
            var cardsFromJson = LoadCardsFromJson(jsonAsset.text);
            if (cardsFromJson != null && cardsFromJson.Count > 0)
            {
                return cardsFromJson;
            }
        }

        // 2. Check explicitly assigned ScriptableObject asset
        if (catalogAsset != null)
        {
            var cardsFromAsset = catalogAsset.CreateCards();
            if (cardsFromAsset != null && cardsFromAsset.Count > 0)
            {
                return cardsFromAsset;
            }
        }

        // 3. Try loading ScriptableObject from Resources
        var resourceCatalog = Resources.Load<EncounterCardCatalogAsset>("EncounterCards/EncounterCardCatalog");
        if (resourceCatalog != null)
        {
            var cardsFromResource = resourceCatalog.CreateCards();
            if (cardsFromResource != null && cardsFromResource.Count > 0)
            {
                return cardsFromResource;
            }
        }

        return new List<EncounterCard>();
    }

    /// <summary>
    /// Parses encounter cards from raw JSON string.
    /// </summary>
    public static IList<EncounterCard> LoadCardsFromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return new List<EncounterCard>();
        }

        string wrappedJson = json.Trim();
        if (wrappedJson.StartsWith("["))
        {
            wrappedJson = "{\"cards\":" + wrappedJson + "}";
        }

        JsonCardWrapper wrapper = JsonUtility.FromJson<JsonCardWrapper>(wrappedJson);
        if (wrapper == null || wrapper.cards == null)
        {
            return new List<EncounterCard>();
        }

        var result = new List<EncounterCard>();
        foreach (var entry in wrapper.cards)
        {
            if (entry == null) continue;

            EncounterCardType cardType = ParseCardType(entry.type);

            EncounterInteractionType interactionType = EncounterInteractionType.Choice;
            if (!string.IsNullOrEmpty(entry.interaction))
            {
                switch (entry.interaction.ToLowerInvariant())
                {
                    case "donation": interactionType = EncounterInteractionType.Donation; break;
                    case "event": interactionType = EncounterInteractionType.Event; break;
                    default: interactionType = EncounterInteractionType.Choice; break;
                }
            }
            else if (cardType == EncounterCardType.Drifter || cardType == EncounterCardType.TraderGift)
            {
                interactionType = EncounterInteractionType.Donation;
            }
            else if (cardType == EncounterCardType.Event)
            {
                interactionType = EncounterInteractionType.Event;
            }

            EncounterOpponentRole opponentRole = EncounterOpponentRole.None;
            if (!string.IsNullOrEmpty(entry.opponent))
            {
                switch (entry.opponent.ToLowerInvariant())
                {
                    case "right": opponentRole = EncounterOpponentRole.Right; break;
                    case "left": opponentRole = EncounterOpponentRole.Left; break;
                    case "secondright": opponentRole = EncounterOpponentRole.SecondRight; break;
                    case "secondleft": opponentRole = EncounterOpponentRole.SecondLeft; break;
                    default: opponentRole = EncounterOpponentRole.None; break;
                }
            }

            EncounterComparisonType comparisonType = EncounterComparisonType.None;
            if (!string.IsNullOrEmpty(entry.comparison))
            {
                switch (entry.comparison.ToLowerInvariant())
                {
                    case "combat": comparisonType = EncounterComparisonType.Combat; break;
                    case "speed": comparisonType = EncounterComparisonType.Speed; break;
                    case "flagshiproll":
                    case "mothershiproll": comparisonType = EncounterComparisonType.FlagshipRoll; break;
                    default: comparisonType = EncounterComparisonType.None; break;
                }
            }

            var outcomes = new List<EncounterCardOutcome>();
            if (entry.outcomes != null)
            {
                foreach (var o in entry.outcomes)
                {
                    if (o == null) continue;
                    LocalizedString condLoc = o.condition != null ? o.condition.ToLocalizedString() : new LocalizedString();
                    LocalizedString effLoc = o.effectSummary != null ? o.effectSummary.ToLocalizedString() : new LocalizedString();
                    EncounterOutcomeEffect effect = o.effects != null ? o.effects.ToOutcomeEffect() : new EncounterOutcomeEffect();
                    outcomes.Add(new EncounterCardOutcome(condLoc.GetText(), effLoc.GetText(), condLoc, effLoc, effect));
                }
            }

            LocalizedString titleLoc = entry.title != null ? entry.title.ToLocalizedString() : new LocalizedString($"Card {entry.number}", $"Karte {entry.number}");
            LocalizedString sitLoc = entry.situation != null ? entry.situation.ToLocalizedString() : new LocalizedString();
            LocalizedString choiceLoc = entry.playerChoice != null ? entry.playerChoice.ToLocalizedString() : new LocalizedString();

            result.Add(new EncounterCard(
                entry.number,
                cardType,
                sitLoc.GetText(),
                choiceLoc.GetText(),
                sitLoc,
                choiceLoc,
                titleLoc,
                interactionType,
                opponentRole,
                comparisonType,
                entry.characterIndex,
                outcomes.ToArray()));
        }

        return result;
    }

    /// <summary>
    /// Populates an EncounterCardCatalogAsset's InlineCards from a JSON string.
    /// </summary>
    public static void PopulateCatalogAssetFromJson(EncounterCardCatalogAsset catalog, string json)
    {
        if (catalog == null || string.IsNullOrEmpty(json)) return;

        string wrappedJson = json.Trim();
        if (wrappedJson.StartsWith("["))
        {
            wrappedJson = "{\"cards\":" + wrappedJson + "}";
        }

        JsonCardWrapper wrapper = JsonUtility.FromJson<JsonCardWrapper>(wrappedJson);
        if (wrapper == null || wrapper.cards == null) return;

        catalog.InlineCards.Clear();
        foreach (var entry in wrapper.cards)
        {
            if (entry == null) continue;

            EncounterCardType cardType = ParseCardType(entry.type);

            var card = new EncounterCardInlineData
            {
                Number = entry.number,
                Type = cardType,
                CharacterIndex = entry.characterIndex,
                Title = entry.title != null ? entry.title.ToLocalizedString() : new LocalizedString(),
                Situation = entry.situation != null ? entry.situation.ToLocalizedString() : new LocalizedString(),
                PlayerChoice = entry.playerChoice != null ? entry.playerChoice.ToLocalizedString() : new LocalizedString(),
                Outcomes = new List<EncounterCardOutcomeData>()
            };

            if (entry.outcomes != null)
            {
                foreach (var o in entry.outcomes)
                {
                    if (o == null) continue;
                    card.Outcomes.Add(new EncounterCardOutcomeData(
                        o.condition != null ? o.condition.en : string.Empty,
                        o.condition != null ? o.condition.de : string.Empty,
                        o.effectSummary != null ? o.effectSummary.en : string.Empty,
                        o.effectSummary != null ? o.effectSummary.de : string.Empty));
                }
            }

            catalog.InlineCards.Add(card);
        }
    }
}

