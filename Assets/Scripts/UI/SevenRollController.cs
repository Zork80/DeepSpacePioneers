using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Coordinates the complete multi-step resolution of a rolled 7 during the Production Phase:
/// 1. Card discard check for all players holding > 7 cards (or > 12 with Tribute Discount).
/// 2. Active player steals 1 random card from a chosen opponent.
/// 3. All opponents draw 1 free card from the Reserve Pile (clockwise from left neighbor).
/// 4. Active player draws 0-2 cards from the Reserve Pile based on current Victory Points.
/// 5. Resolution completes and unlocks the Trade & Build Phase.
/// </summary>
public class SevenRollController
{
    private readonly TriMap _triMap;
    private readonly DiscardCardsDialogUI _discardDialog;
    private readonly StealCardDialogUI _stealDialog;
    private readonly Action<string, bool, float> _showToast;

    private readonly Queue<Player> _pendingDiscardPlayers = new Queue<Player>();
    private Action _onCompleteCallback;

    public bool IsResolving { get; private set; }

    public SevenRollController(
        TriMap triMap,
        DiscardCardsDialogUI discardDialog,
        StealCardDialogUI stealDialog,
        Action<string, bool, float> showToast)
    {
        _triMap = triMap;
        _discardDialog = discardDialog;
        _stealDialog = stealDialog;
        _showToast = showToast;
    }

    public void StartResolution(Action onComplete)
    {
        if (IsResolving) return;

        IsResolving = true;
        _onCompleteCallback = onComplete;

        RuntimeLog.WriteLine("[SevenRoll] Resolution sequence started.");
        _showToast?.Invoke(UIStrings.SevenRollTitle.GetText(), false, 3.5f);

        // Step 1: Check discards
        CheckDiscards();
    }

    private void CheckDiscards()
    {
        _pendingDiscardPlayers.Clear();

        if (_triMap != null && _triMap.Players != null)
        {
            foreach (Player player in _triMap.Players)
            {
                if (player.MustDiscardOnSeven)
                {
                    _pendingDiscardPlayers.Enqueue(player);
                }
            }
        }

        ProcessNextDiscard();
    }

    private void ProcessNextDiscard()
    {
        if (_pendingDiscardPlayers.Count == 0)
        {
            // All discards processed, move to Step 2: Steal
            ExecuteStealStep();
            return;
        }

        Player player = _pendingDiscardPlayers.Dequeue();
        int discardCount = player.DiscardCountOnSeven;

        if (player.Type == Player.PlayerType.AI)
        {
            // AI automatically discards
            var toDiscard = AiPlayerController.ChooseDiscardCards(player, discardCount);
            player.DiscardResources(toDiscard);

            string toastMsg = string.Format(UIStrings.SevenRollDiscardToast.GetText(), player.PlayerName, discardCount);
            RuntimeLog.WriteLine("[SevenRoll] " + toastMsg);
            _showToast?.Invoke(toastMsg, false, 2.5f);

            ProcessNextDiscard();
        }
        else
        {
            // Human player: open discard dialog
            if (_discardDialog != null)
            {
                _discardDialog.Open(player, discardCount, () =>
                {
                    string toastMsg = string.Format(UIStrings.SevenRollDiscardToast.GetText(), player.PlayerName, discardCount);
                    RuntimeLog.WriteLine("[SevenRoll] " + toastMsg);
                    _showToast?.Invoke(toastMsg, false, 2.5f);
                    ProcessNextDiscard();
                });
            }
            else
            {
                var toDiscard = AiPlayerController.ChooseDiscardCards(player, discardCount);
                player.DiscardResources(toDiscard);
                ProcessNextDiscard();
            }
        }
    }

    private void ExecuteStealStep()
    {
        Player activePlayer = _triMap?.CurrentPlayer;
        if (activePlayer == null)
        {
            DistributeReserveCards();
            return;
        }

        // Opponents who have at least 1 card
        List<Player> eligibleTargets = _triMap.Players
            .Where(p => p != activePlayer && p.TotalResources > 0)
            .ToList();

        if (eligibleTargets.Count == 0)
        {
            string noTargets = UIStrings.SevenRollStealNoTargets.GetText();
            RuntimeLog.WriteLine("[SevenRoll] " + noTargets);
            _showToast?.Invoke(noTargets, false, 3.0f);
            DistributeReserveCards();
            return;
        }

        if (activePlayer.Type == Player.PlayerType.AI)
        {
            Player chosenTarget = AiPlayerController.ChooseStealTarget(activePlayer, eligibleTargets);
            ApplySteal(activePlayer, chosenTarget);
            DistributeReserveCards();
        }
        else
        {
            if (_stealDialog != null)
            {
                _stealDialog.Open(activePlayer, eligibleTargets, (chosenTarget) =>
                {
                    if (chosenTarget != null)
                    {
                        ApplySteal(activePlayer, chosenTarget);
                    }
                    DistributeReserveCards();
                });
            }
            else
            {
                Player chosenTarget = eligibleTargets[0];
                ApplySteal(activePlayer, chosenTarget);
                DistributeReserveCards();
            }
        }
    }

    private void ApplySteal(Player activePlayer, Player target)
    {
        if (activePlayer == null || target == null) return;

        ResourceType stolen = target.DrawRandomResourceCard();
        if (stolen != ResourceType.None)
        {
            activePlayer.AddResource(stolen, 1);
            string stealMsg = string.Format(UIStrings.SevenRollStealSuccessToast.GetText(),
                activePlayer.PlayerName, UIStrings.GetResourceName(stolen), target.PlayerName);
            RuntimeLog.WriteLine("[SevenRoll] " + stealMsg);
            _showToast?.Invoke(stealMsg, false, 3.5f);
        }
    }

    private void DistributeReserveCards()
    {
        if (_triMap == null)
        {
            FinishResolution();
            return;
        }

        Player activePlayer = _triMap.CurrentPlayer;
        List<string> opponentDistributions = new List<string>();

        // Step 3: All opponents draw 1 card from the Reserve Pile (starting with left neighbor)
        if (activePlayer != null)
        {
            Player neighbor = _triMap.GetLeftNeighbor(activePlayer);
            while (neighbor != null && neighbor != activePlayer)
            {
                ResourceType res = _triMap.ReserveDeck.DrawCard();
                neighbor.AddResource(res, 1);
                opponentDistributions.Add(string.Format("{0} ({1})", neighbor.PlayerName, UIStrings.GetResourceName(res)));
                neighbor = _triMap.GetLeftNeighbor(neighbor);
            }
        }

        if (opponentDistributions.Count > 0)
        {
            string oppMsg = string.Format(UIStrings.SevenRollOpponentsReserveToast.GetText(),
                string.Join(", ", opponentDistributions));
            RuntimeLog.WriteLine("[SevenRoll] " + oppMsg);
            _showToast?.Invoke(oppMsg, false, 4.0f);
        }

        // Step 4: Active player draws based on Victory Points:
        // 4-7 VP: 2 cards
        // 8-9 VP: 1 card
        // 10+ VP: 0 cards
        if (activePlayer != null)
        {
            int vp = activePlayer.VictoryPoints;
            int cardsToDraw = (vp <= 7) ? 2 : ((vp <= 9) ? 1 : 0);

            List<string> activeDrawnNames = new List<string>();
            for (int i = 0; i < cardsToDraw; i++)
            {
                ResourceType res = _triMap.ReserveDeck.DrawCard();
                activePlayer.AddResource(res, 1);
                activeDrawnNames.Add(UIStrings.GetResourceName(res));
            }

            if (cardsToDraw > 0)
            {
                string activeMsg = string.Format(UIStrings.SevenRollActiveReserveToast.GetText(),
                    activePlayer.PlayerName, cardsToDraw, vp) + " [" + string.Join(", ", activeDrawnNames) + "]";
                RuntimeLog.WriteLine("[SevenRoll] " + activeMsg);
                _showToast?.Invoke(activeMsg, false, 4.0f);
            }
        }

        FinishResolution();
    }

    private void FinishResolution()
    {
        IsResolving = false;
        RuntimeLog.WriteLine("[SevenRoll] Resolution sequence completed.");
        _onCompleteCallback?.Invoke();
        _onCompleteCallback = null;
    }
}

