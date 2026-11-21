using System.Globalization;
using System.IO;
using System.Text;
using SweetBananas.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchTargetMetrics
    {
        [MenuItem("Sweet Bananas/SpikeBench/B — target tip distances (play)")]
        public static void RunFromMenu()
        {
            if (Application.isPlaying)
            {
                WriteNow();
                return;
            }

            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Scene.unity");
            if (scene != null)
                EditorSceneManager.playModeStartScene = scene;
            EditorApplication.EnterPlaymode();
            EditorApplication.update += WaitPlay;
        }

        static void WaitPlay()
        {
            if (!Application.isPlaying)
                return;
            if (UnityEngine.Object.FindAnyObjectByType<Lollipop>() == null)
                return;
            EditorApplication.update -= WaitPlay;
            WriteNow();
            EditorApplication.ExitPlaymode();
        }

        static void WriteNow()
        {
            var lollipop = UnityEngine.Object.FindAnyObjectByType<Lollipop>();
            var target = UnityEngine.Object.FindAnyObjectByType<TargetHolder>();
            if (lollipop == null || target?.Collider == null)
            {
                Debug.LogError("Target metrics: missing objects.");
                return;
            }

            var sb = new StringBuilder();
            if (target != null)
            {
                sb.AppendLine("target_position=" + target.transform.position);
                sb.AppendLine("paths_before=" + target.PathsBeforeSimplify);
                sb.AppendLine("points_before=" + target.PointsBeforeSimplify);
                sb.AppendLine("paths_after=" + target.PathsAfterSimplify);
                sb.AppendLine("points_after=" + target.PointsAfterSimplify);
            }

            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<SweetBananas.Data.GameConfig>("Assets/Data/GameConfig.asset");
            if (config != null)
            {
                MeasureTip(lollipop, target.Collider, config.RestAngleDeg, sb, "rest");
                MeasureTip(lollipop, target.Collider, config.TapPeakAngleDeg, sb, "tap_peak");
            }

            string path = Path.Combine(SpikeBenchMethodology.LogDirectory(), "target_metrics.txt");
            SpikeBenchMethodology.EnsureLogDirectory();
            File.WriteAllText(path, sb.ToString());
            Debug.Log("Target metrics: " + path);
        }

        static void MeasureTip(Lollipop lollipop, Collider2D target, float angleDeg, StringBuilder sb, string label)
        {
            float saved = lollipop.Angle;
            lollipop.SnapRotation(angleDeg);
            Physics2D.SyncTransforms();
            Vector2 tip = lollipop.Pivot + lollipop.StickDirection() * lollipop.StickLength;
            Vector2 closest = target.ClosestPoint(tip);
            float dist = (tip - closest).magnitude;
            sb.AppendLine(label + "_tip_distance=" + dist.ToString(CultureInfo.InvariantCulture));
            lollipop.SnapRotation(saved);
        }
    }
}