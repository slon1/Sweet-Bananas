using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchMethodology
    {
        public const int DefaultSeed = unchecked((int)0x002A2000);
        public const float BenchTimeScale = 12f;
        public const int VerifyAtRealtimeCount = 10;
        public const float TapThrowTimeoutSec = 4f;
        public const float HoldThrowTimeoutSec = 6f;
        public const float RMinExcludeAfterSpawnSec = 0.1f;
        public const float StopSpeedThreshold = 0.05f;
        public const float StopHoldSec = 0.25f;
        public const float GapHardVisual = 0.085f;
        public const float GapBreakSoft = 0.21f;
        public const float MaxSegmentSpeed = 60f;
        public const int SeriesTaps = 100;
        public const int SeriesHolds = 20;
        public const int FinalTaps = 300;
        public const int FinalHolds = 60;

        public static readonly float[] SpawnFractions = { 0.05f, 0.15f, 0.25f, 0.35f, 0.5f, 0.7f, 0.9f };
        public static readonly float[] TapPressDurationsSec =
        {
            0.04f, 0.06f, 0.08f, 0.1f, 0.12f, 0.14f, 0.16f, 0.2f, 0.28f, 0.4f
        };
        public static readonly float[] HoldPressDurationsSec = { 0.4f, 0.8f, 1.2f, 1.6f, 2f, 2.5f, 3f, 4f, 5f, 6f };

        public const string HooksDocumentation =
            "Bench hooks (runtime, UNITY_EDITOR || DEVELOPMENT_BUILD):\n"
            + "- ThrowController.DebugSetDrive(true): bypass Input System; bench drives held state.\n"
            + "- ThrowController.DebugSetHeld(bool): simulates pointer press while drive enabled.\n"
            + "- ThrowController.DebugResetFixedSteps(): reset FixedUpdate counter per throw.\n"
            + "- ThrowController.SnapRest(): force rest angle (also called from Ribbon.ResetToSpawn).\n"
            + "- Ribbon.ResetToSpawn(float? fraction): respawn ribbon; optional spawn fraction override.\n"
            + "- SpikeOverlay.AutoResetEnabled: bench sets false during series; bench applies fail bounds itself.\n"
            + "- Lollipop.Body.angularVelocity: measured for A1 when angularVelocity drive is selected (E1+).\n";

        public static string LogDirectory()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "SpikeBench"));
        }

        public static void EnsureLogDirectory()
        {
            Directory.CreateDirectory(LogDirectory());
        }

        public static void WriteMethodologyArtifact(int seed, string experimentId)
        {
            EnsureLogDirectory();
            var path = Path.Combine(LogDirectory(), $"methodology_{experimentId}_{seed}.txt");
            var sb = new StringBuilder(2048);
            sb.AppendLine("TASK-002-A2 SpikeBench methodology (step 0)");
            sb.AppendLine("seed=" + seed);
            sb.AppendLine("experiment_id=" + experimentId);
            sb.AppendLine("bench_time_scale=" + BenchTimeScale.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("verify_realtime_throws=" + VerifyAtRealtimeCount);
            sb.AppendLine("tap_throw_timeout_sec=" + TapThrowTimeoutSec.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("hold_throw_timeout_sec=" + HoldThrowTimeoutSec.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("series_taps=" + SeriesTaps);
            sb.AppendLine("series_holds=" + SeriesHolds);
            sb.AppendLine("final_taps=" + FinalTaps);
            sb.AppendLine("final_holds=" + FinalHolds);
            sb.AppendLine("spawn_fractions=" + string.Join(",", SpawnFractions));
            sb.AppendLine("tap_press_durations_sec=" + string.Join(",", TapPressDurationsSec));
            sb.AppendLine("hold_press_durations_sec=" + string.Join(",", HoldPressDurationsSec));
            sb.AppendLine("throw_end: tap — release then settle (all segments |v|<0.05 for 0.25s) OR tap_throw_timeout since release; hold — release or hold_throw_timeout since press start; also ends on fail-line (same as SpikeOverlay).");
            sb.AppendLine("gap_bins: 0.085 / 0.21 / 0.5 / 1.0 (A10)");
            sb.AppendLine("csv_path=" + LogDirectory());
            sb.AppendLine();
            sb.Append(HooksDocumentation);
            File.WriteAllText(path, sb.ToString());
        }
    }
}