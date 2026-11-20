using Interfaces;
using UnityEngine;

namespace ControllerStates
{
    public abstract class ControllerStateBase : IControllerState
    {
        // Camera Dragging bookkeeping variables
        protected static int _mouseDragThreshold = 1; // Threshold of mouse movement to start a drag


        public abstract void Enter();
        public virtual IState Execute()
        {
            return BeforeExecute();
        }

        public abstract void Exit();

        public TriMap TriMap { get; protected set; }
        public SelectionController SelectionController { get; protected set; }
        public Tri[] TriPath { get; protected set; }

        public Tri TriUnderMouse { get; protected set; }

        public Tri TriLastUnderMouse { get; protected set; }

        public Vector3 LastMouseGroundPlanePosition { get; protected set;}

        public Vector3 LastMousePosition { get; protected set;}

        public LineRenderer LineRenderer { get; protected set;}

        public LayerMask LayerIDForTriTiles  { get; protected set;}

        public Vector3 CameraTargetOffset {get; protected set;}

        protected IState BeforeExecute()
        {
            TriUnderMouse = MouseToTri();

            if( Input.GetKeyDown(KeyCode.Escape) )
            {
                SelectionController.SelectedUnit = null;
                SelectionController.SelectedColony = null;

                // Also do cleanup of any UI stuff associated with modes.
                TriPath = null;
                return new DetectModeState(this);
            }
            return this;
        }

        protected void AfterExecute(IState nextState)
        {
            Update_ScrollZoom();

            LastMousePosition = Input.mousePosition;
            if (UIManager.IsPointerOverUI())
            {
                TriUnderMouse = null;
            }

            if(TriLastUnderMouse != TriUnderMouse)
            {
                if(TriUnderMouse != null && !UIManager.IsTriUnderHUD(TriUnderMouse))
                {
                    if (TriUnderMouse.SystemKind == SystemKind.Trade && TriUnderMouse.AlienCivilization != AlienCivilization.None)
                    {
                        PlayerHandler.Instance?.ShowOutpostTooltip(TriUnderMouse);
                        PlayerHandler.Instance?.HidePlanetTooltip();
                    }
                    else if (TriUnderMouse.SystemKind == SystemKind.Planets)
                    {
                        PlayerHandler.Instance?.HideOutpostTooltip();
                        PlayerHandler.Instance?.ShowPlanetTooltip(TriUnderMouse);
                    }
                    else
                    {
                        PlayerHandler.Instance?.HideOutpostTooltip();
                        PlayerHandler.Instance?.HidePlanetTooltip();
                    }
                }
                else
                {
                    PlayerHandler.Instance?.HideOutpostTooltip();
                    PlayerHandler.Instance?.HidePlanetTooltip();
                }
            }
            TriLastUnderMouse = TriUnderMouse;
            ((ControllerStateBase)nextState).TriLastUnderMouse = TriLastUnderMouse;

            if(SelectionController.SelectedUnit != null &&
               TriPath != null &&
               TriPath.Length > 0 &&
               TriPath[0] == SelectionController.SelectedUnit.Tri)
            {
                DrawPath(TriPath);
            }
            else
            {
                TriPath = null;
                DrawPath(null);   // Clear the path display
            }   
        }

        public void ClearTriPath()
        {
            TriPath = null;
            DrawPath(null);
        }

        void DrawPath(Tri[] triPath)
        {
            if (LineRenderer == null)
            {
                return;
            }

            if(triPath == null || triPath.Length == 0)
            {
                LineRenderer.enabled = false;
                LineRenderer.positionCount = 0;
                return;
            }
            LineRenderer.enabled = true;

            Vector3[] ps = new Vector3[ triPath.Length ];
            for (int i = 0; i < triPath.Length; i++)
            {
                var tri = triPath[i];
                ps[i] = tri.CenterPosition() + (Vector3.up * 0.05f);
            }

            LineRenderer.positionCount = ps.Length;
            LineRenderer.SetPositions(ps);
        }

        private static Vector3 _lastRaycastMousePos = new Vector3(-9999f, -9999f, -9999f);
        private static Vector3 _lastRaycastCamPos = new Vector3(-9999f, -9999f, -9999f);
        private static Tri _cachedRaycastTri = null;

        Tri MouseToTri()
        {
            if (UIManager.IsPointerOverUI())
            {
                _cachedRaycastTri = null;
                return null;
            }

            Camera mainCam = UIManager.MainCamera;
            if (mainCam == null)
            {
                _cachedRaycastTri = null;
                return null;
            }

            Vector3 mousePos = Input.mousePosition;
            Vector3 camPos = mainCam.transform.position;
            if (mousePos == _lastRaycastMousePos && camPos == _lastRaycastCamPos)
            {
                return _cachedRaycastTri;
            }

            _lastRaycastMousePos = mousePos;
            _lastRaycastCamPos = camPos;

            Ray mouseRay = mainCam.ScreenPointToRay (mousePos);
            RaycastHit hitInfo;

            int layerMask = LayerIDForTriTiles.value;

            if( Physics.Raycast(mouseRay, out hitInfo, Mathf.Infinity, layerMask) )
            {
                TriComponent comp = hitInfo.collider != null ? hitInfo.collider.GetComponentInParent<TriComponent>() : null;
                Tri tri = comp != null ? comp.Tri : null;

                if (tri == null && hitInfo.collider != null)
                {
                    tri = TriMap.GetTriFromGameObject(hitInfo.collider.gameObject);
                }

                if (tri != null && UIManager.IsTriUnderHUD(tri))
                {
                    _cachedRaycastTri = null;
                    return null;
                }

                _cachedRaycastTri = tri;
                return tri;
            }

            _cachedRaycastTri = null;
            return null;
        }

        protected Vector3 MouseToGroundPlane(Vector3 mousePos)
        {
            Camera mainCam = UIManager.MainCamera;
            if (mainCam == null) return Vector3.zero;

            Ray mouseRay = mainCam.ScreenPointToRay (mousePos);
            // What is the point at which the mouse ray intersects Y=0
            if (mouseRay.direction.y >= 0) {
                //Debug.LogError("Why is mouse pointing up?");
                return Vector3.zero;
            }
            float rayLength = (mouseRay.origin.y / mouseRay.direction.y);
            return mouseRay.origin - (mouseRay.direction * rayLength);
        }

        void Update_ScrollZoom()
        {
            // Zoom to scrollwheel
            float scrollAmount = Input.GetAxis ("Mouse ScrollWheel");
            if (scrollAmount == 0f && CameraTargetOffset.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Camera mainCam = UIManager.MainCamera;
            if (mainCam == null) return;

            float minHeight = 2;
            float maxHeight = 20;
            // Move camera towards hitPos
            Vector3 hitPos = MouseToGroundPlane(Input.mousePosition);
            Vector3 dir = hitPos - mainCam.transform.position;

            Vector3 p = mainCam.transform.position;

            // Stop zooming out at a certain distance.
            // TODO: Maybe you should still slide around at 20 zoom?
            if (scrollAmount > 0 || p.y < (maxHeight - 0.1f)) {
                CameraTargetOffset += dir * scrollAmount;
            }
            Vector3 lastCameraPosition = mainCam.transform.position;
            mainCam.transform.position = Vector3.Lerp(mainCam.transform.position, mainCam.transform.position + CameraTargetOffset, Time.deltaTime * 20f);
            CameraTargetOffset -= mainCam.transform.position - lastCameraPosition;

            p = mainCam.transform.position;
            if (p.y < minHeight) {
                p.y = minHeight;
            }
            if (p.y > maxHeight) {
                p.y = maxHeight;
            }
            mainCam.transform.position = p;

            // Change camera angle only if height changed
            if (Mathf.Abs(p.y - lastCameraPosition.y) > 0.001f)
            {
                mainCam.transform.rotation = Quaternion.Euler (
                    Mathf.Lerp (30, 75, mainCam.transform.position.y / maxHeight),
                    mainCam.transform.rotation.eulerAngles.y,
                    mainCam.transform.rotation.eulerAngles.z
                );
            }
        } 
    }
}
