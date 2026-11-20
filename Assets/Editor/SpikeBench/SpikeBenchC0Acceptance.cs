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
        const float FindSceneTimeoutSec = 90f;

        enum C0Phase
        {
            Idle,
            WaitScene,
            GapSanity,
            MetricsAndRest,
            ThrowFlight,
            WaitStopped,
            Done
        }

        static C0Phase _phase = C0Phase.Idle;
        static ThrowController _throw;
        static Ribbon _ribbon;
        static RibbonMesh _mesh;
        static Lollipop _lollipop;
        static float _findStart;
        static int _baselineFixedSteps;
        static int _restTargetSteps;
        static int _flightBaselineSteps;
        static int _stopBaselineSteps;
        static float _stopElapsed;
        static string _stamp;

        [MenuItem("Sweet Bananas/SpikeBench/Run C0 Acceptance")]
        public static void Run()
        {
            if (_phase != C0Phase.Idle)
                return;

            SpikeBenchMathSanity.Run();
            SpikeBenchMethodology.EnsureLogDirectory();
            _phase = C0Phase.WaitScene;
            _findStart = (float)EditorApplication.timeSinceStartup;
            _stamp = System.DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            EditorApplication.update += Tick;

            var startScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Scene.unity");
            if (startScene != null)
                EditorSceneManager.playModeStartScene = startScene;

            if (!Application.isPlaying)
                EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            if (_phase == C0Phase.Idle || _phase == C0Phase.Done)
            {
                EditorApplication.update -= Tick;
                return;
            }

            if (EditorApplication.timeSinceStartup - _findStart > FindSceneTimeoutSec && _throw == null)
            {
                Debug.LogError("SpikeBench C0: scene timeout.");
                StopAll();
                return;
            }

            if (!Application.isPlaying)
            {
                StopAll();
                return;
            }

            if (_phase == C0Phase.WaitScene)
            {
                if (!TryFind())
                    return;
                _phase = C0Phase.GapSanity;
                SpikeBenchGapSanity.StartInSession(_throw, _ribbon, OnGapSanityDone);
                return;
            }

            if (_phase == C0Phase.MetricsAndRest)
            {
                if (_throw.FixedSteps - _baselineFixedSteps >= _restTargetSteps)
                    FinishRestMetrics();
                return;
            }

            if (_phase == C0Phase.ThrowFlight)
            {
                if (_throw.FixedSteps - _flightBaselineSteps >= 8)
                {
                    _throw.DebugSetHeld(false);
                    CaptureGame("c0_game_flight_" + _stamp + ".png", "flight");
                    _phase = C0Phase.WaitStopped;
                    _stopBaselineSteps = _throw.FixedSteps;
                    _stopElapsed = 0f;
                }

                return;
            }

            if (_phase == C0Phase.WaitStopped)
            {
                bool stopped = SpikeBenchMeasurements.AllStopped(_ribbon, SpikeBenchMethodology.StopSpeedThreshold);
                int holdSteps = Mathf.RoundToInt(SpikeBenchMethodology.StopHoldSec / Time.fixedDeltaTime);
                int elapsed = _throw.FixedSteps - _stopBaselineSteps;
                if (stopped)
                    _stopElapsed += Time.deltaTime;
                else
                    _stopElapsed = 0f;

                if ((stopped && elapsed >= holdSteps)
                    || elapsed > Mathf.RoundToInt(5f / Time.fixedDeltaTime))
                {
                    CaptureGame("c0_game_after_stop_" + _stamp + ".png", "after_stop");
                    EditorApplication.ExitPlaymode();
                    StopAll();
                }
            }
        }

        static void OnGapSanityDone(bool passed)
        {
            if (_ribbon.LastBuildSlack > SpikeBenchGapSanity.ResetSlackLimit)
            {
                SpikeBenchJointDump.ResetFile();
                SpikeBenchJointDump.WriteFirstTwoHinges(_ribbon, "c0_fail_build_slack");
                Debug.LogError("SpikeBench C0: LastBuildSlack over limit — stopped.");
                EditorApplication.ExitPlaymode();
                StopAll();
                return;
            }

            if (!passed)
                Debug.LogWarning("SpikeBench C0: gap_sanity_pass=0 (see gap_sanity.txt); continuing per E2 acceptance.");

            Time.timeScale = 1f;
            _throw.SnapRest();
            _ribbon.ResetToSpawn();
            _baselineFixedSteps = _throw.FixedSteps;
            int fixedStepsPerSec = Mathf.Max(1, Mathf.RoundToInt(1f / Time.fixedDeltaTime));
            _restTargetSteps = fixedStepsPerSec;
            _phase = C0Phase.MetricsAndRest;
        }

        static void FinishRestMetrics()
        {
            _ribbon = Object.FindAnyObjectByType<Ribbon>();
            _mesh = Object.FindAnyObjectByType<RibbonMesh>();
            Physics2D.SyncTransforms();
            float hingeSlack = _ribbon != null ? _ribbon.ValidateHingeAnchors() : float.NaN;
            float rRest = _ribbon != null ? SpikeBenchMeasurements.ComputeR(_ribbon) : float.NaN;
            float meshOffset = MeasureMeshVsCapsules(_ribbon, _mesh);
            string logDir = SpikeBenchMethodology.LogDirectory();

            var sb = new StringBuilder();
            sb.AppendLine("supersedes_pre_amendment5_metrics=r_rest_5.33_and_hinge_10.7_were_GetPoint_artifacts");
            sb.AppendLine("metric_source=GetRelativePoint");
            sb.AppendLine("hinge_slack_after_build=" + (_ribbon != null ? _ribbon.LastBuildSlack.ToString(CultureInfo.InvariantCulture) : "NaN"));
            sb.AppendLine("ribbon_bodies_at_1s=" + (_ribbon != null ? _ribbon.Bodies.Count.ToString() : "0"));
            sb.AppendLine("r_rest_at_1.0s=" + rRest.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("hinge_slack_at_1.0s=" + hingeSlack.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("mesh_capsule_center_offset=" + meshOffset.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("mesh_capsule_ok=" + (meshOffset <= 0.05f ? "1" : "0"));
            sb.AppendLine("screenshot_note=spawn_preA8_head_dome");
            File.WriteAllText(Path.Combine(logDir, "c0_results.txt"), sb.ToString());

            if (hingeSlack > SpikeBenchGapSanity.StepSlackLimit || _ribbon.LastBuildSlack > SpikeBenchGapSanity.ResetSlackLimit)
            {
                SpikeBenchJointDump.ResetFile();
                SpikeBenchJointDump.WriteFirstTwoHinges(_ribbon, "c0_e2_threshold_fail");
                Debug.LogError("SpikeBench C0: hinge slack threshold failed.");
                EditorApplication.ExitPlaymode();
                StopAll();
                return;
            }

            CaptureGame("c0_game_rest_preA8_" + _stamp + ".png", "rest_preA8");

            _throw.DebugSetDrive(true);
            _throw.DebugResetFixedSteps();
            _throw.DebugSetHeld(true);
            _flightBaselineSteps = _throw.FixedSteps;
            _phase = C0Phase.ThrowFlight;
        }

        static void CaptureGame(string fileName, string label)
        {
            string path = Path.Combine(SpikeBenchMethodology.LogDirectory(), fileName);
            ScreenCapture.CaptureScreenshot(path);
            File.AppendAllText(Path.Combine(SpikeBenchMethodology.LogDirectory(), "c0_screenshots.txt"),
                label + "=" + path + "\n");
            Debug.Log("SpikeBench C0 screenshot (" + label + "): enable Game view Gizmos if colliders not visible.");
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
            _lollipop = Object.FindAnyObjectByType<Lollipop>();
            return _throw != null && _ribbon != null;
        }

        static void StopAll()
        {
            _phase = C0Phase.Done;
            _throw = null;
            _ribbon = null;
            _mesh = null;
            _lollipop = null;
            EditorApplication.update -= Tick;
            _phase = C0Phase.Idle;
        }
    }
}