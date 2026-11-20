using System.Globalization;
using System.IO;
using System.Text;
using SweetBananas.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchGapSanityOverlapDiag
    {
        static readonly float[] Fractions = { 0.25f, 0.5f };

        enum Phase
        {
            Idle,
            WaitPlay,
            WaitStep,
            Done
        }

        static Phase _phase = Phase.Idle;
        static int _fractionIndex;
        static int _stepBaseline;
        static ThrowController _throw;
        static Ribbon _ribbon;
        static Lollipop _lollipop;
        static StringBuilder _log;
        static bool _ignoresActive;

        [MenuItem("Sweet Bananas/SpikeBench/Run Gap Sanity Overlap Diagnostic")]
        public static void Run()
        {
            if (_phase != Phase.Idle)
                return;

            _log = new StringBuilder();
            _log.AppendLine("diagnostic=IgnoreCollision ribbon segments vs lollipop head+stick");
            _log.AppendLine("fractions=0.25,0.50 step=1 hinge_slack only");
            _fractionIndex = 0;
            _phase = Phase.WaitPlay;
            _ignoresActive = false;
            EditorApplication.update += Tick;

            var startScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Scene.unity");
            if (startScene != null)
                EditorSceneManager.playModeStartScene = startScene;
            if (!Application.isPlaying)
                EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            if (_phase == Phase.Idle || _phase == Phase.Done)
            {
                EditorApplication.update -= Tick;
                return;
            }

            if (!Application.isPlaying)
            {
                Cleanup();
                return;
            }

            if (_phase == Phase.WaitPlay)
            {
                _throw = UnityEngine.Object.FindAnyObjectByType<ThrowController>();
                _ribbon = UnityEngine.Object.FindAnyObjectByType<Ribbon>();
                _lollipop = UnityEngine.Object.FindAnyObjectByType<Lollipop>();
                if (_throw == null || _ribbon == null || _lollipop == null)
                    return;

                EnableIgnores();
                BeginFraction();
                return;
            }

            if (_phase == Phase.WaitStep)
            {
                if (_throw.FixedSteps - _stepBaseline < 1)
                    return;

                float slack = _ribbon.MaxHingeSlack();
                _log.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "fraction={0:F2} step=1 hinge_slack_with_ignore={1}",
                    Fractions[_fractionIndex], slack));

                _fractionIndex++;
                if (_fractionIndex < Fractions.Length)
                    BeginFraction();
                else
                    Finish();
            }
        }

        static void EnableIgnores()
        {
            if (_ignoresActive)
                return;

            Collider2D stick = _lollipop.StickCollider;
            Collider2D head = _lollipop.HeadCollider;
            var colliders = _ribbon.Colliders;
            for (int i = 0; i < colliders.Count; i++)
            {
                CapsuleCollider2D c = colliders[i];
                if (c == null)
                    continue;
                if (stick != null)
                    Physics2D.IgnoreCollision(c, stick, true);
                if (head != null)
                    Physics2D.IgnoreCollision(c, head, true);
            }

            _ignoresActive = true;
            _log.AppendLine("ignore_collisions_enabled=1");
        }

        static void BeginFraction()
        {
            _ribbon.ResetToSpawn(Fractions[_fractionIndex]);
            _stepBaseline = _throw.FixedSteps;
            _phase = Phase.WaitStep;
        }

        static void Finish()
        {
            string path = Path.Combine(SpikeBenchMethodology.LogDirectory(), "gap_sanity_overlap_diag.txt");
            SpikeBenchMethodology.EnsureLogDirectory();
            File.WriteAllText(path, _log.ToString());
            Debug.Log("SpikeBench overlap diagnostic written to " + path);
            EditorApplication.ExitPlaymode();
            Cleanup();
        }

        static void Cleanup()
        {
            _phase = Phase.Idle;
            _throw = null;
            _ribbon = null;
            _lollipop = null;
            _ignoresActive = false;
            EditorApplication.update -= Tick;
        }
    }
}