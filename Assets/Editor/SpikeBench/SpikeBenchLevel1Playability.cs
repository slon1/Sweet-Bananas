using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SweetBananas.Core;
using SweetBananas.Data;
using SweetBananas.Game;
using SweetBananas.Game.Debug;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zenject;

namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchLevel1Playability
    {
        const float TimeoutSec = 10f;
        const float TimeScale = 12f;

        enum Phase { Idle, WaitScene, ThrowWait, Done }

        struct Plan
        {
            public float TapSec;
            public float Fraction;
            public int Index;
        }

        static Phase _state = Phase.Idle;
        static readonly List<Plan> _plans = new();
        static readonly List<Plan> _verifyPlans = new();
        static bool _verifyRun;
        static float _runTimeScale = TimeScale;
        static int _planIndex;
        static float _pressElapsed;
        static float _throwElapsed;
        static float _maxLift;
        static bool _hadContact;
        static float _contactY;
        static ThrowController _throw;
        static Ribbon _ribbon;
        static Lollipop _lollipop;
        static LevelRules _rules;
        static GameFlow _flow;
        static TargetHolder _target;
        static SpikeOverlay _overlay;
        static StreamWriter _csv;
        static string _runId;
        static Action<string> _onComplete;
        static float _findStart;

        public static bool IsRunning => _state != Phase.Idle;

        public static void Run(Action<string> onComplete = null)
        {
            if (IsRunning)
                return;
            _onComplete = onComplete;
            BuildPlans();
            _planIndex = 0;
            _verifyRun = false;
            _runTimeScale = TimeScale;
            _runId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            _state = Phase.WaitScene;
            _findStart = (float)EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            if (!Application.isPlaying)
            {
                var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Scene.unity");
                if (scene != null)
                    EditorSceneManager.playModeStartScene = scene;
                EditorApplication.EnterPlaymode();
            }
        }

        static void BuildPlans()
        {
            _plans.Clear();
            var list = new List<Plan>();
            for (int t = 3; t <= 15; t++)
            {
                float tap = t * 0.01f;
                for (int f = 0; f <= 10; f++)
                {
                    list.Add(new Plan
                    {
                        TapSec = tap,
                        Fraction = f * 0.1f,
                        Index = list.Count
                    });
                }
            }

            var rng = new System.Random(SpikeBenchMethodology.DefaultSeed);
            list = list.OrderBy(_ => rng.Next()).ToList();
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                p.Index = i;
                _plans.Add(p);
            }
        }

        static void Begin()
        {
            Time.timeScale = _runTimeScale;
            if (!TryFind())
            {
                Stop("missing actors");
                return;
            }

            _overlay.AutoResetEnabled = false;
            _rules.DebugSetEnabled(true);
            _throw.DebugSetDrive(true);
            _throw.DebugFixedStep += OnStep;

            SpikeBenchMethodology.EnsureLogDirectory();
            string path = Path.Combine(SpikeBenchMethodology.LogDirectory(),
                $"level1_grid_{SpikeBenchMethodology.DefaultSeed}_{_runId}.csv");
            _csv = new StreamWriter(path, false, Encoding.UTF8);
            _csv.WriteLine("index,tap_sec,spawn_fraction,result,time_sec,target_contact,lift_above_contact");

            StartThrow();
        }

        static bool TryGetActivePlan(out Plan plan)
        {
            plan = default;
            var list = _verifyRun ? _verifyPlans : _plans;
            if (_planIndex < 0 || _planIndex >= list.Count)
                return false;
            plan = list[_planIndex];
            return true;
        }

        static void StartThrow()
        {
            if (!TryGetActivePlan(out Plan plan))
            {
                Stop("plan_index_oob");
                return;
            }
            _rules.Restart(plan.Fraction);
            _pressElapsed = 0f;
            _throwElapsed = 0f;
            _maxLift = 0f;
            _hadContact = false;
            _contactY = 0f;
            _throw.DebugResetFixedSteps();
            _throw.DebugSetHeld(true);
            _state = Phase.ThrowWait;
        }

        static void OnStep()
        {
            if (_state != Phase.ThrowWait)
                return;

            if (!TryGetActivePlan(out Plan plan))
                return;
            _pressElapsed += Time.fixedDeltaTime;
            if (_pressElapsed >= plan.TapSec)
                _throw.DebugSetHeld(false);

            _throwElapsed += Time.fixedDeltaTime;
            TrackContact();

            if (_flow.State == GameState.Win)
                FinishThrow("Win");
            else if (_flow.State == GameState.Fail)
                FinishThrow("Fail");
            else if (_throwElapsed >= TimeoutSec)
                FinishThrow("Timeout");
        }

        static void TrackContact()
        {
            if (_target?.Collider == null)
                return;
            if (!_hadContact)
            {
                foreach (CapsuleCollider2D cap in _ribbon.Colliders)
                {
                    if (cap != null && cap.IsTouching(_target.Collider))
                    {
                        _hadContact = true;
                        _contactY = _ribbon.Center.y;
                        break;
                    }
                }
            }

            if (_hadContact)
            {
                float lift = _ribbon.Center.y - _contactY;
                if (lift > _maxLift)
                    _maxLift = lift;
            }
        }

        static void FinishThrow(string result)
        {
            if (!TryGetActivePlan(out Plan plan))
                return;
            var row = string.Join(",",
                plan.Index.ToString(CultureInfo.InvariantCulture),
                plan.TapSec.ToString(CultureInfo.InvariantCulture),
                plan.Fraction.ToString(CultureInfo.InvariantCulture),
                result,
                _throwElapsed.ToString(CultureInfo.InvariantCulture),
                _hadContact ? "1" : "0",
                _hadContact ? _maxLift.ToString(CultureInfo.InvariantCulture) : "");
            if (_verifyRun)
            {
                string verifyPath = Path.Combine(SpikeBenchMethodology.LogDirectory(),
                    $"level1_verify1x_{SpikeBenchMethodology.DefaultSeed}_{_runId}.csv");
                File.AppendAllText(verifyPath,
                    (_planIndex == 0 ? "index,tap_sec,spawn_fraction,result,time_sec,target_contact,lift_above_contact\n" : "") + row + "\n");
            }
            else
                _csv.WriteLine(row);

            if (_throw != null)
                _throw.DebugFixedStep -= OnStep;
            _state = Phase.Done;

            _planIndex++;
            int total = _verifyRun ? _verifyPlans.Count : _plans.Count;
            if (_planIndex >= total)
            {
                if (!_verifyRun)
                    BeginVerify1x(null);
                else
                    Complete();
                return;
            }

            _throw.DebugFixedStep += OnStep;
            _state = Phase.ThrowWait;
            StartThrow();
        }

        static void BeginVerify1x(string path)
        {
            path ??= Path.Combine(SpikeBenchMethodology.LogDirectory(),
                $"level1_grid_{SpikeBenchMethodology.DefaultSeed}_{_runId}.csv");
            _verifyPlans.Clear();
            var winners = new List<Plan>();
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.StartsWith("index", StringComparison.Ordinal))
                    continue;
                string[] p = line.Split(',');
                if (p.Length < 4 || p[3] != "Win")
                    continue;
                winners.Add(new Plan
                {
                    TapSec = float.Parse(p[1], CultureInfo.InvariantCulture),
                    Fraction = float.Parse(p[2], CultureInfo.InvariantCulture),
                    Index = int.Parse(p[0], CultureInfo.InvariantCulture)
                });
            }

            if (winners.Count == 0)
            {
                Complete();
                return;
            }

            int take = Mathf.Min(10, winners.Count);
            for (int i = 0; i < take; i++)
            {
                int idx = take == 1 ? 0 : i * (winners.Count - 1) / (take - 1);
                _verifyPlans.Add(winners[idx]);
            }

            _csv?.Flush();
            _verifyRun = true;
            _runTimeScale = 1f;
            Time.timeScale = 1f;
            _planIndex = 0;
            if (_throw != null)
                _throw.DebugFixedStep += OnStep;
            _state = Phase.ThrowWait;
            StartThrow();
        }

        static void Complete()
        {
            string path = Path.Combine(SpikeBenchMethodology.LogDirectory(),
                $"level1_grid_{SpikeBenchMethodology.DefaultSeed}_{_runId}.csv");
            _csv?.Flush();
            _csv?.Dispose();
            _csv = null;
            string summary = WriteSummary(path);
            var cb = _onComplete;
            _onComplete = null;
            _verifyRun = false;
            _runTimeScale = TimeScale;
            Stop(summary);
            cb?.Invoke(summary);
            if (cb == null)
                EditorApplication.ExitPlaymode();
        }

        static string WriteSummary(string csvPath)
        {
            var rows = new List<string[]>();
            foreach (string line in File.ReadAllLines(csvPath))
            {
                if (line.StartsWith("index", StringComparison.Ordinal))
                    continue;
                rows.Add(line.Split(','));
            }

            int wins = rows.Count(r => r[3] == "Win");
            int fails = rows.Count(r => r[3] == "Fail");
            int timeouts = rows.Count(r => r[3] == "Timeout");
            float maxLiftWin = 0f;
            foreach (string[] r in rows.Where(r => r[3] == "Win" && r.Length > 6 && !string.IsNullOrEmpty(r[6])))
            {
                float lift = float.Parse(r[6], CultureInfo.InvariantCulture);
                if (lift > maxLiftWin)
                    maxLiftWin = lift;
            }

            var byCell = new Dictionary<(int tap, int frac), char>();
            foreach (string[] r in rows)
            {
                float tap = float.Parse(r[1], CultureInfo.InvariantCulture);
                float frac = float.Parse(r[2], CultureInfo.InvariantCulture);
                int ti = Mathf.RoundToInt(tap * 100f);
                int fi = Mathf.RoundToInt(frac * 10f);
                char code = r[3] switch { "Win" => 'W', "Fail" => 'F', _ => 'T' };
                byCell[(ti, fi)] = code;
            }

            var sb = new StringBuilder();
            sb.AppendLine("wins=" + wins + " fails=" + fails + " timeouts=" + timeouts);
            sb.AppendLine("win_pct=" + (100f * wins / rows.Count).ToString("F1", CultureInfo.InvariantCulture));
            sb.AppendLine("max_lift_win=" + maxLiftWin.ToString("F4", CultureInfo.InvariantCulture));
            sb.AppendLine("matrix_frac_rows tap_cols_0.03..0.15");
            for (int fi = 0; fi <= 10; fi++)
            {
                var line = new StringBuilder("f" + (fi * 0.1f).ToString("F1", CultureInfo.InvariantCulture));
                for (int ti = 3; ti <= 15; ti++)
                {
                    char c = byCell.TryGetValue((ti, fi), out char v) ? v : '?';
                    line.Append('\t').Append(c);
                }
                sb.AppendLine(line.ToString());
            }

            string verifyPath = Path.Combine(SpikeBenchMethodology.LogDirectory(),
                $"level1_verify1x_{SpikeBenchMethodology.DefaultSeed}_{_runId}.csv");
            if (File.Exists(verifyPath))
            {
                int verifyTotal = 0;
                int verifyWin = 0;
                foreach (string line in File.ReadAllLines(verifyPath))
                {
                    if (line.StartsWith("index", StringComparison.Ordinal))
                        continue;
                    verifyTotal++;
                    string[] p = line.Split(',');
                    if (p.Length > 3 && p[3] == "Win")
                        verifyWin++;
                }
                sb.AppendLine("verify1x_throws=" + verifyTotal);
                sb.AppendLine("verify1x_win_matches=" + verifyWin);
            }

            string outPath = Path.Combine(SpikeBenchMethodology.LogDirectory(), "level1_grid_summary.txt");
            File.WriteAllText(outPath, sb.ToString());
            return sb.ToString();
        }

        static bool TryFind()
        {
            _throw = UnityEngine.Object.FindAnyObjectByType<ThrowController>();
            _ribbon = UnityEngine.Object.FindAnyObjectByType<Ribbon>();
            _lollipop = UnityEngine.Object.FindAnyObjectByType<Lollipop>();
            _rules = UnityEngine.Object.FindAnyObjectByType<LevelRules>();
            var sceneContext = UnityEngine.Object.FindAnyObjectByType<SceneContext>();
            _flow = null;
            if (sceneContext != null && sceneContext.Container != null)
            {
                try
                {
                    _flow = sceneContext.Container.Resolve<GameFlow>();
                }
                catch (ZenjectException)
                {
                    _flow = null;
                }
            }
            _target = UnityEngine.Object.FindAnyObjectByType<TargetHolder>();
            _overlay = UnityEngine.Object.FindAnyObjectByType<SpikeOverlay>();
            return _throw != null && _ribbon != null && _rules != null && _flow != null;
        }

        static void Stop(string message = null)
        {
            EditorApplication.update -= Tick;
            if (_throw != null)
            {
                _throw.DebugFixedStep -= OnStep;
                _throw.DebugSetDrive(false);
                _throw.DebugSetHeld(false);
            }

            _csv?.Dispose();
            _csv = null;
            _state = Phase.Idle;
            if (!string.IsNullOrEmpty(message))
                Debug.Log("SpikeBench Level1 grid: " + message);
        }

        static void Tick()
        {
            if (_state == Phase.Idle)
            {
                EditorApplication.update -= Tick;
                return;
            }

            if (_state == Phase.WaitScene)
            {
                if (EditorApplication.timeSinceStartup - _findStart > 180f)
                {
                    Stop("timeout");
                    return;
                }

                if (Application.isPlaying && TryFind())
                    Begin();
                return;
            }

            if (!Application.isPlaying)
                Stop();
        }
    }
}