using SweetBananas.Data;

namespace SweetBananas.Services
{
    public interface ISaveService
    {
        SaveData Data { get; }

        void Load();

        void Save();
    }
}
