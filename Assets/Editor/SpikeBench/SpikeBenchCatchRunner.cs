using System;
using System.Globalization;
using System.IO;
using System.Text;
using SweetBananas.Data;
using SweetBananas.Game;
using SweetBananas.Game.Debug;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchCatchRunner
    {
        enum Phase { Idle, WaitScene, Drop, Measure, Done }

        static readonly float[] Dx = { -1f, -0.5f, 0f, 0.5f, 1f };
        const float AfterContactSec = 0.3f;

        static Phase _state = Phase.Idle;
        static int _dxIndex;
        static bool _contact;
        static int _contactStep;
        static float _contactCenterY;
        static float _maxLift;
        static float _vySum;
        static int _vyCount;
        static int _step;
        static float _findStart;
        static ThrowController _throw;
        static Ribbon _ribbon;
        static Lollipop _lollipop;
        static SpikeOverlay _overlay;
        static readonly StringBuilder _log = new();
        static int _passCount;

        public static bool IsRunning => _state != Phase.Idle;

        [MenuItem("Sweet Bananas/SpikeBench/D24.4 — catch (5× dx)")]
        public static void RunFromMenu()
        {
            if (IsRunning || SpikeBenchSession.IsRunning || SpikeBenchLeverRunner.IsRunning)
                return;
            _dxIndex = 0;
            _passCount = 0;
            _log.Clear();
            _state = Phase.WaitScene;
            _findStart = (float)EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            if (!Application.isPlaying)
            {
                var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Scene.unity");
                if (scene != null)
                    EditorSceneManager.playModeStartScene = scene;
                EditorApplication.EnterPlaymode();
            }
        }

        static void Tick()
        {
            if (_state == Phase.Idle)
            {
                EditorApplication.update -= Tick;
                return;
            }

            if (_state == Phase.WaitScene)
            {
                if (Application.isPlaying && TryFind())
                    Begin();
                else if (EditorApplication.timeSinceStartup - _findStart > 90f)
                    StopRun();
                return;
            }

            if (!Application.isPlaying)
                StopRun();
        }

        static bool TryFind()
        {
            _throw = UnityEngine.Object.FindAnyObjectByType<ThrowController>();
            _ribbon = UnityEngine.Object.FindAnyObjectByType<Ribbon>();
            _lollipop = UnityEngine.Object.FindAnyObjectByType<Lollipop>();
            _overlay = UnityEngine.Object.FindAnyObjectByType<SpikeOverlay>();
            return _throw != null && _ribbon != null && _lollipop != null;
        }

        static void Begin()
        {
            Time.timeScale = 1f;
            _overlay.AutoResetEnabled = false;
            _throw.DebugSetConfigOverride(null);
            _ribbon.DebugSetConfigOverride(null);
            _throw.DebugSetDrive(true);
            _lollipop.SnapRotation(0f);
            _throw.DebugSetHeld(true);
            _throw.DebugFixedStep += OnStep;
            StartDrop();
        }

        static void StartDrop()
        {
            var pose = _lollipop.ActivePose;
            Vector2 pivot = _lollipop.Pivot;
            float along = pose.HeadRadiusUnits + 0.5f * (pose.StickLengthUnits - pose.HeadRadiusUnits);
            float dx = Dx[_dxIndex];
            Vector2 center = new Vector2(pivot.x + along + dx, pivot.y + 3f);
            _ribbon.DebugPlace(center, 0f);
            _throw.DebugResetFixedSteps();
            _contact = false;
            _maxLift = 0f;
            _vySum = 0f;
            _vyCount = 0;
            _step = 0;
            _state = Phase.Drop;
        }

        static void OnStep()
        {
            _step++;
            if (_state == Phase.Drop)
            {
                if (!_contact && SpikeBenchStickFrame.AnySegmentTouchingStick(_ribbon, _lollipop))
                {
                    _contact = true;
                    _contactStep = _step;
                    _contactCenterY = _ribbon.Center.y;
                    _state = Phase.Measure;
                }
                else if (_step > 500)
                {
                    _log.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "dx={0} pass=0 reason=no_contact", Dx[_dxIndex]));
                    NextDx();
                }

                return;
            }

            if (_state == Phase.Measure)
            {
                int afterSteps = Mathf.CeilToInt(AfterContactSec / Time.fixedDeltaTime);
                float lift = _ribbon.Center.y - _contactCenterY;
                if (lift > _maxLift)
                    _maxLift = lift;

                float vy = 0f;
                int n = 0;
                foreach (Rigidbody2D b in _ribbon.Bodies)
                {
                    if (b == null)
                        continue;
                    vy += b.linearVelocity.y;
                    n++;
                }

                if (n > 0)
                {
                    _vySum += vy / n;
                    _vyCount++;
                }

                if (_step - _contactStep >= afterSteps)
                    FinishDx();
            }
        }

        static void FinishDx()
        {
            float avgVy = _vyCount > 0 ? _vySum / _vyCount : float.NaN;
            bool pass = avgVy < 0.5f && _maxLift <= 0.2f;
            if (pass)
                _passCount++;
            _log.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "dx={0} avg_vy={1:F4} max_lift={2:F4} pass={3}",
                Dx[_dxIndex], avgVy, _maxLift, pass ? 1 : 0));
            NextDx();
        }

        static void NextDx()
        {
            _dxIndex++;
            if (_dxIndex >= Dx.Length)
            {
                Flush();
                StopRun();
                EditorApplication.ExitPlaymode();
                return;
            }

            StartDrop();
        }

        static void Flush()
        {
            SpikeBenchMethodology.EnsureLogDirectory();
            string path = Path.Combine(SpikeBenchMethodology.LogDirectory(), "d24_4_catch.txt");
            var sb = new StringBuilder();
            sb.Append(_log);
            sb.AppendLine("pass_count=" + _passCount + " required=5");
            File.WriteAllText(path, sb.ToString());
            Debug.Log("SpikeBench catch: " + path);
        }

        public static void StopRun()
        {
            EditorApplication.update -= Tick;
            if (_throw != null)
            {
                _throw.DebugFixedStep -= OnStep;
                _throw.DebugSetDrive(false);
                _throw.DebugSetHeld(false);
            }

            if (_overlay != null)
                _overlay.AutoResetEnabled = true;
            _state = Phase.Idle;
        }
    }
}