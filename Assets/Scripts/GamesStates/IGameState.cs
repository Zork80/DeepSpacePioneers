using Interfaces;

namespace GamesStates
{
    public interface IGameState : IState
    {
        TriMap TriMap { get; }
    }
}