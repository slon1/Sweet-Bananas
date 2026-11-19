using SweetBananas.Data;
using SweetBananas.Services;
using UnityEngine;
using Zenject;

namespace SweetBananas.Core
{
    public sealed class ProjectInstaller : MonoInstaller
    {
        [SerializeField] GameConfig gameConfig;

        public override void InstallBindings()
        {
            Container.BindInstance(gameConfig).AsSingle();
            Container.Bind<ISaveService>().To<PlayerPrefsSaveService>().AsSingle();
            Container.Bind<IAudioService>().To<NullAudioService>().AsSingle();
        }
    }
}
