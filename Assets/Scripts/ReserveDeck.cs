using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the Reserve Pile (Vorratsstapel / Fragezeichen-Fach) according to Deep Space Pioneers rules.
/// Contains 8 cards of each of the 5 resource types (40 cards total).
/// Automatically restocks and reshuffles when depleted.
/// </summary>
public class ReserveDeck
{
    private readonly List<ResourceType> _cards = new List<ResourceType>();

    public int RemainingCount => _cards.Count;

    public ReserveDeck()
    {
        RefillAndShuffle();
    }

    /// <summary>
    /// Refills the reserve pile with 8 cards of each resource type and shuffles it.
    /// </summary>
    public void RefillAndShuffle()
    {
        _cards.Clear();
        foreach (ResourceType type in GameRules.Resources)
        {
            for (int i = 0; i < 8; i++)
            {
                _cards.Add(type);
            }
        }

        System.Random rng = new System.Random();
        int n = _cards.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            ResourceType val = _cards[k];
            _cards[k] = _cards[n];
            _cards[n] = val;
        }
    }

    /// <summary>
    /// Draws the top card from the reserve pile.
    /// If the pile is empty, automatically refills with 40 new cards and shuffles first.
    /// </summary>
    public ResourceType DrawCard()
    {
        if (_cards.Count == 0)
        {
            RefillAndShuffle();
        }

        int lastIndex = _cards.Count - 1;
        ResourceType card = _cards[lastIndex];
        _cards.RemoveAt(lastIndex);
        return card;
    }
}
