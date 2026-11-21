using System.Linq;
using Interfaces;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ControllerStates
{
    public class DetectModeState : ControllerStateBase
    {  
        public DetectModeState(IControllerState previousState)
        {
            TriMap = previousState.TriMap;
            TriPath = null;
            TriUnderMouse = previousState.TriUnderMouse;
            TriLastUnderMouse = previousState.TriLastUnderMouse;
            SelectionController = previousState.SelectionController;
            LastMouseGroundPlanePosition = previousState.LastMouseGroundPlanePosition;
            LastMousePosition = previousState.LastMousePosition;
            LineRenderer = previousState.LineRenderer;
            LayerIDForTriTiles = previousState.LayerIDForTriTiles;
            CameraTargetOffset = previousState.CameraTargetOffset;
        }

        public override void Enter()
        {
            ClearTriPath();
        }

        public override IState Execute()
        {
            IState nextState = base.Execute();
            if(nextState != this)
                return nextState;

            // Check to see if we are over a UI/HUD element,
            // if so -- ignore mouse clicks and 3D interactions.
            if( UIManager.IsPointerOverUI() )
            {
                // Over UI or HUD; do not process 3D clicks or drags
            }
            else
            {
                if( GameInput.GetMouseButtonUp(0) )
                {
                    // TODO: Are we clicking on a tri with a unit?
                    //          If so, select it

                    if(TriUnderMouse != null)
                    {
                        if (TriMap.IsPlacingShip)
                        {
                            if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected && !Networking.NetworkManager.Instance.IsHost)
                            {
                                StarshipType st = TriMap.PendingShipPlacementType ?? StarshipType.ColonyShip;
                                string actionName = TriMap.IsPlacingCapturedShip ? "DeployCapturedShip" : "BuildShip";
                                Networking.NetworkManager.Instance.Client.SendAction(new AgentBridge.AgentActionDTO
                                {
                                    action = actionName,
                                    shipType = st.ToString(),
                                    q = TriUnderMouse.Q,
                                    r = TriUnderMouse.R
                                });
                                TriMap.CancelShipPlacement();
                            }
                            else
                            {
                                TriMap.TryPlacePendingShipAt(TriUnderMouse);
                            }
                        }
                        else if (TriMap.IsUpgradingSpaceport)
                        {
                            if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected && !Networking.NetworkManager.Instance.IsHost)
                            {
                                Networking.NetworkManager.Instance.Client.SendAction(new AgentBridge.AgentActionDTO
                                {
                                    action = "UpgradeToSpaceport",
                                    q = TriUnderMouse.Q,
                                    r = TriUnderMouse.R
                                });
                                TriMap.CancelSpaceportUpgrade();
                            }
                            else
                            {
                                TriMap.TryUpgradeSpaceportAt(TriUnderMouse);
                            }
                        }
                        else if (TriMap.IsExecutingSpaceJump)
                        {
                            Unit clickedOwnedShip = TriUnderMouse?.Units?.FirstOrDefault(u =>
                                u != null && !u.IsDestroyed && TriMap.CurrentPlayer != null &&
                                System.Array.IndexOf(TriMap.CurrentPlayer.Units, u) >= 0);

                            if (clickedOwnedShip != null && clickedOwnedShip != TriMap.PendingSpaceJumpShip)
                            {
                                TriMap.SetSpaceJumpShip(clickedOwnedShip);
                            }
                            else
                            {
                                TriggerSpaceJump(TriUnderMouse);
                            }
                        }
                        else if (TriMap.CurrentFoundingPhase != FoundingPhase.Complete)
                        {
                            if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected && !Networking.NetworkManager.Instance.IsHost)
                            {
                                if (TriMap.CurrentFoundingPhase == FoundingPhase.SelectStartingShipPoint)
                                {
                                    Networking.NetworkManager.Instance.Client.SendAction(new AgentBridge.AgentActionDTO
                                    {
                                        action = "SelectFoundingShipPoint",
                                        q = TriUnderMouse.Q,
                                        r = TriUnderMouse.R
                                    });
                                }
                                else if (TriMap.CurrentFoundingPhase == FoundingPhase.PlaceFirstColony ||
                                    TriMap.CurrentFoundingPhase == FoundingPhase.PlaceSecondColony ||
                                    TriMap.CurrentFoundingPhase == FoundingPhase.PlaceThirdColony)
                                {
                                    Networking.NetworkManager.Instance.Client.SendAction(new AgentBridge.AgentActionDTO
                                    {
                                        action = "PlaceFoundingColony",
                                        q = TriUnderMouse.Q,
                                        r = TriUnderMouse.R
                                    });
                                }
                                else if (TriMap.CurrentFoundingPhase == FoundingPhase.SelectSpaceport)
                                {
                                    Networking.NetworkManager.Instance.Client.SendAction(new AgentBridge.AgentActionDTO
                                    {
                                        action = "SelectFoundingSpaceport",
                                        q = TriUnderMouse.Q,
                                        r = TriUnderMouse.R
                                    });
                                }
                            }
                            else
                            {
                                if (TriMap.CurrentFoundingPhase == FoundingPhase.SelectStartingShipPoint)
                                {
                                    TriMap.TrySelectFoundingStartingShipPoint(TriUnderMouse);
                                }
                                else if (TriMap.CurrentFoundingPhase == FoundingPhase.PlaceFirstColony ||
                                    TriMap.CurrentFoundingPhase == FoundingPhase.PlaceSecondColony ||
                                    TriMap.CurrentFoundingPhase == FoundingPhase.PlaceThirdColony)
                                {
                                    TriMap.TryPlaceFoundingColonyAt(TriUnderMouse);
                                }
                                else if (TriMap.CurrentFoundingPhase == FoundingPhase.SelectSpaceport)
                                {
                                    TriMap.TrySelectFoundingSpaceportAt(TriUnderMouse);
                                }
                            }
                        }
                        else
                        {
                            Unit clickedUnit = TriUnderMouse.Units.FirstOrDefault();
                            if (clickedUnit != null)
                            {
                                if (TriMap.CanSelectUnit(clickedUnit))
                                {
                                    SelectionController.SelectedUnit =
                                        SelectionController.SelectedUnit == clickedUnit
                                            ? null
                                            : clickedUnit;
                                }
                                else if (TriMap.CurrentPhase == GameTurnPhase.Flight)
                                {
                                    if (TriMap.LastFlagshipRoll == null)
                                    {
                                        PlayerHandler.ShowToast(UIStrings.FlightRollPendingPrompt.GetText(), isWarning: true);
                                    }
                                    else if (TriMap.EncounterPending)
                                    {
                                        PlayerHandler.ShowToast("Beende zuerst die Begegnung.", isWarning: true);
                                    }
                                }
                            }
                            else if (TriMap.CurrentPhase == GameTurnPhase.TradeAndBuild &&
                                     TriUnderMouse.Colony != null)
                            {
                                Player currPlayer = TriMap.CurrentPlayer;
                                bool isOwnColony = currPlayer != null && (
                                    (currPlayer.Colonies != null && currPlayer.Colonies.Any(c => c == TriUnderMouse.Colony || (c.Tri != null && c.Tri == TriUnderMouse)))
                                );

                                if (isOwnColony && !TriUnderMouse.Colony.IsSpaceport && currPlayer.Type == Player.PlayerType.LOCAL)
                                {
                                    if (TriMap.CanBuildSpaceport())
                                    {
                                        TriMap.TryBeginSpaceportUpgrade();
                                    }
                                    else
                                    {
                                        if (currPlayer.AvailableSpaceportsCount <= 0)
                                        {
                                            PlayerHandler.ShowToast($"Maximale Anzahl an Raumhäfen erreicht ({GameRules.MaxSpaceportsPerPlayer}).", isWarning: true);
                                        }
                                        else if (!currPlayer.HasResources(GameRules.SpaceportCost))
                                        {
                                            PlayerHandler.ShowToast($"Für den Ausbau zum Raumhafen fehlen Rohstoffe ({UIStrings.FormatCost(GameRules.SpaceportCost)}).", isWarning: true);
                                        }
                                    }
                                }
                                nextState = new ColonySelectState(this);
                            }
                            else if(TriMap.CurrentPhase == GameTurnPhase.TradeAndBuild &&
                                    TriUnderMouse.IsColonyPoint)
                            {
                                Debug.LogWarning(
                                    "A colony can only be founded by your colony ship on this colony point.");
                            }
                            else if (TriUnderMouse.IsTradePoint &&
                                     TriMap.CurrentPhase == GameTurnPhase.TradeAndBuild)
                            {
                                Debug.LogWarning(
                                    "A trade station can only be established by your trade ship at this outpost.");
                            }
                        }
                    }
                }
                else if (TriMap.IsExecutingSpaceJump && GameInput.GetMouseButtonDown(1))
                {
                    if (TriUnderMouse != null)
                    {
                        Unit clickedOwnedShip = TriUnderMouse.Units?.FirstOrDefault(u =>
                            u != null && !u.IsDestroyed && TriMap.CurrentPlayer != null &&
                            System.Array.IndexOf(TriMap.CurrentPlayer.Units, u) >= 0);

                        if (clickedOwnedShip != null && clickedOwnedShip != TriMap.PendingSpaceJumpShip)
                        {
                            TriMap.SetSpaceJumpShip(clickedOwnedShip);
                        }
                        else
                        {
                            TriggerSpaceJump(TriUnderMouse);
                        }
                    }
                }
                else if (TriMap.CurrentPhase == GameTurnPhase.Flight &&
                         !TriMap.EncounterPending &&
                         !TriMap.AnimationIsPlaying &&
                         !TriMap.IsExecutingSpaceJump &&
                         TriMap.CanMoveUnit(SelectionController.SelectedUnit) &&
                         GameInput.GetMouseButtonDown(1) )
                {
                    // We have a selected unit, and we've pushed down the right
                    // mouse button, so enter unit movement mode.
                    nextState = new UnitMovementState(this);

                }
                else if (TriMap.CurrentPhase == GameTurnPhase.Flight &&
                         !TriMap.IsExecutingSpaceJump &&
                         SelectionController.SelectedUnit != null &&
                         GameInput.GetMouseButtonDown(1))
                {
                    if (TriMap.LastFlagshipRoll == null)
                    {
                        PlayerHandler.ShowToast(UIStrings.FlightRollPendingPrompt.GetText(), isWarning: true);
                    }
                    else if (TriMap.EncounterPending)
                    {
                        PlayerHandler.ShowToast("Beende zuerst die Begegnung.", isWarning: true);
                    }
                    else if (SelectionController.SelectedUnit != null &&
                             SelectionController.SelectedUnit.MovementRemaining <= 0)
                    {
                        PlayerHandler.ShowToast("Dieses Schiff hat in dieser Runde keine Bewegungspunkte mehr.", isWarning: true);
                    }
                }
                else if( (GameInput.GetMouseButton(2) || ((GameInput.GetMouseButton(1) || (GameInput.GetMouseButton(0) && (GameInput.GetKey(KeyCode.LeftAlt) || GameInput.GetKey(KeyCode.Space)))) && (TriMap.CurrentPhase != GameTurnPhase.Flight || SelectionController.SelectedUnit == null))) && 
                         Vector3.Distance( GameInput.MousePosition, LastMousePosition) > _mouseDragThreshold )
                {
                    nextState = new CameraDragState(this);
                }
            }

            if (nextState == this)
            {
                if (TriMap.CurrentPhase == GameTurnPhase.Flight &&
                    !TriMap.EncounterPending &&
                    !TriMap.AnimationIsPlaying &&
                    !TriMap.IsExecutingSpaceJump &&
                    TriMap.CanMoveUnit(SelectionController.SelectedUnit))
                {
                    if (TriUnderMouse != null && TriUnderMouse != SelectionController.SelectedUnit.Tri)
                    {
                        if (TriPath == null || TriUnderMouse != TriLastUnderMouse)
                        {
                            TriPath = TriMap.FindShipPath(SelectionController.SelectedUnit, TriUnderMouse);
                        }
                    }
                    else
                    {
                        TriPath = null;
                    }
                }
                else
                {
                    TriPath = null;
                }
            }

            AfterExecute(nextState);

            return nextState;
        }

        private void TriggerSpaceJump(Tri targetTri)
        {
            if (targetTri == null) return;

            if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected && !Networking.NetworkManager.Instance.IsHost)
            {
                Networking.NetworkManager.Instance.Client.SendAction(new AgentBridge.AgentActionDTO
                {
                    action = "ExecuteSpaceJump",
                    q = targetTri.Q,
                    r = targetTri.R
                });
            }
            else
            {
                TriMap.TryExecuteSpaceJumpAt(targetTri);
            }
        }

        public override void Exit() 
        {

        }
    }
}
