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
        const float TimeoutSec = 30f;

        static bool _running;
        static float _start;
        static Ribbon _ribbon;
        static SpikeOverlay _overlay;

        [MenuItem("Sweet Bananas/SpikeBench/Run D2 Fail Line Probe")]
        public static void Run()
        {
            if (_running)
                return;

            _running = true;
            _start = (float)EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            var startScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Scene.unity");
            if (startScene != null)
                EditorSceneManager.playModeStartScene = startScene;
            if (!Application.isPlaying)
                EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            if (!_running)
            {
                EditorApplication.update -= Tick;
                return;
            }

            if (!Application.isPlaying)
            {
                Stop();
                return;
            }

            if (_ribbon == null)
            {
                _ribbon = Object.FindAnyObjectByType<Ribbon>();
                _overlay = Object.FindAnyObjectByType<SpikeOverlay>();
                if (_ribbon == null)
                    return;
                _overlay.AutoResetEnabled = true;
                _ribbon.ResetToSpawn();
                _start = Time.time;
                return;
            }

            WriteCameraReport();
            if (_ribbon.Center.y < -1f)
            {
                FinishProbe("autoreset_center_below_fail_line");
                return;
            }

            if (Time.time - _start > TimeoutSec)
                FinishProbe("timeout_no_autoreset");
        }

        static void WriteCameraReport()
        {
            if (_cameraWritten)
                return;
            var cam = Object.FindAnyObjectByType<Camera>();
            if (cam == null)
                return;

            float size = cam.orthographicSize;
            float bottom = cam.transform.position.y - size;
            var sb = new StringBuilder();
            sb.AppendLine("screen=" + Screen.width + "x" + Screen.height);
            sb.AppendLine("orthographicSize=" + size.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("camera_pos=" + cam.transform.position);
            sb.AppendLine("world_bottom_edge_y=" + bottom.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("fail_line_y=-1");
            sb.AppendLine("resolutions_note=check Game view 1080x1920, 1080x2160, 1080x2340; bottom edge y should be 0");
            string path = Path.Combine(SpikeBenchMethodology.LogDirectory(), "d2_fail_line.txt");
            SpikeBenchMethodology.EnsureLogDirectory();
            File.WriteAllText(path, sb.ToString());
            _cameraWritten = true;
        }

        static bool _cameraWritten;

        static void FinishProbe(string reason)
        {
            var sb = new StringBuilder();
            sb.AppendLine("probe_end=" + reason);
            if (_ribbon != null)
                sb.AppendLine("center_y_at_end=" + _ribbon.Center.y.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("elapsed_sec=" + (Time.time - _start).ToString(CultureInfo.InvariantCulture));
            File.AppendAllText(Path.Combine(SpikeBenchMethodology.LogDirectory(), "d2_fail_line.txt"), sb.ToString());
            EditorApplication.ExitPlaymode();
            Stop();
        }

        static void Stop()
        {
            _running = false;
            _ribbon = null;
            _overlay = null;
            _cameraWritten = false;
            EditorApplication.update -= Tick;
        }
    }
}