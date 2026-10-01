using System.Collections.Generic;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Services.Implementations;
using Xunit;

namespace BatteryTestingSystem.Tests.Services;

/// <summary>
/// T-38: tests the dashboard's filter/bulk-select rules extracted out of DashboardView.razor
/// into a plain, DI-free class. DashboardView itself can't be safely rendered in a unit test -
/// it injects ChannelManager, a BackgroundService owning the real TCP/UDP hardware listeners -
/// so this is the piece that's actually testable, exercising the exact logic the page delegates
/// to rather than a re-implementation of it.
/// </summary>
public class CircuitSelectionLogicTests
{
    private static (int DeviceID, int SecondaryBoardNumber, int ChannelID) Key(FakeChannelCommandHandler c) =>
        (c.Channel.DeviceID, c.Channel.SecondaryBoardNumber, c.Channel.ChannelNumber);

    // ======================================================================== Filter

    public class FilterTests
    {
        [Fact]
        public void ExcludesCircuitsOutsideAccess()
        {
            var inAccess = FakeChannelCommandHandler.Circuit(1, 1, 1);
            var outsideAccess = FakeChannelCommandHandler.Circuit(9, 9, 9);
            var access = new HashSet<(int, int, int)> { Key(inAccess) };

            var result = CircuitSelectionLogic.Filter(
                new[] { inAccess, outsideAccess }, access, new(), null, false);

            Assert.Equal(new[] { inAccess }, result);
        }

        [Fact]
        public void EmptyVisibleSet_MeansEverythingInAccessIsVisible()
        {
            // _visibleCircuits.Count == 0 is the "no filter applied yet" sentinel, not "hide
            // everything" - ChannelFilter starts with nothing hidden, so an empty set here
            // must mean unfiltered, not empty.
            var c = FakeChannelCommandHandler.Circuit(1, 1, 1);
            var access = new HashSet<(int, int, int)> { Key(c) };

            var result = CircuitSelectionLogic.Filter(new[] { c }, access, new(), null, false);

            Assert.Single(result);
        }

        [Fact]
        public void NonEmptyVisibleSet_HidesCircuitsNotInIt()
        {
            var visible = FakeChannelCommandHandler.Circuit(1, 1, 1);
            var hidden = FakeChannelCommandHandler.Circuit(1, 1, 2);
            var access = new HashSet<(int, int, int)> { Key(visible), Key(hidden) };
            var visibleSet = new HashSet<(int, int, int)> { Key(visible) };

            var result = CircuitSelectionLogic.Filter(new[] { visible, hidden }, access, visibleSet, null, false);

            Assert.Equal(new[] { visible }, result);
        }

        [Fact]
        public void StatusChip_FiltersToOnlyThatStatus()
        {
            var charging = FakeChannelCommandHandler.Circuit(1, 1, 1, CircuitStatus.Charge);
            var idle = FakeChannelCommandHandler.Circuit(1, 1, 2, CircuitStatus.Idle);
            var access = new HashSet<(int, int, int)> { Key(charging), Key(idle) };

            var result = CircuitSelectionLogic.Filter(new[] { charging, idle }, access, new(), CircuitStatus.Charge, false);

            Assert.Equal(new[] { charging }, result);
        }

        [Fact]
        public void OnlineOnly_ExcludesOfflineCircuits_ButIsIndependentOfStatusChip()
        {
            var online = FakeChannelCommandHandler.Circuit(1, 1, 1, CircuitStatus.Charge);
            var offline = FakeChannelCommandHandler.Circuit(1, 1, 2, CircuitStatus.Offline);
            var access = new HashSet<(int, int, int)> { Key(online), Key(offline) };

            var result = CircuitSelectionLogic.Filter(new[] { online, offline }, access, new(), statusChip: null, onlineOnly: true);

            Assert.Equal(new[] { online }, result);
        }
    }

    // ======================================================================== ResolveAnchor

    public class ResolveAnchorTests
    {
        [Fact]
        public void NoSelection_ReturnsNull()
        {
            var pool = new[] { FakeChannelCommandHandler.Circuit(1, 1, 1) };
            Assert.Null(CircuitSelectionLogic.ResolveAnchor(pool, new()));
        }

        [Fact]
        public void ReturnsTheHandlerMatchingTheFirstSelectedKey()
        {
            var target = FakeChannelCommandHandler.Circuit(2, 3, 4);
            var other = FakeChannelCommandHandler.Circuit(1, 1, 1);
            var selected = new HashSet<(int, int, int)> { Key(target) };

            var anchor = CircuitSelectionLogic.ResolveAnchor(new[] { other, target }, selected);

            Assert.Same(target, anchor);
        }

        [Fact]
        public void SelectedKeyNotInThePool_ReturnsNull()
        {
            var pool = new[] { FakeChannelCommandHandler.Circuit(1, 1, 1) };
            var selected = new HashSet<(int, int, int)> { (9, 9, 9) };

            Assert.Null(CircuitSelectionLogic.ResolveAnchor(pool, selected));
        }
    }

    // ======================================================================== SelectAll

    public class SelectAllTests
    {
        [Fact]
        public void EmptyFilteredList_ReturnsNothingToSelect_SelectingNothing()
        {
            // Distinguishes "nothing to look at" (silent no-op in DashboardView) from
            // "looked, found nothing online" (which does show a warning toast).
            var selected = new HashSet<(int, int, int)>();

            var result = CircuitSelectionLogic.SelectAll(new(), existingAnchor: null, selected);

            Assert.Equal(SelectAllOutcome.NothingToSelect, result.Outcome);
            Assert.Empty(selected);
        }

        [Fact]
        public void EveryCircuitOffline_ReturnsNoOnlineCircuit()
        {
            var offline = FakeChannelCommandHandler.Circuit(1, 1, 1, CircuitStatus.Offline);
            var selected = new HashSet<(int, int, int)>();

            var result = CircuitSelectionLogic.SelectAll(new() { offline }, existingAnchor: null, selected);

            Assert.Equal(SelectAllOutcome.NoOnlineCircuit, result.Outcome);
            Assert.Empty(selected);
        }

        [Fact]
        public void NoExistingAnchor_PicksTheFirstOnlineCircuitAsAnchor_AndSelectsMatchingPeers()
        {
            var offlineFirst = FakeChannelCommandHandler.Circuit(1, 1, 1, CircuitStatus.Offline);
            var anchorCandidate = FakeChannelCommandHandler.Circuit(1, 1, 2, CircuitStatus.Charge, ProgramRunningStatus.Running);
            var matchingPeer = FakeChannelCommandHandler.Circuit(1, 1, 3, CircuitStatus.Charge, ProgramRunningStatus.Running);
            var selected = new HashSet<(int, int, int)>();

            var result = CircuitSelectionLogic.SelectAll(
                new() { offlineFirst, anchorCandidate, matchingPeer }, existingAnchor: null, selected);

            Assert.Equal(SelectAllOutcome.PartiallySelected, result.Outcome); // offlineFirst was skipped
            Assert.Equal(2, result.Added);
            Assert.Equal(1, result.Skipped);
            Assert.Equal(CircuitStatus.Charge, result.AnchorCircuitStatus);
            Assert.Contains(Key(anchorCandidate), selected);
            Assert.Contains(Key(matchingPeer), selected);
            Assert.DoesNotContain(Key(offlineFirst), selected);
        }

        [Fact]
        public void ExistingAnchor_IsReused_NotRePicked()
        {
            // The anchor is Idle, but the filtered view's first circuit is Charging - if
            // SelectAll re-picked instead of reusing, it would anchor on the wrong state and
            // select the wrong peers.
            var chargingFirst = FakeChannelCommandHandler.Circuit(1, 1, 1, CircuitStatus.Charge);
            var idleAnchor = FakeChannelCommandHandler.Circuit(2, 2, 2, CircuitStatus.Idle);
            var idlePeer = FakeChannelCommandHandler.Circuit(3, 3, 3, CircuitStatus.Idle);
            var selected = new HashSet<(int, int, int)> { Key(idleAnchor) };

            var result = CircuitSelectionLogic.SelectAll(
                new() { chargingFirst, idleAnchor, idlePeer }, existingAnchor: idleAnchor, selected);

            Assert.Equal(CircuitStatus.Idle, result.AnchorCircuitStatus);
            Assert.Contains(Key(idlePeer), selected);
            Assert.DoesNotContain(Key(chargingFirst), selected);
        }

        [Fact]
        public void MatchingRequiresBothCircuitStatusAndProgramStatus()
        {
            var anchor = FakeChannelCommandHandler.Circuit(1, 1, 1, CircuitStatus.Charge, ProgramRunningStatus.Running);
            var sameCircuitStatusDifferentProgram = FakeChannelCommandHandler.Circuit(1, 1, 2, CircuitStatus.Charge, ProgramRunningStatus.Stop);
            var selected = new HashSet<(int, int, int)>();

            var result = CircuitSelectionLogic.SelectAll(
                new() { anchor, sameCircuitStatusDifferentProgram }, existingAnchor: anchor, selected);

            Assert.Equal(SelectAllOutcome.PartiallySelected, result.Outcome);
            Assert.Equal(1, result.Added);
            Assert.Equal(1, result.Skipped);
            Assert.DoesNotContain(Key(sameCircuitStatusDifferentProgram), selected);
        }

        [Fact]
        public void AllMatching_ReturnsAllSelected_WithZeroSkipped()
        {
            var anchor = FakeChannelCommandHandler.Circuit(1, 1, 1, CircuitStatus.Idle);
            var peer = FakeChannelCommandHandler.Circuit(1, 1, 2, CircuitStatus.Idle);
            var selected = new HashSet<(int, int, int)>();

            var result = CircuitSelectionLogic.SelectAll(new() { anchor, peer }, existingAnchor: anchor, selected);

            Assert.Equal(SelectAllOutcome.AllSelected, result.Outcome);
            Assert.Equal(2, result.Added);
            Assert.Equal(0, result.Skipped);
        }

        [Fact]
        public void AlreadySelectedCircuits_AreNotDoubleCounted()
        {
            var anchor = FakeChannelCommandHandler.Circuit(1, 1, 1, CircuitStatus.Idle);
            var alreadySelected = FakeChannelCommandHandler.Circuit(1, 1, 2, CircuitStatus.Idle);
            var selected = new HashSet<(int, int, int)> { Key(alreadySelected) };

            var result = CircuitSelectionLogic.SelectAll(new() { anchor, alreadySelected }, existingAnchor: anchor, selected);

            // HashSet.Add returns false for an existing member, so `added` only counts genuinely
            // new selections - re-running Select All must not inflate the reported count.
            Assert.Equal(1, result.Added);
            Assert.Equal(2, selected.Count);
        }
    }
}
