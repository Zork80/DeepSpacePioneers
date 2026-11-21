using Interfaces;
using UnityEngine;

namespace ControllerStates
{
    public class CameraDragState : ControllerStateBase
    {  
        public CameraDragState(IControllerState previousState)
        {
            TriMap = previousState.TriMap;
            TriPath = previousState.TriPath;
            TriUnderMouse = previousState.TriUnderMouse;
            TriLastUnderMouse = previousState.TriLastUnderMouse;
            SelectionController = previousState.SelectionController;
            LastMouseGroundPlanePosition = MouseToGroundPlane(GameInput.MousePosition);
            LastMousePosition = previousState.LastMousePosition;   
            LineRenderer = previousState.LineRenderer;
            LayerIDForTriTiles = previousState.LayerIDForTriTiles;
            CameraTargetOffset = previousState.CameraTargetOffset;
        }

        Vector3 _cameraTargetOffset;


        public override void Enter()
        {

        }

        public override IState Execute()
        {
            IState nextState = base.Execute();
            if(nextState != this)
                return nextState;


            if (!GameInput.GetMouseButton(2) && !GameInput.GetMouseButton(1) && !GameInput.GetMouseButton(0))
            {
                nextState = new DetectModeState(this);
            }
            else
            {        
                // Right now, all we need are camera controls

                Vector3 hitPos = MouseToGroundPlane(GameInput.MousePosition);

                if (hitPos != Vector3.zero && LastMouseGroundPlanePosition != Vector3.zero)
                {
                    Vector3 diff = LastMouseGroundPlanePosition - hitPos;
                    Camera mainCam = UIManager.MainCamera;
                    if (mainCam != null)
                    {
                        mainCam.transform.Translate (diff, Space.World);
                    }
                    LastMouseGroundPlanePosition = MouseToGroundPlane(GameInput.MousePosition);
                }
            }

            AfterExecute(nextState);

            return nextState;
        }

        public override void Exit() 
        {

        }
    }
}
