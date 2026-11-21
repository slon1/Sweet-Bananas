using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using SweetBananas.Game;
using SweetBananas.Game.Debug;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchLeverRunner
    {
        enum Phase { Idle, WaitScene, Press, Free, Done }

        const float TapSec = 0.08f;
        static readonly float[] Fractions = { 0.05f, 0.9f };
        const int Reps = 5;

        static Phase _state = Phase.Idle;
        static int _fracIndex;
        static int _rep;
        static float _press;
        static bool _wasAbovePivot;
        static bool _crossRecorded;
        static float _rangeX;
        static float _findStart;
        static ThrowController _throw;
        static Ribbon _ribbon;
        static Lollipop _lollipop;
        static SpikeOverlay _overlay;
        static readonly List<float> _low = new();
        static readonly List<float> _high = new();
        static readonly StringBuilder _log = new();

        public static bool IsRunning => _state != Phase.Idle;

        [MenuItem("Sweet Bananas/SpikeBench/D24.3 — lever (2 spawn × 5)")]
        public static void RunFromMenu()
        {
            if (IsRunning || SpikeBenchSession.IsRunning)
                return;
            _low.Clear();
            _high.Clear();
            _log.Clear();
            _fracIndex = 0;
            _rep = 0;
            _state = Phase.WaitScene;
            EditorApplication.update += Tick;
            if (!Application.isPlaying)
            {
                var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Scene.unity");
                if (scene != null)
                    EditorSceneManager.playModeStartScene = scene;
                EditorApplication.EnterPlaymode();
                _findStart = (float)EditorApplication.timeSinceStartup;
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
            _throw.DebugFixedStep += OnStep;
            StartRep();
        }

        static void StartRep()
        {
            float frac = Fractions[_fracIndex];
            _ribbon.ResetToSpawn(frac);
            _throw.SnapRest();
            _throw.DebugResetFixedSteps();
            _press = 0f;
            _wasAbovePivot = _ribbon.Center.y > _lollipop.Pivot.y;
            _crossRecorded = false;
            _rangeX = 0f;
            _throw.DebugSetHeld(true);
            _state = Phase.Press;
        }

        static void OnStep()
        {
            if (_state == Phase.Press)
            {
                _press += Time.fixedDeltaTime;
                if (_press >= TapSec)
                {
                    _throw.DebugSetHeld(false);
                    _state = Phase.Free;
                }

                return;
            }

            if (_state == Phase.Free)
            {
                float pivotY = _lollipop.Pivot.y;
                Vector2 c = _ribbon.Center;
                if (c.y > pivotY)
                    _wasAbovePivot = true;
                if (_wasAbovePivot && !_crossRecorded && c.y <= pivotY)
                {
                    _crossRecorded = true;
                    _rangeX = c.x - _lollipop.Pivot.x;
                }

                if (_throw.FixedSteps > 400 || (_crossRecorded && _throw.State == ThrowState.Rest))
                    FinishRep();
            }
        }

        static void FinishRep()
        {
            float frac = Fractions[_fracIndex];
            bool noFlight = !_crossRecorded;
            float dist = _crossRecorded ? _rangeX : 0f;
            _log.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "fraction={0} rep={1} distance_x={2:F4} no_flight={3}",
                frac, _rep + 1, dist, noFlight ? 1 : 0));

            if (frac < 0.1f)
                _low.Add(dist);
            else
                _high.Add(dist);

            _rep++;
            if (_rep >= Reps)
            {
                _rep = 0;
                _fracIndex++;
                if (_fracIndex >= Fractions.Length)
                {
                    Flush();
                    StopRun();
                    EditorApplication.ExitPlaymode();
                    return;
                }
            }

            StartRep();
        }

        static void Flush()
        {
            float avgLow = Mean(_low);
            float avgHigh = Mean(_high);
            float spreadLow = Spread(_low);
            float spreadHigh = Spread(_high);
            SpikeBenchMethodology.EnsureLogDirectory();
            string path = Path.Combine(SpikeBenchMethodology.LogDirectory(), "d24_3_lever.txt");
            var sb = new StringBuilder();
            sb.Append(_log);
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "avg_distance_spawn_0.05={0:F4} spread={1:F4}", avgLow, spreadLow));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "avg_distance_spawn_0.90={0:F4} spread={1:F4}", avgHigh, spreadHigh));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "delta_avg_high_minus_low={0:F4} threshold=2", avgHigh - avgLow));
            File.WriteAllText(path, sb.ToString());
            Debug.Log("SpikeBench lever: " + path);
        }

        static float Mean(List<float> v)
        {
            if (v.Count == 0)
                return 0f;
            float s = 0f;
            foreach (float f in v)
                s += f;
            return s / v.Count;
        }

        static float Spread(List<float> v)
        {
            if (v.Count < 2)
                return 0f;
            float m = Mean(v);
            float s = 0f;
            foreach (float f in v)
            {
                float d = f - m;
                s += d * d;
            }

            return Mathf.Sqrt(s / (v.Count - 1));
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