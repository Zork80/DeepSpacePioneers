using Interfaces;

namespace GamesStates
{
    public abstract class GameStateBase : IGameState
    {
        public TriMap TriMap { get; protected set; }

        public abstract void Enter();
        public abstract IState Execute();
        public abstract void Exit();
    }
}