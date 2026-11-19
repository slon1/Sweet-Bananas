#if UNITY_EDITOR || DEVELOPMENT_BUILD
using SweetBananas.Data;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace SweetBananas.Game.Debug
{
    public sealed class SpikeOverlay : MonoBehaviour
    {
        [SerializeField] RectTransform _hudRoot;

        GameConfig _config;
        Ribbon _ribbon;
        Lollipop _lollipop;
        Text _fps;
        Text _frame;
        Text _physics;
        ProfilerRecorder _physicsStep;
        float _fpsAccum;
        int _fpsFrames;
        bool _recorderReady;

        public bool AutoResetEnabled { get; set; } = true;

        [Inject]
        public void Construct(GameConfig config, Ribbon ribbon, Lollipop lollipop)
        {
            _config = config;
            _ribbon = ribbon;
            _lollipop = lollipop;
        }

        void OnEnable()
        {
            _physicsStep = ProfilerRecorder.StartNew(ProfilerCategory.Physics, "Physics2D.Simulate");
            _recorderReady = _physicsStep.Valid;
            if (!_recorderReady)
            {
                _physicsStep.Dispose();
                _physicsStep = ProfilerRecorder.StartNew(ProfilerCategory.Physics, "FixedUpdate.Physics2DFixedUpdate");
                _recorderReady = _physicsStep.Valid;
            }
        }

        void Start()
        {
            if (_hudRoot == null)
            {
                GameObject hud = GameObject.Find("HudRoot");
                if (hud != null)
                    _hudRoot = hud.GetComponent<RectTransform>();
            }

            if (_hudRoot == null)
                return;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var panel = new GameObject("SpikeOverlay", typeof(RectTransform));
            panel.transform.SetParent(_hudRoot, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(24f, -24f);
            panelRect.sizeDelta = new Vector2(520f, 280f);

            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.45f);
            bg.raycastTarget = false;

            _fps = MakeText(panel.transform, font, 0);
            _frame = MakeText(panel.transform, font, 1);
            _physics = MakeText(panel.transform, font, 2);
            MakeButton(panel.transform, font);
        }

        void OnDisable()
        {
            if (_recorderReady)
                _physicsStep.Dispose();
            _recorderReady = false;
        }

        void Update()
        {
            _fpsAccum += Time.unscaledDeltaTime;
            _fpsFrames++;
            if (_fpsAccum >= 0.5f && _fps != null)
            {
                float fps = _fpsFrames / _fpsAccum;
                _fps.text = "FPS " + fps.ToString("0.0");
                _frame.text = "Frame " + (Time.unscaledDeltaTime * 1000f).ToString("0.00") + " ms";
                float stepMs = 0f;
                if (_recorderReady && _physicsStep.Count > 0)
                    stepMs = (float)(_physicsStep.LastValue / 1e6);
                _physics.text = "Physics " + stepMs.ToString("0.000") + " ms";
                _fpsAccum = 0f;
                _fpsFrames = 0;
            }
        }

        void FixedUpdate()
        {
            if (!AutoResetEnabled || _ribbon == null || _lollipop == null || _config == null)
                return;
            if (_ribbon.Bodies.Count == 0)
                return;

            Vector2 center = _ribbon.Center;
            float failY = _lollipop.Pivot.y - _config.FailMarginUnits;
            float minX = -_config.FailMarginUnits;
            float maxX = _config.WorldWidthUnits + _config.FailMarginUnits;
            if (center.y < failY || center.x < minX || center.x > maxX)
                _ribbon.ResetToSpawn();
        }

        static Text MakeText(Transform parent, Font font, int row)
        {
            var go = new GameObject("Line" + row, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(16f, -16f - row * 36f);
            rect.sizeDelta = new Vector2(-32f, 34f);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = 28;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            text.text = "-";
            return text;
        }

        void MakeButton(Transform parent, Font font)
        {
            var go = new GameObject("Restart", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 12f);
            rect.sizeDelta = new Vector2(-24f, 64f);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.95f, 0.35f, 0.55f, 1f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => _ribbon.ResetToSpawn());

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelGo.AddComponent<Text>();
            label.font = font;
            label.fontSize = 28;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.text = "Restart";
            label.raycastTarget = false;
        }
    }
}
#endif
