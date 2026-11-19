using SweetBananas.Data;
using UnityEngine;

namespace SweetBananas.Services
{
    public sealed class PlayerPrefsSaveService : ISaveService
    {
        public const string Key = "save_v1";

        public SaveData Data { get; private set; }

        public PlayerPrefsSaveService()
        {
            Load();
        }

        public void Load()
        {
            if (!PlayerPrefs.HasKey(Key))
            {
                Data = new SaveData();
                return;
            }

            try
            {
                var json = PlayerPrefs.GetString(Key);
                Data = JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to load save data from '{Key}': {ex.Message}");
                Data = new SaveData();
            }
        }

        public void Save()
        {
            if (Data == null)
                Data = new SaveData();

            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
        }
    }
}
