using System;

namespace SweetBananas.Core
{
    public sealed class GameFlow
    {
        public GameState State { get; private set; } = GameState.Boot;

        public event Action<GameState> StateChanged;

        public void SetState(GameState state)
        {
            if (State == state)
                return;

            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
