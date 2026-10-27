using Blackveil.Core;
using Xunit;
namespace Blackveil.Core.Tests;
public sealed class UpdatePolicyTests
{
    [Theory]
    [InlineData("v2.1.0", true)] [InlineData("v2.1.1-beta", false)] [InlineData("2.1.0", false)] [InlineData("v2.1", false)]
    public void OnlyStableThreePartTags(string tag, bool valid) => Assert.Equal(valid, UpdatePolicy.StableVersion(tag) is not null);
    [Fact]
    public void DisabledAndStartupOnlyChecksHaveNoPeriodicWakeup()
    {
        var now = DateTimeOffset.Now;
        Assert.Null(UpdatePolicy.NextCheck(new() { AutoCheck = false, CheckWhen = CheckSchedule.Daily }, null, now));
        Assert.Null(UpdatePolicy.NextCheck(new(), null, now));
    }
    [Fact]
    public void NearTermSchedulesKeepTheirExactDelay()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(TimeSpan.FromSeconds(30), UpdatePolicy.DelayUntil(now.AddSeconds(30), now));
        Assert.Equal(TimeSpan.FromSeconds(1), UpdatePolicy.DelayUntil(now.AddSeconds(-1), now));
    }
    [Fact]
    public void IntervalIsMeasuredFromLastCheck()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(now.AddHours(6), UpdatePolicy.NextCheck(new() { CheckWhen = CheckSchedule.EverySixHours }, now, now));
        Assert.Equal(now.AddDays(7), UpdatePolicy.NextCheck(new() { CheckWhen = CheckSchedule.Weekly }, now, now));
    }
    [Fact]
    public void DailyTimeIsInFutureAndUsesLocalClock()
    {
        var now = DateTimeOffset.Now;
        var next = UpdatePolicy.AtNextLocalTime(now, 20 * 60 + 15);
        Assert.True(next > now); Assert.Equal(20, next.LocalDateTime.Hour); Assert.Equal(15, next.LocalDateTime.Minute);
    }
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, false, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, false, false, false)]
    public void NextStartupPolicy(bool automatic, bool manual, bool startup, bool eligible) =>
        Assert.Equal(eligible, UpdatePolicy.ShouldInstall(new() { AutoInstall = automatic, InstallWhen = InstallSchedule.NextStartup }, manual, startup, false));
    [Fact]
    public void DailyInstallRequiresItsOwnDueTime()
    {
        var prefs = new UpdatePreferences { AutoInstall = true, InstallWhen = InstallSchedule.DailyAtTime };
        Assert.False(UpdatePolicy.ShouldInstall(prefs, false, false, false));
        Assert.True(UpdatePolicy.ShouldInstall(prefs, false, false, true));
    }
    [Fact]
    public void ChangesIncludeSkippedReleasesWithoutOlderHistory()
    {
        const string changes = "# Log\n## [2.1.1] - date\n- Newest\n## [2.1.0] - date\n- Updates\n## [2.0.1] - date\n- Old\n";
        string result = ReleaseNotes.Since(changes, "2.0.1", "2.1.1");
        Assert.Contains("Newest", result); Assert.Contains("Updates", result); Assert.DoesNotContain("- Old", result);
        Assert.DoesNotContain("Updates", ReleaseNotes.Since(changes, null, "2.1.1"));
    }
}
