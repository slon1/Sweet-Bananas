using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SweetBananas.Data;
using UnityEditor;
using UnityEngine;

namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchStickFrictionPicker
    {
        public static readonly float[] FrictionGrid = { 0.05f, 0.10f, 0.15f, 0.20f, 0.24f, 0.40f };

        static int _frictionIndex;
        static readonly List<string> _csvPaths = new();
        static Action<float, string> _onDone;

        public static bool IsRunning { get; private set; }

        public static void RunGrid(Action<float, string> onDone)
        {
            if (IsRunning || SpikeBenchRestProbe.IsRunning)
                return;
            IsRunning = true;
            _onDone = onDone;
            _frictionIndex = 0;
            _csvPaths.Clear();
            RunNext();
        }

        static void RunNext()
        {
            if (_frictionIndex >= FrictionGrid.Length)
            {
                FinishGrid();
                return;
            }

            float f = FrictionGrid[_frictionIndex];
            SpikeBenchRestProbe.Start(f, path =>
            {
                _csvPaths.Add(path);
                _frictionIndex++;
                EditorApplication.delayCall += RunNext;
            });
        }

        static void FinishGrid()
        {
            IsRunning = false;
            var table = new StringBuilder();
            table.AppendLine("stick_friction\tfrac_0.25_tip\tpass_v1_le_0.70");
            float? best = null;
            float bestDelta = float.PositiveInfinity;
            for (int i = 0; i < FrictionGrid.Length; i++)
            {
                float friction = FrictionGrid[i];
                string path = i < _csvPaths.Count ? _csvPaths[i] : null;
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    continue;
                if (!TryEvaluate(path, friction, out float tip25, out bool passAll, table))
                    continue;
                if (!passAll)
                    continue;
                if (tip25 < 2.5f || tip25 > 4f)
                    continue;
                float delta = Mathf.Abs(tip25 - 3.2f);
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = friction;
                }
            }

            string tablePath = Path.Combine(SpikeBenchMethodology.LogDirectory(), "stick_friction_grid_summary.txt");
            File.WriteAllText(tablePath, table.ToString());

            if (best == null)
            {
                Debug.LogError("StickFriction grid: no candidate. See " + tablePath);
                _onDone?.Invoke(float.NaN, table.ToString());
                return;
            }

            ApplyStickFriction(best.Value);
            _onDone?.Invoke(best.Value, table.ToString());
        }

        static bool TryEvaluate(string csvPath, float friction, out float tip25, out bool passAll, StringBuilder table)
        {
            tip25 = float.NaN;
            passAll = true;
            var byFrac = new Dictionary<float, (float tip, float v1)>();
            foreach (string line in File.ReadAllLines(csvPath))
            {
                if (line.StartsWith("stick", StringComparison.Ordinal))
                    continue;
                string[] p = line.Split(',');
                if (p.Length < 8)
                    continue;
                float frac = float.Parse(p[3], CultureInfo.InvariantCulture);
                float tip = string.IsNullOrEmpty(p[5]) ? float.NaN : float.Parse(p[5], CultureInfo.InvariantCulture);
                float v1 = float.Parse(p[6], CultureInfo.InvariantCulture);
                byFrac[frac] = (tip, v1);
            }

            if (byFrac.TryGetValue(0.25f, out var r25))
                tip25 = r25.tip;

            foreach (float frac in SpikeBenchMethodology.SpawnFractions)
            {
                if (frac > 0.70f)
                    continue;
                if (!byFrac.TryGetValue(frac, out var row) || row.v1 >= 0.05f)
                    passAll = false;
            }

            table.AppendLine(string.Join("\t",
                friction.ToString(CultureInfo.InvariantCulture),
                float.IsNaN(tip25) ? "no" : tip25.ToString(CultureInfo.InvariantCulture),
                passAll ? "1" : "0"));
            return true;
        }

        public static void ApplyStickFriction(float value)
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Data/GameConfig.asset");
            if (config == null)
                return;
            config.StickFriction = value;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
    }
}