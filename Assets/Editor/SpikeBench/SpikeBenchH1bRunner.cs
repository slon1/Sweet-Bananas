using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SweetBananas.Data;
using SweetBananas.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchH1bRunner
    {
        static readonly Dictionary<int, int> E0EventStep = new()
        {
            { 11, 33 }, { 18, 38 }, { 53, 33 }, { 95, 33 }, { 60, 38 }
        };

        static readonly int[] Indices = { 11, 18, 53, 95 };

        [MenuItem("Sweet Bananas/SpikeBench/K — H1b contact traces")]
        public static void RunFromMenu()
        {
            SpikeBenchH1Runner.StartH1b(SpikeBenchMethodology.DefaultSeed, 12f);
        }

        [MenuItem("Sweet Bananas/SpikeBench/K — H1b throw 60 @ 1x")]
        public static void Run60At1x()
        {
            var main = SpikeBenchPlanBuilder.BuildSeriesPlans(
                SpikeBenchMethodology.DefaultSeed,
                SpikeBenchMethodology.SeriesTaps,
                SpikeBenchMethodology.SeriesHolds);
            SpikeBenchSession.Start("H1b_60", SpikeBenchMethodology.DefaultSeed, 1f, 0, 0, false, null,
                SpikeBenchPlanBuilder.PickPlansByIndices(main, new[] { 60 }));
        }
    }
}