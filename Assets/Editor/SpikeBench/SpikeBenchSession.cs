using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using SweetBananas.Data;
using SweetBananas.Game;
using SweetBananas.Game.Debug;
using Unity.Profiling;
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
        static readonly int[] _histContact = new int[5];
        static readonly int[] _histFlight = new int[5];
        static readonly int[] _histStopped = new int[5];
        static float _seriesMaxGap;

        static float _spawnClearanceStep0;
        static bool _currentThrowInvalid;
        static float _spawnAngle;
        static int _spawnFixedStepsAtClearance;
        static float _hingeSlackMax;
        static int _spawnInvalidCount;
        static int _nanThrowCount;
        static float _gapStoppedMax;
        static bool _hadStoppedPhase;
        static SpikeBenchTunnelTracker _tunnelTracker;
        static int _throwTunnelViolations;
        static ProfilerRecorder _physicsRecorder;
        static bool _physicsRecorderValid;
        static double _physicsStepNsSum;
        static int _physicsStepSamples;
        static long _meshAllocMax;
        static RibbonMesh _ribbonMesh;
        static readonly List<float> _rMinValid = new();
        static readonly List<string> _firstGap021Events = new();

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

        static LollipopDriveMode? _benchDriveMode;
        static string _rMinPhaseAtMin = "";
        static float _seriesMaxTapOvershoot;
        static float _seriesMaxHoldOvershoot;
        static float _seriesMaxReturnSec;
        static string _worstVerifyMainCsv;
        static Action _beginPlayHook;
        static bool _headOffAfterSpawnClearance;
        static readonly List<float> _j1TimeScales = new();
        static readonly List<int> _j1Reps = new();
        static readonly StringBuilder _j1Report = new();

        public static bool IsRunning => _state != SessionState.Idle;

        public static void StartJ1ReproSeries()
        {
            if (_state != SessionState.Idle)
                return;

            string csv = SpikeBenchE0Analysis.FindLatestE0Csv();
            var soft = SpikeBenchE0Analysis.PickTopSoftBinThrowIndices(csv ?? "", 5);
            var indices = new HashSet<int>(SpikeBenchE0Analysis.BreakThrowIndices);
            foreach (int i in soft)
                indices.Add(i);
            var sorted = new List<int>(indices);
            sorted.Sort();

            var main = SpikeBenchPlanBuilder.BuildSeriesPlans(
                SpikeBenchMethodology.DefaultSeed,
                SpikeBenchMethodology.SeriesTaps,
                SpikeBenchMethodology.SeriesHolds);
            var plans = new List<BenchThrowPlan>();
            _j1TimeScales.Clear();
            _j1Reps.Clear();
            _j1Report.Clear();
            foreach (int idx in sorted)
            {
                plans.Add(main[idx]); _j1TimeScales.Add(1f); _j1Reps.Add(1);
                plans.Add(main[idx]); _j1TimeScales.Add(1f); _j1Reps.Add(2);
                plans.Add(main[idx]); _j1TimeScales.Add(12f); _j1Reps.Add(1);
                plans.Add(main[idx]); _j1TimeScales.Add(12f); _j1Reps.Add(2);
            }

            Start("J1", SpikeBenchMethodology.DefaultSeed, 1f, 0, 0, false, null, plans);
        }

        public static void SetBeginPlayHook(Action hook) => _beginPlayHook = hook;

        /// <summary>E2b: keep head collider on for spawn clearance, disable before throw.</summary>
        public static void SetHeadOffAfterSpawnClearance(bool value) => _headOffAfterSpawnClearance = value;

        public static void StartFinalJ25()
        {
            if (_state != SessionState.Idle)
                return;
            Start("FINAL_J25", SpikeBenchMethodology.DefaultSeed,
                SpikeBenchMethodology.ScreeningTimeScale,
                SpikeBenchMethodology.FinalTaps,
                SpikeBenchMethodology.FinalHolds,
                verifyRealtime: true);
        }

        public static float ScreeningTimeScale => SpikeBenchMethodology.ScreeningTimeScale;

        public static void StartE1()
        {
            Start("E1", SpikeBenchMethodology.DefaultSeed, SpikeBenchMethodology.BenchTimeScale,
                SpikeBenchMethodology.SeriesTaps, SpikeBenchMethodology.SeriesHolds, verifyRealtime: true,
                LollipopDriveMode.AngularVelocity, null);
        }

        public static void StartVerifyWorst(string mainE0CsvPath)
        {
            var main = SpikeBenchPlanBuilder.BuildSeriesPlans(
                SpikeBenchMethodology.DefaultSeed,
                SpikeBenchMethodology.SeriesTaps,
                SpikeBenchMethodology.SeriesHolds);
            var indices = new HashSet<int>(SpikeBenchE0Analysis.BreakThrowIndices);
            foreach (int i in SpikeBenchE0Analysis.PickTopSoftBinThrowIndices(mainE0CsvPath, 5))
                indices.Add(i);
            var sorted = new List<int>(indices);
            sorted.Sort();
            _worstVerifyMainCsv = mainE0CsvPath;
            Start("E0_verify1x_worst", SpikeBenchMethodology.DefaultSeed, 1f, 0, 0, false,
                null, SpikeBenchPlanBuilder.PickPlansByIndices(main, sorted));
        }

        public static void Start(string experimentId, int seed, float timeScale, int taps, int holds, bool verifyRealtime)
        {
            Start(experimentId, seed, timeScale, taps, holds, verifyRealtime, null, null);
        }

        public static void Start(
            string experimentId,
            int seed,
            float timeScale,
            int taps,
            int holds,
            bool verifyRealtime,
            LollipopDriveMode? driveMode,
            List<BenchThrowPlan> customPlans)
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
            _benchDriveMode = driveMode;
            _runId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            _mainResults.Clear();
            _mainPlansForVerify.Clear();
            _seriesMaxTapOvershoot = 0f;
            _seriesMaxHoldOvershoot = 0f;
            _seriesMaxReturnSec = 0f;

            SpikeBenchMethodology.WriteMethodologyArtifact(seed, experimentId, taps, holds);
            _plans.Clear();
            if (customPlans != null && customPlans.Count > 0)
                _plans.AddRange(customPlans);
            else
            {
                _plans.AddRange(SpikeBenchPlanBuilder.BuildSeriesPlans(seed, taps, holds));
                if (verifyRealtime)
                    _mainPlansForVerify.AddRange(_plans);
            }

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
            if (_lollipop != null)
            {
                _lollipop.DebugSetDriveMode(_benchDriveMode ?? LollipopDriveMode.MoveRotation);
            }

            _throw.DebugSetConfigOverride(null);
            _ribbon.DebugSetConfigOverride(null);
            _throw.DebugSetDrive(true);
            _throw.DebugFixedStep += OnDebugFixedStep;
            _beginPlayHook?.Invoke();

            if (_experimentId == "FINAL_J25" && !_isVerifyRun)
            {
                _tunnelTracker = new SpikeBenchTunnelTracker();
                if (_physicsRecorder.Valid)
                    _physicsRecorder.Dispose();
                _physicsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Physics, "Physics2D.Simulate");
                _physicsRecorderValid = _physicsRecorder.Valid;
                if (!_physicsRecorderValid)
                {
                    _physicsRecorder.Dispose();
                    _physicsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Physics,
                        "FixedUpdate.Physics2DFixedUpdate");
                    _physicsRecorderValid = _physicsRecorder.Valid;
                }

                _physicsStepNsSum = 0;
                _physicsStepSamples = 0;
                _meshAllocMax = 0;
            }

            _ribbonMesh = UnityEngine.Object.FindAnyObjectByType<RibbonMesh>();

            string csvExperiment = _isVerifyRun ? _experimentId + "_verify1x" : _experimentId;
            _csv = new SpikeBenchCsv(csvExperiment, _seed, _runId);
            _planIndex = 0;
            _totalBreaks = 0;
            _bin085to21 = 0;
            Array.Clear(_histogram, 0, _histogram.Length);
            Array.Clear(_histContact, 0, _histContact.Length);
            Array.Clear(_histFlight, 0, _histFlight.Length);
            Array.Clear(_histStopped, 0, _histStopped.Length);
            _seriesMaxGap = 0f;
            _spawnInvalidCount = 0;
            _nanThrowCount = 0;
            _rMinValid.Clear();
            _firstGap021Events.Clear();
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
            if (_experimentId == "J1" && _planIndex < _j1TimeScales.Count)
            {
                _timeScale = _j1TimeScales[_planIndex];
                Time.timeScale = _timeScale;
            }

            if (_headOffAfterSpawnClearance && _lollipop != null)
                _lollipop.DebugSetHeadColliderEnabled(true);
            _ribbon.ResetToSpawn(plan.SpawnFraction);
            _throw.DebugResetFixedSteps();
            _spawnFixedStep = _throw.FixedSteps;
            _spawnFixedStepsAtClearance = _throw.FixedSteps;
            Physics2D.SyncTransforms();
            _spawnClearanceStep0 = _ribbon.MinSpawnClearanceDistance();
            _currentThrowInvalid = _spawnClearanceStep0 < 0.01f;
            if (_headOffAfterSpawnClearance && _lollipop != null)
                _lollipop.DebugSetHeadColliderEnabled(false);
            _spawnAngle = _lollipop != null ? _lollipop.Angle : 0f;
            _hingeSlackMax = _ribbon.MaxHingeSlack();
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
            _rMinPhaseAtMin = "";
            _gapStoppedMax = 0f;
            _hadStoppedPhase = false;
            _throwTunnelViolations = 0;
            if (_tunnelTracker != null && !_isVerifyRun)
                _tunnelTracker.BeginThrow(plan.Index);
            _throw.DebugResetDriveMetrics();
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

            if (_phase == SpikeGapPhase.Stopped)
            {
                _hadStoppedPhase = true;
            }

            float maxPair = 0f;
            int worstPair = 0;
            SpikeBenchMeasurements.FillPairGaps(_ribbon, _pairGapsBuf, ref maxPair);
            for (int p = 1; p < _pairGapsBuf.Length; p++)
            {
                if (_pairGapsBuf[p] > _pairGapsBuf[worstPair])
                    worstPair = p;
            }

            if (_phase == SpikeGapPhase.Stopped && !_currentThrowInvalid)
                _gapStoppedMax = Mathf.Max(_gapStoppedMax, maxPair);

            if (_tunnelTracker != null && !_isVerifyRun && !_currentThrowInvalid)
                _throwTunnelViolations += _tunnelTracker.SampleStep(_lollipop, _ribbon);

            if (_ribbonMesh != null && _experimentId == "FINAL_J25")
                _meshAllocMax = System.Math.Max(_meshAllocMax, _ribbonMesh.LastAllocBytes);

            if (_isVerifyRun && _experimentId == "FINAL_J25" && _physicsRecorderValid && _physicsRecorder.Valid)
            {
                _physicsStepNsSum += _physicsRecorder.LastValue;
                _physicsStepSamples++;
            }

            string phaseName = _phase switch
            {
                SpikeGapPhase.Contact => "contact",
                SpikeGapPhase.Flight => "flight",
                SpikeGapPhase.Stopped => "stopped",
                _ => "unknown"
            };

            _hingeSlackMax = Mathf.Max(_hingeSlackMax, _ribbon.MaxHingeSlack());

            float speed = 0f;
            float rSample = float.NaN;
            if (!_currentThrowInvalid)
            {
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

                speed = SpikeBenchMeasurements.MaxSegmentSpeed(_ribbon);
                if (speed > _maxSpeed)
                    _maxSpeed = speed;

                if (!_gapMetricsFrozen && _throw.FixedSteps - _spawnFixedStep >= _rMinExcludeFixedSteps)
                {
                    float r = SpikeBenchMeasurements.ComputeR(_ribbon);
                    rSample = r;
                    if (!float.IsNaN(r) && r < _rMin)
                    {
                        _rMin = r;
                        _rMinPhaseAtMin = phaseName;
                    }
                }
            }

            if (_trace != null && _current.Index == 0 && !_currentThrowInvalid)
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

            bool a10 = !_currentThrowInvalid && _firstGap021Step >= 0;
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
                _currentThrowInvalid ? float.NaN : _rMin,
                _heldSeconds,
                _firstGap085Step,
                _firstGap085Value,
                _firstGap021Step,
                _firstGap021Value,
                _firstGap021Pair,
                _spawnClearanceStep0,
                _currentThrowInvalid,
                _spawnAngle,
                _spawnFixedStepsAtClearance,
                _hingeSlackMax,
                _currentThrowInvalid ? "" : _rMinPhaseAtMin,
                _throw != null ? _throw.FixedSteps - _spawnFixedStep : -1,
                float.NaN,
                float.NaN,
                _hadStoppedPhase ? _gapStoppedMax : float.NaN,
                _naN ? 1 : 0);

            if (_tunnelTracker != null && !_isVerifyRun)
                _tunnelTracker.EndThrow(_current.Index, _throwTunnelViolations);

            if (_naN && !_currentThrowInvalid)
                _nanThrowCount++;

            if (!_currentThrowInvalid && _throw != null)
            {
                _seriesMaxTapOvershoot = Mathf.Max(_seriesMaxTapOvershoot, _throw.DebugMaxTapOvershootDeg);
                _seriesMaxHoldOvershoot = Mathf.Max(_seriesMaxHoldOvershoot, _throw.DebugMaxHoldOvershootDeg);
                _seriesMaxReturnSec = Mathf.Max(_seriesMaxReturnSec, _throw.DebugReturnToRestSec);
            }

            var recorded = new ThrowResult(_current.Index, _maxGap, a10, phaseName);
            if (_isVerifyRun)
                _verifyResults.Add(recorded);
            else
                _mainResults.Add(recorded);

            if (_experimentId == "J1" && _planIndex < _j1Reps.Count)
            {
                int li = _planIndex;
                _j1Report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "throw={0} rep={1} time_scale={2:F0} first021_step={3} first021_val={4:F4} max_gap={5:F4} a10={6}",
                    _current.Index, _j1Reps[li], _j1TimeScales[li],
                    _firstGap021Step, _firstGap021Step < 0 ? 0f : _firstGap021Value,
                    _maxGap, a10 ? 1 : 0));
            }

            if (_currentThrowInvalid)
            {
                _spawnInvalidCount++;
            }
            else
            {
                if (a10)
                    _totalBreaks++;
                if (_maxGap > SpikeBenchMethodology.GapHardVisual && _maxGap <= SpikeBenchMethodology.GapBreakSoft)
                    _bin085to21++;
                int bin = SpikeBenchMeasurements.HistogramBin(_maxGap);
                if (bin >= 0 && bin < _histogram.Length)
                    _histogram[bin]++;
                int[] phaseHist = _gapPhaseAtMax switch
                {
                    SpikeGapPhase.Contact => _histContact,
                    SpikeGapPhase.Flight => _histFlight,
                    SpikeGapPhase.Stopped => _histStopped,
                    _ => null
                };
                if (phaseHist != null && bin >= 0 && bin < phaseHist.Length)
                    phaseHist[bin]++;
                if (_maxGap > _seriesMaxGap)
                    _seriesMaxGap = _maxGap;
                if (!float.IsNaN(_rMin))
                    _rMinValid.Add(_rMin);
                if (_firstGap021Step >= 0)
                {
                    _firstGap021Events.Add(string.Format(CultureInfo.InvariantCulture,
                        "first_gt_021 throw={0} phase={1} step={2} pair={3} value={4:F4}",
                        _current.Index, _firstGap021Phase, _firstGap021Step, _firstGap021Pair, _firstGap021Value));
                }
            }

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
            if (_experimentId == "J1")
                WriteJ1ReproFile();
            _state = SessionState.Done;
            Debug.Log("SpikeBench: series complete. Logs in " + SpikeBenchMethodology.LogDirectory());

            if (!_isVerifyRun && _planIndex > 0 && _experimentId == "E0")
            {
                float invalidPct = 100f * _spawnInvalidCount / _planIndex;
                if (_spawnInvalidCount * 100f > _planIndex * 2f)
                {
                    SpikeBenchJointDump.WriteSpawnInvalidDump(_ribbon, _lollipop, _spawnInvalidCount, invalidPct);
                    Debug.LogError(string.Format(CultureInfo.InvariantCulture,
                        "SpikeBench: E0 aborted — spawn_invalid {0} ({1:F2}%) > 2%. Dump: {2}",
                        _spawnInvalidCount, invalidPct, SpikeBenchJointDump.FullDumpPath()));
                    TearDown();
                    EditorApplication.ExitPlaymode();
                    return;
                }
            }

            if (_experimentId == "FINAL_J25" && !_isVerifyRun && _tunnelTracker != null)
                _tunnelTracker.Flush(_experimentId, _seed, _runId);

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
                Array.Clear(_histContact, 0, _histContact.Length);
                Array.Clear(_histFlight, 0, _histFlight.Length);
                Array.Clear(_histStopped, 0, _histStopped.Length);
                _seriesMaxGap = 0f;
                _spawnInvalidCount = 0;
                _rMinValid.Clear();
                _firstGap021Events.Clear();
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

            if (_experimentId == "FINAL_J25" && _isVerifyRun)
                SpikeBenchD24Performance.Flush(_physicsStepNsSum, _physicsStepSamples, _meshAllocMax);

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
            sb.AppendLine("spawn_invalid_count=" + _spawnInvalidCount);
            float invalidPct = throwsDone > 0 ? 100f * _spawnInvalidCount / throwsDone : 0f;
            sb.AppendLine("spawn_invalid_pct=" + invalidPct.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("valid_throws=" + (throwsDone - _spawnInvalidCount));
            sb.AppendLine("nan_throws_valid=" + _nanThrowCount);
            sb.AppendLine("a10_breaks_gt_0.21_valid=" + _totalBreaks);
            sb.AppendLine("bin_0.085_0.21_valid=" + _bin085to21);
            sb.AppendLine("max_gap_valid=" + _seriesMaxGap.ToString(CultureInfo.InvariantCulture));
            AppendHistogram(sb, "histogram_valid", _histogram);
            AppendHistogram(sb, "histogram_contact", _histContact);
            AppendHistogram(sb, "histogram_flight", _histFlight);
            AppendHistogram(sb, "histogram_stopped", _histStopped);
            float rMinMin = float.NaN;
            float rMinMedian = float.NaN;
            if (_rMinValid.Count > 0)
            {
                rMinMin = _rMinValid[0];
                foreach (float v in _rMinValid)
                    if (v < rMinMin)
                        rMinMin = v;
                rMinMedian = MedianRMin(_rMinValid);
            }

            sb.AppendLine("r_min_throw_min_valid=" + (float.IsNaN(rMinMin) ? "" : rMinMin.ToString(CultureInfo.InvariantCulture)));
            sb.AppendLine("r_min_throw_median_valid=" + (float.IsNaN(rMinMedian) ? "" : rMinMedian.ToString(CultureInfo.InvariantCulture)));
            foreach (string line in _firstGap021Events)
                sb.AppendLine(line);

            int validThrows = throwsDone - _spawnInvalidCount;
            int softLimit = validThrows > 0 ? Mathf.FloorToInt(validThrows * 0.05f) : 0;
            sb.AppendLine("a10_soft_bin_limit_valid=" + softLimit);
            int break2Pct = validThrows > 0 ? Mathf.FloorToInt(validThrows * 0.02f) : 0;
            sb.AppendLine("a10_break_limit_2pct_valid=" + break2Pct);

            if (_experimentId.StartsWith("E1", StringComparison.Ordinal))
            {
                sb.AppendLine("e1_max_tap_overshoot_deg=" + _seriesMaxTapOvershoot.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("e1_max_hold_overshoot_deg=" + _seriesMaxHoldOvershoot.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("e1_max_return_to_rest_sec=" + _seriesMaxReturnSec.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("e1_drive_mode=AngularVelocity");
            }

            if (_isVerifyRun && _mainResults.Count > 0)
                AppendVerifyComparison(sb);

            if (!string.IsNullOrEmpty(_worstVerifyMainCsv) && _experimentId.Contains("verify1x_worst"))
                AppendWorstVerifyComparison(sb);

            File.WriteAllText(summaryPath, sb.ToString());
        }

        static void AppendWorstVerifyComparison(StringBuilder sb)
        {
            var main = SpikeBenchE0Analysis.LoadThrowSummaries(_worstVerifyMainCsv);
            int compared = 0;
            int a10Match = 0;
            for (int i = 0; i < _mainResults.Count; i++)
            {
                ThrowResult verify = _mainResults[i];
                if (!main.TryGetValue(verify.Index, out var row))
                    continue;
                compared++;
                bool mainA10 = row.A10;
                if (mainA10 == verify.A10Break)
                    a10Match++;
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "worst_verify_throw index={0} main_gap={1:F4} verify_gap={2:F4} main_a10={3} verify_a10={4}",
                    verify.Index, row.MaxGap, verify.MaxGap, mainA10 ? 1 : 0, verify.A10Break ? 1 : 0));
            }

            sb.AppendLine("worst_verify_compared=" + compared);
            sb.AppendLine("worst_verify_a10_matches=" + a10Match);
        }

        static void WriteJ1ReproFile()
        {
            string path = Path.Combine(SpikeBenchMethodology.LogDirectory(), "j1_repro.txt");
            var sb = new StringBuilder();
            sb.AppendLine("J1 reproducibility batch (one play session)");
            sb.Append(_j1Report);
            File.WriteAllText(path, sb.ToString());
        }

        static void AppendHistogram(StringBuilder sb, string prefix, int[] hist)
        {
            sb.AppendLine(prefix + "_0_0.085=" + hist[0]);
            sb.AppendLine(prefix + "_1_0.085_0.21=" + hist[1]);
            sb.AppendLine(prefix + "_2_0.21_0.5=" + hist[2]);
            sb.AppendLine(prefix + "_3_0.5_1=" + hist[3]);
            sb.AppendLine(prefix + "_4_gt_1=" + hist[4]);
        }

        static float MedianRMin(List<float> values)
        {
            if (values.Count == 0)
                return float.NaN;
            float[] sorted = values.ToArray();
            Array.Sort(sorted);
            int mid = sorted.Length / 2;
            return sorted.Length % 2 == 0
                ? (sorted[mid - 1] + sorted[mid]) * 0.5f
                : sorted[mid];
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
                _throw.DebugSetConfigOverride(null);
            }

            if (_ribbon != null)
                _ribbon.DebugSetConfigOverride(null);
            if (_lollipop != null)
            {
                _lollipop.DebugSetHeadColliderEnabled(true);
                _lollipop.DebugSetDriveMode(LollipopDriveMode.MoveRotation);
            }

            _beginPlayHook = null;
            _headOffAfterSpawnClearance = false;
            _tunnelTracker = null;
            if (_physicsRecorder.Valid)
                _physicsRecorder.Dispose();
            _ribbonMesh = null;
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