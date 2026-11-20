using System.Globalization;
using System.IO;
using System.Text;
using SweetBananas.Game;
using UnityEngine;

namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchJointDump
    {
        public const string DumpPath = "c0_joint_dump.txt";

        public static string FullDumpPath()
        {
            return Path.Combine(SpikeBenchMethodology.LogDirectory(), DumpPath);
        }

        public static void AppendBanner(string phase)
        {
            SpikeBenchMethodology.EnsureLogDirectory();
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("===== " + phase + " =====");
            sb.AppendLine("time=" + Time.time.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("fixedTime=" + Time.fixedTime.ToString(CultureInfo.InvariantCulture));
            File.AppendAllText(FullDumpPath(), sb.ToString());
        }

        public static void WriteRibbon(Ribbon ribbon, string phase)
        {
            if (ribbon == null)
                return;

            AppendBanner(phase);
            var sb = new StringBuilder(4096);
            sb.AppendLine("segmentLength=" + ribbon.SegmentLength.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("ribbon_lossyScale=" + ribbon.transform.lossyScale);
            sb.AppendLine("ribbon_position=" + ribbon.transform.position);
            sb.AppendLine("ribbon_rotation=" + ribbon.transform.rotation.eulerAngles);

            var joints = ribbon.Joints;
            var bodies = ribbon.Bodies;
            var colliders = ribbon.Colliders;
            for (int i = 0; i < joints.Count; i++)
            {
                HingeJoint2D joint = joints[i];
                Rigidbody2D bodyA = joint.attachedRigidbody;
                Rigidbody2D bodyB = joint.connectedBody;
                if (bodyA == null || bodyB == null)
                    continue;

                Vector2 worldA = bodyA.GetRelativePoint(joint.anchor);
                Vector2 worldB = bodyB.GetRelativePoint(joint.connectedAnchor);
                Vector2 delta = worldA - worldB;
                float slack = delta.magnitude;

                sb.AppendLine("joint_index=" + i);
                sb.AppendLine("  autoConfigureConnectedAnchor=" + joint.autoConfigureConnectedAnchor);
                sb.AppendLine("  anchor=" + joint.anchor);
                sb.AppendLine("  connectedAnchor=" + joint.connectedAnchor);
                sb.AppendLine("  bodyA=" + bodyA.name + " pos=" + bodyA.position + " rot=" + bodyA.rotation);
                sb.AppendLine("  bodyB=" + bodyB.name + " pos=" + bodyB.position + " rot=" + bodyB.rotation);
                sb.AppendLine("  worldAnchorA=" + worldA);
                sb.AppendLine("  worldAnchorB=" + worldB);
                sb.AppendLine("  delta=" + delta);
                sb.AppendLine("  slack=" + slack.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("  delta_x=" + delta.x.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("  delta_y=" + delta.y.ToString(CultureInfo.InvariantCulture));

                int bodyIndexA = -1;
                for (int b = 0; b < bodies.Count; b++)
                {
                    if (bodies[b] == bodyA)
                    {
                        bodyIndexA = b;
                        break;
                    }
                }

                if (bodyIndexA >= 0 && bodyIndexA < colliders.Count)
                {
                    CapsuleCollider2D cap = colliders[bodyIndexA];
                    sb.AppendLine("  capsule_size=" + cap.size);
                    sb.AppendLine("  segment_lossyScale=" + bodyA.transform.lossyScale);
                }
            }

            File.AppendAllText(FullDumpPath(), sb.ToString());
        }

        public static void WriteIsolatedChain(SpikeBenchChainBuilder.ChainData chain, string phase)
        {
            AppendBanner(phase);
            var sb = new StringBuilder(4096);
            sb.AppendLine("segmentLength=" + chain.SegmentLength.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("options=" + chain.OptionsLabel);

            for (int i = 0; i < chain.Joints.Count; i++)
            {
                HingeJoint2D joint = chain.Joints[i];
                Rigidbody2D bodyA = joint.attachedRigidbody;
                Rigidbody2D bodyB = joint.connectedBody;
                Vector2 worldA = bodyA.GetRelativePoint(joint.anchor);
                Vector2 worldB = bodyB.GetRelativePoint(joint.connectedAnchor);
                Vector2 delta = worldA - worldB;
                float slack = delta.magnitude;
                sb.AppendLine("joint_index=" + i);
                sb.AppendLine("  autoConfigureConnectedAnchor=" + joint.autoConfigureConnectedAnchor);
                sb.AppendLine("  anchor=" + joint.anchor);
                sb.AppendLine("  connectedAnchor=" + joint.connectedAnchor);
                sb.AppendLine("  bodyA=" + bodyA.name + " pos=" + bodyA.position + " rot=" + bodyA.rotation);
                sb.AppendLine("  bodyB=" + bodyB.name + " pos=" + bodyB.position + " rot=" + bodyB.rotation);
                sb.AppendLine("  worldAnchorA=" + worldA);
                sb.AppendLine("  worldAnchorB=" + worldB);
                sb.AppendLine("  delta=" + delta);
                sb.AppendLine("  slack=" + slack.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("  delta_x=" + delta.x.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("  delta_y=" + delta.y.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("  capsule_size=" + chain.Colliders[i + 1].size);
            }

            File.AppendAllText(FullDumpPath(), sb.ToString());
        }

        public static void ResetFile()
        {
            SpikeBenchMethodology.EnsureLogDirectory();
            File.WriteAllText(FullDumpPath(), "TASK-002-A2 D1 joint dump\n");
        }

        public static void WriteFirstTwoHinges(Ribbon ribbon, string phase)
        {
            if (ribbon == null)
                return;

            AppendBanner(phase);
            var sb = new StringBuilder(1024);
            var joints = ribbon.Joints;
            int count = Mathf.Min(2, joints.Count);
            for (int i = 0; i < count; i++)
            {
                HingeJoint2D joint = joints[i];
                Rigidbody2D bodyA = joint.attachedRigidbody;
                Rigidbody2D bodyB = joint.connectedBody;
                Vector2 worldA = bodyA.GetRelativePoint(joint.anchor);
                Vector2 worldB = bodyB.GetRelativePoint(joint.connectedAnchor);
                Vector2 delta = worldA - worldB;
                sb.AppendLine("joint_index=" + i + " slack=" + delta.magnitude.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("  worldAnchorA=" + worldA + " worldAnchorB=" + worldB);
            }

            File.AppendAllText(FullDumpPath(), sb.ToString());
        }
    }
}