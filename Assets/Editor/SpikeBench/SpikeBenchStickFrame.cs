using SweetBananas.Game;
using UnityEngine;

namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchStickFrame
    {
        public static void SegmentLocal(Lollipop lollipop, Vector2 world, out float localX, out float localY)
        {
            Vector2 pivot = lollipop.Pivot;
            Vector2 along = lollipop.StickDirection();
            Vector2 perp = lollipop.StickUp();
            Vector2 delta = world - pivot;
            localX = Vector2.Dot(delta, along);
            localY = Vector2.Dot(delta, perp);
        }

        public static bool InStickTunnelBand(Lollipop lollipop, float localX)
        {
            var pose = lollipop.ActivePose;
            return localX >= pose.HeadRadiusUnits && localX <= pose.StickLengthUnits;
        }

        public static bool AnySegmentTouchingStick(Ribbon ribbon, Lollipop lollipop)
        {
            if (ribbon == null || lollipop == null || lollipop.StickCollider == null)
                return false;
            Collider2D stick = lollipop.StickCollider;
            foreach (CapsuleCollider2D c in ribbon.Colliders)
            {
                if (c != null && c.IsTouching(stick))
                    return true;
            }

            return false;
        }
    }
}