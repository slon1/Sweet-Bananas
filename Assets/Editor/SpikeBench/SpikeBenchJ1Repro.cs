namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchJ1Repro
    {
        [UnityEditor.MenuItem("Sweet Bananas/SpikeBench/J1 — reproducibility (10×2×2)")]
        public static void RunFromMenu()
        {
            if (SpikeBenchSession.IsRunning)
                return;
            SpikeBenchSession.StartJ1ReproSeries();
        }
    }
}