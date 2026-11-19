using Zenject;

namespace SweetBananas.Core
{
    public sealed class GameSceneInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.Bind<GameFlow>().AsSingle();
            Container.BindInterfacesAndSelfTo<GameBootstrap>().AsSingle().NonLazy();
        }
    }
}
