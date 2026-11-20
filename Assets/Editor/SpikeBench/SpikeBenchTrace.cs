using System.Globalization;
using System.IO;
using System.Text;
using SweetBananas.Game;

namespace SweetBananas.Editor.SpikeBench
{
    public sealed class SpikeBenchTrace : System.IDisposable
    {
        readonly StreamWriter _writer;
        bool _headerWritten;

        public SpikeBenchTrace(string experimentId, int seed, string runId)
        {
            SpikeBenchMethodology.EnsureLogDirectory();
            string path = Path.Combine(SpikeBenchMethodology.LogDirectory(), $"trace_{experimentId}_{seed}_{runId}.csv");
            _writer = new StreamWriter(path, false, Encoding.UTF8);
        }

        public void WriteRow(
            int step,
            float[] pairGaps,
            float maxSpeed,
            string phase,
            float r,
            float stickAngle)
        {
            if (!_headerWritten)
            {
                _writer.WriteLine("step,gap_0,gap_1,gap_2,gap_3,gap_4,gap_5,gap_6,max_speed,phase,R,angle_stick");
                _headerWritten = true;
            }

            var line = new StringBuilder();
            line.Append(step.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < 7; i++)
            {
                line.Append(',');
                if (pairGaps != null && i < pairGaps.Length)
                    line.Append(pairGaps[i].ToString(CultureInfo.InvariantCulture));
            }

            line.Append(',');
            line.Append(maxSpeed.ToString(CultureInfo.InvariantCulture));
            line.Append(',');
            line.Append(phase);
            line.Append(',');
            line.Append(float.IsNaN(r) ? "" : r.ToString(CultureInfo.InvariantCulture));
            line.Append(',');
            line.Append(stickAngle.ToString(CultureInfo.InvariantCulture));
            _writer.WriteLine(line.ToString());
        }

        public void Dispose()
        {
            _writer?.Flush();
            _writer?.Dispose();
        }
    }
}