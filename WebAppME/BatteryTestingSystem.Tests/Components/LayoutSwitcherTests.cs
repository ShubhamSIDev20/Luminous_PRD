using System;
using System.Collections.Generic;
using System.Linq;
using BatteryTestingSystem.Components.UI.WorkflowCanvas;
using BatteryTestingSystem.Services.Interfaces;
using Bunit;
using Xunit;

namespace BatteryTestingSystem.Tests.Components;

/// <summary>
/// Save gating is the point of these tests: Save must be impossible when there is nothing to
/// save or no layout to save into, so the operator cannot silently create empty rows.
/// </summary>
public class LayoutSwitcherTests : TestContext
{
    private static IReadOnlyList<LayoutSummary> Layouts() => new List<LayoutSummary>
    {
        new(1, "Bench A", null, DateTime.Now),
        new(2, "Bench B", null, DateTime.Now),
    };

    [Fact]
    public void ListsEverySavedLayoutPlusAnUnsavedOption()
    {
        var cut = RenderComponent<LayoutSwitcher>(p => p.Add(x => x.Layouts, Layouts()));

        Assert.Equal(3, cut.FindAll("option").Count);
    }

    [Fact]
    public void SaveIsDisabledWhenNothingHasChanged()
    {
        var cut = RenderComponent<LayoutSwitcher>(p => p
            .Add(x => x.Layouts, Layouts())
            .Add(x => x.CurrentId, 1L)
            .Add(x => x.IsDirty, false));

        Assert.True(cut.Find("button").HasAttribute("disabled"));
    }

    [Fact]
    public void SaveIsDisabledWhenThereIsNoCurrentLayout_EvenIfDirty()
    {
        // An unsaved canvas has no row to update; the operator must use Save as… instead.
        var cut = RenderComponent<LayoutSwitcher>(p => p
            .Add(x => x.Layouts, Layouts())
            .Add(x => x.CurrentId, (long?)null)
            .Add(x => x.IsDirty, true));

        Assert.True(cut.Find("button").HasAttribute("disabled"));
    }

    [Fact]
    public void SaveIsEnabledAndMarkedWhenDirtyWithACurrentLayout()
    {
        var cut = RenderComponent<LayoutSwitcher>(p => p
            .Add(x => x.Layouts, Layouts())
            .Add(x => x.CurrentId, 1L)
            .Add(x => x.IsDirty, true));

        var save = cut.Find("button");
        Assert.False(save.HasAttribute("disabled"));
        Assert.Contains("*", save.TextContent);
    }

    [Fact]
    public void SelectingALayoutRaisesOnLoadWithItsId()
    {
        long? loaded = null;

        var cut = RenderComponent<LayoutSwitcher>(p => p
            .Add(x => x.Layouts, Layouts())
            .Add(x => x.OnLoad, (long id) => loaded = id));

        cut.Find("select").Change("2");

        Assert.Equal(2L, loaded);
    }

    [Fact]
    public void SaveAsRequiresANonEmptyName()
    {
        string? saved = null;

        var cut = RenderComponent<LayoutSwitcher>(p => p
            .Add(x => x.Layouts, Layouts())
            .Add(x => x.OnSaveAs, (string n) => saved = n));

        cut.FindAll("button").Last().Click();       // "Save as…"
        cut.Find("input").Input("   ");

        Assert.True(cut.FindAll("button").First(b => b.TextContent == "Create").HasAttribute("disabled"));
        Assert.Null(saved);
    }

    [Fact]
    public void SaveAsTrimsTheNameBeforeRaisingTheCallback()
    {
        string? saved = null;

        var cut = RenderComponent<LayoutSwitcher>(p => p
            .Add(x => x.Layouts, Layouts())
            .Add(x => x.OnSaveAs, (string n) => saved = n));

        cut.FindAll("button").Last().Click();
        cut.Find("input").Input("  Bench C  ");
        cut.FindAll("button").First(b => b.TextContent == "Create").Click();

        Assert.Equal("Bench C", saved);
    }

    [Fact]
    public void DeleteAppearsOnlyWhenALayoutIsOpen()
    {
        var unsaved = RenderComponent<LayoutSwitcher>(p => p.Add(x => x.Layouts, Layouts()));
        Assert.DoesNotContain("Delete", unsaved.Markup);

        var open = RenderComponent<LayoutSwitcher>(p => p
            .Add(x => x.Layouts, Layouts())
            .Add(x => x.CurrentId, 1L));
        Assert.Contains("Delete", open.Markup);
    }
}
