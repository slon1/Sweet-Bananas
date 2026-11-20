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
            float heldSeconds,
            int firstGap085Step = -1,
            float firstGap085Value = 0f,
            int firstGap021Step = -1,
            float firstGap021Value = 0f,
            int firstGap021Pair = -1,
            float spawnClearanceStep0 = 0f,
            bool spawnInvalid = false,
            float spawnAngle = 0f,
            int spawnFixedSteps = 0,
            float hingeSlackMax = 0f,
            string rMinPhase = "")
        {
            if (!_headerWritten)
            {
                _writer.WriteLine(
                    "experiment_id,throw_index,kind,time_scale,spawn_fraction,press_duration_sec,spawn_clearance_step0,spawn_invalid,spawn_angle,spawn_fixed_steps,hinge_slack_max,max_gap,a10_break,gap_phase,max_speed,r_min_throw,r_min_phase,held_seconds,first_gap_085_step,first_gap_085_value,first_gap_021_step,first_gap_021_value,first_gap_021_pair");
                _headerWritten = true;
            }

            _writer.WriteLine(string.Join(",",
                experimentId,
                throwIndex.ToString(CultureInfo.InvariantCulture),
                kind,
                timeScale.ToString(CultureInfo.InvariantCulture),
                spawnFraction.ToString(CultureInfo.InvariantCulture),
                pressDuration.ToString(CultureInfo.InvariantCulture),
                spawnClearanceStep0.ToString(CultureInfo.InvariantCulture),
                spawnInvalid ? "1" : "0",
                spawnAngle.ToString(CultureInfo.InvariantCulture),
                spawnFixedSteps.ToString(CultureInfo.InvariantCulture),
                hingeSlackMax.ToString(CultureInfo.InvariantCulture),
                maxGap.ToString(CultureInfo.InvariantCulture),
                a10Break ? "1" : "0",
                gapPhase,
                maxSpeed.ToString(CultureInfo.InvariantCulture),
                float.IsNaN(rMinThrow) ? "" : rMinThrow.ToString(CultureInfo.InvariantCulture),
                rMinPhase ?? "",
                heldSeconds.ToString(CultureInfo.InvariantCulture),
                firstGap085Step.ToString(CultureInfo.InvariantCulture),
                firstGap085Step < 0 ? "" : firstGap085Value.ToString(CultureInfo.InvariantCulture),
                firstGap021Step.ToString(CultureInfo.InvariantCulture),
                firstGap021Step < 0 ? "" : firstGap021Value.ToString(CultureInfo.InvariantCulture),
                firstGap021Pair.ToString(CultureInfo.InvariantCulture)));
        }

        public void Dispose()
        {
            _writer?.Flush();
            _writer?.Dispose();
        }
    }
}