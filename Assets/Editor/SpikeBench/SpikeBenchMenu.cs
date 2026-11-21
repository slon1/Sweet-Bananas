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

        [MenuItem("Sweet Bananas/SpikeBench/Run FINAL_J25 (300+60 @12× + verify)")]
        public static void RunFinal() => SpikeBenchAcceptanceOrchestrator.RunFinalOnly();

        [MenuItem("Sweet Bananas/SpikeBench/A21 — Run acceptance (FINAL → Rest → D24)")]
        public static void RunAcceptance() => SpikeBenchAcceptanceOrchestrator.RunAll();

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

        [MenuItem("Sweet Bananas/SpikeBench/Run Rest Probe (B6)")]
        public static void RunRestProbe() => SpikeBenchRestProbe.RunFromMenu();

        [MenuItem("Sweet Bananas/SpikeBench/B24.1 — D24.1 repeat (12×, rules off)")]
        public static void RunB241() => SpikeBenchSession.StartB242D24Repeat();

        [MenuItem("Sweet Bananas/SpikeBench/B — StickFriction grid")]
        public static void RunStickFrictionGrid()
        {
            SpikeBenchStickFrictionPicker.RunGrid((value, table) =>
            {
                Debug.Log("StickFriction selected: " + value);
                Debug.Log(table);
            });
        }

        [MenuItem("Sweet Bananas/SpikeBench/B — Level1 playability grid (12×)")]
        public static void RunLevel1Grid() => SpikeBenchLevel1Playability.Run();

        [MenuItem("Sweet Bananas/SpikeBench/J1 — reproducibility (10×2×2)")]
        public static void RunJ1() => SpikeBenchJ1Repro.RunFromMenu();

        [MenuItem("Sweet Bananas/SpikeBench/J2 — E0b @ 12x")]
        public static void RunE0b()
        {
            if (SpikeBenchSession.IsRunning)
                return;
            SpikeBenchSession.Start("E0b", SpikeBenchMethodology.DefaultSeed, 12f,
                SpikeBenchMethodology.SeriesTaps, SpikeBenchMethodology.SeriesHolds, false);
        }

        [MenuItem("Sweet Bananas/SpikeBench/J2 — E0c @ 1x")]
        public static void RunE0c()
        {
            if (SpikeBenchSession.IsRunning)
                return;
            SpikeBenchSession.Start("E0c", SpikeBenchMethodology.DefaultSeed, 1f,
                SpikeBenchMethodology.SeriesTaps, SpikeBenchMethodology.SeriesHolds, false);
        }

        [MenuItem("Sweet Bananas/SpikeBench/J2 — compare E0/E0b/E0c noise")]
        public static void RunJ2Analyze() => SpikeBenchJ2Decision.AnalyzeFromMenu();

        [MenuItem("Sweet Bananas/SpikeBench/K — H1b contact traces")]
        public static void RunH1b() => SpikeBenchH1bRunner.RunFromMenu();

        [MenuItem("Sweet Bananas/SpikeBench/M — Run E2b then E2s grid")]
        public static void RunE2Grid() => SpikeBenchE2Orchestrator.RunGridFromMenu();

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
                    if (SpikeBenchRestProbe.IsRunning)
                        SpikeBenchRestProbe.Stop();
                    if (SpikeBenchLeverRunner.IsRunning)
                        SpikeBenchLeverRunner.StopRun();
                    if (SpikeBenchCatchRunner.IsRunning)
                        SpikeBenchCatchRunner.StopRun();
                }
            };
        }
    }
}