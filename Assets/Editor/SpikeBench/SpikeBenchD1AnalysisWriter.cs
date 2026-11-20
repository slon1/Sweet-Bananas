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
            File.WriteAllText(Path.Combine(SpikeBenchMethodology.LogDirectory(), "d1_analysis.txt"), sb.ToString());
        }
    }
}