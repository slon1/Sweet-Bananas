using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchA21Verdict
    {
        [MenuItem("Sweet Bananas/SpikeBench/A21 — regenerate verdict file")]
        public static void GenerateFromMenu() => GenerateFromArtifacts();

        public static void GenerateFromArtifacts()
        {
            string dir = SpikeBenchMethodology.LogDirectory();
            string main = FindLatestFinalMainSummary(dir);
            string throwsCsv = main != null
                ? main.Replace("summary_", "throws_").Replace(".txt", ".csv")
                : FindLatestMainThrowsCsv(dir);

            var sb = new StringBuilder();
            sb.AppendLine("A21 acceptance verdict (auto-generated)");
            sb.AppendLine("generated_utc=" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            sb.AppendLine();
            sb.AppendLine("criterion\tvalue\tthreshold\tverdict");

            ParseSummary(main, out int valid, out int a10, out int nanThrows,
                out float spawnPct, out int break2Limit);

            AppendRow(sb, "A21.1 NaN", nanThrows, "0", nanThrows == 0);
            int speedFail = CountCsvFails(throwsCsv, (h, p) =>
                !IsInvalid(h, p) && Field(h, p, "max_speed") > 60.01f);
            AppendRow(sb, "A21.1 speed>60", speedFail, "0", speedFail == 0);
            int stoppedFail = CountCsvFails(throwsCsv, (h, p) =>
            {
                if (IsInvalid(h, p))
                    return false;
                if (!TryField(h, p, "gap_stopped_max", out float g))
                    return false;
                return g > 0.0501f;
            });
            AppendRow(sb, "A21.2 stopped gap", stoppedFail, "0 throws >0.05", stoppedFail == 0);
            AppendRow(sb, "A21.3 breaks>0.21", a10, "<= " + break2Limit,
                valid > 0 && a10 <= break2Limit);
            int rMinFail = CountCsvFails(throwsCsv, (h, p) =>
                !IsInvalid(h, p) && Field(h, p, "r_min_throw") < 0.349f);
            AppendRow(sb, "A21.4 R_min>=0.35", rMinFail, "0 throws", rMinFail == 0);
            AppendRow(sb, "A21.5 spawn_invalid%", spawnPct.ToString("F2", CultureInfo.InvariantCulture), "<=2",
                spawnPct <= 2.01f);

            int tunnelTotal = ParseTunnel(dir);
            AppendRow(sb, "D24.2 tunnel", tunnelTotal, "0", tunnelTotal == 0);
            AppendD24Rest(sb, dir);
            AppendD24Lever(sb, dir);
            AppendD24Catch(sb, dir);
            AppendD24Perf(sb, dir);

            File.WriteAllText(Path.Combine(dir, "a21_verdict.txt"), sb.ToString());
            Debug.Log("SpikeBench A21 verdict: " + Path.Combine(dir, "a21_verdict.txt"));
        }

        static int CountCsvFails(string path, Func<string[], string[], bool> pred)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return -1;
            int fail = 0;
            string[] lines = File.ReadAllLines(path);
            if (lines.Length < 2)
                return 0;
            var header = lines[0].Split(',');
            for (int i = 1; i < lines.Length; i++)
            {
                if (pred(header, lines[i].Split(',')))
                    fail++;
            }

            return fail;
        }

        static bool IsInvalid(string[] header, string[] p)
        {
            int idx = IndexOf(header, "spawn_invalid");
            return idx >= 0 && p.Length > idx && p[idx] == "1";
        }

        static float Field(string[] header, string[] p, string name) =>
            TryField(header, p, name, out float v) ? v : float.NaN;

        static bool TryField(string[] header, string[] p, string name, out float v)
        {
            v = 0f;
            int idx = IndexOf(header, name);
            if (idx < 0 || idx >= p.Length || string.IsNullOrEmpty(p[idx]))
                return false;
            return float.TryParse(p[idx], NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }

        static int IndexOf(string[] header, string name)
        {
            for (int i = 0; i < header.Length; i++)
            {
                if (header[i] == name)
                    return i;
            }

            return -1;
        }

        static void ParseSummary(string path, out int valid, out int a10, out int nanThrows,
            out float spawnPct, out int break2Limit)
        {
            valid = a10 = nanThrows = break2Limit = 0;
            spawnPct = 0f;
            if (path == null || !File.Exists(path))
                return;
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.StartsWith("valid_throws=", StringComparison.Ordinal))
                    int.TryParse(line.Split('=')[1], out valid);
                else if (line.StartsWith("spawn_invalid_pct=", StringComparison.Ordinal))
                    float.TryParse(line.Split('=')[1], NumberStyles.Float, CultureInfo.InvariantCulture, out spawnPct);
                else if (line.StartsWith("a10_breaks_gt_0.21_valid=", StringComparison.Ordinal))
                    int.TryParse(line.Split('=')[1], out a10);
                else if (line.StartsWith("nan_throws_valid=", StringComparison.Ordinal))
                    int.TryParse(line.Split('=')[1], out nanThrows);
                else if (line.StartsWith("a10_break_limit_2pct_valid=", StringComparison.Ordinal))
                    int.TryParse(line.Split('=')[1], out break2Limit);
            }
        }

        static int ParseTunnel(string dir)
        {
            string path = Path.Combine(dir, "tunnel_FINAL_J25.txt");
            if (!File.Exists(path))
                return -1;
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.StartsWith("total_violations=", StringComparison.Ordinal))
                    return int.TryParse(line.Split('=')[1], out int v) ? v : -1;
            }

            return -1;
        }

        static void AppendD24Rest(StringBuilder sb, string dir)
        {
            string csv = FindLatest(dir, "rest_probe_");
            if (csv == null)
            {
                AppendRow(sb, "D24.9 rest", "missing", "see 10a", false);
                return;
            }

            float maxTip = 0f;
            bool anyTip = false;
            float maxV1 = 0f;
            bool anyV1 = false;
            foreach (string line in File.ReadAllLines(csv))
            {
                if (line.StartsWith("experiment", StringComparison.Ordinal))
                    continue;
                string[] p = line.Split(',');
                if (p.Length < 10)
                    continue;
                if (p[2] == "rest_idle" && p.Length > 6 && !string.IsNullOrEmpty(p[6]) &&
                    float.TryParse(p[6], NumberStyles.Float, CultureInfo.InvariantCulture, out float tip))
                {
                    anyTip = true;
                    maxTip = Mathf.Max(maxTip, tip);
                }

                if (p[2] == "hold_0deg" && float.TryParse(p[8], NumberStyles.Float, CultureInfo.InvariantCulture, out float v1))
                {
                    anyV1 = true;
                    maxV1 = Mathf.Max(maxV1, Mathf.Abs(v1));
                }
            }

            if (!anyV1)
                maxV1 = float.NaN;

            if (!anyTip)
                maxTip = float.NaN;

            AppendRow(sb, "D24.9 tip_time_max", float.IsNaN(maxTip) ? "n/a" : maxTip.ToString("F2", CultureInfo.InvariantCulture),
                "2.5-4", !float.IsNaN(maxTip) && maxTip >= 2.5f && maxTip <= 4.01f);
            AppendRow(sb, "D24.9 slide_v_1s_max",
                float.IsNaN(maxV1) ? "n/a" : maxV1.ToString("F4", CultureInfo.InvariantCulture), "<0.05",
                !float.IsNaN(maxV1) && maxV1 < 0.0501f);
        }

        static void AppendD24Lever(StringBuilder sb, string dir)
        {
            string path = Path.Combine(dir, "d24_3_lever.txt");
            if (!File.Exists(path))
            {
                AppendRow(sb, "D24.3 lever", "missing", ">=2", false);
                return;
            }

            float delta = 0f;
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.StartsWith("delta_avg_high_minus_low=", StringComparison.Ordinal))
                {
                    var part = line.Split('=')[1].Split(' ')[0];
                    float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out delta);
                }
            }

            AppendRow(sb, "D24.3 lever delta", delta.ToString("F4", CultureInfo.InvariantCulture), ">=2", delta >= 2f);
        }

        static void AppendD24Catch(StringBuilder sb, string dir)
        {
            string path = Path.Combine(dir, "d24_4_catch.txt");
            if (!File.Exists(path))
            {
                AppendRow(sb, "D24.4 catch", "missing", "5/5", false);
                return;
            }

            int pass = 0;
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.StartsWith("pass_count=", StringComparison.Ordinal))
                    int.TryParse(line.Split('=')[1].Split(' ')[0], out pass);
            }

            AppendRow(sb, "D24.4 catch", pass, "5", pass >= 5);
        }

        static void AppendD24Perf(StringBuilder sb, string dir)
        {
            string path = Path.Combine(dir, "d24_7_profile.txt");
            if (!File.Exists(path))
            {
                AppendRow(sb, "D24.7 physics", "missing", "<=1ms", false);
                return;
            }

            float ms = 0f;
            long mesh = 0;
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.StartsWith("physics_step_avg_ms=", StringComparison.Ordinal))
                    float.TryParse(line.Split('=')[1], NumberStyles.Float, CultureInfo.InvariantCulture, out ms);
                else if (line.StartsWith("ribbon_mesh_last_alloc_max_bytes=", StringComparison.Ordinal))
                    long.TryParse(line.Split('=')[1], NumberStyles.Number, CultureInfo.InvariantCulture, out mesh);
            }

            AppendRow(sb, "D24.7 physics avg ms", ms.ToString("F4", CultureInfo.InvariantCulture), "<=1", ms <= 1.01f);
            AppendRow(sb, "D24.7 mesh alloc max", mesh, "0", mesh == 0);
        }

        static void AppendRow(StringBuilder sb, string name, object value, string threshold, bool pass)
        {
            sb.AppendLine(string.Join("\t",
                name,
                value,
                threshold,
                pass ? "pass" : "FAIL"));
        }

        static string FindLatest(string dir, string prefix)
        {
            if (!Directory.Exists(dir))
                return null;
            string[] files = Directory.GetFiles(dir, prefix + "*");
            Array.Sort(files);
            return files.Length == 0 ? null : files[^1];
        }

        static string FindLatestFinalMainSummary(string dir)
        {
            if (!Directory.Exists(dir))
                return null;
            string[] files = Directory.GetFiles(dir, "summary_FINAL_J25_*.txt");
            Array.Sort(files);
            for (int i = files.Length - 1; i >= 0; i--)
            {
                if (files[i].Contains("verify1x", StringComparison.OrdinalIgnoreCase))
                    continue;
                return files[i];
            }

            return null;
        }

        static string FindLatestMainThrowsCsv(string dir)
        {
            string[] files = Directory.GetFiles(dir, "throws_FINAL_J25_*.csv");
            Array.Sort(files);
            for (int i = files.Length - 1; i >= 0; i--)
            {
                if (files[i].Contains("verify1x", StringComparison.OrdinalIgnoreCase))
                    continue;
                return files[i];
            }

            return null;
        }
    }
}