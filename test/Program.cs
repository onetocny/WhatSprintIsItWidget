using System;
using WhatSprintIsItWidget;

int failures = 0;

void Check(string label, DateTime instant, int expectedSprint, int expectedWeek)
{
    var info = SprintCalculator.Get(instant);
    bool ok = info.Sprint == expectedSprint && info.Week == expectedWeek;
    Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] {label}: {instant:yyyy-MM-dd} => sprint {info.Sprint} week {info.Week} (expected sprint {expectedSprint} week {expectedWeek})");
    if (!ok)
    {
        failures++;
    }
}

// Golden values taken directly from https://whatsprintis.it/ (og:description + rendered card).
Check("Today", new DateTime(2026, 7, 14, 9, 55, 0, DateTimeKind.Utc), 277, 3);

// Epoch itself is sprint 0 week 1.
Check("Epoch", new DateTime(2010, 7, 24, 0, 0, 0, DateTimeKind.Utc), 0, 1);

// One week after epoch => sprint 0 week 2.
Check("Epoch + 1w", new DateTime(2010, 7, 31, 0, 0, 0, DateTimeKind.Utc), 0, 2);

// Three weeks after epoch => sprint 1 week 1.
Check("Epoch + 3w", new DateTime(2010, 8, 14, 0, 0, 0, DateTimeKind.Utc), 1, 1);

Console.WriteLine();
Console.WriteLine(failures == 0 ? "All checks passed." : $"{failures} check(s) failed.");
Environment.Exit(failures == 0 ? 0 : 1);
