using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SweetBananas.Editor.SpikeBench
{
    public readonly struct BenchThrowPlan
    {
        public readonly int Index;
        public readonly bool IsHold;
        public readonly float SpawnFraction;
        public readonly float PressDuration;
        public readonly bool FollowUpTapAfterRest;

        public BenchThrowPlan(int index, bool isHold, float spawnFraction, float pressDuration, bool followUpTapAfterRest)
        {
            Index = index;
            IsHold = isHold;
            SpawnFraction = spawnFraction;
            PressDuration = pressDuration;
            FollowUpTapAfterRest = followUpTapAfterRest;
        }
    }

    public static class SpikeBenchPlanBuilder
    {
        public const float FollowUpTapDurationSec = 0.08f;
        public const int VerifyTapPicks = 8;
        public const int VerifyHoldPicks = 2;

        public static List<BenchThrowPlan> BuildSeriesPlans(int seed, int tapCount, int holdCount)
        {
            var taps = CycleGrid(seed, SpikeBenchMethodology.SpawnFractions, SpikeBenchMethodology.TapPressDurationsSec, tapCount, 101);
            var holds = CycleGrid(seed + 17, SpikeBenchMethodology.SpawnFractions, SpikeBenchMethodology.HoldPressDurationsSec, holdCount, 202);
            var plans = new List<BenchThrowPlan>(tapCount + holdCount);
            int index = 0;
            for (int i = 0; i < taps.Count; i++)
            {
                plans.Add(new BenchThrowPlan(index++, false, taps[i].spawn, taps[i].duration, false));
            }

            int holdOrdinal = 0;
            for (int i = 0; i < holds.Count; i++)
            {
                holdOrdinal++;
                bool followUp = holdOrdinal % 2 == 0;
                plans.Add(new BenchThrowPlan(index++, true, holds[i].spawn, holds[i].duration, followUp));
            }

            return plans;
        }

        public static string DescribeGridOrder(int seed, int tapCount, int holdCount)
        {
            var taps = BuildShuffledGrid(seed, SpikeBenchMethodology.SpawnFractions, SpikeBenchMethodology.TapPressDurationsSec);
            var holds = BuildShuffledGrid(seed + 17, SpikeBenchMethodology.SpawnFractions, SpikeBenchMethodology.HoldPressDurationsSec);
            return "tap_grid_cells=" + taps.Count + ", hold_grid_cells=" + holds.Count + ", tap_series=" + tapCount + ", hold_series=" + holdCount;
        }

        static List<(float spawn, float duration)> CycleGrid(int seed, float[] spawns, float[] durations, int count, int salt)
        {
            var grid = BuildShuffledGrid(seed + salt, spawns, durations);
            var list = new List<(float, float)>(count);
            for (int i = 0; i < count; i++)
                list.Add(grid[i % grid.Count]);
            return list;
        }

        static List<(float spawn, float duration)> BuildShuffledGrid(int seed, float[] spawns, float[] durations)
        {
            var grid = new List<(float, float)>(spawns.Length * durations.Length);
            for (int s = 0; s < spawns.Length; s++)
            {
                for (int d = 0; d < durations.Length; d++)
                    grid.Add((spawns[s], durations[d]));
            }

            var rng = new Random(seed);
            for (int i = grid.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (float, float) tmp = grid[i];
                grid[i] = grid[j];
                grid[j] = tmp;
            }

            return grid;
        }

        public static List<BenchThrowPlan> PickVerifyPlans(List<BenchThrowPlan> mainPlans)
        {
            var tapIndices = new List<int>();
            var holdIndices = new List<int>();
            for (int i = 0; i < mainPlans.Count; i++)
            {
                if (mainPlans[i].IsHold)
                    holdIndices.Add(i);
                else
                    tapIndices.Add(i);
            }

            var picked = new List<int>();
            AppendEvenly(picked, tapIndices, VerifyTapPicks);
            AppendEvenly(picked, holdIndices, VerifyHoldPicks);
            picked.Sort();
            return picked.Select(i => mainPlans[i]).ToList();
        }

        static void AppendEvenly(List<int> dest, List<int> source, int picks)
        {
            if (source.Count == 0 || picks <= 0)
                return;
            picks = Math.Min(picks, source.Count);
            for (int k = 0; k < picks; k++)
            {
                int srcIndex = picks == 1 ? 0 : k * (source.Count - 1) / (picks - 1);
                dest.Add(source[srcIndex]);
            }
        }
    }
}