using Interfaces;
using UnityEngine;

namespace ControllerStates
{
    public class UnitMovementState : ControllerStateBase
    {  
        public UnitMovementState(IControllerState previousState)
        {
            TriMap = previousState.TriMap;
            TriPath = previousState.TriPath;
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

        }

        public override IState Execute()
        {
            IState nextState = base.Execute();
            if(nextState != this)
                return nextState;

            if (TriMap.EncounterPending)
            {
                string msg = UIStrings.EncounterPendingMovementBlocked.GetText();
                Debug.LogWarning(msg);
                PlayerHandler.ShowToast(msg, isWarning: true);
                ClearTriPath();
                nextState = new DetectModeState(this);
                AfterExecute(nextState);
                return nextState;
            }

            if (TriMap.CurrentPhase != GameTurnPhase.Flight ||
                !TriMap.CanMoveUnit(SelectionController.SelectedUnit))
            {
                ClearTriPath();
                nextState = new DetectModeState(this);
            }
            else if (GameInput.GetMouseButtonUp(1) || !GameInput.GetMouseButton(1))
            {
                Debug.Log("Complete unit movement.");

                if (TriPath == null || TriPath.Length < 2 || TriUnderMouse != TriLastUnderMouse)
                {
                    TriPath = TriMap.FindShipPath(SelectionController.SelectedUnit, TriUnderMouse);
                }

                if (TriMap.CanMoveShipAlongPath(SelectionController.SelectedUnit, TriPath))
                {
                    RuntimeLog.WriteLine(string.Format(
                        "[FlightMovement] Executing move for {0} (Player: '{1}') from ({2},{3}) towards target ({4},{5}). Path length: {6} steps to ({7},{8}). Movement remaining: {9} -> {10}.",
                        SelectionController.SelectedUnit.ShipType,
                        TriMap.GetPlayerForUnit(SelectionController.SelectedUnit)?.PlayerName ?? "Unknown",
                        SelectionController.SelectedUnit.Tri.Q,
                        SelectionController.SelectedUnit.Tri.R,
                        TriUnderMouse != null ? TriUnderMouse.Q.ToString() : "?",
                        TriUnderMouse != null ? TriUnderMouse.R.ToString() : "?",
                        TriPath.Length - 1,
                        TriPath[TriPath.Length - 1].Q,
                        TriPath[TriPath.Length - 1].R,
                        SelectionController.SelectedUnit.MovementRemaining,
                        SelectionController.SelectedUnit.MovementRemaining - (TriPath.Length - 1)));

                    SelectionController.SelectedUnit.SetTriPath(TriPath);
                    Tri destTri = TriPath[TriPath.Length - 1];
                    TriMap.RegisterUnitAnimating(SelectionController.SelectedUnit, destTri);

                    var mouseController = Object.FindFirstObjectByType<MouseController>();
                    mouseController.StartCoroutine( TriMap.DoUnitMoves(SelectionController.SelectedUnit) );

                    if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected)
                    {
                        var moveAction = new AgentBridge.AgentActionDTO
                        {
                            action = "MoveShip",
                            fromQ = SelectionController.SelectedUnit.Tri.Q,
                            fromR = SelectionController.SelectedUnit.Tri.R,
                            toQ = destTri.Q,
                            toR = destTri.R,
                            unitId = SelectionController.SelectedUnit.GetHashCode()
                        };
                        Networking.NetworkManager.Instance.Client.SendAction(moveAction);
                    }
                }
                else
                {
                    string failureReason = null;
                    if (TriUnderMouse != null &&
                        !TriMap.CanShipEndMoveAt(SelectionController.SelectedUnit, TriUnderMouse, out failureReason))
                    {
                        Debug.LogWarning(failureReason);
                    }
                    else
                    {
                        failureReason = UIStrings.DestinationNotReachable.GetText();
                        Debug.LogWarning(failureReason);
                    }

                    RuntimeLog.WriteLine(string.Format(
                        "[FlightMovement] Move rejected for {0} (Player: '{1}') targeting ({2},{3}). Reason: {4}",
                        SelectionController.SelectedUnit.ShipType,
                        TriMap.GetPlayerForUnit(SelectionController.SelectedUnit)?.PlayerName ?? "Unknown",
                        TriUnderMouse != null ? TriUnderMouse.Q.ToString() : "?",
                        TriUnderMouse != null ? TriUnderMouse.R.ToString() : "?",
                        failureReason));

                    PlayerHandler.ShowToast(failureReason, isWarning: true);
                }

                ClearTriPath();
                nextState = new DetectModeState(this);
            }
            else
            {
                if( TriPath == null || TriUnderMouse != TriLastUnderMouse )
                {
                    TriPath = TriMap.FindShipPath(
                        SelectionController.SelectedUnit,
                        TriUnderMouse);
                }
            }
        
            AfterExecute(nextState);

            return nextState;
        }

        public override void Exit() 
        {
            ClearTriPath();
        }


        new Vector3 MouseToGroundPlane(Vector3 mousePos)
        {
            Ray mouseRay = Camera.main.ScreenPointToRay (mousePos);
            // What is the point at which the mouse ray intersects Y=0
            if (mouseRay.direction.y >= 0) {
                //Debug.LogError("Why is mouse pointing up?");
                return Vector3.zero;
            }
            float rayLength = (mouseRay.origin.y / mouseRay.direction.y);
            return mouseRay.origin - (mouseRay.direction * rayLength);
        }
    }
}
