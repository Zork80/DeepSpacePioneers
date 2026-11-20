using Interfaces;
using UnityEngine;

public class StateMachine : MonoBehaviour
{
    private IState _currentState;
    public IState CurrentState => _currentState;
    private IState _previousState;

    public void ChangeState(IState newState)
    {
        _currentState?.Exit();

        _previousState = _currentState;

        _currentState = newState;
        _currentState.Enter();
    }

    public void ExecuteStateUpdate()
    {
        var runningState = _currentState;
        if(runningState != null)
        {
            var nextState = runningState.Execute();
            if(nextState != runningState)
            {    runningState.Exit();
                _currentState = nextState;
                _currentState.Enter();
            }
        }
    }

    public void SwitchToPreviousState()
    {
        _currentState.Exit();
        _currentState = _previousState;
        _currentState.Enter();
    }
}
