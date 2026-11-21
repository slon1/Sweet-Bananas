using System;
using SweetBananas.Core;
using SweetBananas.Data;
using UnityEngine;
using Zenject;

namespace SweetBananas.Game
{
    public sealed class LevelRules : MonoBehaviour
    {
        GameConfig _config;
        GameFlow _flow;
        Ribbon _ribbon;
        TargetHolder _target;
        ThrowController _throw;

        float _settleAccum;
        int _failCount;
        bool _enabled = true;

        public int FailCount => _failCount;
        public bool IsFirstAttempt => _failCount == 0;
        public bool HadFirstTargetContact { get; private set; }
        public float FirstContactCenterY { get; private set; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public event Action FirstTargetContact;

        public void DebugSetEnabled(bool enabled) => _enabled = enabled;
#endif

        [Inject]
        public void Construct(
            GameConfig config,
            GameFlow flow,
            Ribbon ribbon,
            TargetHolder target,
            ThrowController throwController)
        {
            _config = config;
            _flow = flow;
            _ribbon = ribbon;
            _target = target;
            _throw = throwController;
        }

        void Start()
        {
            _failCount = 0;
            _settleAccum = 0f;
            ResetContactTracking();
        }

        public void Restart(float? spawnFraction = null)
        {
            _settleAccum = 0f;
            ResetContactTracking();
            if (_flow.State != GameState.Playing)
                _flow.SetState(GameState.Playing);
            if (_ribbon != null)
                _ribbon.ResetToSpawn(spawnFraction);
            if (_throw != null)
                _throw.SnapRest();
        }

        void ResetContactTracking()
        {
            HadFirstTargetContact = false;
            FirstContactCenterY = 0f;
        }

        void FixedUpdate()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_enabled)
                return;
#endif
            if (_flow == null || _flow.State != GameState.Playing)
                return;
            if (_ribbon == null || _config == null)
                return;

            CheckFail();
            if (_flow.State != GameState.Playing)
                return;

            CheckWin();
            TrackFirstContact();
        }

        void CheckFail()
        {
            Vector2 center = _ribbon.Center;
            float failY = -_config.FailMarginUnits;
            float minX = -_config.FailMarginUnits;
            float maxX = _config.WorldWidthUnits + _config.FailMarginUnits;
            if (center.y >= failY && center.x >= minX && center.x <= maxX)
                return;

            _ribbon.SetFrozen(true);
            _flow.SetState(GameState.Fail);
            _failCount++;
            _settleAccum = 0f;
        }

        void CheckWin()
        {
            if (_target == null || _target.Collider == null)
                return;

            Collider2D targetCol = _target.Collider;
            int touching = 0;
            float maxSpeed = 0f;
            foreach (CapsuleCollider2D cap in _ribbon.Colliders)
            {
                if (cap == null)
                    continue;
                if (cap.IsTouching(targetCol))
                    touching++;
                Rigidbody2D body = cap.attachedRigidbody;
                if (body != null)
                    maxSpeed = Mathf.Max(maxSpeed, body.linearVelocity.magnitude);
            }

            if (touching < 2 || maxSpeed >= _config.SettleSpeedThreshold)
            {
                _settleAccum = 0f;
                return;
            }

            _settleAccum += Time.fixedDeltaTime;
            if (_settleAccum < _config.SettleSeconds)
                return;

            _ribbon.SetFrozen(true);
            _flow.SetState(GameState.Win);
            _settleAccum = 0f;
        }

        void TrackFirstContact()
        {
            if (HadFirstTargetContact || _target?.Collider == null)
                return;

            Collider2D targetCol = _target.Collider;
            foreach (CapsuleCollider2D cap in _ribbon.Colliders)
            {
                if (cap != null && cap.IsTouching(targetCol))
                {
                    HadFirstTargetContact = true;
                    FirstContactCenterY = _ribbon.Center.y;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    FirstTargetContact?.Invoke();
#endif
                    return;
                }
            }
        }
    }
}