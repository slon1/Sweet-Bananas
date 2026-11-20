using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchD1AnalysisWriter
    {
        public static void WriteFromDump()
        {
            string dump = SpikeBenchJointDump.FullDumpPath();
            if (!File.Exists(dump))
                return;

            string text = File.ReadAllText(dump);
            var slackRegex = new Regex(@"joint_index=(\d+)\s+.*?slack=([\d.]+)", RegexOptions.Singleline);
            var matches = slackRegex.Matches(text);
            var sb = new StringBuilder();
            sb.AppendLine("Per-joint slack (from c0_joint_dump.txt):");
            float max = 0f;
            for (int i = 0; i < matches.Count; i++)
            {
                string idx = matches[i].Groups[1].Value;
                float slack = float.Parse(matches[i].Groups[2].Value, CultureInfo.InvariantCulture);
                if (slack > max)
                    max = slack;
                sb.AppendLine("hinge " + idx + " slack=" + slack.ToString(CultureInfo.InvariantCulture));
            }

            sb.AppendLine("max_slack=" + max.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine();
            sb.AppendLine("Conclusion (D1):");
            if (matches.Count > 0 && max > 0.4f && max < 0.5f)
                sb.AppendLine("- Uniform slack ~0.42 (≈L) on all joints at step0 → check left-left anchor pairing (A15).");
            if (max > 0.8f)
                sb.AppendLine("- Uniform slack ~0.85 (≈2L) on X only → segment center vs GetPoint/anchor mismatch or wrong center spacing at Build.");
            sb.AppendLine("- Isolation baseline step0 without gravity/restore/stick: if slack≠0, not PlaceAlongStick.");
            File.WriteAllText(Path.Combine(SpikeBenchMethodology.LogDirectory(), "d1_analysis.txt"), sb.ToString());
        }
    }
}