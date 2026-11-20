using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Serializable inline card definition for catalog assets.
/// Allows holding all 32 encounter cards in a single catalog asset.
/// </summary>
[Serializable]
public class EncounterCardInlineData
{
    [Range(1, 32)]
    public int Number = 1;

    public EncounterCardType Type = EncounterCardType.TraderGift;

    [Range(-1, 3)]
    public int CharacterIndex = 0;

    public LocalizedString Title = new LocalizedString();

    public LocalizedString Situation = new LocalizedString();

    public LocalizedString PlayerChoice = new LocalizedString();

    public List<EncounterCardOutcomeData> Outcomes = new List<EncounterCardOutcomeData>();

    public EncounterCard ToRuntimeCard()
    {
        var outcomeList = new List<EncounterCardOutcome>();
        if (Outcomes != null)
        {
            foreach (var o in Outcomes)
            {
                if (o != null)
                {
                    outcomeList.Add(o.ToRuntimeOutcome());
                }
            }
        }

        EncounterInteractionType interactionType = EncounterInteractionType.Choice;
        if (Type == EncounterCardType.Drifter || Type == EncounterCardType.TraderGift)
        {
            interactionType = EncounterInteractionType.Donation;
        }
        else if (Type == EncounterCardType.Event)
        {
            interactionType = EncounterInteractionType.Event;
        }

        EncounterComparisonType comparisonType = EncounterComparisonType.None;
        if (Type == EncounterCardType.TraderMarauder || Type == EncounterCardType.MarauderDemand || Type == EncounterCardType.MarauderAttack)
        {
            comparisonType = EncounterComparisonType.Combat;
        }
        else if (Type == EncounterCardType.SpaceDistortion)
        {
            comparisonType = EncounterComparisonType.Speed;
        }
        else if (Type == EncounterCardType.Rescue)
        {
            string choice = PlayerChoice != null ? PlayerChoice.GetText() : string.Empty;
            if (Number <= 21 ||
                choice.IndexOf("Speed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                choice.IndexOf("Geschwindigkeit", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                comparisonType = EncounterComparisonType.Speed;
            }
            else
            {
                comparisonType = EncounterComparisonType.Combat;
            }
        }

        EncounterOpponentRole opponentRole = (comparisonType != EncounterComparisonType.None)
            ? EncounterOpponentRole.Left
            : EncounterOpponentRole.None;

        return new EncounterCard(
            Number,
            Type,
            Situation.GetText(),
            PlayerChoice.GetText(),
            Situation,
            PlayerChoice,
            Title,
            interactionType,
            opponentRole,
            comparisonType,
            CharacterIndex,
            outcomeList.ToArray());
    }
}

/// <summary>
/// ScriptableObject asset managing the full catalog of encounter cards.
/// Can reference individual EncounterCardAsset objects or store cards inline.
/// </summary>
[CreateAssetMenu(fileName = "EncounterCardCatalog", menuName = "Deep Space Pioneers/Encounter Card Catalog")]
public class EncounterCardCatalogAsset : ScriptableObject
{
    [Tooltip("Individual card assets (if separated).")]
    public List<EncounterCardAsset> CardAssets = new List<EncounterCardAsset>();

    [Tooltip("All 32 encounter cards stored directly inside this catalog asset.")]
    public List<EncounterCardInlineData> InlineCards = new List<EncounterCardInlineData>();

    /// <summary>
    /// Instantiates runtime EncounterCard objects from this asset's data.
    /// </summary>
    public IList<EncounterCard> CreateCards()
    {
        var list = new List<EncounterCard>();

        if (CardAssets != null && CardAssets.Count > 0)
        {
            foreach (var cardAsset in CardAssets)
            {
                if (cardAsset != null)
                {
                    list.Add(cardAsset.ToEncounterCard());
                }
            }
        }
        else if (InlineCards != null && InlineCards.Count > 0)
        {
            foreach (var inlineCard in InlineCards)
            {
                if (inlineCard != null)
                {
                    list.Add(inlineCard.ToRuntimeCard());
                }
            }
        }

        return list;
    }

    [ContextMenu("Sort Cards by Number")]
    public void SortByNumber()
    {
        if (CardAssets != null && CardAssets.Count > 0)
        {
            CardAssets.Sort((a, b) => a != null && b != null ? a.Number.CompareTo(b.Number) : 0);
        }
        if (InlineCards != null && InlineCards.Count > 0)
        {
            InlineCards.Sort((a, b) => a.Number.CompareTo(b.Number));
        }
    }
}

