namespace SweetBananas.Services
{
    public interface IAudioService
    {
        void PlayTap();

        void PlayWin();

        void PlayFail();

        void PlaySquish();

        void SetMuted(bool muted);
    }
}
