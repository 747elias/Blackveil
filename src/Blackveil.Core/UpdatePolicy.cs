namespace Blackveil.Core;

public enum CheckSchedule { Startup, EverySixHours, Daily, Weekly }
public enum InstallSchedule { Immediately, NextStartup, DailyAtTime }
public sealed record UpdatePreferences
{
    public bool AutoCheck { get; init; } = true;
    public bool AutoInstall { get; init; }
    public CheckSchedule CheckWhen { get; init; } = CheckSchedule.Startup;
    public InstallSchedule InstallWhen { get; init; } = InstallSchedule.Immediately;
    public int CheckMinute { get; init; } = 18 * 60;
    public int InstallMinute { get; init; } = 20 * 60;
    public bool IsValid => Enum.IsDefined(CheckWhen) && Enum.IsDefined(InstallWhen) &&
        CheckMinute is >= 0 and < 1440 && InstallMinute is >= 0 and < 1440;
}
public static class UpdatePolicy
{
    public static TimeSpan DelayUntil(DateTimeOffset due, DateTimeOffset now) => due > now ? due - now : TimeSpan.FromSeconds(1);
    public static DateTimeOffset AtNextLocalTime(DateTimeOffset now, int minute)
    {
        DateTime day = now.LocalDateTime.Date.AddMinutes(minute);
        if (day <= now.LocalDateTime) day = day.AddDays(1);
        day = DateTime.SpecifyKind(day, DateTimeKind.Unspecified);
        while (TimeZoneInfo.Local.IsInvalidTime(day)) day = day.AddMinutes(30);
        return new DateTimeOffset(day, TimeZoneInfo.Local.GetUtcOffset(day));
    }
    public static DateTimeOffset? NextCheck(UpdatePreferences prefs, DateTimeOffset? last, DateTimeOffset now)
    {
        if (!prefs.AutoCheck || prefs.CheckWhen == CheckSchedule.Startup) return null;
        if (prefs.CheckWhen == CheckSchedule.Daily) return AtNextLocalTime(now, prefs.CheckMinute);
        return (last ?? now) + (prefs.CheckWhen == CheckSchedule.EverySixHours ? TimeSpan.FromHours(6) : TimeSpan.FromDays(7));
    }
    public static bool ShouldInstall(UpdatePreferences prefs, bool manual, bool startup, bool scheduledInstall) =>
        manual || prefs.AutoInstall && (prefs.InstallWhen == InstallSchedule.Immediately ||
            prefs.InstallWhen == InstallSchedule.NextStartup && startup ||
            prefs.InstallWhen == InstallSchedule.DailyAtTime && scheduledInstall);
    public static Version? StableVersion(string tag)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(tag, @"^v\d+\.\d+\.\d+$")) return null;
        return Version.TryParse(tag[1..], out var version) ? version : null;
    }
}
