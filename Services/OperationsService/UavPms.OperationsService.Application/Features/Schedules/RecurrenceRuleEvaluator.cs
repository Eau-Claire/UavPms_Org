using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UavPms.OperationsService.Domain.Entities;

namespace UavPms.OperationsService.Application.Features.Schedules;

/// <summary>
/// Validates and evaluates simplified iCalendar RRULE strings (FREQ=DAILY|WEEKLY|MONTHLY|YEARLY).
/// </summary>
public static class RecurrenceRuleEvaluator
{
    private static readonly HashSet<string> ValidFrequencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "DAILY", "WEEKLY", "MONTHLY", "YEARLY"
    };

    private static readonly Dictionary<string, DayOfWeek> DayMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SU"] = DayOfWeek.Sunday,
        ["MO"] = DayOfWeek.Monday,
        ["TU"] = DayOfWeek.Tuesday,
        ["WE"] = DayOfWeek.Wednesday,
        ["TH"] = DayOfWeek.Thursday,
        ["FR"] = DayOfWeek.Friday,
        ["SA"] = DayOfWeek.Saturday
    };

    public static bool TryValidate(string? recurrenceRule, out string? error)
    {
        if (string.IsNullOrWhiteSpace(recurrenceRule))
        {
            error = "RecurrenceRule is required.";
            return false;
        }

        var raw = recurrenceRule.Trim();
        if (raw.StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase))
        {
            raw = raw[6..];
        }

        var parts = ParseParts(raw);
        if (!parts.TryGetValue("FREQ", out var freq) || !ValidFrequencies.Contains(freq))
        {
            error = "RecurrenceRule must contain a valid FREQ (DAILY, WEEKLY, MONTHLY, or YEARLY).";
            return false;
        }

        if (parts.TryGetValue("INTERVAL", out var intervalStr) &&
            (!int.TryParse(intervalStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var interval) || interval < 1))
        {
            error = "RecurrenceRule INTERVAL must be an integer >= 1.";
            return false;
        }

        if (parts.TryGetValue("BYDAY", out var byDayStr))
        {
            var days = byDayStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (days.Length == 0 || days.Any(d => !DayMap.ContainsKey(d)))
            {
                error = "RecurrenceRule BYDAY must contain valid weekday codes (SU, MO, TU, WE, TH, FR, SA).";
                return false;
            }
        }

        if (parts.TryGetValue("BYMONTHDAY", out var byMonthDayStr))
        {
            var mdParts = byMonthDayStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (mdParts.Length == 0 || mdParts.Any(p => !int.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) || d < 1 || d > 31))
            {
                error = "RecurrenceRule BYMONTHDAY must contain integers in [1, 31].";
                return false;
            }
        }

        error = null;
        return true;
    }

    public static bool IsDueAt(InspectionSchedule schedule, DateTime utcNow, DateTime? lastTriggeredAt)
    {
        if (!schedule.IsActive || schedule.IsDeleted)
            return false;

        if (!TryValidate(schedule.RecurrenceRule, out _))
            return false;

        if (lastTriggeredAt.HasValue && lastTriggeredAt.Value.Date >= utcNow.Date)
            return false;

        var plannedTod = schedule.PlannedTime.ToTimeSpan();
        var nowTod = utcNow.TimeOfDay;
        if (nowTod < plannedTod || nowTod - plannedTod > TimeSpan.FromMinutes(15))
            return false;

        var raw = schedule.RecurrenceRule.Trim();
        if (raw.StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase))
            raw = raw[6..];

        var parts = ParseParts(raw);
        var freq = parts["FREQ"].ToUpperInvariant();
        var interval = parts.TryGetValue("INTERVAL", out var intervalStr) &&
                       int.TryParse(intervalStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInterval)
            ? Math.Max(1, parsedInterval)
            : 1;

        var anchorDate = schedule.CreatedAt == default ? utcNow.Date : schedule.CreatedAt.Date;
        var today = utcNow.Date;
        if (today < anchorDate)
            return false;

        var matchesFreq = freq switch
        {
            "DAILY" => ((int)(today - anchorDate).TotalDays) % interval == 0,
            "WEEKLY" => MatchesWeekly(parts, anchorDate, today, interval),
            "MONTHLY" => MatchesMonthly(parts, anchorDate, today, interval),
            "YEARLY" => MatchesYearly(anchorDate, today, interval),
            _ => false
        };

        return matchesFreq;
    }

    private static bool MatchesWeekly(Dictionary<string, string> parts, DateTime anchorDate, DateTime today, int interval)
    {
        var weeksDiff = (int)((today - anchorDate).TotalDays / 7);
        if (weeksDiff % interval != 0)
            return false;

        if (parts.TryGetValue("BYDAY", out var byDayStr))
        {
            var targetDays = byDayStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(d => DayMap[d])
                .ToHashSet();
            return targetDays.Contains(today.DayOfWeek);
        }

        return today.DayOfWeek == anchorDate.DayOfWeek;
    }

    private static bool MatchesMonthly(Dictionary<string, string> parts, DateTime anchorDate, DateTime today, int interval)
    {
        var monthsDiff = ((today.Year - anchorDate.Year) * 12) + (today.Month - anchorDate.Month);
        if (monthsDiff < 0 || monthsDiff % interval != 0)
            return false;

        if (parts.TryGetValue("BYMONTHDAY", out var byMonthDayStr))
        {
            var targetDays = byMonthDayStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(p => int.Parse(p, CultureInfo.InvariantCulture))
                .ToHashSet();
            return targetDays.Contains(today.Day);
        }

        return today.Day == Math.Min(anchorDate.Day, DateTime.DaysInMonth(today.Year, today.Month));
    }

    private static bool MatchesYearly(DateTime anchorDate, DateTime today, int interval)
    {
        var yearsDiff = today.Year - anchorDate.Year;
        if (yearsDiff < 0 || yearsDiff % interval != 0)
            return false;

        return today.Month == anchorDate.Month &&
               today.Day == Math.Min(anchorDate.Day, DateTime.DaysInMonth(today.Year, today.Month));
    }

    private static Dictionary<string, string> ParseParts(string raw)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eqIndex = token.IndexOf('=');
            if (eqIndex <= 0 || eqIndex == token.Length - 1) continue;
            var key = token[..eqIndex].Trim();
            var val = token[(eqIndex + 1)..].Trim();
            result[key] = val;
        }
        return result;
    }
}

