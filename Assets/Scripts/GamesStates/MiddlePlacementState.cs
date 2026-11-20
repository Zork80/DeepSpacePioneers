using Interfaces;

namespace GamesStates
{
    public class MiddlePlacementState : GameStateBase
    {
        public MiddlePlacementState(GameStateBase gameState)
        {
            TriMap = gameState.TriMap;
        }

        public override void Enter()
        {
            throw new System.NotImplementedException();
        }

        public override IState Execute()
        {
            throw new System.NotImplementedException();
        }

        public override void Exit()
        {
            throw new System.NotImplementedException();
        }
    }
}