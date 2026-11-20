using System;
using System.Globalization;
using System.IO;
using System.Text;
using SweetBananas.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchGapSanity
    {
        public static readonly float[] GapFractions = { 0.05f, 0.25f, 0.5f, 0.9f };
        static readonly int[] StepMarks = { 1, 2, 3, 4, 5, 20, 120 };

        public const float ResetSlackLimit = 0.001f;
        public const float StepSlackLimit = 0.02f;
        const float RestWaitSec = 1f;

        enum Phase
        {
            Idle,
            Running,
            WaitRest,
            Done
        }

        static Phase _phase = Phase.Idle;
        static int _fractionIndex;
        static int _stepMarkIndex;
        static int _baselineFixedSteps;
        static int _restTargetSteps;
        static ThrowController _throw;
        static Ribbon _ribbon;
        static StringBuilder _log;
        static bool _failed;
        static bool _exitPlayWhenDone;
        static Action<bool> _onComplete;
        static string _logFileName = "gap_sanity.txt";

        public static bool IsRunning => _phase == Phase.Running || _phase == Phase.WaitRest;

        [MenuItem("Sweet Bananas/SpikeBench/Run Gap Sanity")]
        public static void RunMenu()
        {
            _logFileName = "gap_sanity.txt";
            StartStandalone();
        }

        public static void StartInSession(ThrowController throwCtl, Ribbon ribbon, Action<bool> onComplete)
        {
            _logFileName = "gap_sanity.txt";
            Begin(throwCtl, ribbon, exitPlayWhenDone: false, onComplete);
        }

        static void StartStandalone()
        {
            if (IsRunning)
                return;

            SpikeBenchMethodology.EnsureLogDirectory();
            _phase = Phase.Running;
            _fractionIndex = -1;
            _log = new StringBuilder();
            _failed = false;
            _exitPlayWhenDone = true;
            _onComplete = null;
            EditorApplication.update += Tick;

            var startScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Scene.unity");
            if (startScene != null)
                EditorSceneManager.playModeStartScene = startScene;
            if (!Application.isPlaying)
                EditorApplication.EnterPlaymode();
        }

        static void Begin(ThrowController throwCtl, Ribbon ribbon, bool exitPlayWhenDone, Action<bool> onComplete)
        {
            if (IsRunning)
                return;

            _throw = throwCtl;
            _ribbon = ribbon;
            _exitPlayWhenDone = exitPlayWhenDone;
            _onComplete = onComplete;
            _log = new StringBuilder();
            _failed = false;
            _phase = Phase.Running;
            _fractionIndex = -1;
            EditorApplication.update += Tick;
            TryKickoff();
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
                Complete(false);
                return;
            }

            if (_throw == null || _ribbon == null)
            {
                _throw = UnityEngine.Object.FindAnyObjectByType<ThrowController>();
                _ribbon = UnityEngine.Object.FindAnyObjectByType<Ribbon>();
                if (_throw == null || _ribbon == null)
                    return;
            }

            TryKickoff();
            Advance();
        }

        static void TryKickoff()
        {
            if (_fractionIndex >= 0)
                return;
            if (_ribbon == null || _throw == null)
                return;

            _fractionIndex = 0;
            _log.AppendLine("criteria=reset<=0.001; steps_5_20_120_and_rest<=0.02; steps_1_4_info_only");
            _log.AppendLine("last_build_slack=" + _ribbon.LastBuildSlack.ToString(CultureInfo.InvariantCulture));
            if (_ribbon.LastBuildSlack > ResetSlackLimit)
                _failed = true;
            StartFraction();
        }

        static void StartFraction()
        {
            float f = GapFractions[_fractionIndex];
            _ribbon.ResetToSpawn(f);
            float slack = _ribbon.ValidateHingeAnchors();
            _log.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "fraction={0:F2} after_reset_hinge_slack={1}", f, slack));
            if (slack > ResetSlackLimit)
                _failed = true;
            _baselineFixedSteps = _throw.FixedSteps;
            _stepMarkIndex = 0;
        }

        static void Advance()
        {
            if (_phase == Phase.WaitRest)
            {
                if (_throw.FixedSteps - _baselineFixedSteps >= _restTargetSteps)
                {
                    float slack = _ribbon.ValidateHingeAnchors();
                    _log.AppendLine("rest_1.0s_hinge_slack=" + slack.ToString(CultureInfo.InvariantCulture));
                    if (slack > StepSlackLimit)
                        _failed = true;
                    Complete(!_failed);
                }

                return;
            }

            if (_fractionIndex < 0 || _fractionIndex >= GapFractions.Length)
                return;

            int steps = _throw.FixedSteps - _baselineFixedSteps;
            int target = StepMarks[_stepMarkIndex];
            if (steps < target)
                return;

            float stepSlack = _ribbon.ValidateHingeAnchors();
            bool infoOnly = target < 5;
            string tag = infoOnly ? "info" : "pass";
            _log.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "fraction={0:F2} step={1} tag={2} hinge_slack={3}",
                GapFractions[_fractionIndex], target, tag, stepSlack));
            if (!infoOnly && stepSlack > StepSlackLimit)
                _failed = true;

            _stepMarkIndex++;
            if (_stepMarkIndex < StepMarks.Length)
                return;

            _fractionIndex++;
            if (_fractionIndex < GapFractions.Length)
            {
                StartFraction();
                return;
            }

            _ribbon.ResetToSpawn();
            float afterDefault = _ribbon.ValidateHingeAnchors();
            _log.AppendLine("after_first_default_reset_hinge_slack=" + afterDefault.ToString(CultureInfo.InvariantCulture));
            if (afterDefault > ResetSlackLimit)
                _failed = true;

            _baselineFixedSteps = _throw.FixedSteps;
            int fixedStepsPerSec = Mathf.Max(1, Mathf.RoundToInt(1f / Time.fixedDeltaTime));
            _restTargetSteps = Mathf.RoundToInt(RestWaitSec * fixedStepsPerSec);
            _phase = Phase.WaitRest;
        }

        static void Complete(bool passed)
        {
            _phase = Phase.Done;
            _log.AppendLine("gap_sanity_pass=" + (passed ? "1" : "0"));
            File.WriteAllText(Path.Combine(SpikeBenchMethodology.LogDirectory(), _logFileName), _log.ToString());
            EditorApplication.update -= Tick;
            var cb = _onComplete;
            _onComplete = null;

            if (_exitPlayWhenDone && Application.isPlaying)
                EditorApplication.ExitPlaymode();

            cb?.Invoke(passed);
            _phase = Phase.Idle;
            if (_exitPlayWhenDone)
            {
                _throw = null;
                _ribbon = null;
            }
        }
    }
}