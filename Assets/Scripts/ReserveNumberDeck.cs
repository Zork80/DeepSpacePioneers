using System;
using System.Collections.Generic;

/// <summary>
/// Manages the 5 Reserve Number Discs (Reserve-Zahlenscheiben) according to Deep Space Pioneers rules.
/// When an Ice Planet is terraformed or a Marauder Base is defeated, a random reserve number disc is drawn
/// and placed on the planet to determine its future resource production.
/// </summary>
public class ReserveNumberDeck
{
    private readonly List<int> _numbers = new List<int>();

    public int RemainingCount => _numbers.Count;

    public ReserveNumberDeck()
    {
        Reset();
    }

    /// <summary>
    /// Resets the reserve number deck with the official 5 reserve number discs: { 3, 4, 5, 9, 11 }.
    /// </summary>
    public void Reset()
    {
        _numbers.Clear();
        _numbers.AddRange(new int[] { 3, 4, 5, 9, 11 });
        Shuffle();
    }

    /// <summary>
    /// Shuffles the remaining reserve number discs.
    /// </summary>
    public void Shuffle()
    {
        System.Random rng = new System.Random();
        int n = _numbers.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            int val = _numbers[k];
            _numbers[k] = _numbers[n];
            _numbers[n] = val;
        }
    }

    /// <summary>
    /// Draws a reserve number disc.
    /// If the deck is empty (e.g. edge cases), draws a random valid production number (excluding 7).
    /// </summary>
    public int DrawNumber()
    {
        if (_numbers.Count == 0)
        {
            int[] fallback = { 3, 4, 5, 6, 8, 9, 10, 11 };
            return fallback[UnityEngine.Random.Range(0, fallback.Length)];
        }

        int lastIndex = _numbers.Count - 1;
        int drawn = _numbers[lastIndex];
        _numbers.RemoveAt(lastIndex);
        return drawn;
    }
}

