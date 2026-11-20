using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Controller for the Encounter modal dialog workflow.
/// Decouples encounter presentation, active player prompt construction,
/// interactive choice resolution (donations, combat/speed rolls),
/// and outcome propagation from PlayerHandler.
/// </summary>
public class EncounterController
{
    private readonly TriMap _triMap;
    private readonly EncounterDialogUI _dialog;
    private readonly Action<string, bool, float> _showToast;
    private readonly DiscardCardsDialogUI _discardDialog;
    private readonly UpgradeChoiceDialogUI _upgradeDialog;
    private readonly ShipChoiceDialogUI _shipChoiceDialog;
    private readonly Action _onResourcesChanged;
    private readonly Dictionary<ResourceType, int> _donationSelections = new Dictionary<ResourceType, int>();
    private readonly Dictionary<ResourceType, int> _rewardSelections = new Dictionary<ResourceType, int>();

    public EncounterController(
        TriMap triMap,
        EncounterDialogUI dialog,
        Action<string, bool, float> showToast,
        DiscardCardsDialogUI discardDialog = null,
        Action onResourcesChanged = null,
        UpgradeChoiceDialogUI upgradeDialog = null,
        ShipChoiceDialogUI shipChoiceDialog = null)
    {
        _triMap = triMap;
        _dialog = dialog;
        _showToast = showToast;
        _discardDialog = discardDialog;
        _upgradeDialog = upgradeDialog;
        _shipChoiceDialog = shipChoiceDialog;
        _onResourcesChanged = onResourcesChanged;
    }

    public void OnEncounterButtonClicked()
    {
        if (_triMap == null || _triMap.CurrentEncounterCard == null) return;

        bool outcomeDetermined = _triMap.SelectedEncounterOutcome != null ||
            (_triMap.NetworkEncounter != null && _triMap.NetworkEncounter.selectedOutcomeIndex >= 0) ||
            (_triMap.CurrentEncounterCard.Outcomes != null && _triMap.CurrentEncounterCard.Outcomes.Count == 1);

        if (Networking.NetworkManager.Instance != null &&
            Networking.NetworkManager.Instance.IsClientConnected &&
            !Networking.NetworkManager.Instance.IsHost)
        {
            if (_triMap.CurrentEncounterCard.RefreshesEncounterDeck ||
                _triMap.CurrentEncounterPhase == EncounterCardPhase.ApplyOutcome ||
                outcomeDetermined)
            {
                if (_triMap.CurrentEncounterPhase == EncounterCardPhase.SelectOutcome &&
                    _triMap.SelectedEncounterOutcome == null &&
                    (_triMap.NetworkEncounter == null || _triMap.NetworkEncounter.selectedOutcomeIndex < 0) &&
                    _triMap.CurrentEncounterCard.Outcomes != null && _triMap.CurrentEncounterCard.Outcomes.Count == 1)
                {
                    SelectOutcomeWithResourceCheck(_triMap.CurrentEncounterCard, 0, advanceFirst: false);
                }
                else
                {
                    Networking.NetworkManager.Instance.Client.SendAction(new AgentBridge.AgentActionDTO
                    {
                        action = "ConfirmEncounter"
                    });
                }
            }
            else if (_triMap.CurrentEncounterPhase == EncounterCardPhase.ReadPrompt)
            {
                Networking.NetworkManager.Instance.Client.SendAction(new AgentBridge.AgentActionDTO
                {
                    action = "AdvanceEncounter"
                });
            }
            return;
        }

        if (_triMap.CurrentEncounterCard.RefreshesEncounterDeck)
        {
            _triMap.ConfirmEncounterResolved();
        }
        else if (_triMap.CurrentEncounterPhase == EncounterCardPhase.ReadPrompt)
        {
            _triMap.AdvanceEncounterAfterPlayerResponse();
        }
        else if (_triMap.CurrentEncounterPhase == EncounterCardPhase.SelectOutcome)
        {
            if (_triMap.SelectedEncounterOutcome != null)
            {
                _triMap.ConfirmEncounterResolved();
            }
            else if (_triMap.CurrentEncounterCard.Outcomes != null && _triMap.CurrentEncounterCard.Outcomes.Count == 1)
            {
                SelectOutcomeWithResourceCheck(_triMap.CurrentEncounterCard, 0, advanceFirst: false);
                if (_triMap.CurrentEncounterPhase == EncounterCardPhase.ApplyOutcome)
                {
                    _triMap.ConfirmEncounterResolved();
                }
            }
        }
        else if (_triMap.CurrentEncounterPhase == EncounterCardPhase.ApplyOutcome)
        {
            _triMap.ConfirmEncounterResolved();
        }
    }

    public void Refresh()
    {
        bool pending = _triMap != null &&
            _triMap.EncounterPending &&
            _triMap.CurrentEncounterCard != null;

        // Only prompt the player who actually needs to interact (local human active player)
        bool shouldShowDialog = pending && _triMap.CurrentPlayer != null && _triMap.CurrentPlayer.Type == Player.PlayerType.LOCAL;

        _dialog.SetVisible(shouldShowDialog);
        _dialog.SetResolveButton(shouldShowDialog);
        ClearOutcomeButtons();

        if (!shouldShowDialog)
        {
            _dialog.SetCardText(string.Empty);
            return;
        }

        EncounterCard card = _triMap.CurrentEncounterCard;

        if (_dialog != null)
        {
            Sprite folkSprite = LoadEncounterCardSprite(card);
            _dialog.SetFolkSprite(folkSprite);
        }

        StringBuilder text = new StringBuilder();
        text.Append(UIStrings.EncounterCardHeading.GetText());

        text.AppendLine();
        text.AppendLine();
        text.AppendLine(card.Situation);
        if (!string.IsNullOrEmpty(card.PlayerChoice))
        {
            text.AppendLine();
            text.AppendLine(card.PlayerChoice);
        }

        if (card.OpponentRole != EncounterOpponentRole.None &&
            _triMap.CurrentPlayer != null &&
            !card.RefreshesEncounterDeck)
        {
            Player opponent = _triMap.GetOpponentPlayer(_triMap.CurrentPlayer, card.OpponentRole);
            if (opponent != null)
            {
                text.AppendLine();
                text.Append(BuildOpponentComparisonInfo(card, _triMap.CurrentPlayer, opponent));
            }
        }

        EncounterCardOutcome determinedOutcome = _triMap.SelectedEncounterOutcome;
        if (determinedOutcome == null && _triMap.NetworkEncounter != null &&
            _triMap.NetworkEncounter.selectedOutcomeIndex >= 0 &&
            card.Outcomes != null &&
            _triMap.NetworkEncounter.selectedOutcomeIndex < card.Outcomes.Count)
        {
            determinedOutcome = card.Outcomes[_triMap.NetworkEncounter.selectedOutcomeIndex];
        }
        if (determinedOutcome == null &&
            card.Outcomes != null &&
            card.Outcomes.Count == 1 &&
            _triMap.CurrentEncounterPhase != EncounterCardPhase.ReadPrompt)
        {
            determinedOutcome = card.Outcomes[0];
        }

        if (card.RefreshesEncounterDeck)
        {
            text.AppendLine();
            text.AppendLine(UIStrings.EncounterExecuteAllSteps.GetText());
            foreach (EncounterCardOutcome outcome in card.Outcomes)
            {
                text.AppendLine();
                text.Append(outcome.Condition);
                text.Append(": ");
                text.Append(outcome.EffectSummary);
            }
            _dialog.SetResolveButton(true, UIStrings.EncounterEventExecutedButton.GetText());
        }
        else if (determinedOutcome != null || _triMap.CurrentEncounterPhase == EncounterCardPhase.ApplyOutcome)
        {
            if (determinedOutcome != null)
            {
                text.AppendLine();
                text.Append(UIStrings.EncounterOutcomePrefix.GetText());
                text.AppendLine(determinedOutcome.Condition);
                text.AppendLine();
                text.Append(determinedOutcome.EffectSummary);
                text.AppendLine();
            }

            if (!string.IsNullOrEmpty(_triMap.LastEncounterStealSummary))
            {
                text.AppendLine();
                text.AppendLine(_triMap.LastEncounterStealSummary);
            }

            text.AppendLine();
            text.AppendLine(UIStrings.EncounterExecuteConsequenceInstruction.GetText());
            _dialog.SetResolveButton(true, UIStrings.EncounterConsequenceExecutedButton.GetText());
        }
        else if (_triMap.CurrentEncounterPhase == EncounterCardPhase.ReadPrompt)
        {
            _dialog.SetResolveButton(false);
            BuildEncounterPromptInteractiveButtons(card);
        }
        else if (_triMap.CurrentEncounterPhase == EncounterCardPhase.SelectOutcome)
        {
            for (int i = 0; i < card.Outcomes.Count; i++)
            {
                CreateOutcomeButton(card.Outcomes[i], i, card.Outcomes[i].Condition);
            }
            _dialog.SetResolveButton(false);
        }

        _dialog.SetCardText(text.ToString());
    }

    private void BuildEncounterPromptInteractiveButtons(EncounterCard card)
    {
        Player player = _triMap.CurrentPlayer;
        if (card.InteractionType == EncounterInteractionType.Donation ||
            card.Type == EncounterCardType.Drifter ||
            card.Type == EncounterCardType.TraderGift)
        {
            int totalRes = player != null ? GameRules.Resources.Sum(r => player.GetResourceCount(r)) : 0;
            int maxDonation = Mathf.Min(3, totalRes);
            for (int amt = 0; amt <= maxDonation; amt++)
            {
                int donationAmount = amt;
                string label = string.Format(UIStrings.EncounterDonateAmount.GetText(), amt);
                CreateActionButton(label, amt, true, () =>
                {
                    if (donationAmount == 0)
                    {
                        _triMap.PendingEncounterDonationAmount = 0;
                        _triMap.PendingEncounterDonatedResources = new Dictionary<ResourceType, int>();
                        int targetIndex = GetDonationOutcomeIndex(card, 0);
                        SelectOutcomeWithResourceCheck(card, targetIndex, advanceFirst: true);
                    }
                    else if (player != null && player.Type == Player.PlayerType.AI)
                    {
                        _triMap.PendingEncounterDonationAmount = donationAmount;
                        _triMap.PendingEncounterDonatedResources = new Dictionary<ResourceType, int>();
                        int targetIndex = GetDonationOutcomeIndex(card, donationAmount);
                        SelectOutcomeWithResourceCheck(card, targetIndex, advanceFirst: true);
                    }
                    else
                    {
                        PresentDonationResourceSelection(card, donationAmount);
                    }
                });
            }
        }
        else if (card.InteractionType == EncounterInteractionType.Choice)
        {
            if (card.Type == EncounterCardType.TraderMarauder)
            {
                CreateActionButton(UIStrings.EncounterDoNotHelp.GetText(), 0, true, () =>
                {
                    SelectOutcomeWithResourceCheck(card, 0, advanceFirst: true);
                });
                CreateActionButton(UIStrings.EncounterFight.GetText(), 1, true, () =>
                {
                    PresentCombatShakeButton(card, winIndex: 1, loseIndex: 2);
                });
            }
            else if (card.Type == EncounterCardType.MarauderDemand)
            {
                string payLabel = string.Format(UIStrings.EncounterPayDemand.GetText(), "2");
                CreateActionButton(payLabel, 0, true, () =>
                {
                    SelectOutcomeWithResourceCheck(card, 0, advanceFirst: true);
                });
                CreateActionButton(UIStrings.EncounterFight.GetText(), 1, true, () =>
                {
                    PresentCombatShakeButton(card, winIndex: 1, loseIndex: 2);
                });
            }
            else if (card.Type == EncounterCardType.MarauderTrade)
            {
                CreateActionButton(UIStrings.EncounterDeclineTrade.GetText(), 0, true, () =>
                {
                    SelectOutcomeWithResourceCheck(card, 0, advanceFirst: true);
                });
                int totalRes = player != null ? GameRules.Resources.Sum(r => player.GetResourceCount(r)) : 0;
                bool canTrade = totalRes >= 1;
                CreateActionButton(UIStrings.EncounterAcceptTrade.GetText(), 1, canTrade, () =>
                {
                    SelectOutcomeWithResourceCheck(card, 1, advanceFirst: true);
                });
                CreateActionButton(UIStrings.EncounterFight.GetText(), 2, true, () =>
                {
                    PresentCombatShakeButton(card, winIndex: 2, loseIndex: 3);
                });
            }
            else if (card.Type == EncounterCardType.MarauderRaidOffer)
            {
                CreateActionButton(UIStrings.EncounterDeclineRaid.GetText(), 0, true, () =>
                {
                    SelectOutcomeWithResourceCheck(card, 0, advanceFirst: true);
                });
                int totalRes = player != null ? GameRules.Resources.Sum(r => player.GetResourceCount(r)) : 0;
                bool canPayRaid = totalRes >= 1;
                CreateActionButton(UIStrings.EncounterAcceptRaid.GetText(), 1, canPayRaid, () =>
                {
                    SelectOutcomeWithResourceCheck(card, 1, advanceFirst: true);
                });
                CreateActionButton(UIStrings.EncounterFight.GetText(), 2, true, () =>
                {
                    PresentCombatShakeButton(card, winIndex: 2, loseIndex: 3);
                });
            }
            else if (card.Type == EncounterCardType.MarauderAttack)
            {
                CreateActionButton(UIStrings.EncounterFlee.GetText(), 0, true, () =>
                {
                    PresentEscapeShakeButton(card);
                });
                CreateActionButton(UIStrings.EncounterFight.GetText(), 1, true, () =>
                {
                    PresentCombatShakeButton(card, winIndex: 1, loseIndex: 2);
                });
            }
            else if (card.Type == EncounterCardType.Rescue)
            {
                CreateActionButton(UIStrings.EncounterDoNotHelp.GetText(), 0, true, () =>
                {
                    SelectOutcomeWithResourceCheck(card, 0, advanceFirst: true);
                });
                if (card.ComparisonType == EncounterComparisonType.Combat)
                {
                    CreateActionButton(UIStrings.EncounterHelp.GetText(), 1, true, () =>
                    {
                        PresentCombatShakeButton(card, winIndex: 1, loseIndex: 2);
                    });
                }
                else
                {
                    CreateActionButton(UIStrings.EncounterHelp.GetText(), 1, true, () =>
                    {
                        PresentSpeedShakeButton(card, winIndex: 1, loseIndex: 2);
                    });
                }
            }
            else if (card.Type == EncounterCardType.SpaceDistortion)
            {
                CreateActionButton(UIStrings.EncounterDoNotJump.GetText(), 0, true, () =>
                {
                    SelectOutcomeWithResourceCheck(card, 0, advanceFirst: true);
                });
                CreateActionButton(UIStrings.EncounterAttemptJump.GetText(), 1, true, () =>
                {
                    PresentSpeedShakeButton(card, winIndex: 1, loseIndex: 2);
                });
            }
        }
        else
        {
            _dialog.SetResolveButton(true, UIStrings.EncounterAnswerGivenButton.GetText());
        }
    }

    private void PresentEscapeShakeButton(EncounterCard card)
    {
        if (_triMap.CurrentPlayer?.Type == Player.PlayerType.AI)
        {
            ResolveMarauderEscape(card);
            return;
        }

        ClearOutcomeButtons();
        CreateActionButton(UIStrings.EncounterShakeSpeedButton.GetText(), 0, true, () =>
        {
            ResolveMarauderEscape(card);
        });
    }

    private void ResolveMarauderEscape(EncounterCard card)
    {
        Player player = _triMap.CurrentPlayer;
        Player opponent = _triMap.GetOpponentPlayer(player, card.OpponentRole);

        int mySpeed = TriMap.GetFlagshipCombatRoll(out FlagshipRollResult myRoll, player?.EffectiveDriveCount ?? 0);
        int oppSpeed = TriMap.GetFlagshipCombatRoll(out FlagshipRollResult oppRoll, opponent?.EffectiveDriveCount ?? 0);
        bool escaped = mySpeed >= oppSpeed;
        string opponentName = opponent?.PlayerName ?? "Opponent";

        if (escaped)
        {
            string msg = string.Format(UIStrings.EncounterEscapeWon.GetText(), mySpeed, oppSpeed, opponentName);
            _showToast?.Invoke(msg, false, 4.5f);
            SelectOutcomeWithResourceCheck(card, 0, advanceFirst: true);
        }
        else
        {
            string msg = string.Format(UIStrings.EncounterEscapeFailed.GetText(), mySpeed, oppSpeed, opponentName);
            _showToast?.Invoke(msg, true, 4.5f);
            PresentCombatShakeButton(card, winIndex: 1, loseIndex: 2);
        }
    }

    private void ResolveCombatEncounter(EncounterCard card, int winIndex, int loseIndex)
    {
        Player player = _triMap.CurrentPlayer;
        Player opponent = _triMap.GetOpponentPlayer(player, card.OpponentRole);

        int myCombat = TriMap.GetFlagshipCombatRoll(out FlagshipRollResult myRoll, player?.EffectiveCannonCount ?? 0);
        int oppCombat = TriMap.GetFlagshipCombatRoll(out FlagshipRollResult oppRoll, opponent?.EffectiveCannonCount ?? 0);

        bool won = myCombat >= oppCombat;
        string msg = won
            ? string.Format(UIStrings.EncounterCombatWon.GetText(), myCombat, oppCombat, opponent?.PlayerName ?? "Opponent")
            : string.Format(UIStrings.EncounterCombatLost.GetText(), myCombat, oppCombat, opponent?.PlayerName ?? "Opponent");

        _showToast(msg, !won, 4.5f);
        SelectOutcomeWithResourceCheck(card, won ? winIndex : loseIndex, advanceFirst: true);
    }

    private void ResolveSpeedEncounter(EncounterCard card, int winIndex, int loseIndex)
    {
        Player player = _triMap.CurrentPlayer;
        Player opponent = _triMap.GetOpponentPlayer(player, card.OpponentRole);

        int mySpeed = TriMap.GetFlagshipCombatRoll(out FlagshipRollResult myRoll, player?.EffectiveDriveCount ?? 0);
        int oppSpeed = TriMap.GetFlagshipCombatRoll(out FlagshipRollResult oppRoll, opponent?.EffectiveDriveCount ?? 0);

        bool won = mySpeed >= oppSpeed;
        string msg = won
            ? string.Format(UIStrings.EncounterSpeedWon.GetText(), mySpeed, oppSpeed, opponent?.PlayerName ?? "Opponent")
            : string.Format(UIStrings.EncounterSpeedLost.GetText(), mySpeed, oppSpeed, opponent?.PlayerName ?? "Opponent");

        _showToast(msg, !won, 4.5f);
        SelectOutcomeWithResourceCheck(card, won ? winIndex : loseIndex, advanceFirst: true);
    }

    private void PresentCombatShakeButton(EncounterCard card, int winIndex, int loseIndex)
    {
        if (_triMap.CurrentPlayer?.Type == Player.PlayerType.AI)
        {
            ResolveCombatEncounter(card, winIndex, loseIndex);
            return;
        }

        ClearOutcomeButtons();
        CreateActionButton(UIStrings.EncounterShakeCombatButton.GetText(), 0, true, () =>
        {
            ResolveCombatEncounter(card, winIndex, loseIndex);
        });
    }

    private void PresentSpeedShakeButton(EncounterCard card, int winIndex, int loseIndex)
    {
        if (_triMap.CurrentPlayer?.Type == Player.PlayerType.AI)
        {
            ResolveSpeedEncounter(card, winIndex, loseIndex);
            return;
        }

        ClearOutcomeButtons();
        CreateActionButton(UIStrings.EncounterShakeSpeedButton.GetText(), 0, true, () =>
        {
            ResolveSpeedEncounter(card, winIndex, loseIndex);
        });
    }

    private void PresentTradeShakeButton(EncounterCard card)
    {
        if (_triMap.CurrentPlayer?.Type == Player.PlayerType.AI)
        {
            ResolveFlagshipRollEncounter(card);
            return;
        }

        ClearOutcomeButtons();
        CreateActionButton(UIStrings.EncounterShakeTradeButton.GetText(), 0, true, () =>
        {
            ResolveFlagshipRollEncounter(card);
        });
    }

    private void PresentDonationResourceSelection(EncounterCard card, int targetDonationAmount)
    {
        Player player = _triMap.CurrentPlayer;
        if (player == null) return;

        if (_discardDialog != null)
        {
            string title = UIStrings.EncounterSurrenderTitle.GetText();
            string prompt = string.Format(UIStrings.EncounterDonationSelectionPrompt.GetText(), targetDonationAmount);

            _discardDialog.Open(
                player,
                targetDonationAmount,
                onComplete: () =>
                {
                    _triMap.PendingEncounterDonationAmount = targetDonationAmount;
                    _triMap.SkipNextOutcomeResourceDeduction = true;
                    _onResourcesChanged?.Invoke();
                    int targetIndex = GetDonationOutcomeIndex(card, targetDonationAmount);
                    SelectOutcomeWithResourceCheck(card, targetIndex, advanceFirst: true);
                },
                customTitle: title,
                customPrompt: prompt,
                onDiscardWithSelection: (selected) =>
                {
                    _triMap.PendingEncounterDonatedResources = selected;
                });
            return;
        }

        _donationSelections.Clear();
        foreach (ResourceType r in GameRules.Resources)
        {
            _donationSelections[r] = 0;
        }

        int remainingToFill = targetDonationAmount;
        foreach (ResourceType r in GameRules.Resources)
        {
            int inStock = player.GetResourceCount(r);
            int take = Mathf.Min(inStock, remainingToFill);
            if (take > 0)
            {
                _donationSelections[r] = take;
                remainingToFill -= take;
                if (remainingToFill <= 0) break;
            }
        }

        RefreshDonationResourceSelectionUI(card, targetDonationAmount);
    }

    private void RefreshDonationResourceSelectionUI(EncounterCard card, int targetDonationAmount)
    {
        ClearOutcomeButtons();
        Player player = _triMap.CurrentPlayer;
        if (player == null) return;

        int currentSelectedTotal = _donationSelections.Values.Sum();
        bool isValid = currentSelectedTotal == targetDonationAmount;

        _dialog.AddText(
            string.Format(UIStrings.EncounterDonationSelectionPrompt.GetText(), targetDonationAmount),
            14,
            new Color(0.9f, 0.95f, 1f));

        ResourceType[] resources = new ResourceType[]
        {
            ResourceType.Ore,
            ResourceType.Fuel,
            ResourceType.Carbon,
            ResourceType.Food,
            ResourceType.TradeGoods
        };

        RectTransform row = _dialog.AddRow(12f);
        Color stepperColor = new Color(0.2f, 0.25f, 0.3f, 1f);
        foreach (ResourceType resource in resources)
        {
            ResourceType res = resource;
            int selectedCount = _donationSelections.ContainsKey(res) ? _donationSelections[res] : 0;
            int inStock = player.GetResourceCount(res);
            bool canAdd = selectedCount < inStock && currentSelectedTotal < targetDonationAmount;

            GameObject group = UIFactory.CreateElement("ResourceStepper", row);
            UIFactory.AddHorizontalLayout(group, 2f).childForceExpandWidth = false;

            Button minus = _dialog.AddButton("-", selectedCount > 0, () =>
            {
                _donationSelections[res] = selectedCount - 1;
                RefreshDonationResourceSelectionUI(card, targetDonationAmount);
            }, stepperColor, 12, 28f, group.transform);
            EncounterDialogUI.SetPreferredSize(minus, 26f, 28f);

            Text label = _dialog.AddText(
                string.Format("{0}\n{1}/{2}", UIStrings.GetResourceName(res), selectedCount, inStock),
                11,
                Color.white,
                group.transform);
            EncounterDialogUI.SetPreferredSize(label, 52f, 32f);

            Button plus = _dialog.AddButton("+", canAdd, () =>
            {
                _donationSelections[res] = selectedCount + 1;
                RefreshDonationResourceSelectionUI(card, targetDonationAmount);
            }, stepperColor, 12, 28f, group.transform);
            EncounterDialogUI.SetPreferredSize(plus, 26f, 28f);
        }

        _dialog.AddText(
            string.Format(UIStrings.EncounterSelectedDonationCount.GetText(), currentSelectedTotal, targetDonationAmount),
            13,
            isValid ? new Color(0.4f, 1f, 0.6f) : new Color(1f, 0.75f, 0.3f));

        _dialog.AddButton(
            string.Format(UIStrings.EncounterConfirmDonation.GetText(), currentSelectedTotal, targetDonationAmount),
            isValid,
            () =>
            {
                _triMap.PendingEncounterDonationAmount = targetDonationAmount;
                _triMap.PendingEncounterDonatedResources = new Dictionary<ResourceType, int>(_donationSelections);
                int targetIndex = GetDonationOutcomeIndex(card, targetDonationAmount);
                SelectOutcomeWithResourceCheck(card, targetIndex, advanceFirst: true);
            },
            new Color(0.18f, 0.55f, 0.32f, 1f),
            13,
            36f);
    }

    private void ResolveFlagshipRollEncounter(EncounterCard card)
    {
        int roll = TriMap.GetFlagshipCombatRoll(out FlagshipRollResult rollRes, 0);
        string toastMsg = string.Format(UIStrings.EncounterRollResult.GetText(), roll, rollRes.FirstCore, rollRes.SecondCore);
        _showToast(toastMsg, false, 4.5f);

        int targetIndex = 1;
        if (roll <= 2) targetIndex = 1;
        else if (roll == 3) targetIndex = 2;
        else targetIndex = 3;

        SelectOutcomeWithResourceCheck(card, targetIndex, advanceFirst: true);
    }

    private void SelectOutcomeWithResourceCheck(EncounterCard card, int outcomeIndex, bool advanceFirst)
    {
        if (card == null || card.Outcomes == null || outcomeIndex < 0 || outcomeIndex >= card.Outcomes.Count)
        {
            return;
        }

        EncounterCardOutcome outcome = card.Outcomes[outcomeIndex];
        EncounterOutcomeEffect effect = outcome.Effect;
        Player player = _triMap.CurrentPlayer;

        int choiceCost = 0;
        if (effect != null && !string.IsNullOrEmpty(effect.ResourceCost) && effect.ResourceCost.StartsWith("Choice:"))
        {
            int.TryParse(effect.ResourceCost.Substring(7), out choiceCost);
        }

        int countToGive = choiceCost > 0 && player != null ? Mathf.Min(choiceCost, player.TotalResources) : 0;

        if (countToGive > 0 && !_triMap.SkipNextOutcomeResourceDeduction)
        {
            if (player.Type != Player.PlayerType.AI && _discardDialog != null)
            {
                string title = UIStrings.EncounterSurrenderTitle.GetText();
                string prompt = card.Type == EncounterCardType.MarauderDemand
                    ? string.Format(UIStrings.EncounterMarauderDemandPrompt.GetText(), player.PlayerName, countToGive)
                    : string.Format(UIStrings.EncounterSurrenderPrompt.GetText(), player.PlayerName, countToGive);

                _discardDialog.Open(
                    player,
                    countToGive,
                    onComplete: () =>
                    {
                        _triMap.SkipNextOutcomeResourceDeduction = true;
                        string toast = string.Format(UIStrings.EncounterSurrenderToast.GetText(), player.PlayerName, countToGive);
                        _showToast?.Invoke(toast, false, 3.5f);
                        _onResourcesChanged?.Invoke();

                        ChooseUpgradeLossThenSelect(effect, player, advanceFirst, outcomeIndex);
                    },
                    customTitle: title,
                    customPrompt: prompt);
                return;
            }
            else
            {
                var toDiscard = AiPlayerController.ChooseDiscardCards(player, countToGive);
                player.DiscardResources(toDiscard);
                _triMap.SkipNextOutcomeResourceDeduction = true;
                string toast = string.Format(UIStrings.EncounterSurrenderToast.GetText(), player.PlayerName, countToGive);
                _showToast?.Invoke(toast, false, 3.5f);
                _onResourcesChanged?.Invoke();
            }
        }
        else if (choiceCost > 0)
        {
            _triMap.SkipNextOutcomeResourceDeduction = true;
        }

        ChooseUpgradeLossThenSelect(effect, player, advanceFirst, outcomeIndex);
    }

    /// <summary>
    /// Stage 2 of outcome selection: if the outcome removes a mothership upgrade, a human player
    /// picks which one in a dialog; otherwise (AI or nothing to choose) the outcome is applied directly.
    /// </summary>
    private void ChooseUpgradeLossThenSelect(EncounterOutcomeEffect effect, Player player, bool advanceFirst, int outcomeIndex)
    {
        bool needsChoice = effect != null && effect.LoseUpgrade &&
                           !_triMap.SkipNextOutcomeUpgradeLoss &&
                           player != null && player.TotalFlagshipUpgrades > 0;

        if (needsChoice && player.Type != Player.PlayerType.AI && _upgradeDialog != null)
        {
            _upgradeDialog.Open(player, (FlagshipUpgrade upgrade) =>
            {
                RemoveUpgrade(player, upgrade);
                _triMap.SkipNextOutcomeUpgradeLoss = true;
                string toast = string.Format(UIStrings.EncounterLoseUpgradeToast.GetText(),
                    player.PlayerName, UIStrings.GetUpgradeSingularName(upgrade));
                _showToast?.Invoke(toast, true, 3.5f);
                _onResourcesChanged?.Invoke();

                ChooseUpgradeGainThenSelect(effect, player, advanceFirst, outcomeIndex);
            });
            return;
        }

        ChooseUpgradeGainThenSelect(effect, player, advanceFirst, outcomeIndex);
    }

    /// <summary>
    /// Stage 2b of outcome selection: if the outcome grants a flagship upgrade, a human player
    /// picks which one in a dialog; otherwise (AI or default) it is applied automatically.
    /// </summary>
    private void ChooseUpgradeGainThenSelect(EncounterOutcomeEffect effect, Player player, bool advanceFirst, int outcomeIndex)
    {
        bool hasAvailableUpgrades = player != null &&
            (player.AvailableCannonsCount > 0 || player.AvailableBoostersCount > 0 || player.AvailableFreightPodsCount > 0);
        bool needsChoice = effect != null && effect.GainUpgrade &&
                           !_triMap.SkipNextOutcomeUpgradeGain &&
                           hasAvailableUpgrades;

        if (needsChoice && player.Type != Player.PlayerType.AI && _upgradeDialog != null)
        {
            _upgradeDialog.OpenForGain(player, (FlagshipUpgrade upgrade) =>
            {
                AddUpgrade(player, upgrade);
                _triMap.SkipNextOutcomeUpgradeGain = true;
                string toast = string.Format(UIStrings.EncounterGainUpgradeToast.GetText(),
                    player.PlayerName, UIStrings.GetUpgradeSingularName(upgrade));
                _showToast?.Invoke(toast, false, 3.5f);
                _onResourcesChanged?.Invoke();

                ChooseShipToBlockThenSelect(effect, player, advanceFirst, outcomeIndex);
            });
            return;
        }
        else if (needsChoice && player.Type == Player.PlayerType.AI)
        {
            FlagshipUpgrade aiUpgrade;
            if (player.AvailableCannonsCount > 0) aiUpgrade = FlagshipUpgrade.Cannon;
            else if (player.AvailableBoostersCount > 0) aiUpgrade = FlagshipUpgrade.Drive;
            else aiUpgrade = FlagshipUpgrade.CargoModule;

            AddUpgrade(player, aiUpgrade);
            _triMap.SkipNextOutcomeUpgradeGain = true;
            _onResourcesChanged?.Invoke();
        }
        else if (effect != null && effect.GainUpgrade && !hasAvailableUpgrades)
        {
            _showToast?.Invoke(UIStrings.LimitUpgradesReached.GetText(), true, 3.5f);
        }

        ChooseShipToBlockThenSelect(effect, player, advanceFirst, outcomeIndex);
    }

    /// <summary>
    /// Stage 3 of outcome selection: if the outcome blocks one ship for the flight phase and the player
    /// has more than 1 movable ship, prompt the human player to select which ship cannot fly.
    /// </summary>
    private void ChooseShipToBlockThenSelect(EncounterOutcomeEffect effect, Player player, bool advanceFirst, int outcomeIndex)
    {
        var movableUnits = player?.Units?.Where(u => u != null && !u.IsDestroyed && u.MovementRemaining > 0).ToList();
        bool needsShipChoice = effect != null && effect.BlockOneShip &&
                               player != null && player.Type != Player.PlayerType.AI &&
                               movableUnits != null && movableUnits.Count > 1;

        if (needsShipChoice && _shipChoiceDialog != null)
        {
            _shipChoiceDialog.Open(
                movableUnits,
                UIStrings.EncounterBlockShipTitle.GetText(),
                UIStrings.EncounterBlockShipPrompt.GetText(),
                chosenShip =>
                {
                    ChooseSpaceJumpThenSelect(effect, player, advanceFirst, outcomeIndex, chosenShip);
                });
            return;
        }

        Unit autoShip = movableUnits?.FirstOrDefault();
        ChooseSpaceJumpThenSelect(effect, player, advanceFirst, outcomeIndex, autoShip);
    }

    /// <summary>
    /// Stage 4 of outcome selection: if the outcome grants a space jump and the player has multiple ships,
    /// prompt the human player to select which ship jumps.
    /// </summary>
    private void ChooseSpaceJumpThenSelect(EncounterOutcomeEffect effect, Player player, bool advanceFirst, int outcomeIndex, Unit blockedShip)
    {
        var allUnits = player?.Units?.Where(u => u != null && !u.IsDestroyed).ToList();
        bool needsJumpChoice = effect != null && effect.SpaceJump &&
                               player != null && player.Type != Player.PlayerType.AI &&
                               allUnits != null && allUnits.Count > 1;

        if (needsJumpChoice && _shipChoiceDialog != null)
        {
            _shipChoiceDialog.Open(
                allUnits,
                UIStrings.EncounterSpaceJumpTitle.GetText(),
                UIStrings.EncounterSpaceJumpPrompt.GetText(),
                chosenJump =>
                {
                    ChooseResourceRewardThenSelect(effect, player, advanceFirst, outcomeIndex, blockedShip, chosenJump);
                },
                buttonColor: new Color(0.18f, 0.42f, 0.65f, 0.95f));
            return;
        }

        Unit autoJump = allUnits?.FirstOrDefault();
        ChooseResourceRewardThenSelect(effect, player, advanceFirst, outcomeIndex, blockedShip, autoJump);
    }

    /// <summary>
    /// Stage 5 of outcome selection: if the outcome rewards resources of player's choice ("Choice:N"),
    /// prompt the human player to select which resources they want to receive.
    /// </summary>
    private void ChooseResourceRewardThenSelect(
        EncounterOutcomeEffect effect,
        Player player,
        bool advanceFirst,
        int outcomeIndex,
        Unit blockedShip,
        Unit jumpShip)
    {
        int choiceCount = 0;
        if (effect != null && !string.IsNullOrEmpty(effect.ResourceReward) && effect.ResourceReward.StartsWith("Choice:"))
        {
            int.TryParse(effect.ResourceReward.Substring(7), out choiceCount);
        }

        if (choiceCount > 0 && player != null && player.Type != Player.PlayerType.AI)
        {
            PresentRewardResourceSelection(choiceCount, (Dictionary<ResourceType, int> chosenRewards) =>
            {
                FinishOutcomeSelection(advanceFirst, outcomeIndex, blockedShip, jumpShip, chosenRewards);
            });
            return;
        }

        FinishOutcomeSelection(advanceFirst, outcomeIndex, blockedShip, jumpShip, null);
    }

    private void PresentRewardResourceSelection(int targetRewardAmount, Action<Dictionary<ResourceType, int>> onConfirmed)
    {
        _rewardSelections.Clear();
        foreach (ResourceType r in GameRules.Resources)
        {
            _rewardSelections[r] = 0;
        }

        RefreshRewardResourceSelectionUI(targetRewardAmount, onConfirmed);
    }

    private void RefreshRewardResourceSelectionUI(int targetRewardAmount, Action<Dictionary<ResourceType, int>> onConfirmed)
    {
        ClearOutcomeButtons();
        _dialog.SetResolveButton(false);

        int currentSelectedTotal = _rewardSelections.Values.Sum();
        bool isValid = currentSelectedTotal == targetRewardAmount;

        _dialog.AddText(
            string.Format(UIStrings.EncounterChooseResourceRewardPrompt.GetText(), targetRewardAmount),
            14,
            new Color(0.9f, 0.95f, 1f));

        ResourceType[] resources = new ResourceType[]
        {
            ResourceType.Ore,
            ResourceType.Fuel,
            ResourceType.Carbon,
            ResourceType.Food,
            ResourceType.TradeGoods
        };

        RectTransform row = _dialog.AddRow(12f);
        Color stepperColor = new Color(0.2f, 0.25f, 0.3f, 1f);
        foreach (ResourceType resource in resources)
        {
            ResourceType res = resource;
            int selectedCount = _rewardSelections.ContainsKey(res) ? _rewardSelections[res] : 0;
            bool canAdd = currentSelectedTotal < targetRewardAmount;

            GameObject group = UIFactory.CreateElement("RewardStepper", row);
            UIFactory.AddHorizontalLayout(group, 2f).childForceExpandWidth = false;

            Button minus = _dialog.AddButton("-", selectedCount > 0, () =>
            {
                _rewardSelections[res] = selectedCount - 1;
                RefreshRewardResourceSelectionUI(targetRewardAmount, onConfirmed);
            }, stepperColor, 12, 28f, group.transform);
            EncounterDialogUI.SetPreferredSize(minus, 26f, 28f);

            Text label = _dialog.AddText(
                string.Format("{0}\n+{1}", UIStrings.GetResourceName(res), selectedCount),
                11,
                Color.white,
                group.transform);
            EncounterDialogUI.SetPreferredSize(label, 52f, 32f);

            Button plus = _dialog.AddButton("+", canAdd, () =>
            {
                _rewardSelections[res] = selectedCount + 1;
                RefreshRewardResourceSelectionUI(targetRewardAmount, onConfirmed);
            }, stepperColor, 12, 28f, group.transform);
            EncounterDialogUI.SetPreferredSize(plus, 26f, 28f);
        }

        _dialog.AddText(
            string.Format(UIStrings.EncounterSelectedRewardCount.GetText(), currentSelectedTotal, targetRewardAmount),
            13,
            isValid ? new Color(0.4f, 1f, 0.6f) : new Color(1f, 0.75f, 0.3f));

        _dialog.AddButton(
            string.Format(UIStrings.EncounterConfirmReward.GetText(), currentSelectedTotal, targetRewardAmount),
            isValid,
            () =>
            {
                ClearOutcomeButtons();
                string toast = string.Format(UIStrings.EncounterRewardReceivedToast.GetText(), targetRewardAmount);
                _showToast?.Invoke(toast, false, 3.5f);
                onConfirmed(new Dictionary<ResourceType, int>(_rewardSelections));
            },
            new Color(0.18f, 0.55f, 0.32f, 1f),
            13,
            36f);
    }

    private static int GetDonationOutcomeIndex(EncounterCard card, int donationAmount)
    {
        if (card == null || card.Outcomes == null || card.Outcomes.Count == 0) return 0;
        if (card.Outcomes.Count == 3)
        {
            if (donationAmount <= 0) return 0;
            if (donationAmount < 3) return 1;
            return 2;
        }
        return Mathf.Clamp(donationAmount, 0, card.Outcomes.Count - 1);
    }

    private void FinishOutcomeSelection(
        bool advanceFirst,
        int outcomeIndex,
        Unit blockedShip = null,
        Unit jumpShip = null,
        Dictionary<ResourceType, int> rewardResources = null)
    {
        if (Networking.NetworkManager.Instance != null &&
            Networking.NetworkManager.Instance.IsClientConnected &&
            !Networking.NetworkManager.Instance.IsHost)
        {
            var actionDto = new AgentBridge.AgentActionDTO
            {
                action = "SelectEncounterOutcome",
                outcomeIndex = outcomeIndex
            };

            if (blockedShip != null && blockedShip.Tri != null)
            {
                actionDto.hasTargetUnit = true;
                actionDto.targetUnitQ = blockedShip.Tri.Q;
                actionDto.targetUnitR = blockedShip.Tri.R;

                string blockMsg = string.Format(UIStrings.EncounterShipBlockedToast.GetText(), blockedShip.Tri.Q, blockedShip.Tri.R);
                _showToast?.Invoke(blockMsg, true, 4.0f);
            }
            else if (jumpShip != null && jumpShip.Tri != null)
            {
                actionDto.hasTargetUnit = true;
                actionDto.targetUnitQ = jumpShip.Tri.Q;
                actionDto.targetUnitR = jumpShip.Tri.R;
            }

            if (_triMap.PendingEncounterDonatedResources != null && _triMap.PendingEncounterDonatedResources.Count > 0)
            {
                _triMap.PendingEncounterDonatedResources.TryGetValue(ResourceType.Ore, out actionDto.ore);
                _triMap.PendingEncounterDonatedResources.TryGetValue(ResourceType.Fuel, out actionDto.fuel);
                _triMap.PendingEncounterDonatedResources.TryGetValue(ResourceType.Carbon, out actionDto.carbon);
                _triMap.PendingEncounterDonatedResources.TryGetValue(ResourceType.Food, out actionDto.food);
                _triMap.PendingEncounterDonatedResources.TryGetValue(ResourceType.TradeGoods, out actionDto.tradeGoods);
            }
            else if (rewardResources != null && rewardResources.Count > 0)
            {
                rewardResources.TryGetValue(ResourceType.Ore, out actionDto.ore);
                rewardResources.TryGetValue(ResourceType.Fuel, out actionDto.fuel);
                rewardResources.TryGetValue(ResourceType.Carbon, out actionDto.carbon);
                rewardResources.TryGetValue(ResourceType.Food, out actionDto.food);
                rewardResources.TryGetValue(ResourceType.TradeGoods, out actionDto.tradeGoods);
            }

            Networking.NetworkManager.Instance.Client.SendAction(actionDto);
            return;
        }

        if (advanceFirst && _triMap.CurrentEncounterPhase == EncounterCardPhase.ReadPrompt)
        {
            _triMap.AdvanceEncounterAfterPlayerResponse();
        }

        if (blockedShip != null && blockedShip.Tri != null)
        {
            _triMap.PendingBlockedShipCoord = new Vector2Int(blockedShip.Tri.Q, blockedShip.Tri.R);
        }
        if (jumpShip != null && jumpShip.Tri != null)
        {
            _triMap.PendingSpaceJumpShipCoord = new Vector2Int(jumpShip.Tri.Q, jumpShip.Tri.R);
        }

        if (rewardResources != null && rewardResources.Count > 0)
        {
            _triMap.PendingEncounterRewardResources = new Dictionary<ResourceType, int>(rewardResources);
        }

        _triMap.SelectEncounterOutcome(outcomeIndex);
    }

    private static void RemoveUpgrade(Player player, FlagshipUpgrade upgrade)
    {
        switch (upgrade)
        {
            case FlagshipUpgrade.Drive: player.RemoveFlagshipDrive(); break;
            case FlagshipUpgrade.Cannon: player.RemoveFlagshipCannon(); break;
            case FlagshipUpgrade.CargoModule: player.RemoveFlagshipCargoModule(); break;
        }
    }

    private static void AddUpgrade(Player player, FlagshipUpgrade upgrade)
    {
        if (player == null) return;
        switch (upgrade)
        {
            case FlagshipUpgrade.Drive: player.AddFlagshipDrive(); break;
            case FlagshipUpgrade.Cannon: player.AddFlagshipCannon(); break;
            case FlagshipUpgrade.CargoModule: player.AddFlagshipCargoModule(); break;
        }
    }

    private void CreateActionButton(string label, int index, bool interactable, UnityAction onClick)
    {
        _dialog.AddActionButton(label, interactable, onClick).transform.SetAsFirstSibling();
    }

    private void CreateOutcomeButton(EncounterCardOutcome outcome, int index, string labelText = null)
    {
        string label = !string.IsNullOrEmpty(labelText) ? labelText : outcome.Condition;
        if (!string.IsNullOrEmpty(outcome.EffectSummary) && !label.Contains(outcome.EffectSummary))
        {
            label = string.Format("{0}\n({1})", label, outcome.EffectSummary);
        }

        _dialog.AddButton(
            label,
            true,
            () => SelectOutcomeWithResourceCheck(_triMap.CurrentEncounterCard, index, advanceFirst: false),
            new Color(0.12f, 0.35f, 0.55f, 0.95f),
            12,
            42f);
    }

    private void ClearOutcomeButtons()
    {
        if (_dialog != null)
        {
            _dialog.ClearContent();
        }
    }

    private static string GetOpponentRoleDisplayName(EncounterOpponentRole role)
    {
        switch (role)
        {
            case EncounterOpponentRole.Right: return UIStrings.OpponentRoleRight.GetText();
            case EncounterOpponentRole.Left: return UIStrings.OpponentRoleLeft.GetText();
            case EncounterOpponentRole.SecondRight: return UIStrings.OpponentRoleSecondRight.GetText();
            case EncounterOpponentRole.SecondLeft: return UIStrings.OpponentRoleSecondLeft.GetText();
            default: return UIStrings.EncounterOpponentHeading.GetText();
        }
    }

    private string BuildOpponentComparisonInfo(EncounterCard card, Player player, Player opponent)
    {
        StringBuilder sb = new StringBuilder();
        string roleName = GetOpponentRoleDisplayName(card.OpponentRole);
        sb.AppendLine();
        sb.Append(string.Format("{0}: {1} ({2})", UIStrings.EncounterOpponentHeading.GetText(), opponent.PlayerName, roleName));

        bool showDrives = card.Type == EncounterCardType.MarauderAttack ||
                          card.Type == EncounterCardType.SpaceDistortion ||
                          (card.Type == EncounterCardType.Rescue && card.ComparisonType == EncounterComparisonType.Speed);

        bool showCannons = card.Type == EncounterCardType.MarauderAttack ||
                           card.Type == EncounterCardType.TraderMarauder ||
                           card.Type == EncounterCardType.MarauderDemand ||
                           card.Type == EncounterCardType.MarauderTrade ||
                           card.Type == EncounterCardType.MarauderRaidOffer ||
                           (card.Type == EncounterCardType.Rescue && card.ComparisonType == EncounterComparisonType.Combat);

        int myDrives = player.EffectiveDriveCount;
        int oppDrives = opponent.EffectiveDriveCount;
        int myCannons = player.EffectiveCannonCount;
        int oppCannons = opponent.EffectiveCannonCount;

        string myDriveLabel = myDrives == 1 ? UIStrings.Booster.GetText() : UIStrings.Boosters.GetText();
        string oppDriveLabel = oppDrives == 1 ? UIStrings.Booster.GetText() : UIStrings.Boosters.GetText();
        string myCannonLabel = myCannons == 1 ? UIStrings.Cannon.GetText() : UIStrings.Cannons.GetText();
        string oppCannonLabel = oppCannons == 1 ? UIStrings.Cannon.GetText() : UIStrings.Cannons.GetText();

        string myStats;
        string oppStats;

        if (showDrives && showCannons)
        {
            myStats = string.Format("{0} {1}  ·  {2} {3}", myDrives, myDriveLabel, myCannons, myCannonLabel);
            oppStats = string.Format("{0} {1}  ·  {2} {3}", oppDrives, oppDriveLabel, oppCannons, oppCannonLabel);
        }
        else if (showDrives)
        {
            myStats = string.Format("{0} {1}", myDrives, myDriveLabel);
            oppStats = string.Format("{0} {1}", oppDrives, oppDriveLabel);
        }
        else
        {
            myStats = string.Format("{0} {1}", myCannons, myCannonLabel);
            oppStats = string.Format("{0} {1}", oppCannons, oppCannonLabel);
        }

        sb.AppendLine();
        sb.Append(string.Format("• {0}: {1}", UIStrings.EncounterYourFlagship.GetText(), myStats));
        sb.AppendLine();
        sb.Append(string.Format("• {0}: {1}", UIStrings.EncounterOpponentFlagship.GetText(), oppStats));

        return sb.ToString();
    }

    private static Sprite LoadEncounterCardSprite(EncounterCard card)
    {
        if (card == null)
        {
            return null;
        }

        int charIdx = Mathf.Clamp(card.CharacterIndex, 0, 3);
        string resourceName;
        switch (card.Type)
        {
            case EncounterCardType.TraderGift:
                resourceName = "Traders_" + charIdx;
                break;
            case EncounterCardType.TraderMarauder:
            case EncounterCardType.MarauderDemand:
            case EncounterCardType.MarauderTrade:
            case EncounterCardType.MarauderRaidOffer:
            case EncounterCardType.MarauderAttack:
                resourceName = "Marauders_" + charIdx;
                break;
            case EncounterCardType.Drifter:
                resourceName = "Drifters_" + charIdx;
                break;
            case EncounterCardType.Rescue:
                switch (card.Number)
                {
                    case 20:
                        resourceName = "Diplomats_" + charIdx;
                        break;
                    case 21:
                        resourceName = "Scientists_" + charIdx;
                        break;
                    case 24:
                        resourceName = "Cultivators_" + charIdx;
                        break;
                    case 25:
                        resourceName = "Drifters_" + charIdx;
                        break;
                    default:
                        resourceName = "Traders_" + charIdx;
                        break;
                }
                break;
            case EncounterCardType.SpaceDistortion:
                resourceName = "Wormhole";
                break;
            case EncounterCardType.Event:
                resourceName = "RavagesOfTime";
                break;
            default:
                return null;
        }

        Sprite sprite = Resources.Load<Sprite>("FolkImages/" + resourceName);
        if (sprite == null)
        {
            Texture2D tex = Resources.Load<Texture2D>("FolkImages/" + resourceName);
            if (tex != null)
            {
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
        }
        return sprite;
    }
}
