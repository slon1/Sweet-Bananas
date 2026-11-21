using SweetBananas.Data;
using UnityEngine;
using Zenject;

namespace SweetBananas.Game
{
    public enum LollipopDriveMode
    {
        MoveRotation,
        AngularVelocity
    }

    public sealed class Lollipop : MonoBehaviour
    {
        [SerializeField] PointerPose[] _poses;
        [SerializeField] int _activePose;
        [SerializeField] Transform _visual;
        [SerializeField] SpriteRenderer _renderer;
        [SerializeField] CircleCollider2D _head;
        [SerializeField] CapsuleCollider2D _stick;

        Rigidbody2D _body;
        PhysicsMaterial2D _stickMaterial;
        GameConfig _config;
        bool _collidersReady;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        LollipopDriveMode _driveMode = LollipopDriveMode.MoveRotation;
#endif

        public PointerPose ActivePose => _poses[_activePose];
        public PointerPose[] Poses => _poses;
        public float Angle => _body.rotation;
        public float StickLength => ActivePose.StickLengthUnits;
        public Vector2 Pivot => _body.position;
        public Collider2D HeadCollider => _head;
        public Collider2D StickCollider => _stick;
        public Rigidbody2D Body => _body;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugSetHeadColliderEnabled(bool enabled)
        {
            if (_head != null)
                _head.enabled = enabled;
        }

        public void DebugSetStickFriction(float friction)
        {
            if (_stickMaterial == null)
                _stickMaterial = new PhysicsMaterial2D("StickFriction");
            _stickMaterial.friction = friction;
            _stickMaterial.bounciness = 0f;
            if (_stick != null)
                _stick.sharedMaterial = _stickMaterial;
            if (_head != null)
                _head.sharedMaterial = _stickMaterial;
        }
#endif

        [Inject]
        public void Construct(GameConfig config)
        {
            _config = config;
            EnsureBody();
            ApplyStickMaterial();
        }

        void Awake()
        {
            EnsureBody();
            BuildColliders();
            if (_config != null)
                ApplyStickMaterial();
        }

        public Vector2 StickDirection()
        {
            float rad = _body.rotation * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        public Vector2 StickUp()
        {
            Vector2 dir = StickDirection();
            return new Vector2(-dir.y, dir.x);
        }

        public void ApplyRotation(float degrees)
        {
            DriveToward(degrees, float.PositiveInfinity);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugSetDriveMode(LollipopDriveMode mode) => _driveMode = mode;

        public LollipopDriveMode DebugDriveMode => _driveMode;

        public float MeasuredAngularVelocityDegPerSec => _body != null ? _body.angularVelocity : 0f;
#endif

        public void DriveToward(float targetDegrees, float maxSpeedDegPerSec)
        {
            if (_body == null)
                return;

            float dt = Time.fixedDeltaTime;
            float delta = Mathf.DeltaAngle(_body.rotation, targetDegrees);
            if (Mathf.Abs(delta) <= 0.05f)
            {
                SnapRotation(targetDegrees);
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_driveMode == LollipopDriveMode.AngularVelocity && maxSpeedDegPerSec < float.PositiveInfinity)
            {
                _body.angularVelocity = Mathf.Clamp(delta / dt, -maxSpeedDegPerSec, maxSpeedDegPerSec);
                return;
            }
#endif
            _body.MoveRotation(targetDegrees);
        }

        public void SnapRotation(float degrees)
        {
            _body.rotation = degrees;
            _body.angularVelocity = 0f;
        }

        void EnsureBody()
        {
            if (_body != null)
                return;

            _body = GetComponent<Rigidbody2D>();
            if (_body == null)
                _body = gameObject.AddComponent<Rigidbody2D>();

            _body.bodyType = RigidbodyType2D.Kinematic;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _body.useFullKinematicContacts = true;
            _body.gravityScale = 0f;
        }

        void BuildColliders()
        {
            if (_collidersReady || _poses == null || _poses.Length == 0)
                return;

            PointerPose pose = ActivePose;
            if (_visual == null)
            {
                var visualGo = new GameObject("Visual");
                visualGo.transform.SetParent(transform, false);
                _visual = visualGo.transform;
                _renderer = visualGo.AddComponent<SpriteRenderer>();
            }

            _visual.localPosition = Vector3.zero;
            _visual.localRotation = Quaternion.Euler(0f, 0f, pose.VisualLocalZ);
            _renderer.sprite = pose.Sprite;
            _renderer.color = Color.white;
            _renderer.sortingLayerName = "World";
            _renderer.sortingOrder = 1;
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader != null)
                _renderer.sharedMaterial = new Material(shader);

            if (_head == null)
                _head = gameObject.AddComponent<CircleCollider2D>();
            _head.radius = pose.HeadRadiusUnits;
            _head.offset = Vector2.zero;

            if (_stick == null)
                _stick = gameObject.AddComponent<CapsuleCollider2D>();
            float overlap = 0.02f;
            float length = Mathf.Max(0.05f, pose.StickLengthUnits - pose.HeadRadiusUnits + overlap);
            _stick.direction = CapsuleDirection2D.Horizontal;
            _stick.size = new Vector2(length, pose.StickThicknessUnits);
            _stick.offset = new Vector2(pose.HeadRadiusUnits - overlap + length * 0.5f, 0f);

            _collidersReady = true;
        }

        void ApplyStickMaterial()
        {
            if (_stick == null || _config == null)
                return;

            if (_stickMaterial == null)
                _stickMaterial = new PhysicsMaterial2D("StickFriction");

            _stickMaterial.friction = _config.StickFriction;
            _stickMaterial.bounciness = 0f;
            _stick.sharedMaterial = _stickMaterial;
            if (_head != null)
                _head.sharedMaterial = _stickMaterial;
        }
    }
}
