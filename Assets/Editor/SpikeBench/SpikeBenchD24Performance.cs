using System.Globalization;
using System.IO;
using System.Text;

namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchD24Performance
    {
        public static void Flush(double nsSum, int samples, long meshAllocMax)
        {
            SpikeBenchMethodology.EnsureLogDirectory();
            string path = Path.Combine(SpikeBenchMethodology.LogDirectory(), "d24_7_profile.txt");
            double avgMs = samples > 0 ? nsSum / samples / 1e6 : 0;
            var sb = new StringBuilder();
            sb.AppendLine("method=ProfilerRecorder Physics2D.Simulate per FixedUpdate step during FINAL_J25_verify1x");
            sb.AppendLine("samples=" + samples);
            sb.AppendLine("physics_step_avg_ms=" + avgMs.ToString("F4", CultureInfo.InvariantCulture));
            sb.AppendLine("ribbon_mesh_last_alloc_max_bytes=" + meshAllocMax);
            sb.AppendLine("threshold_physics_ms=1");
            sb.AppendLine("threshold_mesh_alloc=0");
            File.WriteAllText(path, sb.ToString());
        }
    }
}