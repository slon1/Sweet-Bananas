using System.Globalization;
using System.IO;
using SweetBananas.Data;
using SweetBananas.Game;
using SweetBananas.Game.Debug;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchA1Probe
    {
        enum Phase
        {
            Idle,
            WaitScene,
            RunTap,
            Done
        }

        static Phase _phase = Phase.Idle;
        static ThrowController _throw;
        static Lollipop _lollipop;
        static SpikeOverlay _overlay;
        static GameConfig _assetConfig;
        static GameConfig _benchConfig;
        static float _maxOmega;
        static float _findStart;
        static int _holdSteps;
        static bool _playBegun;

        public static bool IsRunning => _phase != Phase.Idle;

        public static void Stop() => Shutdown();

        [MenuItem("Sweet Bananas/SpikeBench/A1 — angular velocity at 600 deg/s command")]
        public static void RunFromMenu()
        {
            if (IsRunning || SpikeBenchSession.IsRunning || SpikeBenchH1Runner.IsRunning)
                return;
            Start();
        }

        public static void Start()
        {
            _phase = Phase.WaitScene;
            _maxOmega = 0f;
            _holdSteps = 0;
            _playBegun = false;
            EditorApplication.update += Tick;
            if (!Application.isPlaying)
            {
                var startScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Scene.unity");
                if (startScene != null)
                    EditorSceneManager.playModeStartScene = startScene;
                EditorApplication.EnterPlaymode();
                _findStart = (float)EditorApplication.timeSinceStartup;
                return;
            }

            _findStart = (float)EditorApplication.timeSinceStartup;
        }

        static void Begin()
        {
            _throw = Object.FindAnyObjectByType<ThrowController>();
            _lollipop = Object.FindAnyObjectByType<Lollipop>();
            _overlay = Object.FindAnyObjectByType<SpikeOverlay>();
            _assetConfig = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Data/GameConfig.asset");
            if (_throw == null || _lollipop == null || _assetConfig == null)
            {
                Debug.LogError("SpikeBench A1: scene missing.");
                Shutdown();
                return;
            }

            _benchConfig = Object.Instantiate(_assetConfig);
            _benchConfig.TapSpeedDegPerSec = 600f;
            _throw.DebugSetConfigOverride(_benchConfig);
            _lollipop.DebugSetDriveMode(LollipopDriveMode.AngularVelocity);
            if (_overlay != null)
                _overlay.AutoResetEnabled = false;
            _throw.DebugSetDrive(true);
            _throw.DebugFixedStep += OnStep;
            _throw.SnapRest();
            _throw.DebugResetFixedSteps();
            _throw.DebugSetHeld(true);
            _phase = Phase.RunTap;
            Time.timeScale = 1f;
        }

        static void OnStep()
        {
            _holdSteps++;
            if (_holdSteps == 5)
                _throw.DebugSetHeld(false);

            float omega = Mathf.Abs(_lollipop.MeasuredAngularVelocityDegPerSec);
            if (omega > _maxOmega)
                _maxOmega = omega;
            if (_throw.State == ThrowState.Return || _throw.State == ThrowState.Rest)
            {
                if (_throw.State == ThrowState.Rest)
                    Finish();
            }
        }

        static void Finish()
        {
            string dir = SpikeBenchMethodology.LogDirectory();
            string path = Path.Combine(dir, "a1_probe.txt");
            var lines = string.Join("\n",
                "tap_speed_command_deg_s=600",
                "config_source=Instantiate(GameConfig.asset)",
                "drive_mode=AngularVelocity",
                "max_measured_angular_velocity_deg_s=" + _maxOmega.ToString(CultureInfo.InvariantCulture),
                "pass_A1_ge_570=" + (_maxOmega >= 570f ? "1" : "0"));
            File.WriteAllText(path, lines + "\n");
            Debug.Log("SpikeBench A1: " + path + " omega_max=" + _maxOmega);
            Shutdown();
            EditorApplication.ExitPlaymode();
        }

        static void Shutdown()
        {
            EditorApplication.update -= Tick;
            if (_throw != null)
            {
                _throw.DebugFixedStep -= OnStep;
                _throw.DebugSetDrive(false);
                _throw.DebugSetHeld(false);
                _throw.DebugSetConfigOverride(null);
            }

            if (_benchConfig != null)
            {
                Object.Destroy(_benchConfig);
                _benchConfig = null;
            }

            if (_overlay != null)
                _overlay.AutoResetEnabled = true;
            _phase = Phase.Idle;
        }

        static void Tick()
        {
            if (_phase == Phase.Idle)
            {
                EditorApplication.update -= Tick;
                return;
            }

            if (_phase == Phase.WaitScene)
            {
                if (EditorApplication.timeSinceStartup - _findStart > 60f)
                {
                    Debug.LogError("SpikeBench A1: timeout.");
                    Shutdown();
                    return;
                }

                if (!Application.isPlaying || _playBegun)
                    return;
                _throw = Object.FindAnyObjectByType<ThrowController>();
                if (_throw != null)
                {
                    _playBegun = true;
                    Begin();
                }

                return;
            }

            if (!Application.isPlaying)
                Shutdown();
        }
    }
}