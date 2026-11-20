using System;
using SweetBananas.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Zenject;

namespace SweetBananas.Game
{
    public enum ThrowState
    {
        Rest,
        Rise,
        Hold,
        TapRush,
        Return
    }

    public sealed class ThrowController : MonoBehaviour
    {
        Lollipop _lollipop;
        GameConfig _config;
        InputAction _press;
        ThrowState _state = ThrowState.Rest;
        float _angle;
        float _heldTime;
        bool _held;
        bool _suppressUntilRelease;
        bool _ready;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool _debugDrive;
        bool _debugHeld;
        GameConfig _debugConfigOverride;
        float _debugMaxTapOvershootDeg;
        float _debugMaxHoldOvershootDeg;
        float _debugReturnToRestElapsed;
        bool _debugReturnTimerActive;
        public int FixedSteps { get; private set; }
        public void DebugSetDrive(bool enabled) => _debugDrive = enabled;
        public void DebugSetHeld(bool held) => _debugHeld = held;
        public void DebugResetFixedSteps() => FixedSteps = 0;
        public void DebugSetConfigOverride(GameConfig config) => _debugConfigOverride = config;
        public void DebugResetDriveMetrics()
        {
            _debugMaxTapOvershootDeg = 0f;
            _debugMaxHoldOvershootDeg = 0f;
            _debugReturnToRestElapsed = 0f;
            _debugReturnTimerActive = false;
        }

        public float DebugMaxTapOvershootDeg => _debugMaxTapOvershootDeg;
        public float DebugMaxHoldOvershootDeg => _debugMaxHoldOvershootDeg;
        public float DebugReturnToRestSec => _debugReturnToRestElapsed;
        public event Action DebugFixedStep;

        GameConfig ActiveConfig => _debugConfigOverride != null ? _debugConfigOverride : _config;
#else
        GameConfig ActiveConfig => _config;
#endif

        public ThrowState State => _state;
        public float Angle => _angle;

        [Inject]
        public void Construct(GameConfig config, Lollipop lollipop)
        {
            _config = config;
            _lollipop = lollipop;
            _ready = true;
            _angle = _config.RestAngleDeg;
        }

        void OnEnable()
        {
            _press = new InputAction("Press", InputActionType.Button, "<Pointer>/press");
            _press.started += OnPressStarted;
            _press.canceled += OnPressCanceled;
            _press.Enable();
        }

        void OnDisable()
        {
            if (_press == null)
                return;
            _press.started -= OnPressStarted;
            _press.canceled -= OnPressCanceled;
            _press.Disable();
            _press.Dispose();
            _press = null;
        }

        void Start()
        {
            if (!_ready)
                return;
            _angle = ActiveConfig.RestAngleDeg;
            _lollipop.SnapRotation(_angle);
        }

        public void SnapRest()
        {
            _state = ThrowState.Rest;
            _heldTime = 0f;
            _held = false;
            _suppressUntilRelease = false;
            if (ActiveConfig != null)
                _angle = ActiveConfig.RestAngleDeg;
            if (_lollipop != null)
                _lollipop.SnapRotation(_angle);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugResetDriveMetrics();
#endif
        }

        void OnPressStarted(InputAction.CallbackContext ctx)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_debugDrive)
                return;
#endif
            if (IsBlockedByUi(ctx))
            {
                _suppressUntilRelease = true;
                return;
            }

            _suppressUntilRelease = false;
            _held = true;
        }

        void OnPressCanceled(InputAction.CallbackContext ctx)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_debugDrive)
                return;
#endif
            _held = false;
            _suppressUntilRelease = false;
        }

        void FixedUpdate()
        {
            if (!_ready)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            FixedSteps++;
            if (_debugDrive)
                _held = _debugHeld;
#endif
            float dt = Time.fixedDeltaTime;
            bool held = _held && !_suppressUntilRelease;
            if (held)
                _heldTime += dt;

            switch (_state)
            {
                case ThrowState.Rest:
                    if (held)
                    {
                        _state = ThrowState.Rise;
                        _heldTime = dt;
                    }
                    break;
                case ThrowState.Rise:
                    if (!held)
                        _state = _heldTime <= ActiveConfig.HoldThresholdSeconds ? ThrowState.TapRush : ThrowState.Return;
                    else if (_heldTime > ActiveConfig.HoldThresholdSeconds)
                        _state = ThrowState.Hold;
                    break;
                case ThrowState.Hold:
                    if (!held)
                        _state = ThrowState.Return;
                    break;
                case ThrowState.TapRush:
                    if (Reached(ActiveConfig.TapPeakAngleDeg))
                        _state = ThrowState.Return;
                    break;
                case ThrowState.Return:
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (!_debugReturnTimerActive)
                    {
                        _debugReturnTimerActive = true;
                        _debugReturnToRestElapsed = 0f;
                    }

                    _debugReturnToRestElapsed += dt;
#endif
                    if (held)
                    {
                        _state = ThrowState.Rise;
                        _heldTime = dt;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        _debugReturnTimerActive = false;
#endif
                    }
                    else if (Reached(ActiveConfig.RestAngleDeg))
                    {
                        _state = ThrowState.Rest;
                        _heldTime = 0f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        _debugReturnTimerActive = false;
#endif
                    }
                    break;
            }

            float commandSpeed;
            switch (_state)
            {
                case ThrowState.Rise:
                case ThrowState.Hold:
                    StepToward(ActiveConfig.HoldAngleDeg, ActiveConfig.HoldRiseSpeedDegPerSec, dt);
                    commandSpeed = ActiveConfig.HoldRiseSpeedDegPerSec;
                    break;
                case ThrowState.TapRush:
                    StepToward(ActiveConfig.TapPeakAngleDeg, ActiveConfig.TapSpeedDegPerSec, dt);
                    commandSpeed = ActiveConfig.TapSpeedDegPerSec;
                    break;
                default:
                    StepToward(ActiveConfig.RestAngleDeg, ActiveConfig.ReturnSpeedDegPerSec, dt);
                    commandSpeed = ActiveConfig.ReturnSpeedDegPerSec;
                    break;
            }

            _lollipop.DriveToward(_angle, commandSpeed);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            float stickDeg = _lollipop != null ? _lollipop.Angle : _angle;
            if (_state == ThrowState.Hold)
            {
                float overshoot = Mathf.Abs(stickDeg - ActiveConfig.HoldAngleDeg);
                if (overshoot > _debugMaxHoldOvershootDeg)
                    _debugMaxHoldOvershootDeg = overshoot;
            }
            else if (_state == ThrowState.TapRush)
            {
                float overshoot = stickDeg - ActiveConfig.TapPeakAngleDeg;
                if (overshoot > _debugMaxTapOvershootDeg)
                    _debugMaxTapOvershootDeg = overshoot;
            }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugFixedStep?.Invoke();
#endif
        }

        void StepToward(float target, float speed, float dt)
        {
            float maxDelta = speed * dt;
            _angle = Mathf.MoveTowards(_angle, target, maxDelta);
        }

        bool Reached(float target)
        {
            return Mathf.Abs(_angle - target) <= 0.05f;
        }

        static bool IsBlockedByUi(InputAction.CallbackContext ctx)
        {
            EventSystem es = EventSystem.current;
            if (es == null)
                return false;

            if (ctx.control?.device is Touchscreen screen)
            {
                int id = screen.primaryTouch.touchId.ReadValue();
                return es.IsPointerOverGameObject(id);
            }

            return es.IsPointerOverGameObject();
        }
    }
}
