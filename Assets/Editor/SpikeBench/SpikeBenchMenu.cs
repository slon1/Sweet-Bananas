using UnityEditor;
using UnityEngine;

namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchMenu
    {
        [MenuItem("Sweet Bananas/SpikeBench/Step 0 — Write Methodology")]
        public static void Step0WriteMethodology()
        {
            SpikeBenchMethodology.WriteMethodologyArtifact(
                SpikeBenchMethodology.DefaultSeed,
                "step0",
                SpikeBenchMethodology.SeriesTaps,
                SpikeBenchMethodology.SeriesHolds);
            Debug.Log("SpikeBench step 0: methodology written to " + SpikeBenchMethodology.LogDirectory());
        }

        [MenuItem("Sweet Bananas/SpikeBench/Run E0 (100+20 @ timeScale 12)")]
        public static void RunE0()
        {
            if (SpikeBenchSession.IsRunning)
            {
                Debug.LogWarning("SpikeBench: stop the current session first.");
                return;
            }

            SpikeBenchSession.Start(
                "E0",
                SpikeBenchMethodology.DefaultSeed,
                SpikeBenchMethodology.BenchTimeScale,
                SpikeBenchMethodology.SeriesTaps,
                SpikeBenchMethodology.SeriesHolds,
                verifyRealtime: true);
        }

        [MenuItem("Sweet Bananas/SpikeBench/Run Smoke (4 throws @ 1x)")]
        public static void RunSmoke()
        {
            if (SpikeBenchSession.IsRunning)
                return;

            SpikeBenchSession.Start("smoke", SpikeBenchMethodology.DefaultSeed, 1f, 3, 1, verifyRealtime: false);
        }

        [MenuItem("Sweet Bananas/SpikeBench/Stop Bench")]
        public static void StopBench()
        {
            SpikeBenchSession.Stop();
            if (Application.isPlaying)
                EditorApplication.ExitPlaymode();
        }

        [InitializeOnLoadMethod]
        static void HookPlayMode()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode && SpikeBenchSession.IsRunning)
                    SpikeBenchSession.Stop();
            };
        }
    }
}