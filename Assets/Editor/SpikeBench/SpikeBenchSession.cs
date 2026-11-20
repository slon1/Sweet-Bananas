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
    public static class SpikeBenchSession
    {
        const float FindSceneTimeoutSec = 90f;

        enum SessionState
        {
            Idle,
            WaitScene,
            ThrowPress,
            ThrowFree,
            ThrowSecondPress,
            Done
        }

        readonly struct ThrowResult
        {
            public readonly int Index;
            public readonly float MaxGap;
            public readonly bool A10Break;
            public readonly string Phase;

            public ThrowResult(int index, float maxGap, bool a10Break, string phase)
            {
                Index = index;
                MaxGap = maxGap;
                A10Break = a10Break;
                Phase = phase;
            }
        }

        static SessionState _state = SessionState.Idle;
        static string _experimentId = "E0";
        static int _seed;
        static float _timeScale = SpikeBenchMethodology.BenchTimeScale;
        static int _tapCount = SpikeBenchMethodology.SeriesTaps;
        static int _holdCount = SpikeBenchMethodology.SeriesHolds;
        static bool _verifyRealtime;
        static bool _isVerifyRun;
        static string _runId;

        static readonly List<BenchThrowPlan> _plans = new();
        static readonly List<BenchThrowPlan> _mainPlansForVerify = new();
        static readonly List<ThrowResult> _mainResults = new();
        static readonly List<ThrowResult> _verifyResults = new();
        static int _planIndex;
        static BenchThrowPlan _current;
        static SpikeBenchCsv _csv;

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
        static SpikeGapPhase _gapPhaseAtMax = SpikeGapPhase.Unknown;
        static SpikeGapPhase _phase = SpikeGapPhase.Unknown;
        static float _maxSpeed;
        static float _rMin = float.PositiveInfinity;
        static bool _naN;
        static float _heldSeconds;
        static float _findSceneStart;

        static bool _followUpTapDone;

        static int _totalBreaks;
        static int _bin085to21;
        static readonly int[] _histogram = new int[5];
        static float _seriesMaxGap;

        static readonly float[] _pairGapsBuf = new float[7];
        static bool _gapMetricsFrozen;
        static int _firstGap085Step = -1;
        static float _firstGap085Value;
        static int _firstGap085Pair;
        static string _firstGap085Phase = "";
        static int _firstGap021Step = -1;
        static float _firstGap021Value;
        static int _firstGap021Pair;
        static string _firstGap021Phase = "";
        static SpikeBenchTrace _trace;
        static int _throwPhysicsStep;

        public static bool IsRunning => _state != SessionState.Idle;

        public static void Start(string experimentId, int seed, float timeScale, int taps, int holds, bool verifyRealtime)
        {
            if (_state != SessionState.Idle)
            {
                Debug.LogWarning("SpikeBench: session already running.");
                return;
            }

            _experimentId = experimentId;
            _seed = seed;
            _timeScale = timeScale;
            _tapCount = taps;
            _holdCount = holds;
            _verifyRealtime = verifyRealtime;
            _isVerifyRun = false;
            _runId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            _mainResults.Clear();
            _mainPlansForVerify.Clear();

            SpikeBenchMethodology.WriteMethodologyArtifact(seed, experimentId, taps, holds);
            _plans.Clear();
            _plans.AddRange(SpikeBenchPlanBuilder.BuildSeriesPlans(seed, taps, holds));
            if (verifyRealtime)
                _mainPlansForVerify.AddRange(_plans);

            EditorApplication.update += Tick;

            if (!Application.isPlaying)
            {
                var startScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Scene.unity");
                if (startScene != null)
                    EditorSceneManager.playModeStartScene = startScene;

                EditorApplication.EnterPlaymode();
                _state = SessionState.WaitScene;
                _findSceneStart = (float)EditorApplication.timeSinceStartup;
                return;
            }

            _state = SessionState.WaitScene;
            _findSceneStart = (float)EditorApplication.timeSinceStartup;
        }

        public static void Stop()
        {
            TearDown();
            _state = SessionState.Idle;
        }

        static void BeginPlay()
        {
            Time.timeScale = _timeScale;
            if (!TryFindActors())
            {
                Debug.LogError("SpikeBench: scene objects not found.");
                Stop();
                return;
            }

            _rMinExcludeFixedSteps = Mathf.Max(1, Mathf.CeilToInt(
                SpikeBenchMethodology.RMinExcludeAfterSpawnSec / _config.FixedDeltaTime));

            _overlay.AutoResetEnabled = false;
            _throw.DebugSetDrive(true);
            _throw.DebugFixedStep += OnDebugFixedStep;

            string csvExperiment = _isVerifyRun ? _experimentId + "_verify1x" : _experimentId;
            _csv = new SpikeBenchCsv(csvExperiment, _seed, _runId);
            _planIndex = 0;
            _totalBreaks = 0;
            _bin085to21 = 0;
            Array.Clear(_histogram, 0, _histogram.Length);
            _seriesMaxGap = 0f;
            StartThrow(_plans[_planIndex]);
            Debug.Log("SpikeBench: started " + csvExperiment + " seed=" + _seed + " timeScale=" + _timeScale);
        }

        static bool TryFindActors()
        {
            _throw = UnityEngine.Object.FindAnyObjectByType<ThrowController>();
            _ribbon = UnityEngine.Object.FindAnyObjectByType<Ribbon>();
            _lollipop = UnityEngine.Object.FindAnyObjectByType<Lollipop>();
            _overlay = UnityEngine.Object.FindAnyObjectByType<SpikeOverlay>();
            if (_config == null)
                _config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Data/GameConfig.asset");
            return _throw != null && _ribbon != null && _lollipop != null && _config != null;
        }

        static void StartThrow(BenchThrowPlan plan)
        {
            _current = plan;
            _ribbon.ResetToSpawn(plan.SpawnFraction);
            _throw.DebugResetFixedSteps();
            _spawnFixedStep = _throw.FixedSteps;
            _pressElapsed = 0f;
            _freeElapsed = 0f;
            _stopElapsed = 0f;
            _maxGap = 0f;
            _gapPhaseAtMax = SpikeGapPhase.Unknown;
            _phase = SpikeGapPhase.Contact;
            _maxSpeed = 0f;
            _rMin = float.PositiveInfinity;
            _naN = false;
            _heldSeconds = 0f;
            _followUpTapDone = !plan.FollowUpTapAfterRest;
            _gapMetricsFrozen = false;
            _firstGap085Step = -1;
            _firstGap021Step = -1;
            _throwPhysicsStep = 0;
            if (plan.Index == 0)
            {
                _trace?.Dispose();
                string csvExperiment = _isVerifyRun ? _experimentId + "_verify1x" : _experimentId;
                _trace = new SpikeBenchTrace(csvExperiment, _seed, _runId);
            }

            _throw.DebugSetHeld(true);
            _state = SessionState.ThrowPress;
        }

        static void Tick()
        {
            if (_state == SessionState.Idle)
            {
                EditorApplication.update -= Tick;
                return;
            }

            if (_state == SessionState.WaitScene)
            {
                if (EditorApplication.timeSinceStartup - _findSceneStart > FindSceneTimeoutSec)
                {
                    Debug.LogError("SpikeBench: timeout waiting for scene.");
                    Stop();
                    return;
                }

                if (Application.isPlaying && TryFindActors())
                    BeginPlay();

                return;
            }

            if (!Application.isPlaying)
                Stop();
        }

        static void OnDebugFixedStep()
        {
            if (_state == SessionState.Idle || _state == SessionState.WaitScene || _state == SessionState.Done)
                return;
            if (_throw == null || _ribbon == null)
                return;

            SampleFixedStep();
        }

        static void SampleFixedStep()
        {
            UpdateNaNFlag();

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
            SpikeBenchMeasurements.FillPairGaps(_ribbon, _pairGapsBuf, ref maxPair);
            for (int p = 1; p < _pairGapsBuf.Length; p++)
            {
                if (_pairGapsBuf[p] > _pairGapsBuf[worstPair])
                    worstPair = p;
            }

            string phaseName = _phase switch
            {
                SpikeGapPhase.Contact => "contact",
                SpikeGapPhase.Flight => "flight",
                SpikeGapPhase.Stopped => "stopped",
                _ => "unknown"
            };

            if (!_gapMetricsFrozen)
            {
                if (maxPair > _maxGap)
                {
                    _maxGap = maxPair;
                    _gapPhaseAtMax = _phase;
                }

                if (_firstGap085Step < 0 && maxPair > SpikeBenchMethodology.GapHardVisual)
                {
                    _firstGap085Step = _throwPhysicsStep;
                    _firstGap085Value = maxPair;
                    _firstGap085Pair = worstPair;
                    _firstGap085Phase = phaseName;
                }

                if (maxPair > SpikeBenchMethodology.GapBreakSoft)
                {
                    if (_firstGap021Step < 0)
                    {
                        _firstGap021Step = _throwPhysicsStep;
                        _firstGap021Value = maxPair;
                        _firstGap021Pair = worstPair;
                        _firstGap021Phase = phaseName;
                    }

                    _gapMetricsFrozen = true;
                }
            }

            float speed = SpikeBenchMeasurements.MaxSegmentSpeed(_ribbon);
            if (speed > _maxSpeed)
                _maxSpeed = speed;

            float rSample = float.NaN;
            if (!_gapMetricsFrozen && _throw.FixedSteps - _spawnFixedStep >= _rMinExcludeFixedSteps)
            {
                float r = SpikeBenchMeasurements.ComputeR(_ribbon);
                rSample = r;
                if (!float.IsNaN(r) && r < _rMin)
                    _rMin = r;
            }

            if (_trace != null && _current.Index == 0)
            {
                float stickAngle = _lollipop != null ? _lollipop.Angle : 0f;
                _trace.WriteRow(_throwPhysicsStep, _pairGapsBuf, speed, phaseName, rSample, stickAngle);
            }

            _throwPhysicsStep++;

            if (_state == SessionState.ThrowPress || _state == SessionState.ThrowSecondPress)
            {
                _pressElapsed += Time.fixedDeltaTime;
                _heldSeconds += Time.fixedDeltaTime;
                float limit = _state == SessionState.ThrowSecondPress
                    ? SpikeBenchPlanBuilder.FollowUpTapDurationSec
                    : _current.PressDuration;
                if (_current.IsHold && _state == SessionState.ThrowPress)
                    limit = Mathf.Min(limit, SpikeBenchMethodology.HoldThrowTimeoutSec);
                if (_pressElapsed >= limit)
                {
                    _throw.DebugSetHeld(false);
                    _state = SessionState.ThrowFree;
                    _freeElapsed = 0f;
                    _stopElapsed = 0f;
                }

                return;
            }

            if (_state == SessionState.ThrowFree)
            {
                if (_current.IsHold && _current.FollowUpTapAfterRest && !_followUpTapDone && _throw.State == ThrowState.Rest)
                {
                    _followUpTapDone = true;
                    _pressElapsed = 0f;
                    _throw.DebugSetHeld(true);
                    _state = SessionState.ThrowSecondPress;
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

        static void UpdateNaNFlag()
        {
            if (_naN)
                return;
            foreach (Rigidbody2D b in _ribbon.Bodies)
            {
                if (b == null)
                    continue;
                if (float.IsNaN(b.position.x) || float.IsNaN(b.position.y))
                {
                    _naN = true;
                    return;
                }
            }
        }

        static void FinishThrow()
        {
            if (_rMin == float.PositiveInfinity)
                _rMin = float.NaN;

            bool a10 = _firstGap021Step >= 0;
            string phaseName = _gapPhaseAtMax switch
            {
                SpikeGapPhase.Contact => "contact",
                SpikeGapPhase.Flight => "flight",
                SpikeGapPhase.Stopped => "stopped",
                _ => "unknown"
            };

            if (_current.Index == 0)
            {
                _trace?.Dispose();
                _trace = null;
            }

            string csvExperiment = _isVerifyRun ? _experimentId + "_verify1x" : _experimentId;
            _csv.WriteThrowRow(
                csvExperiment,
                _current.Index,
                _current.IsHold ? "hold" : "tap",
                _timeScale,
                _current.SpawnFraction,
                _current.PressDuration,
                _maxGap,
                a10,
                phaseName,
                _maxSpeed,
                _rMin,
                _heldSeconds,
                _firstGap085Step,
                _firstGap085Value,
                _firstGap021Step,
                _firstGap021Value,
                _firstGap021Pair);

            var recorded = new ThrowResult(_current.Index, _maxGap, a10, phaseName);
            if (_isVerifyRun)
                _verifyResults.Add(recorded);
            else
                _mainResults.Add(recorded);

            if (a10)
                _totalBreaks++;
            if (_maxGap > SpikeBenchMethodology.GapHardVisual && _maxGap <= SpikeBenchMethodology.GapBreakSoft)
                _bin085to21++;
            int bin = SpikeBenchMeasurements.HistogramBin(_maxGap);
            if (bin >= 0 && bin < _histogram.Length)
                _histogram[bin]++;
            if (_maxGap > _seriesMaxGap)
                _seriesMaxGap = _maxGap;

            _planIndex++;
            if (_planIndex >= _plans.Count)
            {
                CompleteSeries();
                return;
            }

            StartThrow(_plans[_planIndex]);
        }

        static void CompleteSeries()
        {
            WriteSummary();
            _state = SessionState.Done;
            Debug.Log("SpikeBench: series complete. Logs in " + SpikeBenchMethodology.LogDirectory());

            if (_verifyRealtime && _timeScale > 1.01f && !_isVerifyRun)
            {
                _timeScale = 1f;
                Time.timeScale = 1f;
                _csv?.Dispose();
                _runId += "_verify1x";
                _isVerifyRun = true;
                _totalBreaks = 0;
                _bin085to21 = 0;
                Array.Clear(_histogram, 0, _histogram.Length);
                _seriesMaxGap = 0f;
                _verifyResults.Clear();
                _plans.Clear();
                _plans.AddRange(SpikeBenchPlanBuilder.PickVerifyPlans(_mainPlansForVerify));
                _planIndex = 0;
                _state = SessionState.ThrowPress;
                _csv = new SpikeBenchCsv(_experimentId + "_verify1x", _seed, _runId);
                StartThrow(_plans[0]);
                _verifyRealtime = false;
                return;
            }

            TearDown();
            EditorApplication.ExitPlaymode();
        }

        static void WriteSummary()
        {
            int throwsDone = _planIndex;
            string csvExperiment = _isVerifyRun ? _experimentId + "_verify1x" : _experimentId;
            string dir = SpikeBenchMethodology.LogDirectory();
            string summaryPath = Path.Combine(dir, $"summary_{csvExperiment}_{_seed}_{_runId}.txt");
            var sb = new StringBuilder();
            sb.AppendLine("experiment_id=" + csvExperiment);
            sb.AppendLine("seed=" + _seed);
            sb.AppendLine("time_scale=" + _timeScale.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("throws=" + throwsDone);
            sb.AppendLine("a10_breaks_gt_0.21=" + _totalBreaks);
            sb.AppendLine("bin_0.085_0.21=" + _bin085to21);
            sb.AppendLine("max_gap=" + _seriesMaxGap.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("histogram_0_0.085=" + _histogram[0]);
            sb.AppendLine("histogram_1_0.085_0.21=" + _histogram[1]);
            sb.AppendLine("histogram_2_0.21_0.5=" + _histogram[2]);
            sb.AppendLine("histogram_3_0.5_1=" + _histogram[3]);
            sb.AppendLine("histogram_4_gt_1=" + _histogram[4]);
            int softLimit = throwsDone > 0 ? Mathf.FloorToInt(throwsDone * 0.05f) : 0;
            sb.AppendLine("a10_soft_bin_limit=" + softLimit);

            if (_isVerifyRun && _mainResults.Count > 0)
                AppendVerifyComparison(sb);

            File.WriteAllText(summaryPath, sb.ToString());
        }

        static void AppendVerifyComparison(StringBuilder sb)
        {
            int matchesA10 = 0;
            int compared = 0;
            float maxGapDelta = 0f;
            for (int v = 0; v < _verifyResults.Count; v++)
            {
                ThrowResult verify = _verifyResults[v];
                ThrowResult? main = null;
                for (int m = 0; m < _mainResults.Count; m++)
                {
                    if (_mainResults[m].Index == verify.Index)
                    {
                        main = _mainResults[m];
                        break;
                    }
                }

                if (main == null)
                    continue;

                compared++;
                if (main.Value.A10Break == verify.A10Break)
                    matchesA10++;
                float delta = Mathf.Abs(main.Value.MaxGap - verify.MaxGap);
                if (delta > maxGapDelta)
                    maxGapDelta = delta;
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "verify_throw index={0} main_gap={1:F4} verify_gap={2:F4} a10_main={3} a10_verify={4}",
                    verify.Index, main.Value.MaxGap, verify.MaxGap, main.Value.A10Break ? 1 : 0, verify.A10Break ? 1 : 0));
            }

            sb.AppendLine("verify_compare_throws=" + compared);
            sb.AppendLine("verify_a10_flag_matches=" + matchesA10);
            sb.AppendLine("verify_max_gap_delta=" + maxGapDelta.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("verify_result=" + (matchesA10 == compared && compared > 0 ? "match" : "mismatch"));
        }

        static void TearDown()
        {
            EditorApplication.update -= Tick;
            if (_throw != null)
            {
                _throw.DebugFixedStep -= OnDebugFixedStep;
                _throw.DebugSetHeld(false);
                _throw.DebugSetDrive(false);
            }

            if (_overlay != null)
                _overlay.AutoResetEnabled = true;

            _csv?.Dispose();
            _csv = null;
            _trace?.Dispose();
            _trace = null;
            Time.timeScale = 1f;
            _state = SessionState.Idle;
            _isVerifyRun = false;
        }
    }
}