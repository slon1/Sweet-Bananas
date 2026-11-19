using System;

namespace SweetBananas.Data
{
    [Serializable]
    public sealed class SaveData
    {
        public int version = 1;
        public int coins;
        public int ownedSkinsMask = 1;
        public int equippedSkin;
        public int levelIndex;
        public bool soundOn = true;
    }
}
