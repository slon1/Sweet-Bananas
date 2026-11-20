using System.Globalization;
using System.IO;
using System.Text;
using SweetBananas.Game;
using SweetBananas.Game.Debug;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchFailLineProbe
    {
        static readonly (int w, int h)[] Resolutions = { (1080, 1920), (1080, 2160), (1080, 2340) };

        enum Phase
        {
            Idle,
            WaitPlay,
            TestY,
            WaitSteps,
            Done
        }

        static Phase _phase;
        static Ribbon _ribbon;
        static SpikeOverlay _overlay;
        static Lollipop _lollipop;
        static int _resolutionIndex;
        static int _yTestIndex;
        static float[] _yTests = { -0.9f, -1.1f };
        static int _stepBaseline;
        static float _placedCenterY;
        static StringBuilder _log;

        [MenuItem("Sweet Bananas/SpikeBench/Run D2 Fail Line Probe")]
        public static void Run()
        {
            if (_phase != Phase.Idle && _phase != Phase.Done)
                return;

            SpikeBenchMethodology.EnsureLogDirectory();
            _log = new StringBuilder();
            _phase = Phase.WaitPlay;
            _resolutionIndex = 0;
            _yTestIndex = 0;
            WriteResolutionLines();

            EditorApplication.update += Tick;
            var startScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Scene.unity");
            if (startScene != null)
                EditorSceneManager.playModeStartScene = startScene;
            if (!Application.isPlaying)
                EditorApplication.EnterPlaymode();
        }

        static void WriteResolutionLines()
        {
            var cam = Object.FindAnyObjectByType<Camera>();
            if (cam == null)
                return;

            for (int i = 0; i < Resolutions.Length; i++)
            {
                (int w, int h) = Resolutions[i];
                Vector3 w0 = cam.ViewportToWorldPoint(new Vector3(0f, 0f, 0f));
                float bottom = cam.transform.position.y - cam.orthographicSize;
                _log.AppendLine("target_game_view=" + w + "x" + h + " actual_screen=" + Screen.width + "x" + Screen.height
                    + " viewport00_y=" + w0.y.ToString(CultureInfo.InvariantCulture)
                    + " world_bottom_edge_y=" + bottom.ToString(CultureInfo.InvariantCulture));
            }

            _log.AppendLine("fail_line_y=-1 (FailMarginUnits=1)");
            _log.AppendLine("resolutions_note=orthographic bottom edge should be 0; switch Game view size manually if needed");
        }

        static void Tick()
        {
            if (_phase == Phase.Idle || _phase == Phase.Done)
            {
                EditorApplication.update -= Tick;
                return;
            }

            if (!Application.isPlaying)
            {
                Finish();
                return;
            }

            if (_phase == Phase.WaitPlay)
            {
                _ribbon = Object.FindAnyObjectByType<Ribbon>();
                _overlay = Object.FindAnyObjectByType<SpikeOverlay>();
                _lollipop = Object.FindAnyObjectByType<Lollipop>();
                var cam = Object.FindAnyObjectByType<Camera>();
                if (_ribbon == null || _overlay == null || _lollipop == null || cam == null)
                    return;

                WriteResolutionLines();
                _overlay.AutoResetEnabled = true;
                _phase = Phase.TestY;
                BeginYTest();
                return;
            }

            if (_phase == Phase.WaitSteps)
            {
                var throwCtl = Object.FindAnyObjectByType<ThrowController>();
                if (throwCtl == null)
                    return;
                if (throwCtl.FixedSteps - _stepBaseline < 2)
                    return;

                float cy = _ribbon.Center.y;
                bool resetTriggered = cy - _placedCenterY > 3f;
                bool expectReset = _placedCenterY < -1f;
                bool pass = resetTriggered == expectReset;
                _log.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "autoreset_test placed_y={0:F3} center_y_after_2_steps={1:F3} expect_reset={2} got_reset={3} pass={4}",
                    _placedCenterY, cy, expectReset ? 1 : 0, resetTriggered ? 1 : 0, pass ? 1 : 0));

                _yTestIndex++;
                if (_yTestIndex < _yTests.Length)
                {
                    _phase = Phase.TestY;
                    BeginYTest();
                }
                else
                    Finish();
            }
        }

        static void BeginYTest()
        {
            float targetY = _yTests[_yTestIndex];
            float angle = _lollipop.Angle;
            _ribbon.DebugPlace(new Vector2(_ribbon.Center.x, targetY), angle);
            _placedCenterY = targetY;
            var throwCtl = Object.FindAnyObjectByType<ThrowController>();
            if (throwCtl != null)
                _stepBaseline = throwCtl.FixedSteps;
            _phase = Phase.WaitSteps;
        }

        static void Finish()
        {
            string path = Path.Combine(SpikeBenchMethodology.LogDirectory(), "d2_fail_line.txt");
            File.WriteAllText(path, _log.ToString());
            EditorApplication.ExitPlaymode();
            _phase = Phase.Done;
            _ribbon = null;
            _overlay = null;
            EditorApplication.update -= Tick;
            _phase = Phase.Idle;
        }
    }
}