using UnityEngine;

namespace SweetBananas.Data
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Sweet Bananas/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        public float WorldWidthUnits = 10.8f;
        public int PixelsPerUnit = 100;
        public int TargetFrameRate = 60;
        public float SettleSeconds = 1.5f;
        public float SettleSpeedThreshold = 0.15f;
        public int RewardBase = 5;
        public int RewardFirstThrowBonus = 5;
        public int SkinPrice = 10;
        public float FailMarginUnits = 1f;
    }
}
