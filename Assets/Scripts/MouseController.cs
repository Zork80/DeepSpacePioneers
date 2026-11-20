using ControllerStates;
using UnityEngine;

public class MouseController : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        _stateMachine = gameObject.AddComponent<StateMachine>();
        _stateMachine.ChangeState(new InitialState(
            FindFirstObjectByType<TriMap>(),
            FindFirstObjectByType<SelectionController>(),
            transform.GetComponentInChildren<LineRenderer>(),
             LayerIDForTriTiles));
    }

    StateMachine _stateMachine;

    public LayerMask LayerIDForTriTiles;

    public void ClearPath()
    {
        LineRenderer lr = transform.GetComponentInChildren<LineRenderer>();
        if (lr != null)
        {
            lr.enabled = false;
            lr.positionCount = 0;
        }

        if (_stateMachine != null && _stateMachine.CurrentState is ControllerStateBase csb)
        {
            csb.ClearTriPath();
        }
    }

    void Update()
    {
        _stateMachine.ExecuteStateUpdate();

        return;     
    }
}
