using System.Globalization;
using System.IO;
using System.Text;
using SweetBananas.Data;
using SweetBananas.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchD1Diagnostics
    {
        enum D1ScenePhase
        {
            WaitScene,
            AfterSceneStart,
            AfterReset,
            AfterOneStep,
            After120Steps,
            Done
        }

        static bool _running;
        static D1ScenePhase _phase;
        static int _stepTarget;
        static int _baselineSteps;
        static ThrowController _throw;
        static Ribbon _ribbon;

        [MenuItem("Sweet Bananas/SpikeBench/Run D1 Scene Dump")]
        public static void RunSceneDump()
        {
            if (_running)
                return;

            SpikeBenchJointDump.ResetFile();
            _running = true;
            _phase = D1ScenePhase.WaitScene;
            EditorApplication.update += SceneTick;
            var startScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Scene.unity");
            if (startScene != null)
                EditorSceneManager.playModeStartScene = startScene;
            if (!Application.isPlaying)
                EditorApplication.EnterPlaymode();
        }

        static void SceneTick()
        {
            if (!_running)
            {
                EditorApplication.update -= SceneTick;
                return;
            }

            if (!Application.isPlaying)
            {
                StopScene();
                return;
            }

            if (_phase == D1ScenePhase.WaitScene)
            {
                _throw = Object.FindAnyObjectByType<ThrowController>();
                _ribbon = Object.FindAnyObjectByType<Ribbon>();
                if (_ribbon == null)
                    return;
                SpikeBenchJointDump.WriteRibbon(_ribbon, "after_scene_start_includes_build_and_initial_reset");
                _phase = D1ScenePhase.AfterSceneStart;
                _ribbon.ResetToSpawn();
                SpikeBenchJointDump.WriteRibbon(_ribbon, "after_reset_to_spawn");
                _baselineSteps = _throw != null ? _throw.FixedSteps : 0;
                _stepTarget = 1;
                _phase = D1ScenePhase.AfterOneStep;
                return;
            }

            if (_throw == null)
                _throw = Object.FindAnyObjectByType<ThrowController>();
            int steps = _throw != null ? _throw.FixedSteps : 0;

            if (_phase == D1ScenePhase.AfterOneStep && steps - _baselineSteps >= _stepTarget)
            {
                SpikeBenchJointDump.WriteRibbon(_ribbon, "after_1_physics_step");
                _baselineSteps = steps;
                _stepTarget = 120;
                _phase = D1ScenePhase.After120Steps;
                return;
            }

            if (_phase == D1ScenePhase.After120Steps && steps - _baselineSteps >= _stepTarget)
            {
                SpikeBenchJointDump.WriteRibbon(_ribbon, "after_120_physics_steps");
                WriteAnalysisTable();
                EditorApplication.ExitPlaymode();
                StopScene();
            }
        }

        static void StopScene()
        {
            _running = false;
            _throw = null;
            _ribbon = null;
            EditorApplication.update -= SceneTick;
        }

        static bool _pendingIsolation;

        [MenuItem("Sweet Bananas/SpikeBench/Run Chain Isolation")]
        public static void RunChainIsolation()
        {
            if (!Application.isPlaying)
            {
                _pendingIsolation = true;
                EditorSceneManager.playModeStartScene = null;
                EditorApplication.EnterPlaymode();
                EditorApplication.update += IsolationPlayHook;
                return;
            }

            ExecuteChainIsolation();
        }

        static void IsolationPlayHook()
        {
            if (!Application.isPlaying || !_pendingIsolation)
                return;
            _pendingIsolation = false;
            EditorApplication.update -= IsolationPlayHook;
            ExecuteChainIsolation();
        }

        static void ExecuteChainIsolation()
        {
            SpikeBenchJointDump.ResetFile();
            GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Data/GameConfig.asset");
            RunIsolationVariant(config, SpikeBenchChainBuilder.BuildOptions.IsolationBaseline);

            var withGravity = SpikeBenchChainBuilder.BuildOptions.IsolationBaseline;
            withGravity.GravityScale = 1f;
            withGravity.Label = "gravity";
            RunIsolationVariant(config, withGravity);

            var withRestore = withGravity;
            withRestore.RestoreTorque = true;
            withRestore.Label = "gravity_restore";
            RunIsolationWithSteps(config, withRestore, simulateRestore: true);

            var withStick = SpikeBenchChainBuilder.BuildOptions.IsolationBaseline;
            withStick.KinematicStick = true;
            withStick.Label = "kinematic_stick";
            RunIsolationVariant(config, withStick);

            var withPlace = SpikeBenchChainBuilder.BuildOptions.IsolationBaseline;
            withPlace.PlaceAlongStickStyle = true;
            withPlace.Label = "place_along_stick_style";
            RunIsolationVariant(config, withPlace);

            WriteAnalysisTable();
            EditorApplication.ExitPlaymode();
        }

        static void RunIsolationVariant(GameConfig config, SpikeBenchChainBuilder.BuildOptions options)
        {
            RunIsolationWithSteps(config, options, options.RestoreTorque);
        }

        static void RunIsolationWithSteps(GameConfig config, SpikeBenchChainBuilder.BuildOptions options, bool simulateRestore)
        {
            var chain = SpikeBenchChainBuilder.Build(config, options);
            SpikeBenchJointDump.WriteIsolatedChain(chain, "isolation_" + options.Label + "_step0");
            for (int s = 1; s <= 120; s++)
            {
                if (simulateRestore)
                    chain.ApplyRestoreTorque();
                Physics2D.Simulate(Time.fixedDeltaTime);
                if (s == 1 || s == 10 || s == 120)
                    SpikeBenchJointDump.WriteIsolatedChain(chain, "isolation_" + options.Label + "_step" + s);
            }

            SpikeBenchChainBuilder.DestroyChain(chain);
        }

        static void SimulateSteps(SpikeBenchChainBuilder.ChainData chain, int start, bool simulateRestore)
        {
            int[] markers = { 1, 10, 120 };
            for (int s = 1; s <= 120; s++)
            {
                if (simulateRestore)
                    chain.ApplyRestoreTorque();
                Physics2D.Simulate(Time.fixedDeltaTime);
                for (int m = 0; m < markers.Length; m++)
                {
                    if (s == markers[m])
                        SpikeBenchJointDump.WriteIsolatedChain(chain, "isolation_" + chain.OptionsLabel + "_step" + s);
                }
            }
        }

        static void WriteAnalysisTable()
        {
            SpikeBenchD1AnalysisWriter.WriteFromDump();
        }
    }
}