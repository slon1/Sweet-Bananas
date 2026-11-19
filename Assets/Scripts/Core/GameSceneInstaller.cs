using SweetBananas.Game;
using UnityEngine;
using Zenject;

namespace SweetBananas.Core
{
    public sealed class GameSceneInstaller : MonoInstaller
    {
        [SerializeField] Lollipop _lollipop;
        [SerializeField] ThrowController _throwController;
        [SerializeField] Ribbon _ribbon;
        [SerializeField] RibbonMesh _ribbonMesh;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField] SweetBananas.Game.Debug.SpikeOverlay _spikeOverlay;
#endif

        public override void InstallBindings()
        {
            Container.Bind<GameFlow>().AsSingle();
            Container.Bind<Lollipop>().FromInstance(_lollipop).AsSingle();
            Container.Bind<ThrowController>().FromInstance(_throwController).AsSingle();
            Container.Bind<Ribbon>().FromInstance(_ribbon).AsSingle();
            Container.Bind<RibbonMesh>().FromInstance(_ribbonMesh).AsSingle();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_spikeOverlay != null)
                Container.Bind<SweetBananas.Game.Debug.SpikeOverlay>().FromInstance(_spikeOverlay).AsSingle();
#endif
            Container.BindInterfacesAndSelfTo<GameBootstrap>().AsSingle().NonLazy();
        }
    }
}
