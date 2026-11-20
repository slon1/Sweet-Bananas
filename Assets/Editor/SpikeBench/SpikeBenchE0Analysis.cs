using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchE0Analysis
    {
        public static readonly int[] BreakThrowIndices = { 11, 18, 53, 60, 95 };

        public static string FindLatestE0Csv()
        {
            string dir = SpikeBenchMethodology.LogDirectory();
            if (!Directory.Exists(dir))
                return null;
            return Directory.GetFiles(dir, "throws_E0_*.csv")
                .Where(p => !p.Contains("_verify"))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        [MenuItem("Sweet Bananas/SpikeBench/H2 — E0 correlation table")]
        public static void RunH2FromMenu()
        {
            string csv = FindLatestE0Csv();
            if (string.IsNullOrEmpty(csv))
            {
                Debug.LogError("SpikeBench H2: no throws_E0_*.csv in " + SpikeBenchMethodology.LogDirectory());
                return;
            }

            string outPath = WriteCorrelation(csv);
            Debug.Log("SpikeBench H2: " + outPath);
        }

        public static string WriteCorrelation(string csvPath)
        {
            var rows = ParseRows(csvPath);
            string outPath = Path.Combine(SpikeBenchMethodology.LogDirectory(), "e0_correlation.txt");
            var sb = new StringBuilder();
            sb.AppendLine("source_csv=" + csvPath);
            int valid = rows.Count(r => r.SpawnInvalid == 0);
            int gt021 = rows.Count(r => r.SpawnInvalid == 0 && r.A10 == 1);
            int soft = rows.Count(r => r.SpawnInvalid == 0 && r.MaxGap > SpikeBenchMethodology.GapHardVisual && r.MaxGap <= SpikeBenchMethodology.GapBreakSoft);
            int rLow = rows.Count(r => r.SpawnInvalid == 0 && !float.IsNaN(r.RMin) && r.RMin < 0.35f);
            sb.AppendLine("valid_throws=" + valid);
            sb.AppendLine("count_gt_0.21=" + gt021);
            sb.AppendLine("count_bin_0.085_0.21=" + soft);
            sb.AppendLine("count_r_min_lt_0.35=" + rLow);
            sb.AppendLine();
            WriteGroup(sb, "(a) gt_0.21", rows.Where(r => r.SpawnInvalid == 0 && r.A10 == 1));
            WriteGroup(sb, "(b) bin_0.085_0.21", rows.Where(r => r.SpawnInvalid == 0 && r.MaxGap > SpikeBenchMethodology.GapHardVisual && r.MaxGap <= SpikeBenchMethodology.GapBreakSoft));
            WriteGroup(sb, "(c) r_min_lt_0.35", rows.Where(r => r.SpawnInvalid == 0 && !float.IsNaN(r.RMin) && r.RMin < 0.35f));
            File.WriteAllText(outPath, sb.ToString());
            return outPath;
        }

        public static int CountRLow(string csvPath, float threshold = 0.35f)
        {
            int n = 0;
            foreach (var row in ParseRows(csvPath))
            {
                if (row.SpawnInvalid == 0 && !float.IsNaN(row.RMin) && row.RMin < threshold)
                    n++;
            }

            return n;
        }

        public static Dictionary<int, (float MaxGap, bool A10)> LoadThrowSummaries(string csvPath)
        {
            var dict = new Dictionary<int, (float, bool)>();
            foreach (var row in ParseRows(csvPath))
                dict[row.ThrowIndex] = (row.MaxGap, row.A10 == 1);
            return dict;
        }

        public static List<int> PickTopSoftBinThrowIndices(string csvPath, int count)
        {
            var picks = ParseRows(csvPath)
                .Where(r => r.SpawnInvalid == 0 && r.MaxGap > SpikeBenchMethodology.GapHardVisual && r.MaxGap <= SpikeBenchMethodology.GapBreakSoft)
                .OrderByDescending(r => r.MaxGap)
                .Take(count)
                .Select(r => r.ThrowIndex)
                .Distinct()
                .ToList();
            return picks;
        }

        static void WriteGroup(StringBuilder sb, string title, IEnumerable<ThrowRow> subset)
        {
            sb.AppendLine(title);
            var list = subset.ToList();
            sb.AppendLine("throws=" + list.Count);
            WriteBucket(sb, "by_kind", list, r => r.Kind);
            WriteBucket(sb, "by_spawn", list, r => r.Spawn.ToString("F2", CultureInfo.InvariantCulture));
            WriteBucket(sb, "by_press", list, r => r.Press.ToString("F2", CultureInfo.InvariantCulture));
            sb.AppendLine();
        }

        static void WriteBucket(StringBuilder sb, string label, List<ThrowRow> list, Func<ThrowRow, string> key)
        {
            var groups = list.GroupBy(key).OrderByDescending(g => g.Count());
            foreach (var g in groups)
                sb.AppendLine(label + " " + g.Key + "=" + g.Count());
        }

        static List<ThrowRow> ParseRows(string csvPath)
        {
            var rows = new List<ThrowRow>();
            string[] lines = File.ReadAllLines(csvPath);
            if (lines.Length < 2)
                return rows;
            for (int li = 1; li < lines.Length; li++)
            {
                string line = lines[li];
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                string[] p = line.Split(',');
                if (p.Length < 17)
                    continue;
                var row = new ThrowRow
                {
                    ThrowIndex = ParseInt(p[1]),
                    Kind = p[2],
                    Spawn = ParseFloat(p[4]),
                    Press = ParseFloat(p[5]),
                    SpawnInvalid = ParseInt(p[7]),
                    MaxGap = ParseFloat(p[11]),
                    A10 = ParseInt(p[12]),
                    RMin = ParseFloatOrNaN(p[15]),
                    RMinPhase = p.Length > 16 ? p[16] : ""
                };
                rows.Add(row);
            }

            return rows;
        }

        static int ParseInt(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;

        static float ParseFloat(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;

        static float ParseFloatOrNaN(string s) => string.IsNullOrWhiteSpace(s) ? float.NaN : ParseFloat(s);

        sealed class ThrowRow
        {
            public int ThrowIndex;
            public string Kind;
            public float Spawn;
            public float Press;
            public int SpawnInvalid;
            public float MaxGap;
            public int A10;
            public float RMin;
            public string RMinPhase;
        }
    }
}