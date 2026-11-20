using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using SweetBananas.Data;
using SweetBananas.Game;
using UnityEditor;
using UnityEngine;

namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchE2Orchestrator
    {
        readonly struct E2Job
        {
            public readonly string ExperimentId;
            public readonly Action<GameConfig> Mutate;
            public readonly Action<Lollipop, Ribbon, ThrowController> SceneSetup;
            public readonly bool RebuildRibbon;

            public E2Job(string id, Action<GameConfig> mutate, Action<Lollipop, Ribbon, ThrowController> sceneSetup, bool rebuild)
            {
                ExperimentId = id;
                Mutate = mutate;
                SceneSetup = sceneSetup;
                RebuildRibbon = rebuild;
            }
        }

        static readonly Queue<E2Job> Queue = new();
        static GameConfig _benchClone;
        static bool _running;

        [MenuItem("Sweet Bananas/SpikeBench/M — Run E2b then E2s grid")]
        public static void RunGridFromMenu()
        {
            if (_running || SpikeBenchSession.IsRunning)
                return;
            BuildQueue();
            _running = true;
            SpikeBenchE2Results.Reset();
            RunNext();
        }

        [MenuItem("Sweet Bananas/SpikeBench/M — E2b only (head off after spawn)")]
        public static void RunE2bOnlyFromMenu()
        {
            if (_running || SpikeBenchSession.IsRunning)
                return;
            Queue.Clear();
            Queue.Enqueue(new E2Job("E2b", null, null, false));
            _running = true;
            RunNext();
        }

        static void BuildQueue()
        {
            Queue.Clear();
            Queue.Enqueue(new E2Job("E2b", null, null, false));
            foreach (float lim in new[] { 65f, 35f, 25f, 15f })
            {
                float capture = lim;
                Queue.Enqueue(new E2Job("E2s_J" + capture.ToString("F0", CultureInfo.InvariantCulture),
                    c => c.JointLimitDeg = capture,
                    (l, r, t) => l.DebugSetHeadColliderEnabled(true),
                    true));
            }
        }

        static void RunNext()
        {
            if (Queue.Count == 0)
            {
                SpikeBenchE2Results.Flush();
                _running = false;
                return;
            }

            E2Job job = Queue.Dequeue();
            SpikeBenchSession.SetHeadOffAfterSpawnClearance(job.ExperimentId == "E2b");
            SpikeBenchSession.SetBeginPlayHook(() =>
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Data/GameConfig.asset");
                _benchClone = UnityEngine.Object.Instantiate(asset);
                job.Mutate?.Invoke(_benchClone);
                _throwSetup(job);
            });

            float ts = SpikeBenchMethodology.ScreeningTimeScale;
            SpikeBenchSession.Start(job.ExperimentId, SpikeBenchMethodology.DefaultSeed, ts,
                SpikeBenchMethodology.SeriesTaps, SpikeBenchMethodology.SeriesHolds, false);
            EditorApplication.playModeStateChanged += OnPlayEnd;
        }

        static ThrowController _tc;
        static Ribbon _rb;
        static Lollipop _lp;

        static void _throwSetup(E2Job job)
        {
            _tc = UnityEngine.Object.FindAnyObjectByType<ThrowController>();
            _rb = UnityEngine.Object.FindAnyObjectByType<Ribbon>();
            _lp = UnityEngine.Object.FindAnyObjectByType<Lollipop>();
            if (_tc == null || _rb == null || _lp == null)
                return;
            _tc.DebugSetConfigOverride(_benchClone);
            _rb.DebugSetConfigOverride(_benchClone);
            if (job.RebuildRibbon)
                _rb.DebugRebuild();
            job.SceneSetup?.Invoke(_lp, _rb, _tc);
        }

        static void OnPlayEnd(PlayModeStateChange s)
        {
            if (s != PlayModeStateChange.EnteredEditMode)
                return;
            EditorApplication.playModeStateChanged -= OnPlayEnd;
            if (_benchClone != null)
            {
                UnityEngine.Object.DestroyImmediate(_benchClone);
                _benchClone = null;
            }

            string dir = SpikeBenchMethodology.LogDirectory();
            string[] sums = Directory.GetFiles(dir, "summary_E2*.txt");
            Array.Sort(sums);
            if (sums.Length > 0)
            {
                string text = File.ReadAllText(sums[^1]);
                SpikeBenchE2Results.Absorb(Path.GetFileName(sums[^1]), text);
            }

            if (SpikeBenchE2Results.ShouldStopA12())
            {
                SpikeBenchE2Results.Flush();
                _running = false;
                Debug.LogWarning("SpikeBench E2s: A12 stop — no joint limit passed.");
                return;
            }

            EditorApplication.delayCall += RunNext;
        }
    }

    public static class SpikeBenchE2Results
    {
        static readonly StringBuilder Table = new();

        public static void Reset() => Table.Clear();

        public static void Absorb(string fileName, string summaryText)
        {
            Table.AppendLine("file=" + fileName);
            Table.AppendLine(summaryText);
            Table.AppendLine("---");
        }

        public static bool ShouldStopA12()
        {
            if (!Table.ToString().Contains("E2s_J15"))
                return false;
            return Table.ToString().Contains("r_min_throw_min_valid=0.") &&
                   !Table.ToString().Contains("r_min_throw_min_valid=0.35");
        }

        public static void Flush()
        {
            string path = Path.Combine(SpikeBenchMethodology.LogDirectory(), "e2_summary_table.txt");
            File.WriteAllText(path, Table.ToString());
            Debug.Log("SpikeBench E2 table: " + path);
        }
    }
}