using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using SweetBananas.Game;
using UnityEngine;

namespace SweetBananas.Editor.SpikeBench
{
    public sealed class SpikeBenchTunnelTracker
    {
        readonly StringBuilder _throwLog = new();
        int _totalViolations;
        int _throwsWithViolation;

        bool _stickContact;
        readonly float[] _prevY = new float[8];
        readonly bool[] _prevValid = new bool[8];

        public void BeginThrow(int throwIndex)
        {
            _stickContact = false;
            for (int i = 0; i < _prevY.Length; i++)
            {
                _prevY[i] = 0f;
                _prevValid[i] = false;
            }
        }

        public int SampleStep(Lollipop lollipop, Ribbon ribbon)
        {
            if (lollipop == null || ribbon == null)
                return 0;
            if (!SpikeBenchStickFrame.AnySegmentTouchingStick(ribbon, lollipop))
                return 0;

            _stickContact = true;
            int violations = 0;
            var bodies = ribbon.Bodies;
            int count = Mathf.Min(bodies.Count, _prevY.Length);
            for (int i = 0; i < count; i++)
            {
                Rigidbody2D b = bodies[i];
                if (b == null)
                    continue;
                SpikeBenchStickFrame.SegmentLocal(lollipop, b.position, out float x, out float y);
                bool inBand = SpikeBenchStickFrame.InStickTunnelBand(lollipop, x);
                if (inBand && _prevValid[i])
                {
                    if (_prevY[i] > 0f && y < 0f)
                        violations++;
                }

                if (inBand)
                {
                    _prevY[i] = y;
                    _prevValid[i] = true;
                }
                else
                    _prevValid[i] = false;
            }

            return violations;
        }

        public void EndThrow(int throwIndex, int violationsThisThrow)
        {
            if (violationsThisThrow > 0)
                _throwsWithViolation++;
            _totalViolations += violationsThisThrow;
            _throwLog.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "throw={0} violations={1} stick_contact={2}",
                throwIndex, violationsThisThrow, _stickContact ? 1 : 0));
        }

        public void Flush(string experimentId, int seed, string runId)
        {
            SpikeBenchMethodology.EnsureLogDirectory();
            string path = Path.Combine(SpikeBenchMethodology.LogDirectory(),
                "tunnel_" + experimentId + ".txt");
            var sb = new StringBuilder();
            sb.AppendLine("experiment_id=" + experimentId);
            sb.AppendLine("seed=" + seed);
            sb.AppendLine("run_id=" + runId);
            sb.Append(_throwLog);
            sb.AppendLine("total_violations=" + _totalViolations);
            sb.AppendLine("throws_with_violation=" + _throwsWithViolation);
            File.WriteAllText(path, sb.ToString());
        }
    }
}