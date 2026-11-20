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

        [MenuItem("Sweet Bananas/SpikeBench/H1 — trace five E0 breaks")]
        public static void RunH1() => SpikeBenchH1Runner.RunFromMenu();

        [MenuItem("Sweet Bananas/SpikeBench/H2 — E0 correlation table")]
        public static void RunH2() => SpikeBenchE0Analysis.RunH2FromMenu();

        [MenuItem("Sweet Bananas/SpikeBench/H3 — verify1x worst (10 throws)")]
        public static void RunH3VerifyWorst()
        {
            if (SpikeBenchSession.IsRunning)
                return;
            string csv = SpikeBenchE0Analysis.FindLatestE0Csv();
            if (string.IsNullOrEmpty(csv))
            {
                Debug.LogError("SpikeBench H3: no E0 CSV.");
                return;
            }

            SpikeBenchSession.StartVerifyWorst(csv);
        }

        [MenuItem("Sweet Bananas/SpikeBench/A1 — angular velocity at 600 deg/s command")]
        public static void RunA1() => SpikeBenchA1Probe.RunFromMenu();

        [MenuItem("Sweet Bananas/SpikeBench/Run E1 (100+20 @ 12x, angularVelocity drive)")]
        public static void RunE1()
        {
            if (SpikeBenchSession.IsRunning)
                return;
            SpikeBenchSession.StartE1();
        }

        [MenuItem("Sweet Bananas/SpikeBench/Run Smoke (4 throws @ 1x)")]
        public static void RunSmoke()
        {
            if (SpikeBenchSession.IsRunning)
                return;

            SpikeBenchSession.Start("smoke", SpikeBenchMethodology.DefaultSeed, 1f, 2, 2, verifyRealtime: false);
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
                if (state == PlayModeStateChange.ExitingPlayMode)
                {
                    if (SpikeBenchSession.IsRunning)
                        SpikeBenchSession.Stop();
                    if (SpikeBenchH1Runner.IsRunning)
                        SpikeBenchH1Runner.Stop();
                    if (SpikeBenchA1Probe.IsRunning)
                        SpikeBenchA1Probe.Stop();
                }
            };
        }
    }
}