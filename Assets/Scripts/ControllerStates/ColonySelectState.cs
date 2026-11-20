using Interfaces;
using UnityEngine;

namespace ControllerStates
{
    public class ColonySelectState : ControllerStateBase
    {  
        public ColonySelectState(IControllerState previousState)
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
            Debug.Log("ColonySelectState");
        }

        public override IState Execute()
        {
            IState nextState = base.Execute();
            if(nextState != this)
                return nextState;

            SelectionController.SelectedColony = TriLastUnderMouse.Colony;
            nextState = new ColonySelectedState(this);

            AfterExecute(nextState);

            return nextState;
        }

        public override void Exit() 
        {

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
