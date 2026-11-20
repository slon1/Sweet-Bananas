using System.Collections.Generic;
using SweetBananas.Data;
using UnityEngine;
using Zenject;

namespace SweetBananas.Game
{
    public sealed class Ribbon : MonoBehaviour
    {
        readonly List<Rigidbody2D> _bodies = new();
        readonly List<CapsuleCollider2D> _colliders = new();
        readonly List<HingeJoint2D> _joints = new();

        GameConfig _config;
        Lollipop _lollipop;
        ThrowController _throw;
        PhysicsMaterial2D _material;
        float _segmentLength;
        bool _built;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameConfig _benchConfigOverride;
        GameConfig ActiveConfig => _benchConfigOverride != null ? _benchConfigOverride : _config;
#else
        GameConfig ActiveConfig => _config;
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public float LastBuildSlack { get; private set; }
        public float LastSpawnLift { get; private set; }
#endif

        const float SpawnClearanceUnits = 0.01f;
        const float SpawnLiftStepUnits = 0.02f;
        const int SpawnLiftMaxIterations = 200;

        public IReadOnlyList<Rigidbody2D> Bodies => _bodies;
        public IReadOnlyList<CapsuleCollider2D> Colliders => _colliders;
        public IReadOnlyList<HingeJoint2D> Joints => _joints;
        public float SegmentLength => _segmentLength;

        public Vector2 Center
        {
            get
            {
                if (_bodies.Count == 0)
                    return transform.position;
                Vector2 sum = Vector2.zero;
                for (int i = 0; i < _bodies.Count; i++)
                    sum += _bodies[i].position;
                return sum / _bodies.Count;
            }
        }

        [Inject]
        public void Construct(GameConfig config, Lollipop lollipop, ThrowController throwController)
        {
            _config = config;
            _lollipop = lollipop;
            _throw = throwController;
        }

        void Start()
        {
            Build();
            ResetToSpawn();
        }

        public void ResetToSpawn(float? fraction = null)
        {
            if (!_built)
                Build();
            if (_throw != null)
                _throw.SnapRest();
            PlaceAlongStick(fraction ?? ActiveConfig.RibbonSpawnFraction);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ValidateHingeAnchors();
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public float MaxHingeSlack()
        {
            float maxSlack = 0f;
            for (int i = 0; i < _joints.Count; i++)
            {
                HingeJoint2D joint = _joints[i];
                Rigidbody2D bodyA = joint.attachedRigidbody;
                Rigidbody2D bodyB = joint.connectedBody;
                if (bodyA == null || bodyB == null)
                    continue;

                Vector2 worldA = bodyA.GetRelativePoint(joint.anchor);
                Vector2 worldB = bodyB.GetRelativePoint(joint.connectedAnchor);
                float slack = (worldA - worldB).magnitude;
                maxSlack = Mathf.Max(maxSlack, slack);
            }

            return maxSlack;
        }

        public float ValidateHingeAnchors()
        {
            float maxSlack = MaxHingeSlack();
            for (int i = 0; i < _joints.Count; i++)
            {
                HingeJoint2D joint = _joints[i];
                Rigidbody2D bodyA = joint.attachedRigidbody;
                Rigidbody2D bodyB = joint.connectedBody;
                if (bodyA == null || bodyB == null)
                    continue;

                Vector2 worldA = bodyA.GetRelativePoint(joint.anchor);
                Vector2 worldB = bodyB.GetRelativePoint(joint.connectedAnchor);
                float slack = (worldA - worldB).magnitude;
                if (slack > 0.001f)
                    UnityEngine.Debug.LogError($"Ribbon hinge {i} anchor slack {slack:F6} (limit 0.001)");
            }

            return maxSlack;
        }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugPlace(Vector2 center, float angleDeg)
        {
            if (!_built)
                Build();
            PlaceStraight(center, angleDeg);
        }
#endif

        void FixedUpdate()
        {
            if (!_built)
                return;

            float k = ActiveConfig.RestoreK;
            float c = ActiveConfig.RestoreC;
            for (int i = 0; i < _joints.Count; i++)
            {
                HingeJoint2D joint = _joints[i];
                Rigidbody2D body = joint.attachedRigidbody;
                Rigidbody2D other = joint.connectedBody;
                if (body == null || other == null)
                    continue;

                float angleRad = joint.jointAngle * Mathf.Deg2Rad;
                float speedRad = joint.jointSpeed * Mathf.Deg2Rad;
                float tau = -k * angleRad - c * speedRad;
                body.AddTorque(tau, ForceMode2D.Force);
                other.AddTorque(-tau, ForceMode2D.Force);
            }
        }

        void Build()
        {
            if (_built || ActiveConfig == null)
                return;

            ClearSegments();
            int count = Mathf.Max(2, ActiveConfig.SegmentCount);
            float ribbonLength = RibbonMesh.OpaqueLengthUnits;
            _segmentLength = ribbonLength / count;
            float thickness = ActiveConfig.RibbonColliderThickness;

            if (_material == null)
                _material = new PhysicsMaterial2D("Ribbon");
            _material.friction = ActiveConfig.RibbonFriction;
            _material.bounciness = ActiveConfig.RibbonBounciness;

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Segment" + i);
                go.transform.SetParent(transform, false);
                var body = go.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Dynamic;
                body.mass = ActiveConfig.SegmentMass;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.linearDamping = ActiveConfig.RibbonLinearDamping;
                body.angularDamping = ActiveConfig.RibbonAngularDamping;
                body.gravityScale = 1f;
                body.sleepMode = RigidbodySleepMode2D.NeverSleep;
                body.position = transform.TransformPoint(new Vector3(i * _segmentLength, 0f, 0f));
                body.rotation = 0f;

                var capsule = go.AddComponent<CapsuleCollider2D>();
                capsule.direction = CapsuleDirection2D.Horizontal;
                capsule.size = new Vector2(_segmentLength, thickness);
                capsule.sharedMaterial = _material;

                _bodies.Add(body);
                _colliders.Add(capsule);
            }

            for (int i = 1; i < count; i++)
            {
                var joint = _bodies[i].gameObject.AddComponent<HingeJoint2D>();
                joint.autoConfigureConnectedAnchor = false;
                joint.anchor = new Vector2(-_segmentLength * 0.5f, 0f);
                joint.connectedAnchor = new Vector2(_segmentLength * 0.5f, 0f);
                joint.connectedBody = _bodies[i - 1];
                joint.useLimits = true;
                joint.limits = new JointAngleLimits2D
                {
                    min = -ActiveConfig.JointLimitDeg,
                    max = ActiveConfig.JointLimitDeg
                };
                joint.enableCollision = false;
                _joints.Add(joint);
            }

            for (int i = 0; i < _colliders.Count; i++)
            {
                for (int j = i + 1; j < _colliders.Count; j++)
                    Physics2D.IgnoreCollision(_colliders[i], _colliders[j], true);
            }

            Physics2D.SyncTransforms();
            _built = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LastBuildSlack = ValidateHingeAnchors();
#endif
        }

        void PlaceAlongStick(float fraction)
        {
            PointerPose pose = _lollipop.ActivePose;
            Vector2 pivot = _lollipop.Pivot;
            Vector2 dir = _lollipop.StickDirection();
            Vector2 up = _lollipop.StickUp();
            float stickSpan = Mathf.Max(0.01f, pose.StickLengthUnits - pose.HeadRadiusUnits);
            float along = pose.HeadRadiusUnits + fraction * stickSpan;
            float ribbonHalf = _config.RibbonColliderThickness * 0.5f;
            float stickHalf = pose.StickThicknessUnits * 0.5f;
            float perpBase = stickHalf + ribbonHalf + 0.02f;
            float angle = _lollipop.Angle;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LastSpawnLift = 0f;
#endif
            float extraLift = 0f;
            for (int iter = 0; iter <= SpawnLiftMaxIterations; iter++)
            {
                float perp = perpBase + extraLift;
                Vector2 center = pivot + dir * along + up * perp;
                PlaceStraight(center, angle);
                float minClear = MinLollipopClearanceDistance();
                if (minClear >= SpawnClearanceUnits)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    LastSpawnLift = extraLift;
#endif
                    return;
                }

                extraLift += SpawnLiftStepUnits;
            }

            UnityEngine.Debug.LogError(
                $"Ribbon spawn clearance failed: fraction={fraction:F4}, extraLift={extraLift:F2}, minDist={MinLollipopClearanceDistance():F4}");
        }

        float MinLollipopClearanceDistance()
        {
            if (_lollipop == null || _colliders.Count == 0)
                return float.PositiveInfinity;

            Physics2D.SyncTransforms();

            Collider2D head = _lollipop.HeadCollider;
            Collider2D stick = _lollipop.StickCollider;
            float min = float.PositiveInfinity;
            for (int i = 0; i < _colliders.Count; i++)
            {
                CapsuleCollider2D segment = _colliders[i];
                if (segment == null)
                    continue;
                if (head != null)
                    min = Mathf.Min(min, Physics2D.Distance(segment, head).distance);
                if (stick != null)
                    min = Mathf.Min(min, Physics2D.Distance(segment, stick).distance);
            }

            return min;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public float MinSpawnClearanceDistance() => MinLollipopClearanceDistance();

        public void DebugSetConfigOverride(GameConfig config) => _benchConfigOverride = config;

        public void DebugRebuild()
        {
            _built = false;
            ClearSegments();
            Build();
            ResetToSpawn();
        }
#endif

        void PlaceStraight(Vector2 center, float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            float total = _segmentLength * _bodies.Count;
            Vector2 first = center - dir * (total * 0.5f - _segmentLength * 0.5f);
            for (int i = 0; i < _bodies.Count; i++)
            {
                Rigidbody2D body = _bodies[i];
                Vector2 pos = first + dir * (_segmentLength * i);
                body.position = pos;
                body.rotation = angleDeg;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.WakeUp();
            }

            Physics2D.SyncTransforms();
        }

        void ClearSegments()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying)
                    Destroy(transform.GetChild(i).gameObject);
                else
                    DestroyImmediate(transform.GetChild(i).gameObject);
            }

            _bodies.Clear();
            _colliders.Clear();
            _joints.Clear();
        }
    }
}
