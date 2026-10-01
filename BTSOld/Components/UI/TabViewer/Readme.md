// ================================================================
// Example 1: Opening tabs from anywhere in your application
// ================================================================

using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.JSInterop;
using windows.Components.TabViewer;

@inject TabService TabService

// Open a new tab with a component
private void OpenUserProfile(int userId)
{
    var parameters = new Dictionary<string, object>
    {
        { "UserId", userId }
    };

    TabService.AddTab(
        title: $"User #{userId}",
        componentType: typeof(UserProfileComponent),
        parameters: parameters,
        icon: "User"
    );
}

// ================================================================
// Example 2: Dynamic tab title updates
// ================================================================

// In your component that's loaded in a tab
@inject TabService TabService
@inject NavigationManager Navigation

protected override void OnInitialized()
{
    // Update the tab title based on loaded data
    LoadUserData();
}

private async Task LoadUserData()
{
    var user = await GetUserAsync(UserId);

    // Update the current tab's title
    var currentPath = Navigation.Uri;
    // Find current tab and update title
    TabService.UpdateTabTitle(currentTabId, $"User: {user.Name}");
}

// ================================================================
// Example 3: Programmatic tab management
// ================================================================

@code {
    private void DemoTabOperations()
{
    // Add multiple tabs
    var tab1 = TabService.AddTab("Reports", typeof(ReportsComponent), icon: "FileText");
    var tab2 = TabService.AddTab("Analytics", typeof(AnalyticsComponent), icon: "BarChart");
    var tab3 = TabService.AddTab("Logs", typeof(LogsComponent), icon: "FileText");

    // Pin a tab
    TabService.TogglePinTab(tab1.Id);

    // Close all except pinned
    TabService.CloseAllTabs(exceptPinned: true);

    // Activate a specific tab
    TabService.SetActiveTab(tab2.Id);
}
}

// ================================================================
// Example 4: Tab with parameters and state
// ================================================================

// UserProfileComponent.razor
@code {
    [Parameter] public int UserId { get; set; }

private User? _user;

protected override async Task OnParametersSetAsync()
{
    _user = await UserService.GetUserAsync(UserId);

    // Update tab title with user name
    TabService.UpdateTabTitle(GetCurrentTabId(), $"{_user.Name}'s Profile");
}

private string GetCurrentTabId()
{
    // Find the tab that contains this component
    return TabService.ActiveTab?.Id ?? string.Empty;
}
}

// ================================================================
// Example 5: Handling unsaved changes before closing
// ================================================================

public class EnhancedTab : Tab
{
    public bool HasUnsavedChanges { get; set; }
    public Func<Task<bool>>? OnBeforeClose { get; set; }
}

public class EnhancedTabService : TabService
{
    public async Task<bool> CloseTabWithConfirmation(string tabId)
    {
        var tab = Tabs.FirstOrDefault(t => t.Id == tabId) as EnhancedTab;
        if (tab?.HasUnsavedChanges == true)
        {
            // Show confirmation dialog
            var confirmed = await ShowConfirmDialog("You have unsaved changes. Close anyway?");
            if (!confirmed) return false;
        }

        if (tab?.OnBeforeClose != null)
        {
            var canClose = await tab.OnBeforeClose();
            if (!canClose) return false;
        }

        CloseTab(tabId);
        return true;
    }
}

// ================================================================
// Example 6: Tab presets/workspaces
// ================================================================

public class TabWorkspace
{
    public string Name { get; set; } = string.Empty;
    public List<TabDefinition> Tabs { get; set; } = new();
}

public class TabDefinition
{
    public string Title { get; set; } = string.Empty;
    public string ComponentTypeName { get; set; } = string.Empty;
    public Dictionary<string, object>? Parameters { get; set; }
    public string Icon { get; set; } = "File";
    public bool IsPinned { get; set; }
}

public static class TabWorkspaces
{
    public static TabWorkspace DeveloperWorkspace => new()
    {
        Name = "Developer",
        Tabs = new List<TabDefinition>
        {
            new() { Title = "Code Editor", ComponentTypeName = "CodeEditorComponent", Icon = "Code", IsPinned = true },
            new() { Title = "Terminal", ComponentTypeName = "TerminalComponent", Icon = "Terminal" },
            new() { Title = "File Explorer", ComponentTypeName = "FileExplorerComponent", Icon = "Folder" },
            new() { Title = "Git", ComponentTypeName = "GitComponent", Icon = "GitBranch" }
        }
    };

    public static TabWorkspace AnalyticsWorkspace => new()
    {
        Name = "Analytics",
        Tabs = new List<TabDefinition>
        {
            new() { Title = "Dashboard", ComponentTypeName = "DashboardComponent", Icon = "LayoutDashboard", IsPinned = true },
            new() { Title = "Reports", ComponentTypeName = "ReportsComponent", Icon = "FileText" },
            new() { Title = "Charts", ComponentTypeName = "ChartsComponent", Icon = "BarChart" }
        }
    };
}

// Usage:
private void LoadWorkspace(TabWorkspace workspace)
{
    TabService.CloseAllTabs(exceptPinned: false);

    foreach (var tabDef in workspace.Tabs)
    {
        var componentType = Type.GetType($"YourApp.Components.{tabDef.ComponentTypeName}");
        var tab = TabService.AddTab(
            tabDef.Title,
            componentType,
            tabDef.Parameters,
            tabDef.Icon
        );

        if (tabDef.IsPinned)
        {
            TabService.TogglePinTab(tab.Id);
        }
    }
}

// ================================================================
// Example 7: Keyboard shortcuts for tab navigation
// ================================================================

@inject IJSRuntime JS

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        await JS.InvokeVoidAsync("setupTabKeyboardShortcuts",
            DotNetObjectReference.Create(this));
    }
}

[JSInvokable]
public void NextTab()
{
    var currentIndex = TabService.Tabs.ToList()
        .FindIndex(t => t.Id == TabService.ActiveTab?.Id);
    if (currentIndex >= 0 && currentIndex < TabService.Tabs.Count - 1)
    {
        TabService.SetActiveTab(TabService.Tabs[currentIndex + 1].Id);
    }
}

[JSInvokable]
public void PreviousTab()
{
    var currentIndex = TabService.Tabs.ToList()
        .FindIndex(t => t.Id == TabService.ActiveTab?.Id);
    if (currentIndex > 0)
    {
        TabService.SetActiveTab(TabService.Tabs[currentIndex - 1].Id);
    }
}

[JSInvokable]
public void CloseCurrentTab()
{
    if (TabService.ActiveTab?.IsClosable == true)
    {
        TabService.CloseTab(TabService.ActiveTab.Id);
    }
}

// JavaScript (in your index.html or separate JS file):
/*
window.setupTabKeyboardShortcuts = (dotNetHelper) => {
    document.addEventListener('keydown', (e) => {
        // Ctrl+Tab or Cmd+Tab - Next tab
        if ((e.ctrlKey || e.metaKey) && e.key === 'Tab' && !e.shiftKey) {
            e.preventDefault();
            dotNetHelper.invokeMethodAsync('NextTab');
        }
        // Ctrl+Shift+Tab - Previous tab
        if ((e.ctrlKey || e.metaKey) && e.key === 'Tab' && e.shiftKey) {
            e.preventDefault();
            dotNetHelper.invokeMethodAsync('PreviousTab');
        }
        // Ctrl+W - Close current tab
        if ((e.ctrlKey || e.metaKey) && e.key === 'w') {
            e.preventDefault();
            dotNetHelper.invokeMethodAsync('CloseCurrentTab');
        }
    });
};
*/