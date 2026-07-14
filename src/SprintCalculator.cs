using System;

namespace WhatSprintIsItWidget
{
    /// <summary>
    /// Computes the Azure DevOps sprint and week for a given instant.
    /// Ported from the algorithm used by https://whatsprintis.it/ so the widget
    /// works entirely offline (no dependency on the web page being reachable).
    ///
    /// Reference algorithm:
    ///   epoch          = 2010-07-24 (UTC)
    ///   weeksPerSprint = 3
    ///   weeksSinceEpoch = floor((now - epoch) / 1 week)
    ///   sprint          = floor(weeksSinceEpoch / weeksPerSprint)
    ///   week            = (weeksSinceEpoch % weeksPerSprint) + 1
    /// </summary>
    public static class SprintCalculator
    {
        // 2010-07-24T00:00:00Z, matching Date.UTC(2010, 6, 24) in the source page.
        private static readonly DateTime Epoch = new DateTime(2010, 7, 24, 0, 0, 0, DateTimeKind.Utc);

        private const int WeeksPerSprint = 3;

        public readonly struct SprintInfo
        {
            public SprintInfo(int sprint, int week)
            {
                Sprint = sprint;
                Week = week;
            }

            public int Sprint { get; }

            public int Week { get; }

            public override string ToString() => $"sprint {Sprint} week {Week}";
        }

        /// <summary>Returns the current Azure DevOps sprint and week (UTC-based).</summary>
        public static SprintInfo GetCurrent() => Get(DateTime.UtcNow);

        /// <summary>Returns the Azure DevOps sprint and week for the supplied instant.</summary>
        public static SprintInfo Get(DateTime instant)
        {
            DateTime utc = instant.Kind == DateTimeKind.Utc ? instant : instant.ToUniversalTime();

            // Floor division on whole weeks since the epoch.
            long weeksSinceEpoch = (long)Math.Floor((utc - Epoch).TotalDays / 7.0);

            int sprint = (int)FloorDiv(weeksSinceEpoch, WeeksPerSprint);
            int week = (int)Mod(weeksSinceEpoch, WeeksPerSprint) + 1;

            return new SprintInfo(sprint, week);
        }

        private static long FloorDiv(long a, long b) => (long)Math.Floor((double)a / b);

        private static long Mod(long a, long b)
        {
            long r = a % b;
            return r < 0 ? r + b : r;
        }
    }
}
