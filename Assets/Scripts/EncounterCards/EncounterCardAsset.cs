using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Outcome entry within a localizable EncounterCardAsset.
/// </summary>
[Serializable]
public class EncounterCardOutcomeData
{
    public LocalizedString Condition = new LocalizedString();
    public LocalizedString EffectSummary = new LocalizedString();

    public EncounterCardOutcomeData() { }

    public EncounterCardOutcomeData(string conditionEn, string conditionDe, string effectEn, string effectDe)
    {
        Condition = new LocalizedString(conditionEn, conditionDe);
        EffectSummary = new LocalizedString(effectEn, effectDe);
    }

    public EncounterCardOutcome ToRuntimeOutcome()
    {
        return new EncounterCardOutcome(Condition.GetText(), EffectSummary.GetText(), Condition, EffectSummary);
    }
}

/// <summary>
/// ScriptableObject asset representing an individual localizable Encounter Card.
/// Can be edited directly in the Unity Inspector.
/// </summary>
[CreateAssetMenu(fileName = "EncounterCard", menuName = "Deep Space Pioneers/Encounter Card")]
public class EncounterCardAsset : ScriptableObject
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

    public EncounterCard ToEncounterCard()
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

