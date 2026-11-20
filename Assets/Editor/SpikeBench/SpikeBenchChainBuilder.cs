using System.Collections.Generic;
using System.Globalization;
using SweetBananas.Data;
using SweetBananas.Game;
using UnityEngine;

namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchChainBuilder
    {
        public struct BuildOptions
        {
            public float GravityScale;
            public bool RestoreTorque;
            public bool KinematicStick;
            public bool PlaceAlongStickStyle;
            public string Label;

            public static BuildOptions IsolationBaseline =>
                new()
                {
                    GravityScale = 0f,
                    RestoreTorque = false,
                    KinematicStick = false,
                    PlaceAlongStickStyle = false,
                    Label = "baseline"
                };
        }

        public sealed class ChainData
        {
            public readonly float SegmentLength;
            public readonly string OptionsLabel;
            public readonly List<Rigidbody2D> Bodies = new();
            public readonly List<CapsuleCollider2D> Colliders = new();
            public readonly List<HingeJoint2D> Joints = new();
            public readonly GameObject Root;
            public GameConfig Config;

            public ChainData(float segmentLength, string label, GameObject root)
            {
                SegmentLength = segmentLength;
                OptionsLabel = label;
                Root = root;
            }

            public void ApplyRestoreTorque()
            {
                if (Config == null)
                    return;
                float k = Config.RestoreK;
                float c = Config.RestoreC;
                for (int i = 0; i < Joints.Count; i++)
                {
                    HingeJoint2D joint = Joints[i];
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
        }

        public static ChainData Build(GameConfig config, BuildOptions options)
        {
            var root = new GameObject("SpikeBenchIsolation_" + options.Label);
            int count = Mathf.Max(2, config.SegmentCount);
            float ribbonLength = RibbonMesh.OpaqueLengthUnits;
            float segmentLength = ribbonLength / count;
            var chainFixed = new ChainData(segmentLength, options.Label, root);
            chainFixed.Config = config;

            var material = new PhysicsMaterial2D("IsolationRibbon");
            material.friction = config.RibbonFriction;
            material.bounciness = config.RibbonBounciness;

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Segment" + i);
                go.transform.SetParent(root.transform, false);
                var body = go.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Dynamic;
                body.mass = config.SegmentMass;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.linearDamping = config.RibbonLinearDamping;
                body.angularDamping = config.RibbonAngularDamping;
                body.gravityScale = options.GravityScale;
                body.sleepMode = RigidbodySleepMode2D.NeverSleep;
                body.position = root.transform.TransformPoint(new Vector3(i * segmentLength, 0f, 0f));
                body.rotation = 0f;

                var capsule = go.AddComponent<CapsuleCollider2D>();
                capsule.direction = CapsuleDirection2D.Horizontal;
                capsule.size = new Vector2(segmentLength, config.RibbonColliderThickness);
                capsule.sharedMaterial = material;

                chainFixed.Bodies.Add(body);
                chainFixed.Colliders.Add(capsule);
            }

            for (int i = 1; i < count; i++)
            {
                var joint = chainFixed.Bodies[i].gameObject.AddComponent<HingeJoint2D>();
                joint.autoConfigureConnectedAnchor = false;
                joint.anchor = new Vector2(-segmentLength * 0.5f, 0f);
                joint.connectedAnchor = new Vector2(segmentLength * 0.5f, 0f);
                joint.connectedBody = chainFixed.Bodies[i - 1];
                joint.useLimits = true;
                joint.limits = new JointAngleLimits2D
                {
                    min = -config.JointLimitDeg,
                    max = config.JointLimitDeg
                };
                joint.enableCollision = false;
                chainFixed.Joints.Add(joint);
            }

            for (int i = 0; i < chainFixed.Colliders.Count; i++)
            {
                for (int j = i + 1; j < chainFixed.Colliders.Count; j++)
                    Physics2D.IgnoreCollision(chainFixed.Colliders[i], chainFixed.Colliders[j], true);
            }

            if (options.KinematicStick)
            {
                var stick = new GameObject("KinematicStickProbe");
                stick.transform.SetParent(root.transform, false);
                var rb = stick.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
                var cap = stick.AddComponent<CapsuleCollider2D>();
                cap.size = new Vector2(4f, 0.3f);
                cap.direction = CapsuleDirection2D.Horizontal;
                rb.position = new Vector2(2f, 1f);
                rb.rotation = -25f;
            }

            if (options.PlaceAlongStickStyle)
            {
                PlaceStraight(chainFixed, new Vector2(2f, 2f), -25f);
            }

            Physics2D.SyncTransforms();
            return chainFixed;
        }

        static void PlaceStraight(ChainData chain, Vector2 center, float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            float total = chain.SegmentLength * chain.Bodies.Count;
            Vector2 first = center - dir * (total * 0.5f - chain.SegmentLength * 0.5f);
            for (int i = 0; i < chain.Bodies.Count; i++)
            {
                Rigidbody2D body = chain.Bodies[i];
                body.position = first + dir * (chain.SegmentLength * i);
                body.rotation = angleDeg;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.WakeUp();
            }

            Physics2D.SyncTransforms();
        }

        public static void DestroyChain(ChainData chain)
        {
            if (chain?.Root != null)
                Object.Destroy(chain.Root);
        }
    }
}