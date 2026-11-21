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
    public static class SpikeBenchRestProbe
    {
        enum Phase
        {
            Idle,
            WaitScene,
            RestSettle,
            HoldSlide,
            Done
        }

        static Phase _state = Phase.Idle;
        static int _fractionIndex;
        static int _stepCount;
        static float _tipReachTime = float.NaN;
        static float _rRestAt1s = float.NaN;
        static readonly List<float> _holdSpeedSamples = new();
        static ThrowController _throw;
        static Ribbon _ribbon;
        static Lollipop _lollipop;
        static SpikeOverlay _overlay;
        static GameConfig _config;
        static StreamWriter _csv;
        static string _runId;
        static float _friction;
        static bool _playBegun;
        static float _findStart;
        static Action<string> _onComplete;

        public static bool IsRunning => _state != Phase.Idle;

        [MenuItem("Sweet Bananas/SpikeBench/Run Rest Probe (B6)")]
        public static void RunFromMenu()
        {
            if (IsRunning || SpikeBenchSession.IsRunning)
                return;
            Start(0.4f, null);
        }

        public static void Start(float stickFriction, Action<string> onComplete)
        {
            _friction = stickFriction;
            _onComplete = onComplete;
            _runId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            _fractionIndex = 0;
            _playBegun = false;
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

        public static void Stop()
        {
            EditorApplication.update -= Tick;
            _csv?.Flush();
            _csv?.Dispose();
            _csv = null;
            if (_throw != null)
            {
                _throw.DebugFixedStep -= OnStep;
                _throw.DebugSetDrive(false);
                _throw.DebugSetHeld(false);
            }

            if (_overlay != null)
                _overlay.AutoResetEnabled = true;
            _state = Phase.Idle;
        }

        static void Begin()
        {
            Time.timeScale = 1f;
            _throw = UnityEngine.Object.FindAnyObjectByType<ThrowController>();
            _ribbon = UnityEngine.Object.FindAnyObjectByType<Ribbon>();
            _lollipop = UnityEngine.Object.FindAnyObjectByType<Lollipop>();
            _overlay = UnityEngine.Object.FindAnyObjectByType<SpikeOverlay>();
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Data/GameConfig.asset");
            if (_throw == null || _ribbon == null || _lollipop == null)
            {
                Debug.LogError("SpikeBench Rest Probe: scene missing.");
                Stop();
                EditorApplication.ExitPlaymode();
                return;
            }

            LevelRules rules = UnityEngine.Object.FindAnyObjectByType<LevelRules>();
            rules?.DebugSetEnabled(false);

            SpikeBenchMethodology.EnsureLogDirectory();
            string path = Path.Combine(SpikeBenchMethodology.LogDirectory(),
                $"rest_probe_f{_friction:F2}_{SpikeBenchMethodology.DefaultSeed}_{_runId}.csv");
            _csv = new StreamWriter(path, false, Encoding.UTF8);
            _csv.WriteLine("stick_friction,row_index,kind,spawn_fraction,r_rest_at_1s,slide_time_sec,slide_v_1s_hold,slide_v_2s_hold");
            _overlay.AutoResetEnabled = false;
            _lollipop.DebugSetStickFriction(_friction);
            _throw.DebugSetDrive(true);
            _throw.DebugFixedStep += OnStep;
            BeginFractionRest();
        }

        static void BeginFractionRest()
        {
            float frac = SpikeBenchMethodology.SpawnFractions[_fractionIndex];
            _ribbon.ResetToSpawn(frac);
            _throw.SnapRest();
            _throw.DebugResetFixedSteps();
            _stepCount = 0;
            _tipReachTime = float.NaN;
            _rRestAt1s = float.NaN;
            _throw.DebugSetHeld(false);
            _state = Phase.RestSettle;
        }

        static void OnStep()
        {
            if (_state == Phase.RestSettle)
            {
                _stepCount++;
                float dt = Time.fixedDeltaTime;
                float along = ProjectAlongStick(_ribbon.Center);
                float stickEnd = _lollipop.StickLength;
                if (float.IsNaN(_tipReachTime) && along >= stickEnd - 0.05f)
                    _tipReachTime = _stepCount * dt;

                int steps1s = Mathf.CeilToInt(1f / dt);
                int steps8s = Mathf.CeilToInt(8f / dt);
                if (_stepCount == steps1s)
                    _rRestAt1s = SpikeBenchMeasurements.ComputeR(_ribbon);

                bool tipDone = !float.IsNaN(_tipReachTime) || _stepCount >= steps8s;
                if (_stepCount >= steps1s && tipDone)
                {
                    if (_stepCount >= steps8s && float.IsNaN(_tipReachTime))
                        _tipReachTime = float.NaN;
                    BeginHoldSlide();
                }

                return;
            }

            if (_state == Phase.HoldSlide)
            {
                _stepCount++;
                _holdSpeedSamples.Add(StickProjectedSpeed());
                int steps2s = Mathf.CeilToInt(2f / Time.fixedDeltaTime);
                int tail = Mathf.CeilToInt(0.1f / Time.fixedDeltaTime);
                if (_stepCount < steps2s + tail)
                    return;

                float v1 = AverageAroundHoldSecond(1f);
                float v2 = AverageAroundHoldSecond(2f);
                WriteHoldRow(v1, v2);
                _fractionIndex++;
                if (_fractionIndex >= SpikeBenchMethodology.SpawnFractions.Length)
                {
                    Complete();
                    return;
                }

                BeginFractionRest();
            }
        }

        static void BeginHoldSlide()
        {
            float frac = SpikeBenchMethodology.SpawnFractions[_fractionIndex];
            _ribbon.ResetToSpawn(frac);
            _throw.SnapRest();
            _throw.DebugResetFixedSteps();
            _stepCount = 0;
            _holdSpeedSamples.Clear();
            _throw.DebugSetHeld(true);
            _state = Phase.HoldSlide;
        }

        static void WriteHoldRow(float v1, float v2)
        {
            _csv.WriteLine(string.Join(",",
                _friction.ToString(CultureInfo.InvariantCulture),
                _fractionIndex.ToString(CultureInfo.InvariantCulture),
                "hold_0deg",
                SpikeBenchMethodology.SpawnFractions[_fractionIndex].ToString(CultureInfo.InvariantCulture),
                float.IsNaN(_rRestAt1s) ? "" : _rRestAt1s.ToString(CultureInfo.InvariantCulture),
                float.IsNaN(_tipReachTime) ? "" : _tipReachTime.ToString(CultureInfo.InvariantCulture),
                v1.ToString(CultureInfo.InvariantCulture),
                v2.ToString(CultureInfo.InvariantCulture)));
        }

        static float StickProjectedSpeed()
        {
            Vector2 dir = _lollipop.StickDirection();
            float sum = 0f;
            int n = 0;
            foreach (Rigidbody2D body in _ribbon.Bodies)
            {
                if (body == null)
                    continue;
                sum += Vector2.Dot(body.linearVelocity, dir);
                n++;
            }

            return n == 0 ? 0f : Mathf.Abs(sum / n);
        }

        static float AverageAroundHoldSecond(float seconds)
        {
            float dt = Time.fixedDeltaTime;
            int center = Mathf.RoundToInt(seconds / dt);
            int window = Mathf.Max(1, Mathf.RoundToInt(0.1f / dt));
            float sum = 0f;
            int count = 0;
            for (int i = center - window; i <= center + window; i++)
            {
                if (i < 0 || i >= _holdSpeedSamples.Count)
                    continue;
                sum += _holdSpeedSamples[i];
                count++;
            }

            return count == 0 ? float.NaN : sum / count;
        }

        static float ProjectAlongStick(Vector2 world)
        {
            Vector2 pivot = _lollipop.Pivot;
            Vector2 along = _lollipop.StickDirection();
            return Vector2.Dot(world - pivot, along);
        }

        static void Complete()
        {
            string csvPath = Path.Combine(SpikeBenchMethodology.LogDirectory(),
                $"rest_probe_f{_friction:F2}_{SpikeBenchMethodology.DefaultSeed}_{_runId}.csv");
            _csv?.Flush();
            _csv?.Dispose();
            _csv = null;
            WriteSummaryTable(csvPath);
            Debug.Log("SpikeBench Rest Probe complete: friction=" + _friction);
            var complete = _onComplete;
            _onComplete = null;
            Stop();
            complete?.Invoke(csvPath);
            if (complete == null)
                EditorApplication.ExitPlaymode();
        }

        static void WriteSummaryTable(string csvPath)
        {
            if (!File.Exists(csvPath))
                return;
            var sb = new StringBuilder();
            sb.AppendLine("friction\trow\tfraction\ttip_reach_s\tr_rest@1s\tslide_v_1s\tslide_v_2s");
            foreach (string line in File.ReadAllLines(csvPath))
            {
                if (line.StartsWith("stick", StringComparison.Ordinal))
                    continue;
                string[] p = line.Split(',');
                if (p.Length < 8)
                    continue;
                sb.AppendLine(string.Join("\t", p[0], p[1], p[3], p[5], p[4], p[6], p[7]));
            }

            string outPath = Path.Combine(SpikeBenchMethodology.LogDirectory(),
                $"rest_probe_f{_friction:F2}_table.txt");
            File.WriteAllText(outPath, sb.ToString());
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
                if (EditorApplication.timeSinceStartup - _findStart > 90f)
                {
                    Stop();
                    return;
                }

                if (!Application.isPlaying || _playBegun)
                    return;
                _throw = UnityEngine.Object.FindAnyObjectByType<ThrowController>();
                if (_throw != null)
                {
                    _playBegun = true;
                    Begin();
                }

                return;
            }

            if (!Application.isPlaying)
                Stop();
        }
    }
}