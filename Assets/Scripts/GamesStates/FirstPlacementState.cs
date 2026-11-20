using Interfaces;

namespace GamesStates
{
    public class FirstPlacementState : GameStateBase
    {
        public FirstPlacementState(TriMap triMap)
        {
            TriMap = triMap;
        }

        public override void Enter()
        {
        }

        public override IState Execute()
        {
            IState nextState = TriMap.CurrentPlayer.Colonies.Length == 0 ? (IState) this : new MiddlePlacementState(this);
            return nextState;
        }

        public override void Exit()
        {
        }
    }
}