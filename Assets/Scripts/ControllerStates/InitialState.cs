using Interfaces;
using UnityEngine;

namespace ControllerStates
{
    public class InitialState : ControllerStateBase
    {  
        public InitialState(TriMap triMap, SelectionController selectionController, LineRenderer lineRenderer, LayerMask layerIDForTriTiles)
        {
            TriMap = triMap;
            SelectionController = selectionController;
            // LastMouseGroundPlanePosition = lastMouseGroundPlanePosition;
            LineRenderer = lineRenderer;
            LayerIDForTriTiles = layerIDForTriTiles;
        }

        public override void Enter()
        {

        }

        public override IState Execute()
        {
            IState nextState = new DetectModeState(this);       
            return nextState;
        }

        public override void Exit() 
        {

        }
    }
}
