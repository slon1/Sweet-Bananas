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

        public float GravityY = -9.81f;
        public float FixedDeltaTime = 1f / 60f;
        public int VelocityIterations = 12;
        public int PositionIterations = 6;
        public int SegmentCount = 8;
        public float SegmentMass = 0.2f;
        public float JointLimitDeg = 65f;
        public float RestoreK = 2f;
        public float RestoreC = 0.2f;
        public float RibbonFriction = 0.9f;
        public float RibbonBounciness = 0.05f;
        public float StickFriction = 0.2f;
        public float TargetFriction = 0.9f;
        public float RibbonLinearDamping = 0.1f;
        public float RibbonAngularDamping = 0.5f;
        public float RestAngleDeg = -25f;
        public float TapPeakAngleDeg = 35f;
        public float HoldAngleDeg = 0f;
        public float TapSpeedDegPerSec = 600f;
        public float ReturnSpeedDegPerSec = 360f;
        public float HoldRiseSpeedDegPerSec = 180f;
        public float HoldThresholdSeconds = 0.15f;
    }
}