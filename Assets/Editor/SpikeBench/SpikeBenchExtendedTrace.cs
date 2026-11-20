using System.Globalization;
using System.IO;
using System.Text;
using SweetBananas.Game;
using UnityEngine;

namespace SweetBananas.Editor.SpikeBench
{
    public sealed class SpikeBenchExtendedTrace : System.IDisposable
    {
        readonly StreamWriter _writer;
        bool _headerWritten;

        public SpikeBenchExtendedTrace(string fileName)
        {
            SpikeBenchMethodology.EnsureLogDirectory();
            string path = Path.Combine(SpikeBenchMethodology.LogDirectory(), fileName);
            _writer = new StreamWriter(path, false, Encoding.UTF8);
        }

        public void WriteRow(
            int step,
            float[] pairGaps,
            Ribbon ribbon,
            Lollipop lollipop,
            ThrowController throwCtrl,
            string phase)
        {
            if (!_headerWritten)
            {
                _writer.WriteLine(
                    "step,gap_0,gap_1,gap_2,gap_3,gap_4,gap_5,gap_6,speed_5,speed_6,speed_7,stick_angle,stick_angular_velocity,contact_head_seg_mask,contact_stick_seg_mask,seg7_local_x,seg7_local_y,phase,throw_state");
                _headerWritten = true;
            }

            float speed5 = SegmentSpeed(ribbon, 5);
            float speed6 = SegmentSpeed(ribbon, 6);
            float speed7 = SegmentSpeed(ribbon, 7);
            float stickAngle = lollipop != null ? lollipop.Angle : 0f;
            float stickOmega = lollipop != null ? lollipop.MeasuredAngularVelocityDegPerSec : 0f;
            uint headMask = ContactMask(ribbon, lollipop, true);
            uint stickMask = ContactMask(ribbon, lollipop, false);
            Seg7Local(lollipop, ribbon, out float localX, out float localY);
            string throwState = throwCtrl != null ? throwCtrl.State.ToString() : "";

            var line = new StringBuilder();
            line.Append(step.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < 7; i++)
            {
                line.Append(',');
                if (pairGaps != null && i < pairGaps.Length)
                    line.Append(pairGaps[i].ToString(CultureInfo.InvariantCulture));
            }

            line.Append(',');
            line.Append(speed5.ToString(CultureInfo.InvariantCulture));
            line.Append(',');
            line.Append(speed6.ToString(CultureInfo.InvariantCulture));
            line.Append(',');
            line.Append(speed7.ToString(CultureInfo.InvariantCulture));
            line.Append(',');
            line.Append(stickAngle.ToString(CultureInfo.InvariantCulture));
            line.Append(',');
            line.Append(stickOmega.ToString(CultureInfo.InvariantCulture));
            line.Append(',');
            line.Append(headMask.ToString(CultureInfo.InvariantCulture));
            line.Append(',');
            line.Append(stickMask.ToString(CultureInfo.InvariantCulture));
            line.Append(',');
            line.Append(localX.ToString(CultureInfo.InvariantCulture));
            line.Append(',');
            line.Append(localY.ToString(CultureInfo.InvariantCulture));
            line.Append(',');
            line.Append(phase);
            line.Append(',');
            line.Append(throwState);
            _writer.WriteLine(line.ToString());
        }

        static float SegmentSpeed(Ribbon ribbon, int index)
        {
            if (ribbon == null)
                return 0f;
            var bodies = ribbon.Bodies;
            if (index < 0 || index >= bodies.Count)
                return 0f;
            Rigidbody2D body = bodies[index];
            return body != null ? body.linearVelocity.magnitude : 0f;
        }

        static uint ContactMask(Ribbon ribbon, Lollipop lollipop, bool head)
        {
            if (ribbon == null || lollipop == null)
                return 0;
            Collider2D target = head ? lollipop.HeadCollider : lollipop.StickCollider;
            var colliders = ribbon.Colliders;
            uint mask = 0;
            for (int i = 0; i < colliders.Count; i++)
            {
                CapsuleCollider2D c = colliders[i];
                if (c != null && c.IsTouching(target))
                    mask |= 1u << i;
            }

            return mask;
        }

        static void Seg7Local(Lollipop lollipop, Ribbon ribbon, out float localX, out float localY)
        {
            localX = 0f;
            localY = 0f;
            if (lollipop == null || ribbon == null)
                return;
            var bodies = ribbon.Bodies;
            if (bodies.Count < 8)
                return;
            Vector2 world = bodies[7].position;
            Vector2 pivot = lollipop.Pivot;
            Vector2 along = lollipop.StickDirection();
            Vector2 perp = lollipop.StickUp();
            Vector2 delta = world - pivot;
            localX = Vector2.Dot(delta, along);
            localY = Vector2.Dot(delta, perp);
        }

        public void Dispose()
        {
            _writer?.Flush();
            _writer?.Dispose();
        }
    }
}