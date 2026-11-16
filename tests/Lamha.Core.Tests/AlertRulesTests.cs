using Lamha.Core.Alerts;
using Lamha.Core.Prayer;
using Lamha.Core.Settings;

namespace Lamha.Core.Tests;

public class AlertRulesTests
{
    private static readonly TimeSpan Tz = TimeSpan.FromHours(3);
    private static readonly DateTimeOffset Dhuhr = new(2026, 11, 16, 11, 52, 0, Tz);
    private static readonly PrayerSettings Prayer = new() { AlertMinutesBefore = 10 };

    private static PrayerState Normal(DateTimeOffset now, PrayerName target, DateTimeOffset adhan) =>
        new(PrayerPhase.Normal, target, adhan, adhan - now);

    private static IReadOnlyList<Alert> TickAt(AlertRules r, DateTimeOffset now, bool busy = false) =>
        r.Tick(now, Normal(now, PrayerName.Dhuhr, Dhuhr), busy);

    [Fact]
    public void Tick_BeforeAlertTime_Nothing() =>
        Assert.Empty(TickAt(new AlertRules(Prayer, dndEnabled: true), Dhuhr.AddMinutes(-11)));

    [Fact]
    public void Tick_AtAlertTime_EmitsOnce()
    {
        var r = new AlertRules(Prayer, dndEnabled: true);
        var first = TickAt(r, Dhuhr.AddMinutes(-10));
        var second = TickAt(r, Dhuhr.AddMinutes(-9.5));

        var alert = Assert.Single(first);
        Assert.Equal(PrayerName.Dhuhr, alert.Prayer);
        Assert.Equal(Dhuhr, alert.Due);
        Assert.Empty(second);
    }

    [Fact]
    public void Tick_AlertsDisabled_Nothing() =>
        Assert.Empty(TickAt(new AlertRules(Prayer with { AlertsEnabled = false }, true), Dhuhr.AddMinutes(-5)));

    [Fact]
    public void Tick_Shuruq_Nothing()
    {
        var r = new AlertRules(Prayer, true);
        var sunrise = Dhuhr.AddHours(-6);
        Assert.Empty(r.Tick(sunrise.AddMinutes(-5), Normal(sunrise.AddMinutes(-5), PrayerName.Shuruq, sunrise), false));
    }

    [Fact]
    public void Tick_FullscreenBusy_Queued() =>
        Assert.Empty(TickAt(new AlertRules(Prayer, true), Dhuhr.AddMinutes(-5), busy: true));

    [Fact]
    public void Tick_AfterBusyEnds_OneMissedAlert()
    {
        var r = new AlertRules(Prayer, true);
        TickAt(r, Dhuhr.AddMinutes(-5), busy: true);
        var after = TickAt(r, Dhuhr.AddMinutes(-2), busy: false);
        var again = TickAt(r, Dhuhr.AddMinutes(-1), busy: false);

        var missed = Assert.Single(after);
        Assert.Equal("missed", missed.Key);
        Assert.Empty(again);
    }

    [Fact]
    public void Tick_DndOff_EmitsEvenWhenBusy() =>
        Assert.Single(TickAt(new AlertRules(Prayer, dndEnabled: false), Dhuhr.AddMinutes(-5), busy: true));

    [Fact]
    public void Tick_NewDay_ResetsKeys()
    {
        var r = new AlertRules(Prayer, true);
        TickAt(r, Dhuhr.AddMinutes(-5));
        var tomorrow = Dhuhr.AddDays(1);
        Assert.Single(r.Tick(tomorrow.AddMinutes(-5), Normal(tomorrow.AddMinutes(-5), PrayerName.Dhuhr, tomorrow), false));
    }

    [Fact]
    public void Tick_ZeroMinutes_EmitsAtAdhan()
    {
        var r = new AlertRules(Prayer with { AlertMinutesBefore = 0 }, true);
        var state = new PrayerState(PrayerPhase.IqamaWait, PrayerName.Dhuhr, Dhuhr.AddMinutes(20), TimeSpan.FromMinutes(20));
        var alert = Assert.Single(r.Tick(Dhuhr, state, false));
        Assert.Equal(Dhuhr, alert.Due);
    }
}
