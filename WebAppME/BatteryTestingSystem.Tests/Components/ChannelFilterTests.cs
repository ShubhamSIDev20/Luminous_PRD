using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BatteryTestingSystem.Components.UI.Dashboard;
using BatteryTestingSystem.Services;
using BatteryTestingSystem.Services.Implementations;
using BatteryTestingSystem.Services.Interfaces;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BatteryTestingSystem.Tests.Components;

/// <summary>
/// Pass 3 of the test-project build-out: renders the real ChannelFilter.razor component
/// through bUnit (not a hand-extracted copy of its logic), so these tests exercise the actual
/// markup, tri-state checkbox rendering, and click handlers a browser would run - including the
/// T-34 fix where the My Channels popover must render three distinct indentation levels
/// (device -&gt; secondary board -&gt; channel), not two.
///
/// ChannelFilter injects ServerSessionStorageService, whose constructor unconditionally calls
/// ServiceLocator.GetScoped&lt;IConfigStorageService&gt;() to preload DB-persisted state.
/// ServiceLocator throws if SetProvider was never called, so each test wires a minimal DI
/// container through it first. ServiceLocator is a process-wide static, so this is a real
/// point of test-isolation risk if a future test class also touches it concurrently - there is
/// none today, but any new user of ServiceLocator in this test project should be aware.
/// </summary>
public class ChannelFilterTests : TestContext
{
    public ChannelFilterTests()
    {
        var services = new ServiceCollection();
        services.AddScoped<IConfigStorageService, FakeConfigStorageService>();
        ServiceLocator.SetProvider(services.BuildServiceProvider());

        Services.AddSingleton<PopupService>();
        Services.AddSingleton<ServerSessionStorageService>();
    }

    private static List<(int DeviceID, int SecondaryBoardNumber, int ChannelNumber, string Name)> ThreeLevelTree() => new()
    {
        (1, 1, 1, "Ch A"),
        (1, 1, 2, "Ch B"),
        (1, 2, 1, "Ch C"),
        (2, 1, 1, "Ch D"),
    };

    private static void Open(IRenderedComponent<ChannelFilter> cut) => cut.Find("button").Click();

    [Fact]
    public void Opening_RendersThreeDistinctIndentationLevels_DeviceBoardChannel()
    {
        var cut = RenderComponent<ChannelFilter>(p => p.Add(x => x.AccessChannels, ThreeLevelTree()));
        Open(cut);

        // Device rows carry no left-padding utility class at all - they are the baseline.
        var deviceRow = cut.Find("span.text-foreground.cursor-pointer.flex-1");
        Assert.DoesNotContain("pl-", deviceRow.ParentElement!.ClassName);

        // Secondary board rows are wrapped in the pl-6 div (T-34's fix - was pl-5, a class
        // absent from the prebuilt CSS bundle, which silently collapsed to 0px).
        Assert.NotEmpty(cut.FindAll("div.pl-6"));

        // Channel leaves sit one level deeper still, at pl-9.
        Assert.NotEmpty(cut.FindAll("label.pl-9"));
    }

    [Fact]
    public void EveryChannel_StartsVisible_WithZeroHiddenCount()
    {
        var cut = RenderComponent<ChannelFilter>(p => p.Add(x => x.AccessChannels, ThreeLevelTree()));

        // The badge on the filter icon only renders once _hiddenCount > 0.
        Assert.Empty(cut.FindAll("span.bg-destructive"));

        Open(cut);
        Assert.Equal(4, cut.FindAll("label.pl-9 input[type=checkbox]").Count(cb => cb.HasAttribute("checked")));
    }

    [Fact]
    public async Task HidingASingleChannel_MarksItsParentsIndeterminate_NotUnchecked()
    {
        var cut = RenderComponent<ChannelFilter>(p => p.Add(x => x.AccessChannels, ThreeLevelTree()));
        Open(cut);

        var firstChannelCheckbox = cut.FindAll("label.pl-9 input[type=checkbox]")[0];
        await cut.InvokeAsync(() => firstChannelCheckbox.Change(false));

        // TriStateCheckboxClass appends "opacity-70" only for Indeterminate - distinguishing it
        // from a parent whose every child is unchecked.
        var secondaryCheckbox = cut.FindAll("div.pl-6 input[type=checkbox]")[0];
        Assert.Contains("opacity-70", secondaryCheckbox.GetAttribute("class"));
    }

    [Fact]
    public async Task HidingEveryChannelUnderADevice_MarksTheDeviceUnchecked_NotIndeterminate()
    {
        var cut = RenderComponent<ChannelFilter>(p => p.Add(x => x.AccessChannels, ThreeLevelTree()));
        Open(cut);

        // Device 1 has 3 channels (across 2 boards) - hide all three individually. Re-find
        // after each change: hiding a channel re-renders the tree, and reusing a stale node
        // reference throws UnknownEventHandlerIdException.
        for (int i = 0; i < 3; i++)
        {
            var checkbox = cut.FindAll("label.pl-9 input[type=checkbox]").First(cb => cb.HasAttribute("checked"));
            await cut.InvokeAsync(() => checkbox.Change(false));
        }

        var deviceCheckboxes = cut.FindAll("div.flex.items-center.gap-1.px-1.py-1.hover\\:bg-muted\\/50.rounded.text-xs.font-medium input[type=checkbox]");
        var device1Checkbox = deviceCheckboxes[0];

        Assert.DoesNotContain("opacity-70", device1Checkbox.GetAttribute("class"));
        Assert.False(device1Checkbox.HasAttribute("checked"));
    }

    [Fact]
    public async Task TogglingADeviceCheckbox_HidesEveryChannelBeneathIt_AndUpdatesTheBadgeCount()
    {
        var cut = RenderComponent<ChannelFilter>(p => p.Add(x => x.AccessChannels, ThreeLevelTree()));
        Open(cut);

        var deviceCheckbox = cut.FindAll("div.flex.items-center.gap-1.px-1.py-1.hover\\:bg-muted\\/50.rounded.text-xs.font-medium input[type=checkbox]")[0];
        await cut.InvokeAsync(() => deviceCheckbox.Change(false));

        // Device 1 owns 3 of the 4 fixture channels.
        var badge = cut.Find("span.bg-destructive");
        Assert.Equal("3", badge.TextContent.Trim());
    }

    [Fact]
    public async Task VisibleChannelsChanged_FiresWithExactlyTheAccessSetMinusHidden()
    {
        HashSet<(int DeviceID, int SecondaryBoardNumber, int ChannelNumber)>? emitted = null;

        var cut = RenderComponent<ChannelFilter>(p => p
            .Add(x => x.AccessChannels, ThreeLevelTree())
            .Add(x => x.VisibleChannelsChanged, EventCallback.Factory.Create<HashSet<(int, int, int)>>(this, set => emitted = set)));
        Open(cut);

        var firstChannelCheckbox = cut.FindAll("label.pl-9 input[type=checkbox]")[0];
        await cut.InvokeAsync(() => firstChannelCheckbox.Change(false));

        Assert.NotNull(emitted);
        Assert.Equal(3, emitted!.Count);
        Assert.DoesNotContain((1, 1, 1), emitted);
    }

    [Fact]
    public async Task HideAll_ThenShowAll_RoundTripsBackToEveryChannelVisible()
    {
        var cut = RenderComponent<ChannelFilter>(p => p.Add(x => x.AccessChannels, ThreeLevelTree()));
        Open(cut);

        var hideAll = cut.FindAll("button").First(b => b.TextContent.Trim() == "Hide All");
        await cut.InvokeAsync(() => hideAll.Click());

        Assert.Equal("4", cut.Find("span.bg-destructive").TextContent.Trim());

        var showAll = cut.FindAll("button").First(b => b.TextContent.Trim() == "Show All");
        await cut.InvokeAsync(() => showAll.Click());

        Assert.Empty(cut.FindAll("span.bg-destructive"));
    }

    [Fact]
    public void SearchFilter_NarrowsTheTreeToMatchingChannelsOnly()
    {
        var cut = RenderComponent<ChannelFilter>(p => p.Add(x => x.AccessChannels, ThreeLevelTree()));
        Open(cut);

        var search = cut.Find("input[type=search]");
        search.Input("Ch D");

        var leaves = cut.FindAll("label.pl-9");
        Assert.Single(leaves);
        Assert.Contains("Ch D", leaves[0].TextContent);
    }

    [Fact]
    public void NoAccessChannels_RendersTheEmptyStateMessage_NotAnEmptyTree()
    {
        var cut = RenderComponent<ChannelFilter>(p => p.Add(x => x.AccessChannels, new()));
        Open(cut);

        Assert.Contains("No channels match.", cut.Markup);
    }
}
