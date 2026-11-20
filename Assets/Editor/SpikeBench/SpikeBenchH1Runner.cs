using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using SweetBananas.Data;
using SweetBananas.Game;
using SweetBananas.Game.Debug;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchH1Runner
    {
        enum State
        {
            Idle,
            WaitScene,
            ThrowPress,
            ThrowFree,
            ThrowSecondPress,
            Done
        }

        static readonly int[] BreakIndices = SpikeBenchE0Analysis.BreakThrowIndices;

        static State _state = State.Idle;
        static int _seed = SpikeBenchMethodology.DefaultSeed;
        static float _timeScale = SpikeBenchMethodology.BenchTimeScale;
        static string _runId;
        static readonly List<BenchThrowPlan> _plans = new();
        static int _planIndex;
        static BenchThrowPlan _current;
        static SpikeBenchExtendedTrace _trace;
        static int _throwIndex;

        static ThrowController _throw;
        static Ribbon _ribbon;
        static Lollipop _lollipop;
        static SpikeOverlay _overlay;
        static GameConfig _config;

        static float _pressElapsed;
        static float _freeElapsed;
        static float _stopElapsed;
        static int _spawnFixedStep;
        static int _rMinExcludeFixedSteps;
        static float _maxGap;
        static SpikeGapPhase _phase = SpikeGapPhase.Contact;
        static bool _followUpTapDone;
        static int _throwPhysicsStep;
        static int _firstGap021Step = -1;
        static int _firstGap021Pair;
        static bool _gapFrozen;
        static readonly float[] _gaps = new float[7];
        static float _findSceneStart;
        static bool _playBegun;

        public static bool IsRunning => _state != State.Idle;

        [MenuItem("Sweet Bananas/SpikeBench/H1 — trace five E0 breaks")]
        public static void RunFromMenu()
        {
            if (IsRunning || SpikeBenchSession.IsRunning)
                return;
            Start(SpikeBenchMethodology.DefaultSeed, SpikeBenchMethodology.BenchTimeScale);
        }

        public static void Start(int seed, float timeScale)
        {
            _seed = seed;
            _timeScale = timeScale;
            _runId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            _playBegun = false;
            SpikeBenchH1Conclusions.Reset();
            _plans.Clear();
            var series = SpikeBenchPlanBuilder.BuildSeriesPlans(seed, SpikeBenchMethodology.SeriesTaps, SpikeBenchMethodology.SeriesHolds);
            foreach (int idx in BreakIndices)
                _plans.Add(series[idx]);

            EditorApplication.update += Tick;
            if (!Application.isPlaying)
            {
                var startScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Scene.unity");
                if (startScene != null)
                    EditorSceneManager.playModeStartScene = startScene;
                EditorApplication.EnterPlaymode();
                _state = State.WaitScene;
                _findSceneStart = (float)EditorApplication.timeSinceStartup;
                return;
            }

            _state = State.WaitScene;
            _findSceneStart = (float)EditorApplication.timeSinceStartup;
        }

        public static void Stop()
        {
            EditorApplication.update -= Tick;
            _trace?.Dispose();
            _trace = null;
            if (_throw != null)
            {
                _throw.DebugFixedStep -= OnDebugFixedStep;
                _throw.DebugSetHeld(false);
                _throw.DebugSetDrive(false);
            }

            if (_overlay != null)
                _overlay.AutoResetEnabled = true;
            Time.timeScale = 1f;
            _state = State.Idle;
        }

        static void BeginPlay()
        {
            Time.timeScale = _timeScale;
            _throw = UnityEngine.Object.FindAnyObjectByType<ThrowController>();
            _ribbon = UnityEngine.Object.FindAnyObjectByType<Ribbon>();
            _lollipop = UnityEngine.Object.FindAnyObjectByType<Lollipop>();
            _overlay = UnityEngine.Object.FindAnyObjectByType<SpikeOverlay>();
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Data/GameConfig.asset");
            if (_throw == null || _ribbon == null || _lollipop == null || _config == null)
            {
                Debug.LogError("SpikeBench H1: scene objects missing.");
                Stop();
                EditorApplication.ExitPlaymode();
                return;
            }

            _rMinExcludeFixedSteps = Mathf.Max(1, Mathf.CeilToInt(
                SpikeBenchMethodology.RMinExcludeAfterSpawnSec / _config.FixedDeltaTime));
            _overlay.AutoResetEnabled = false;
            _throw.DebugSetDrive(true);
            _throw.DebugFixedStep += OnDebugFixedStep;
            _planIndex = 0;
            StartThrow(_plans[0]);
        }

        static void StartThrow(BenchThrowPlan plan)
        {
            _current = plan;
            _throwIndex = plan.Index;
            _ribbon.ResetToSpawn(plan.SpawnFraction);
            _throw.DebugResetFixedSteps();
            _throw.SnapRest();
            _spawnFixedStep = _throw.FixedSteps;
            _pressElapsed = 0f;
            _freeElapsed = 0f;
            _stopElapsed = 0f;
            _maxGap = 0f;
            _phase = SpikeGapPhase.Contact;
            _followUpTapDone = !plan.FollowUpTapAfterRest;
            _gapFrozen = false;
            _firstGap021Step = -1;
            _throwPhysicsStep = 0;
            _trace?.Dispose();
            _trace = new SpikeBenchExtendedTrace($"trace_E0_{_throwIndex}.csv");
            _throw.DebugSetHeld(true);
            _state = State.ThrowPress;
        }

        static void Tick()
        {
            if (_state == State.Idle)
            {
                EditorApplication.update -= Tick;
                return;
            }

            if (_state == State.WaitScene)
            {
                if (EditorApplication.timeSinceStartup - _findSceneStart > 90f)
                {
                    Debug.LogError("SpikeBench H1: timeout.");
                    Stop();
                    return;
                }

                if (!Application.isPlaying || _playBegun)
                    return;
                _throw = UnityEngine.Object.FindAnyObjectByType<ThrowController>();
                _ribbon = UnityEngine.Object.FindAnyObjectByType<Ribbon>();
                if (_throw != null && _ribbon != null)
                {
                    _playBegun = true;
                    BeginPlay();
                }

                return;
            }

            if (!Application.isPlaying)
                Stop();
        }

        static void OnDebugFixedStep() => Sample();

        static void Sample()
        {
            bool touching = SpikeBenchMeasurements.AnyTouchingLollipop(_ribbon, _lollipop);
            bool stopped = SpikeBenchMeasurements.AllStopped(_ribbon, SpikeBenchMethodology.StopSpeedThreshold);
            if (stopped)
                _stopElapsed += Time.fixedDeltaTime;
            else
                _stopElapsed = 0f;

            if (touching)
                _phase = SpikeGapPhase.Contact;
            else if (_stopElapsed >= SpikeBenchMethodology.StopHoldSec)
                _phase = SpikeGapPhase.Stopped;
            else
                _phase = SpikeGapPhase.Flight;

            float maxPair = 0f;
            int worstPair = 0;
            SpikeBenchMeasurements.FillPairGaps(_ribbon, _gaps, ref maxPair);
            for (int p = 1; p < _gaps.Length; p++)
            {
                if (_gaps[p] > _gaps[worstPair])
                    worstPair = p;
            }

            string phaseName = _phase switch
            {
                SpikeGapPhase.Contact => "contact",
                SpikeGapPhase.Flight => "flight",
                SpikeGapPhase.Stopped => "stopped",
                _ => "unknown"
            };

            if (!_gapFrozen)
            {
                if (maxPair > _maxGap)
                    _maxGap = maxPair;
                if (maxPair > SpikeBenchMethodology.GapBreakSoft)
                {
                    if (_firstGap021Step < 0)
                    {
                        _firstGap021Step = _throwPhysicsStep;
                        _firstGap021Pair = worstPair;
                    }

                    _gapFrozen = true;
                }
            }

            _trace?.WriteRow(_throwPhysicsStep, _gaps, _ribbon, _lollipop, _throw, phaseName);
            _throwPhysicsStep++;

            if (_state == State.ThrowPress || _state == State.ThrowSecondPress)
            {
                _pressElapsed += Time.fixedDeltaTime;
                float limit = _state == State.ThrowSecondPress
                    ? SpikeBenchPlanBuilder.FollowUpTapDurationSec
                    : _current.PressDuration;
                if (_current.IsHold && _state == State.ThrowPress)
                    limit = Mathf.Min(limit, SpikeBenchMethodology.HoldThrowTimeoutSec);
                if (_pressElapsed >= limit)
                {
                    _throw.DebugSetHeld(false);
                    _state = State.ThrowFree;
                    _freeElapsed = 0f;
                    _stopElapsed = 0f;
                }

                return;
            }

            if (_state == State.ThrowFree)
            {
                if (_current.IsHold && _current.FollowUpTapAfterRest && !_followUpTapDone && _throw.State == ThrowState.Rest)
                {
                    _followUpTapDone = true;
                    _pressElapsed = 0f;
                    _throw.DebugSetHeld(true);
                    _state = State.ThrowSecondPress;
                    return;
                }

                _freeElapsed += Time.fixedDeltaTime;
                float timeout = _current.IsHold
                    ? SpikeBenchMethodology.HoldThrowTimeoutSec
                    : SpikeBenchMethodology.TapThrowTimeoutSec;

                bool settled = _stopElapsed >= SpikeBenchMethodology.StopHoldSec;
                bool timedOut = _freeElapsed >= timeout;
                bool failed = SpikeBenchMeasurements.FailBounds(_ribbon, _lollipop, _config);
                bool canEnd = _followUpTapDone || !_current.FollowUpTapAfterRest;
                if (canEnd && (settled || timedOut || failed))
                    FinishThrow();
            }
        }

        static void FinishThrow()
        {
            _trace?.Dispose();
            _trace = null;
            bool broke = _firstGap021Step >= 0;
            string line = SpikeBenchH1Conclusions.AnalyzeTraceFile(_throwIndex, _timeScale, broke, _firstGap021Step, _firstGap021Pair);
            SpikeBenchH1Conclusions.Append(line);
            _planIndex++;
            if (_planIndex >= _plans.Count)
            {
                Complete();
                return;
            }

            StartThrow(_plans[_planIndex]);
        }

        static void Complete()
        {
            _state = State.Done;
            Debug.Log("SpikeBench H1 complete. See " + SpikeBenchH1Conclusions.OutputPath());
            Stop();
            EditorApplication.ExitPlaymode();
        }
    }

    public static class SpikeBenchH1Conclusions
    {
        public static string OutputPath() => Path.Combine(SpikeBenchMethodology.LogDirectory(), "h1_conclusions.txt");

        public static void Reset()
        {
            SpikeBenchMethodology.EnsureLogDirectory();
            File.WriteAllText(OutputPath(), "H1 break trace conclusions seed=" + SpikeBenchMethodology.DefaultSeed + "\n");
        }

        public static void Append(string line)
        {
            SpikeBenchMethodology.EnsureLogDirectory();
            if (!File.Exists(OutputPath()))
                Reset();
            File.AppendAllText(OutputPath(), line + "\n");
        }

        public static string AnalyzeTraceFile(int throwIndex, float timeScale, bool broke, int breakStep, int breakPair)
        {
            string path = Path.Combine(SpikeBenchMethodology.LogDirectory(), $"trace_E0_{throwIndex}.csv");
            if (!File.Exists(path))
                return "throw=" + throwIndex + " time_scale=" + timeScale.ToString(CultureInfo.InvariantCulture) + " trace_missing=1";

            string[] lines = File.ReadAllLines(path);
            int rowIdx = breakStep + 1;
            if (rowIdx >= lines.Length)
                rowIdx = lines.Length - 1;
            string row = lines[Math.Max(1, rowIdx)];
            string[] p = row.Split(',');
            float omega = p.Length > 12 ? Parse(p[12]) : 0f;
            string phase = p.Length > 17 ? p[17] : "";
            string throwState = p.Length > 18 ? p[18] : "";
            uint headMask = p.Length > 14 ? (uint)ParseInt(p[14]) : 0;
            uint stickMask = p.Length > 15 ? (uint)ParseInt(p[15]) : 0;
            string contact = DescribeContact(headMask, stickMask, breakPair);
            if (!broke)
                return "throw=" + throwIndex + " time_scale=" + timeScale.ToString(CultureInfo.InvariantCulture) +
                       " reproduce_gt_0.21=0 max_series_gap=see_csv";
            return "throw=" + throwIndex + " time_scale=" + timeScale.ToString(CultureInfo.InvariantCulture) +
                   " step=" + breakStep + " pair=" + breakPair + " contact=" + contact +
                   " stick_omega=" + omega.ToString("F1", CultureInfo.InvariantCulture) +
                   " phase=" + phase + " press_state=" + throwState +
                   " repro=1";
        }

        static string DescribeContact(uint headMask, uint stickMask, int pair)
        {
            if ((headMask & (1u << (pair + 1))) != 0)
                return "seg" + (pair + 1) + "_head";
            if ((stickMask & (1u << (pair + 1))) != 0)
                return "seg" + (pair + 1) + "_stick";
            if ((headMask & (1u << pair)) != 0)
                return "seg" + pair + "_head";
            if ((stickMask & (1u << pair)) != 0)
                return "seg" + pair + "_stick";
            return "geom_near_pair" + pair;
        }

        static float Parse(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
        static int ParseInt(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
    }
}