using System;
using UnityEngine;

namespace SweetBananas.Game
{
    [Serializable]
    public struct PointerPose
    {
        public Sprite Sprite;
        public float StickAxisDegrees;
        public float VisualLocalZ;
        public float StickLengthUnits;
        public float HeadRadiusUnits;
        public float StickThicknessUnits;
        public float HeadCenterOffsetUnits;
    }
}
