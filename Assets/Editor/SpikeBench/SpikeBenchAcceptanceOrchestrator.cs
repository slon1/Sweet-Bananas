using UnityEditor;

namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchAcceptanceOrchestrator
    {
        enum Step { Idle, Final, Rest, Lever, Catch, Done }

        static Step _step = Step.Idle;
        static bool _hooked;

        [MenuItem("Sweet Bananas/SpikeBench/A21 — Run acceptance (FINAL → Rest → D24)")]
        public static void RunAll()
        {
            if (_step != Step.Idle || SpikeBenchSession.IsRunning)
                return;
            _step = Step.Final;
            EnsureHook();
            SpikeBenchSession.StartFinalJ25();
        }

        [MenuItem("Sweet Bananas/SpikeBench/Run FINAL_J25 (300+60 @12× + verify)")]
        public static void RunFinalOnly() => SpikeBenchSession.StartFinalJ25();

        static void EnsureHook()
        {
            if (_hooked)
                return;
            EditorApplication.playModeStateChanged += OnPlay;
            _hooked = true;
        }

        static void OnPlay(PlayModeStateChange change)
        {
            if (_step == Step.Idle || change != PlayModeStateChange.EnteredEditMode)
                return;

            switch (_step)
            {
                case Step.Final:
                    _step = Step.Rest;
                    EditorApplication.delayCall += () => SpikeBenchRestProbe.RunFromMenu();
                    break;
                case Step.Rest:
                    _step = Step.Lever;
                    EditorApplication.delayCall += () => SpikeBenchLeverRunner.RunFromMenu();
                    break;
                case Step.Lever:
                    _step = Step.Catch;
                    EditorApplication.delayCall += () => SpikeBenchCatchRunner.RunFromMenu();
                    break;
                case Step.Catch:
                    _step = Step.Done;
                    EditorApplication.delayCall += () =>
                    {
                        SpikeBenchA21Verdict.GenerateFromArtifacts();
                        _step = Step.Idle;
                    };
                    break;
            }
        }
    }
}