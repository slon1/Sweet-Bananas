using UnityEditor;
using UnityEngine;

namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchTask002BRunner
    {
        enum Step { Idle, Friction, Catch, Level1, Session, Geometry, Done }

        static Step _step = Step.Idle;
        static float _selectedFriction;

        [MenuItem("Sweet Bananas/SpikeBench/B — TASK-002-B full acceptance")]
        public static void RunAll()
        {
            if (SpikeBenchRestProbe.IsRunning || SpikeBenchStickFrictionPicker.IsRunning
                || SpikeBenchCatchRunner.IsRunning || SpikeBenchLevel1Playability.IsRunning
                || SpikeBenchSession.IsRunning)
            {
                Debug.LogWarning("TASK-002-B runner: busy.");
                return;
            }

            _step = Step.Friction;
            SpikeBenchStickFrictionPicker.RunGrid((friction, table) =>
            {
                if (float.IsNaN(friction))
                {
                    Debug.LogError("TASK-002-B: StickFriction selection failed.\n" + table);
                    _step = Step.Idle;
                    return;
                }

                _selectedFriction = friction;
                _step = Step.Catch;
                EditorApplication.delayCall += () =>
                {
                    SpikeBenchCatchRunner.RunFromMenu();
                    EditorApplication.update += WaitCatch;
                };
            });
        }

        static void WaitCatch()
        {
            if (SpikeBenchCatchRunner.IsRunning)
                return;
            EditorApplication.update -= WaitCatch;
            _step = Step.Level1;
            SpikeBenchLevel1Playability.Run(_ =>
            {
                _step = Step.Session;
                SpikeBenchSession.StartB242D24Repeat();
                EditorApplication.update += WaitSession;
            });
        }

        static void WaitSession()
        {
            if (SpikeBenchSession.IsRunning)
                return;
            EditorApplication.update -= WaitSession;
            _step = Step.Geometry;
            SpikeBenchGeometryRunner.RunFromMenu();
            SpikeBenchTargetMetrics.RunFromMenu();
            _step = Step.Done;
            Debug.Log("TASK-002-B acceptance chain finished. Fill DOC/reports/REPORT-002-B.md from Logs/SpikeBench.");
            _step = Step.Idle;
        }
    }
}