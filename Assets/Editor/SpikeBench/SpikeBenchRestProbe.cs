using System;
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
        static int _holdPhase;
        static float _slideStartAlong;
        static float _tipReachTime = float.NaN;
        static Vector2 _centerStart;
        static ThrowController _throw;
        static Ribbon _ribbon;
        static Lollipop _lollipop;
        static SpikeOverlay _overlay;
        static GameConfig _config;
        static StreamWriter _csv;
        static string _runId;
        static bool _playBegun;
        static float _findStart;

        public static bool IsRunning => _state != Phase.Idle;

        [MenuItem("Sweet Bananas/SpikeBench/Run Rest Probe (B6)")]
        public static void RunFromMenu()
        {
            if (IsRunning || SpikeBenchSession.IsRunning)
                return;
            Start();
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

        static void Start()
        {
            _runId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            _fractionIndex = 0;
            _playBegun = false;
            _state = Phase.WaitScene;
            EditorApplication.update += Tick;
            if (!Application.isPlaying)
            {
                var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Scene.unity");
                if (scene != null)
                    EditorSceneManager.playModeStartScene = scene;
                EditorApplication.EnterPlaymode();
                _findStart = (float)EditorApplication.timeSinceStartup;
            }
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

            SpikeBenchMethodology.EnsureLogDirectory();
            string path = Path.Combine(SpikeBenchMethodology.LogDirectory(),
                $"rest_probe_{SpikeBenchMethodology.DefaultSeed}_{_runId}.csv");
            _csv = new StreamWriter(path, false, Encoding.UTF8);
            _csv.WriteLine("experiment_id,row_index,kind,time_scale,spawn_fraction,r_rest_at_1s,slide_time_sec,center_slide_along_1s,slide_v_1s_hold,slide_v_2s_hold");
            _overlay.AutoResetEnabled = false;
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
            _centerStart = _ribbon.Center;
            _slideStartAlong = ProjectAlongStick(_centerStart);
            _throw.DebugSetHeld(false);
            _state = Phase.RestSettle;
        }

        static void OnStep()
        {
            if (_state == Phase.RestSettle)
            {
                _stepCount++;
                float along = ProjectAlongStick(_ribbon.Center);
                float stickEnd = _lollipop.StickLength;
                if (float.IsNaN(_tipReachTime) && along >= stickEnd - 0.05f)
                    _tipReachTime = _stepCount * Time.fixedDeltaTime;

                int steps1s = Mathf.CeilToInt(1f / Time.fixedDeltaTime);
                if (_stepCount < steps1s)
                    return;

                float rRest = SpikeBenchMeasurements.ComputeR(_ribbon);
                float slideAlong = along - _slideStartAlong;
                WriteRow("rest_idle", rRest, _tipReachTime, slideAlong, float.NaN, float.NaN);
                BeginHoldSlide(frac: SpikeBenchMethodology.SpawnFractions[_fractionIndex]);
                return;
            }

            if (_state == Phase.HoldSlide)
            {
                _stepCount++;
                int s1 = Mathf.CeilToInt(1f / Time.fixedDeltaTime);
                int s2 = Mathf.CeilToInt(2f / Time.fixedDeltaTime);
                if (_stepCount == s1)
                {
                    float v = (ProjectAlongStick(_ribbon.Center) - _slideStartAlong) / 1f;
                    _holdV1 = v;
                }

                if (_stepCount == s2)
                {
                    float v2 = (ProjectAlongStick(_ribbon.Center) - _holdV1Marker) / 1f;
                    WriteRow("hold_0deg", SpikeBenchMeasurements.ComputeR(_ribbon), float.NaN, 0f, _holdV1, v2);
                    _fractionIndex++;
                    if (_fractionIndex >= SpikeBenchMethodology.SpawnFractions.Length)
                    {
                        Complete();
                        return;
                    }

                    BeginFractionRest();
                }
            }
        }

        static float _holdV1;
        static float _holdV1Marker;

        static void BeginHoldSlide(float frac)
        {
            _ribbon.ResetToSpawn(frac);
            _throw.SnapRest();
            _throw.DebugResetFixedSteps();
            _stepCount = 0;
            _slideStartAlong = ProjectAlongStick(_ribbon.Center);
            _holdV1Marker = _slideStartAlong;
            _throw.DebugSetHeld(true);
            _state = Phase.HoldSlide;
        }

        static void WriteRow(string kind, float rRest, float slideTime, float centerSlide, float v1, float v2)
        {
            _csv.WriteLine(string.Join(",",
                "rest_probe",
                _fractionIndex.ToString(CultureInfo.InvariantCulture),
                kind,
                "1",
                SpikeBenchMethodology.SpawnFractions[_fractionIndex].ToString(CultureInfo.InvariantCulture),
                rRest.ToString(CultureInfo.InvariantCulture),
                float.IsNaN(slideTime) ? "" : slideTime.ToString(CultureInfo.InvariantCulture),
                centerSlide.ToString(CultureInfo.InvariantCulture),
                float.IsNaN(v1) ? "" : v1.ToString(CultureInfo.InvariantCulture),
                float.IsNaN(v2) ? "" : v2.ToString(CultureInfo.InvariantCulture)));
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
                $"rest_probe_{SpikeBenchMethodology.DefaultSeed}_{_runId}.csv");
            _csv?.Flush();
            _csv?.Dispose();
            _csv = null;
            WriteSummaryTable(csvPath);
            Debug.Log("SpikeBench Rest Probe complete.");
            Stop();
            EditorApplication.ExitPlaymode();
        }

        static void WriteSummaryTable(string csvPath)
        {
            if (!File.Exists(csvPath))
                return;
            var sb = new StringBuilder();
            sb.AppendLine("fraction\ttip_reach_s\tr_rest@1s\tslide_v_1s\tslide_v_2s");
            foreach (string line in File.ReadAllLines(csvPath))
            {
                if (line.StartsWith("experiment", StringComparison.Ordinal))
                    continue;
                string[] p = line.Split(',');
                if (p.Length < 10)
                    continue;
                sb.AppendLine(string.Join("\t", p[4], p[6], p[5], p[8], p[9]));
            }

            string outPath = Path.Combine(SpikeBenchMethodology.LogDirectory(), "rest_probe_table_J25.txt");
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