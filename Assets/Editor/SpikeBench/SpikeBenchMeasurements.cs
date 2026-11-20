using SweetBananas.Data;
using SweetBananas.Game;
using UnityEngine;

namespace SweetBananas.Editor.SpikeBench
{
    public enum SpikeGapPhase
    {
        Unknown,
        Contact,
        Flight,
        Stopped
    }

    public readonly struct SpikeThrowSample
    {
        public readonly float MaxGap;
        public readonly SpikeGapPhase GapPhase;
        public readonly float MaxSpeed;
        public readonly float RMinThrow;
        public readonly bool A10Break;
        public readonly bool NaN;

        public SpikeThrowSample(float maxGap, SpikeGapPhase gapPhase, float maxSpeed, float rMinThrow, bool a10Break, bool naN)
        {
            MaxGap = maxGap;
            GapPhase = gapPhase;
            MaxSpeed = maxSpeed;
            RMinThrow = rMinThrow;
            A10Break = a10Break;
            NaN = naN;
        }
    }

    public static class SpikeBenchMeasurements
    {
        public static bool AnyTouchingLollipop(Ribbon ribbon, Lollipop lollipop)
        {
            Collider2D stick = lollipop.StickCollider;
            Collider2D head = lollipop.HeadCollider;
            var colliders = ribbon.Colliders;
            for (int i = 0; i < colliders.Count; i++)
            {
                CapsuleCollider2D c = colliders[i];
                if (c == null)
                    continue;
                if (c.IsTouching(stick) || c.IsTouching(head))
                    return true;
            }

            return false;
        }

        public static bool AllStopped(Ribbon ribbon, float speedThreshold)
        {
            var bodies = ribbon.Bodies;
            for (int i = 0; i < bodies.Count; i++)
            {
                Rigidbody2D b = bodies[i];
                if (b == null)
                    continue;
                if (b.linearVelocity.sqrMagnitude > speedThreshold * speedThreshold)
                    return false;
            }

            return bodies.Count > 0;
        }

        public static float MaxSegmentSpeed(Ribbon ribbon)
        {
            float max = 0f;
            var bodies = ribbon.Bodies;
            for (int i = 0; i < bodies.Count; i++)
            {
                float s = bodies[i].linearVelocity.magnitude;
                if (s > max)
                    max = s;
            }

            return max;
        }

        public static float ComputeR(Ribbon ribbon)
        {
            var bodies = ribbon.Bodies;
            if (bodies.Count == 0)
                return float.NaN;

            float half = ribbon.SegmentLength * 0.5f;
            Vector2 start = bodies[0].GetPoint(new Vector2(-half, 0f));
            Vector2 end = bodies[bodies.Count - 1].GetPoint(new Vector2(half, 0f));
            float length = ribbon.SegmentLength * bodies.Count;
            if (length < 1e-4f)
                return float.NaN;
            return (start - end).magnitude / length;
        }

        public static float MaxAnchorGap(Ribbon ribbon)
        {
            var bodies = ribbon.Bodies;
            if (bodies.Count < 2)
                return 0f;

            float half = ribbon.SegmentLength * 0.5f;
            float max = 0f;
            for (int i = 0; i < bodies.Count - 1; i++)
            {
                Vector2 a = bodies[i].GetPoint(new Vector2(half, 0f));
                Vector2 b = bodies[i + 1].GetPoint(new Vector2(-half, 0f));
                float d = (a - b).magnitude;
                if (d > max)
                    max = d;
            }

            return max;
        }

        public static bool IsA10Break(float maxGap)
        {
            return maxGap > SpikeBenchMethodology.GapBreakSoft;
        }

        public static int HistogramBin(float maxGap)
        {
            if (maxGap <= SpikeBenchMethodology.GapHardVisual)
                return 0;
            if (maxGap <= SpikeBenchMethodology.GapBreakSoft)
                return 1;
            if (maxGap <= 0.5f)
                return 2;
            if (maxGap <= 1f)
                return 3;
            return 4;
        }

        public static bool FailBounds(Ribbon ribbon, Lollipop lollipop, GameConfig config)
        {
            if (ribbon == null || config == null)
                return false;
            Vector2 center = ribbon.Center;
            float failY = -config.FailMarginUnits;
            float minX = -config.FailMarginUnits;
            float maxX = config.WorldWidthUnits + config.FailMarginUnits;
            return center.y < failY || center.x < minX || center.x > maxX;
        }
    }
}