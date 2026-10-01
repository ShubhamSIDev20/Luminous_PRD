using System;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Services.Alarms;
using Xunit;

namespace BatteryTestingSystem.Tests.Services;

public class AlarmPolicyTests
{
    private static AlarmPolicy MakePolicy() => new(new AlarmOptions
    {
        EscalationCooldownSec = 60,
        StormThreshold = 3,
        StormWindowMinutes = 5,
        StormCooloffMinutes = 15
    });

    private static readonly DateTime T0 = new(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FirstCriticalAlarm_Escalates()
    {
        var policy = MakePolicy();

        var decision = policy.Evaluate("dev1/comms-loss", SeverityLevel.CRITICAL, "1", T0);

        Assert.True(decision.Escalate);
    }

    [Fact]
    public void InfoAndWarning_DoNotEscalate()
    {
        var policy = MakePolicy();

        Assert.False(policy.Evaluate("k1", SeverityLevel.INFO, "1", T0).Escalate);
        Assert.False(policy.Evaluate("k2", SeverityLevel.WARNING, "1", T0).Escalate);
    }

    [Fact]
    public void RepeatInsideCooldown_DoesNotEscalateAgain()
    {
        var policy = MakePolicy();
        policy.Evaluate("k", SeverityLevel.ERROR, "1", T0);

        var second = policy.Evaluate("k", SeverityLevel.ERROR, "1", T0.AddSeconds(30));

        Assert.False(second.Escalate);
    }

    [Fact]
    public void RepeatAfterCooldown_EscalatesAgain()
    {
        var policy = MakePolicy();
        policy.Evaluate("k", SeverityLevel.ERROR, "1", T0);

        var later = policy.Evaluate("k", SeverityLevel.ERROR, "1", T0.AddSeconds(61));

        Assert.True(later.Escalate);
    }

    [Fact]
    public void DifferentKeys_HaveIndependentCooldowns()
    {
        var policy = MakePolicy();
        policy.Evaluate("a", SeverityLevel.ERROR, "1", T0);

        var other = policy.Evaluate("b", SeverityLevel.ERROR, "1", T0.AddSeconds(1));

        Assert.True(other.Escalate);
    }

    [Fact]
    public void ExceedingStormThreshold_TriggersStormOnceAndSuppresses()
    {
        var policy = MakePolicy();

        // Threshold is 3 inside a 5 minute window.
        policy.Evaluate("k", SeverityLevel.ERROR, "1", T0);
        policy.Evaluate("k", SeverityLevel.ERROR, "1", T0.AddSeconds(10));
        policy.Evaluate("k", SeverityLevel.ERROR, "1", T0.AddSeconds(20));
        var storming = policy.Evaluate("k", SeverityLevel.ERROR, "1", T0.AddSeconds(30));

        Assert.True(storming.StormTriggered);
        Assert.False(storming.Escalate);

        // Storm is announced exactly once, and escalation stays suppressed through cooloff.
        var afterStorm = policy.Evaluate("k", SeverityLevel.ERROR, "1", T0.AddMinutes(2));
        Assert.False(afterStorm.StormTriggered);
        Assert.False(afterStorm.Escalate);
    }

    [Fact]
    public void AfterStormCooloff_EscalationResumes()
    {
        var policy = MakePolicy();
        for (var i = 0; i < 4; i++)
            policy.Evaluate("k", SeverityLevel.ERROR, "1", T0.AddSeconds(i * 10));

        var resumed = policy.Evaluate("k", SeverityLevel.ERROR, "1", T0.AddMinutes(16));

        Assert.True(resumed.Escalate);
    }

    [Fact]
    public void GlobalMute_SuppressesEscalation_UntilExpiry()
    {
        var policy = MakePolicy();
        policy.MuteAll(T0.AddMinutes(15));

        Assert.False(policy.Evaluate("k", SeverityLevel.CRITICAL, "1", T0.AddMinutes(1)).Escalate);
        Assert.True(policy.Evaluate("k", SeverityLevel.CRITICAL, "1", T0.AddMinutes(16)).Escalate);
    }

    [Fact]
    public void DeviceMute_SuppressesOnlyThatDevice()
    {
        var policy = MakePolicy();
        policy.MuteDevice("1", T0.AddMinutes(15));

        Assert.False(policy.Evaluate("k1", SeverityLevel.CRITICAL, "1", T0.AddMinutes(1)).Escalate);
        Assert.True(policy.Evaluate("k2", SeverityLevel.CRITICAL, "2", T0.AddMinutes(1)).Escalate);
    }
}
