using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace SweetBananas.Editor.SpikeBench
{
    public sealed class SpikeBenchCsv : IDisposable
    {
        readonly StreamWriter _writer;
        bool _headerWritten;

        public SpikeBenchCsv(string experimentId, int seed, string runId)
        {
            SpikeBenchMethodology.EnsureLogDirectory();
            string fileName = $"throws_{experimentId}_{seed}_{runId}.csv";
            string path = Path.Combine(SpikeBenchMethodology.LogDirectory(), fileName);
            _writer = new StreamWriter(path, false, Encoding.UTF8);
        }

        public void WriteThrowRow(
            string experimentId,
            int throwIndex,
            string kind,
            float timeScale,
            float spawnFraction,
            float pressDuration,
            float maxGap,
            bool a10Break,
            string gapPhase,
            float maxSpeed,
            float rMinThrow,
            float heldSeconds)
        {
            if (!_headerWritten)
            {
                _writer.WriteLine(
                    "experiment_id,throw_index,kind,time_scale,spawn_fraction,press_duration_sec,max_gap,a10_break,gap_phase,max_speed,r_min_throw,held_seconds");
                _headerWritten = true;
            }

            _writer.WriteLine(string.Join(",",
                experimentId,
                throwIndex.ToString(CultureInfo.InvariantCulture),
                kind,
                timeScale.ToString(CultureInfo.InvariantCulture),
                spawnFraction.ToString(CultureInfo.InvariantCulture),
                pressDuration.ToString(CultureInfo.InvariantCulture),
                maxGap.ToString(CultureInfo.InvariantCulture),
                a10Break ? "1" : "0",
                gapPhase,
                maxSpeed.ToString(CultureInfo.InvariantCulture),
                float.IsNaN(rMinThrow) ? "" : rMinThrow.ToString(CultureInfo.InvariantCulture),
                heldSeconds.ToString(CultureInfo.InvariantCulture)));
        }

        public void Dispose()
        {
            _writer?.Flush();
            _writer?.Dispose();
        }
    }
}