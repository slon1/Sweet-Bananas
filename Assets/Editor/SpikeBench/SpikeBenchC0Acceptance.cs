using System.Globalization;
using System.IO;
using System.Text;
using SweetBananas.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchC0Acceptance
    {
        const float RestWaitSec = 1f;
        const float FindSceneTimeoutSec = 60f;

        static bool _running;
        static bool _awaitingRest;
        static int _targetFixedSteps;
        static int _baselineFixedSteps;
        static ThrowController _throw;
        static Ribbon _ribbon;
        static RibbonMesh _mesh;
        static float _findStart;

        [MenuItem("Sweet Bananas/SpikeBench/Run C0 Acceptance")]
        public static void Run()
        {
            if (_running)
                return;

            SpikeBenchMethodology.EnsureLogDirectory();
            _running = true;
            _awaitingRest = false;
            _findStart = (float)EditorApplication.timeSinceStartup;
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

            if (EditorApplication.timeSinceStartup - _findStart > FindSceneTimeoutSec && _throw == null)
            {
                Debug.LogError("SpikeBench C0: scene timeout.");
                StopRun();
                return;
            }

            if (!Application.isPlaying)
            {
                StopRun();
                return;
            }

            if (!_awaitingRest)
            {
                if (!TryFind())
                    return;
                StartRestProbe();
                return;
            }

            if (_throw.FixedSteps - _baselineFixedSteps >= _targetFixedSteps)
                FinishAndCapture();
        }

        static void StartRestProbe()
        {
            Time.timeScale = 1f;
            _ribbon.ResetToSpawn();
            float hingeSlack = _ribbon.ValidateHingeAnchors();
            float gapMax = SpikeBenchMeasurements.MaxAnchorGap(_ribbon);

            string logDir = SpikeBenchMethodology.LogDirectory();
            var sb = new StringBuilder();
            sb.AppendLine("hinge_slack_after_reset=" + hingeSlack.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("max_pair_gap_after_reset=" + gapMax.ToString(CultureInfo.InvariantCulture));
            File.WriteAllText(Path.Combine(logDir, "c0_after_reset.txt"), sb.ToString());

            _baselineFixedSteps = _throw.FixedSteps;
            int fixedStepsPerSec = Mathf.Max(1, Mathf.RoundToInt(1f / Time.fixedDeltaTime));
            _targetFixedSteps = Mathf.RoundToInt(RestWaitSec * fixedStepsPerSec);
            _awaitingRest = true;
        }

        static void FinishAndCapture()
        {
            float hingeSlack = _ribbon.ValidateHingeAnchors();
            float rRest = SpikeBenchMeasurements.ComputeR(_ribbon);
            float meshOffset = MeasureMeshVsCapsules(_ribbon, _mesh);
            string logDir = SpikeBenchMethodology.LogDirectory();
            string stamp = System.DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

            var sb = new StringBuilder();
            sb.AppendLine("r_rest_at_1.0s=" + rRest.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("hinge_slack_at_1.0s=" + hingeSlack.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("mesh_capsule_center_offset=" + meshOffset.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("mesh_capsule_ok=" + (meshOffset <= 0.05f ? "1" : "0"));
            File.WriteAllText(Path.Combine(logDir, "c0_results.txt"), sb.ToString());

            EditorApplication.isPaused = true;
            Debug.Log("SpikeBench C0: paused — enable Scene Gizmos, then menu C0 Finish (screenshots + exit play).");
        }

        static void CaptureSceneView(string path)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null)
            {
                Debug.LogWarning("SpikeBench C0: no Scene view to capture.");
                return;
            }

            view.drawGizmos = true;
            view.Repaint();
            int w = Mathf.Max(400, (int)view.position.width);
            int h = Mathf.Max(300, (int)view.position.height);
            var rt = new RenderTexture(w, h, 24);
            var prevTarget = view.camera.targetTexture;
            view.camera.targetTexture = rt;
            view.camera.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            view.camera.targetTexture = prevTarget;
            RenderTexture.active = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
        }

        static float MeasureMeshVsCapsules(Ribbon ribbon, RibbonMesh mesh)
        {
            if (ribbon == null || mesh == null)
                return float.NaN;

            MeshFilter filter = mesh.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                return float.NaN;

            Bounds meshBounds = filter.sharedMesh.bounds;
            Vector3 meshWorld = mesh.transform.TransformPoint(meshBounds.center);
            return Vector2.Distance(meshWorld, ribbon.Center);
        }

        static bool TryFind()
        {
            _throw = Object.FindAnyObjectByType<ThrowController>();
            _ribbon = Object.FindAnyObjectByType<Ribbon>();
            _mesh = Object.FindAnyObjectByType<RibbonMesh>();
            return _throw != null && _ribbon != null;
        }

        [MenuItem("Sweet Bananas/SpikeBench/C0 Finish (screenshots + exit play)")]
        public static void FinishPausedC0()
        {
            string logDir = SpikeBenchMethodology.LogDirectory();
            string stamp = System.DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            ScreenCapture.CaptureScreenshot(Path.Combine(logDir, $"c0_game_view_{stamp}.png"));
            CaptureSceneView(Path.Combine(logDir, $"c0_scene_view_{stamp}.png"));
            EditorApplication.isPaused = false;
            EditorApplication.ExitPlaymode();
            StopRun();
        }

        static void StopRun()
        {
            _running = false;
            _awaitingRest = false;
            _throw = null;
            _ribbon = null;
            _mesh = null;
            EditorApplication.update -= Tick;
        }
    }
}