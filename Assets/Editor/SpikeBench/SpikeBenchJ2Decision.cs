using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchJ2Decision
    {
        [MenuItem("Sweet Bananas/SpikeBench/J2 — compare E0/E0b/E0c noise")]
        public static void AnalyzeFromMenu()
        {
            string dir = SpikeBenchMethodology.LogDirectory();
            var e0 = FindLatestBaselineE0();
            var e0b = FindLatest("summary_E0b_");
            var e0c = FindLatest("summary_E0c_");
            if (e0 == null || e0b == null || e0c == null)
            {
                Debug.LogError("SpikeBench J2: need summary_E0_, E0b_, E0c_");
                return;
            }

            Parse(e0, out int a0, out int soft0, out float max0, out float rmin0, out int rlow0);
            Parse(e0b, out int a1, out int soft1, out float max1, out float rmin1, out int rlow1);
            Parse(e0c, out int a2, out int soft2, out float max2, out float rmin2, out int rlow2);

            int spread12 = Mathf.Abs(a0 - a1) + Mathf.Abs(soft0 - soft1);
            int spread1c = Mathf.Abs(a0 - a2) + Mathf.Abs(soft0 - soft2);
            bool useRealtime = spread1c > spread12;
            SpikeBenchMethodology.ScreeningTimeScale = useRealtime ? 1f : SpikeBenchMethodology.BenchTimeScale;

            string path = Path.Combine(dir, "j2_timescale_rule.txt");
            var sb = new StringBuilder();
            sb.AppendLine("e0=" + Path.GetFileName(e0));
            sb.AppendLine("e0b=" + Path.GetFileName(e0b));
            sb.AppendLine("e0c=" + Path.GetFileName(e0c));
            sb.AppendLine("e0 a10=" + a0 + " soft=" + soft0 + " max_gap=" + max0 + " r_min=" + rmin0 + " r_low=" + rlow0);
            sb.AppendLine("e0b a10=" + a1 + " soft=" + soft1 + " max_gap=" + max1 + " r_min=" + rmin1 + " r_low=" + rlow1);
            sb.AppendLine("e0c a10=" + a2 + " soft=" + soft2 + " max_gap=" + max2 + " r_min=" + rmin2 + " r_low=" + rlow2);
            sb.AppendLine("spread_12x_e0_vs_e0b=" + spread12);
            sb.AppendLine("spread_1x_e0_vs_e0c=" + spread1c);
            sb.AppendLine("screening_time_scale=" + SpikeBenchMethodology.ScreeningTimeScale.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("rule=" + (useRealtime
                ? "E0c_vs_E0 > E0b_vs_E0 => screening at 1x"
                : "default screening at 12x, confirm on 1x"));
            File.WriteAllText(path, sb.ToString());
            Debug.Log("SpikeBench J2: " + path);
        }

        static string FindLatest(string prefix)
        {
            string dir = SpikeBenchMethodology.LogDirectory();
            if (!Directory.Exists(dir))
                return null;
            string[] files = Directory.GetFiles(dir, prefix + "*.txt");
            Array.Sort(files);
            return files.Length == 0 ? null : files[^1];
        }

        static string FindLatestBaselineE0()
        {
            string dir = SpikeBenchMethodology.LogDirectory();
            if (!Directory.Exists(dir))
                return null;
            string[] files = Directory.GetFiles(dir, "summary_E0_*.txt");
            Array.Sort(files);
            for (int i = files.Length - 1; i >= 0; i--)
            {
                string name = Path.GetFileName(files[i]);
                if (name.Contains("E0b", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("E0c", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("verify", StringComparison.OrdinalIgnoreCase))
                    continue;
                return files[i];
            }

            return null;
        }

        static void Parse(string path, out int a10, out int soft, out float maxGap, out float rMin, out int rLow)
        {
            a10 = soft = rLow = 0;
            maxGap = rMin = 0f;
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.StartsWith("a10_breaks_gt_0.21_valid=", StringComparison.Ordinal))
                    int.TryParse(line.Split('=')[1], out a10);
                else if (line.StartsWith("bin_0.085_0.21_valid=", StringComparison.Ordinal))
                    int.TryParse(line.Split('=')[1], out soft);
                else if (line.StartsWith("max_gap_valid=", StringComparison.Ordinal))
                    float.TryParse(line.Split('=')[1], NumberStyles.Float, CultureInfo.InvariantCulture, out maxGap);
                else if (line.StartsWith("r_min_throw_min_valid=", StringComparison.Ordinal))
                    float.TryParse(line.Split('=')[1], NumberStyles.Float, CultureInfo.InvariantCulture, out rMin);
            }

            string csv = path.Replace("summary_", "throws_").Replace(".txt", ".csv");
            if (File.Exists(csv))
                rLow = SpikeBenchE0Analysis.CountRLow(csv);
        }
    }
}