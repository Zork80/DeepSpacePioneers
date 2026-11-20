using Interfaces;
using UnityEngine;

namespace ControllerStates
{
    public interface IControllerState:IState
    {
        TriMap TriMap { get; }
        SelectionController SelectionController { get; }
        Tri[] TriPath { get; }

        Tri TriUnderMouse { get; }

        Tri TriLastUnderMouse { get; }

        Vector3 LastMouseGroundPlanePosition { get; }

        Vector3 LastMousePosition { get; }
        LayerMask LayerIDForTriTiles { get; }

        LineRenderer LineRenderer { get; }

        Vector3 CameraTargetOffset {get;}
        void ClearTriPath();
    }
}
