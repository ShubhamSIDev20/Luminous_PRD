using BatteryTestingSystem.Components.Layout;
using BatteryTestingSystem.Components.Pages.Home;
using BatteryTestingSystem.Services;
using BatteryTestingSystem.Services.Implementations.Workflow;
using BatteryTestingSystem.Utils;
using Blazicons;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace BatteryTestingSystem.Components.UI.TabViewer;

public class Tab
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "New Tab";
    public SvgIcon Icon { get; set; }
    public Type? ComponentType { get; set; }
    public Dictionary<string, dynamic>? Parameters { get; set; }
    public bool IsClosable { get; set; } = true;
    public bool IsPinned { get; set; } = false;
    public bool HasUnsavedChanges { get; set; } = false;
    public bool Unique { get; set; } = false; 
    public bool KeepAlive { get; set; } = false; // If true, component stays rendered when inactive
    public bool IsMaximized { get; set; } = false; // NEW: Track if tab content is maximized
    public bool WasMaximized { get; set; } = false; // NEW: Track previous maximized state for animation
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Callback to check if tab can be closed
    public Func<Task<bool>>? OnBeforeClose { get; set; }
}
public class BoundingRect
{
    public double Width { get; set; }
    public double Height { get; set; }
    public double Top { get; set; }
    public double Left { get; set; }
    public double Right { get; set; }
    public double Bottom { get; set; }
}

public class TabService 
{
    private string? _sessionId;
    private readonly List<Tab> _tabs = new();
    private Tab? _activeTab;
    private readonly IJSRuntime? _jsRuntime;
    private ServerSessionStorageService _sessionStore;

    /// <summary>Title of the tab the constructor seeds. Excluded from persistence so restore does
    /// not duplicate it — see NotifyStateChanged.</summary>
    private readonly string _defaultTabTitle;

    public event Action? OnChange;

    public IReadOnlyList<Tab> Tabs => _tabs.AsReadOnly();
    public Tab? ActiveTab => _activeTab;

    public TabService(
        ServerSessionStorageService serverSession,
        IJSRuntime? jsRuntime = null,
        IOptions<WorkflowFeatureOptions>? features = null)
    {
        _sessionStore = serverSession;
        _jsRuntime = jsRuntime;

        // Initialize with a default tab. Which component that is belongs to WorkflowDefaultTab:
        // this app navigates by tab rather than by route, so the landing page is decided here and
        // NOT by the nav menu — which is why hiding the dashboard's menu entry alone never stopped
        // it loading on sign-in.
        var landing = features?.Value;
        _defaultTabTitle = WorkflowDefaultTab.TitleFor(landing);
        var defaultTab = new Tab
        {
            Title = _defaultTabTitle,
            Icon = WorkflowDefaultTab.IconFor(landing),
            IsClosable = false,
            ComponentType = WorkflowDefaultTab.ComponentFor(landing)
        };

        _tabs.Add(defaultTab);

        _activeTab = defaultTab;

        _sessionId = CurrentUser.SessionId;

        if (!string.IsNullOrEmpty(_sessionId))
        {
            var Tablist = _sessionStore.GetComponentState<List<Tab>>($"{_sessionId}_tabData");
          
            if (Tablist != null)
                _tabs.AddRange(Tablist);

            var AtTab = _sessionStore.GetComponentState<Tab>($"{_sessionId}_atTab");

            if (AtTab != null && AtTab is Tab)
                _activeTab = AtTab;
        }

        if (_activeTab != null)
            SetActiveTab(_activeTab.Id);

    }


    public Tab AddTab(NavMenuItem navMenuItem)
    {
        SvgIcon svgicon = null;
        string title = null;

        if (navMenuItem.Title == "Display")
        {
            title = "Home";
            svgicon = Lucide.House;
        }

        var tab = new Tab
        {
            Title = title ?? navMenuItem.Title,
            Icon = svgicon ?? navMenuItem.Icon ?? Lucide.File,
            ComponentType = navMenuItem.ComponentType,
            Parameters = navMenuItem.Parameters,
            KeepAlive = navMenuItem.keepAlive,
            Unique = navMenuItem.Unique
        };

        if (navMenuItem.Unique)
        {
            var existingTab = _tabs.FirstOrDefault(t => t.Title == tab.Title);
            if (existingTab != null)
            {
                existingTab.Parameters = tab.Parameters; // Update parameters if needed
                SetActiveTab(existingTab.Id);
                return existingTab;
            }
            else
            {
                _tabs.Add(tab);
                SetActiveTab(tab.Id);
            }
        }
        else
        {
            _tabs.Add(tab);
            SetActiveTab(tab.Id);
        }
      
        NotifyStateChanged();
        return tab;
    }

    public async Task<bool> CanCloseTab(Tab tab)
    {
        if (tab.HasUnsavedChanges && tab.OnBeforeClose != null)
        {
            return await tab.OnBeforeClose.Invoke();
        }

        if (tab.HasUnsavedChanges && _jsRuntime != null)
        {
            return await _jsRuntime.InvokeAsync<bool>(
                "confirm",
                $"'{tab.Title}' has unsaved changes. Do you want to close it anyway?"
            );
        }

        return true;
    }

    public async Task<bool> CanSwitchFromActiveTab()
    {
        if (_activeTab?.HasUnsavedChanges == true && _activeTab.OnBeforeClose != null)
        {
            return await _activeTab.OnBeforeClose.Invoke();
        }

        return true;
    }

    public void CloseTab(string tabId)
    {
        var tab = _tabs.FirstOrDefault(t => t.Id == tabId);
        if (tab == null || !tab.IsClosable) return;

        var index = _tabs.IndexOf(tab);
        _tabs.Remove(tab);

        // If closing active tab, activate another one
        if (_activeTab?.Id == tabId)
        {
            if (_tabs.Count > 0)
            {
                // Activate the tab before the closed one, or the first tab
                var newIndex = Math.Max(0, index - 1);
                _activeTab = _tabs[Math.Min(newIndex, _tabs.Count - 1)];
            }
            else
            {
                _activeTab = null;
            }
        }

        NotifyStateChanged();
    }

    public async Task CloseAllTabsAsync(bool exceptPinned = true)
    {
        var tabsToClose = exceptPinned
            ? _tabs.Where(t => t.IsClosable && !t.IsPinned).ToList()
            : _tabs.Where(t => t.IsClosable).ToList();

        foreach (var tab in tabsToClose)
        {
            var canClose = await CanCloseTab(tab);
            if (!canClose) continue;

            _tabs.Remove(tab);
        }

        if (_activeTab != null && !_tabs.Contains(_activeTab))
        {
            _activeTab = _tabs.FirstOrDefault();
        }

        NotifyStateChanged();
    }

    public async Task CloseOtherTabsAsync(string tabId)
    {
        var keepTab = _tabs.FirstOrDefault(t => t.Id == tabId);
        if (keepTab == null) return;

        var tabsToClose = _tabs
            .Where(t => t.Id != tabId && t.IsClosable && !t.IsPinned)
            .ToList();

        foreach (var tab in tabsToClose)
        {
            var canClose = await CanCloseTab(tab);
            if (!canClose) continue;

            _tabs.Remove(tab);
        }

        SetActiveTab(tabId);
    }

    public async Task CloseTabsToRightAsync(string tabId)
    {
        var index = _tabs.FindIndex(t => t.Id == tabId);
        if (index == -1) return;

        var tabsToClose = _tabs
            .Skip(index + 1)
            .Where(t => t.IsClosable && !t.IsPinned)
            .ToList();

        foreach (var tab in tabsToClose)
        {
            var canClose = await CanCloseTab(tab);
            if (!canClose) continue;

            _tabs.Remove(tab);
        }

        NotifyStateChanged();
    }

    public void SetActiveTab(string tabId)
    {
        var tab = _tabs.FirstOrDefault(t => t.Id == tabId);

        if (tab != null)
        {
            _activeTab = tab;
        }
        else
        {
            _activeTab = _tabs.FirstOrDefault();
        }

        NotifyStateChanged();

    }

    public void UpdateTabTitle(string tabId, string title)
    {
        var tab = _tabs.FirstOrDefault(t => t.Id == tabId);
        if (tab != null)
        {
            tab.Title = title;
            NotifyStateChanged();
        }
    }

    public void SetTabUnsavedChanges(string tabId, bool hasChanges)
    {
        var tab = _tabs.FirstOrDefault(t => t.Id == tabId);
        if (tab != null)
        {
            tab.HasUnsavedChanges = hasChanges;
            NotifyStateChanged();
        }
    }

    public void TogglePinTab(string tabId)
    {
        var tab = _tabs.FirstOrDefault(t => t.Id == tabId);
        if (tab != null)
        {
            tab.IsPinned = !tab.IsPinned;

            // Move pinned tabs to the front
            if (tab.IsPinned)
            {
                _tabs.Remove(tab);
                var lastPinnedIndex = _tabs.FindLastIndex(t => t.IsPinned);
                _tabs.Insert(lastPinnedIndex + 1, tab);
            }

            NotifyStateChanged();
        }
    }

    public void ReorderTabs(string tabId, int newIndex)
    {
        var tab = _tabs.FirstOrDefault(t => t.Id == tabId);
        if (tab == null) return;

        var oldIndex = _tabs.IndexOf(tab);
        if (oldIndex == newIndex) return;

        _tabs.RemoveAt(oldIndex);
        _tabs.Insert(newIndex, tab);
        NotifyStateChanged();
    }

    // NEW: Maximize tab content
    public void MaximizeTab(string tabId)
    {
        var tab = _tabs.FirstOrDefault(t => t.Id == tabId);
        if (tab != null)
        {
            tab.WasMaximized = tab.IsMaximized;
            tab.IsMaximized = true;
            NotifyStateChanged();
        }
    }

    // NEW: Restore tab content to normal size
    public void RestoreTab(string tabId)
    {
        var tab = _tabs.FirstOrDefault(t => t.Id == tabId);
        if (tab != null)
        {
            tab.WasMaximized = tab.IsMaximized;
            tab.IsMaximized = false;
            NotifyStateChanged();
        }
    }

    // NEW: Check if tab is maximized
    public bool IsTabMaximized(string tabId)
    {
        var tab = _tabs.FirstOrDefault(t => t.Id == tabId);
        return tab?.IsMaximized ?? false;
    }

    // NEW: Check if tab was previously maximized (for animation)
    public bool WasTabMaximized(string tabId)
    {
        var tab = _tabs.FirstOrDefault(t => t.Id == tabId);
        return tab?.WasMaximized ?? false;
    }

    private void NotifyStateChanged()
    {

        if (!string.IsNullOrEmpty(_sessionId))
        {
            var store = Tabs.ToList();

            // Never persist the seeded default tab: the constructor re-creates it on every circuit,
            // so saving it here means restore appends a second copy of it. Keyed on the field the
            // constructor actually used rather than a literal - this was hard-coded to "Home", so
            // renaming the default tab silently slipped it past this filter and duplicated it.
            store.RemoveAll(e => e.Title == _defaultTabTitle);
            _sessionStore.SetComponentState($"{_sessionId}_tabData", store);

            if (_activeTab != null)
                _sessionStore.SetComponentState($"{_sessionId}_atTab", _activeTab);

        }

        OnChange?.Invoke();

    }


}
