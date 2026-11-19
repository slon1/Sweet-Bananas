using SweetBananas.Data;
using UnityEngine;
using Zenject;

namespace SweetBananas.Core
{
    public sealed class GameBootstrap : IInitializable
    {
        readonly GameConfig _config;
        readonly GameFlow _flow;

        public GameBootstrap(GameConfig config, GameFlow flow)
        {
            _config = config;
            _flow = flow;
        }

        public void Initialize()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = _config.TargetFrameRate;

            Physics2D.gravity = new Vector2(0f, _config.GravityY);
            Time.fixedDeltaTime = _config.FixedDeltaTime;
            Physics2D.velocityIterations = _config.VelocityIterations;
            Physics2D.positionIterations = _config.PositionIterations;

            _flow.SetState(GameState.Playing);
        }
    }
}
