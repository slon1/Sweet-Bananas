using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using SweetBananas.Data;
using SweetBananas.Game;
using SweetBananas.Game.Debug;
using UnityEditor;
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
            Done
        }

        struct ThrowPlan
        {
            public int Index;
            public bool IsHold;
            public float SpawnFraction;
            public float PressDuration;
        }

        static SessionState _state = SessionState.Idle;
        static string _experimentId = "E0";
        static int _seed;
        static float _timeScale = SpikeBenchMethodology.BenchTimeScale;
        static int _tapCount = SpikeBenchMethodology.SeriesTaps;
        static int _holdCount = SpikeBenchMethodology.SeriesHolds;
        static bool _verifyRealtime;
        static string _runId;

        static readonly List<ThrowPlan> _plans = new();
        static int _planIndex;
        static ThrowPlan _current;
        static SpikeBenchCsv _csv;

        static ThrowController _throw;
        static Ribbon _ribbon;
        static Lollipop _lollipop;
        static SpikeOverlay _overlay;
        static GameConfig _config;

        static int _lastFixedSteps;
        static float _pressElapsed;
        static float _freeElapsed;
        static float _stopElapsed;
        static float _spawnTime;
        static float _maxGap;
        static SpikeGapPhase _gapPhaseAtMax = SpikeGapPhase.Unknown;
        static SpikeGapPhase _phase = SpikeGapPhase.Unknown;
        static float _maxSpeed;
        static float _rMin = float.PositiveInfinity;
        static bool _naN;
        static float _heldSeconds;
        static float _findSceneStart;

        static int _totalBreaks;
        static int _bin085to21;
        static readonly int[] _histogram = new int[5];
        static float _seriesMaxGap;

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
            _runId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

            SpikeBenchMethodology.WriteMethodologyArtifact(seed, experimentId);
            BuildPlans(seed, taps, holds);

            EditorApplication.update += Tick;

            if (!Application.isPlaying)
            {
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

        static void BuildPlans(int seed, int taps, int holds)
        {
            _plans.Clear();
            var rng = new System.Random(seed);
            int index = 0;
            for (int i = 0; i < taps; i++)
            {
                _plans.Add(new ThrowPlan
                {
                    Index = index++,
                    IsHold = false,
                    SpawnFraction = Pick(rng, SpikeBenchMethodology.SpawnFractions),
                    PressDuration = Pick(rng, SpikeBenchMethodology.TapPressDurationsSec)
                });
            }

            for (int i = 0; i < holds; i++)
            {
                _plans.Add(new ThrowPlan
                {
                    Index = index++,
                    IsHold = true,
                    SpawnFraction = Pick(rng, SpikeBenchMethodology.SpawnFractions),
                    PressDuration = Pick(rng, SpikeBenchMethodology.HoldPressDurationsSec)
                });
            }
        }

        static float Pick(System.Random rng, float[] values)
        {
            return values[rng.Next(values.Length)];
        }

        static void BeginPlay()
        {
            Time.timeScale = _timeScale;
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Data/GameConfig.asset");
            if (!TryFindActors())
            {
                Debug.LogError("SpikeBench: scene objects not found.");
                Stop();
                return;
            }

            _overlay.AutoResetEnabled = false;
            _throw.DebugSetDrive(true);
            _csv = new SpikeBenchCsv(_experimentId, _seed, _runId);
            _planIndex = 0;
            _totalBreaks = 0;
            _bin085to21 = 0;
            Array.Clear(_histogram, 0, _histogram.Length);
            _seriesMaxGap = 0f;
            _state = SessionState.ThrowPress;
            StartThrow(_plans[_planIndex]);
            Debug.Log("SpikeBench: started " + _experimentId + " seed=" + _seed + " timeScale=" + _timeScale);
        }

        static bool TryFindActors()
        {
            _throw = UnityEngine.Object.FindAnyObjectByType<ThrowController>();
            _ribbon = UnityEngine.Object.FindAnyObjectByType<Ribbon>();
            _lollipop = UnityEngine.Object.FindAnyObjectByType<Lollipop>();
            _overlay = UnityEngine.Object.FindAnyObjectByType<SpikeOverlay>();
            return _throw != null && _ribbon != null && _lollipop != null && _config != null;
        }

        static void StartThrow(ThrowPlan plan)
        {
            _current = plan;
            _ribbon.ResetToSpawn(plan.SpawnFraction);
            _throw.DebugResetFixedSteps();
            _lastFixedSteps = _throw.FixedSteps;
            _pressElapsed = 0f;
            _freeElapsed = 0f;
            _stopElapsed = 0f;
            _spawnTime = Time.time;
            _maxGap = 0f;
            _gapPhaseAtMax = SpikeGapPhase.Unknown;
            _phase = SpikeGapPhase.Contact;
            _maxSpeed = 0f;
            _rMin = float.PositiveInfinity;
            _naN = false;
            _heldSeconds = 0f;
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
                {
                    BeginPlay();
                }

                return;
            }

            if (!Application.isPlaying)
            {
                Stop();
                return;
            }

            if (_throw == null || _ribbon == null)
                return;

            while (_throw.FixedSteps > _lastFixedSteps)
            {
                _lastFixedSteps++;
                SampleFixedStep();
            }
        }

        static void SampleFixedStep()
        {
            UpdateNaNFlag();

            float gap = SpikeBenchMeasurements.MaxAnchorGap(_ribbon);
            if (gap > _maxGap)
            {
                _maxGap = gap;
                _gapPhaseAtMax = _phase;
            }

            float speed = SpikeBenchMeasurements.MaxSegmentSpeed(_ribbon);
            if (speed > _maxSpeed)
                _maxSpeed = speed;

            if (Time.time - _spawnTime >= SpikeBenchMethodology.RMinExcludeAfterSpawnSec)
            {
                float r = SpikeBenchMeasurements.ComputeR(_ribbon);
                if (!float.IsNaN(r) && r < _rMin)
                    _rMin = r;
            }

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

            if (_state == SessionState.ThrowPress)
            {
                _pressElapsed += Time.fixedDeltaTime;
                _heldSeconds = _pressElapsed;
                float limit = _current.PressDuration;
                if (_current.IsHold)
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
                _freeElapsed += Time.fixedDeltaTime;
                float timeout = _current.IsHold
                    ? SpikeBenchMethodology.HoldThrowTimeoutSec
                    : SpikeBenchMethodology.TapThrowTimeoutSec;

                bool settled = _stopElapsed >= SpikeBenchMethodology.StopHoldSec;
                bool timedOut = _freeElapsed >= timeout;
                bool failed = SpikeBenchMeasurements.FailBounds(_ribbon, _lollipop, _config);
                if (settled || timedOut || failed)
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

            bool a10 = SpikeBenchMeasurements.IsA10Break(_maxGap);
            string phaseName = _gapPhaseAtMax switch
            {
                SpikeGapPhase.Contact => "contact",
                SpikeGapPhase.Flight => "flight",
                SpikeGapPhase.Stopped => "stopped",
                _ => "unknown"
            };

            _csv.WriteThrowRow(
                _experimentId,
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
                _heldSeconds);

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

            if (_verifyRealtime && _timeScale > 1.01f)
            {
                _timeScale = 1f;
                Time.timeScale = 1f;
                int verify = Mathf.Min(SpikeBenchMethodology.VerifyAtRealtimeCount, _plans.Count);
                _csv?.Dispose();
                _runId += "_verify1x";
                _totalBreaks = 0;
                _bin085to21 = 0;
                Array.Clear(_histogram, 0, _histogram.Length);
                _seriesMaxGap = 0f;
                _csv = new SpikeBenchCsv(_experimentId + "_verify1x", _seed, _runId);
                var subset = _plans.GetRange(0, verify);
                _plans.Clear();
                _plans.AddRange(subset);
                _planIndex = 0;
                StartThrow(_plans[0]);
                _state = SessionState.ThrowPress;
                _verifyRealtime = false;
                return;
            }

            TearDown();
            EditorApplication.ExitPlaymode();
        }

        static void WriteSummary()
        {
            int throwsDone = _planIndex;
            string dir = SpikeBenchMethodology.LogDirectory();
            string summaryPath = Path.Combine(dir, $"summary_{_experimentId}_{_seed}_{_runId}.txt");
            var sb = new StringBuilder();
            sb.AppendLine("experiment_id=" + _experimentId);
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
            File.WriteAllText(summaryPath, sb.ToString());
        }

        static void TearDown()
        {
            EditorApplication.update -= Tick;
            if (_throw != null)
            {
                _throw.DebugSetHeld(false);
                _throw.DebugSetDrive(false);
            }

            if (_overlay != null)
                _overlay.AutoResetEnabled = true;

            _csv?.Dispose();
            _csv = null;
            Time.timeScale = 1f;
            _state = SessionState.Idle;
        }
    }
}