---
name: gortex-components-layout-6-dirs
description: "Work in the Components\Layout +6 dirs area — 119 symbols across 11 files (91% cohesion)"
---

# Components\Layout +6 dirs

119 symbols | 11 files | 91% cohesion

## When to Use

Use this skill when working on files in:
- `Components\Layout\AppLayout.razor`
- `Components\Layout\LayoutModels.cs`
- `Components\Layout\MainLayout.razor`
- `Components\Layout\Navbar.razor`
- `Components\Pages\Home\DashboardView.razor`
- `Components\Pages\Programs\ProgramList.razor`
- `Components\Pages\Reports\Reports.razor`
- `Components\UI\Program\TableRowContextMenu.razor`
- `Components\UI\TabViewer\TabService.cs`
- `Components\UI\TabViewer\TabViewer.razor`
- `Services\ServerSessionStorageService.cs`

## Key Files

| File | Symbols |
|------|---------|
| `Components\Layout\AppLayout.razor` | MenuItems |
| `Components\Layout\LayoutModels.cs` | Icon, Title, keepAlive, Unique, ComponentType, ... |
| `Components\Layout\MainLayout.razor` | _menuItems, navigator, HandleNavigate |
| `Components\Layout\Navbar.razor` | MenuItems |
| `Components\Pages\Home\DashboardView.razor` | ToggleDbcPanel, SaveDbcPanelState |
| `Components\Pages\Programs\ProgramList.razor` | OpenCode, SwitchToEditMode, programId |
| `Components\Pages\Reports\Reports.razor` | OpenView, session |
| `Components\UI\Program\TableRowContextMenu.razor` | Invoke, cb |
| `Components\UI\TabViewer\TabService.cs` | IsTabMaximized, UpdateTabTitle, HasUnsavedChanges, tabId, tabId, ... |
| `Components\UI\TabViewer\TabViewer.razor` | CloseAll, action, CloseContextMenu, tab, HandleTabClick, ... |
| `Services\ServerSessionStorageService.cs` | state, Permanent, SetComponentState, storeId |

## Connected Communities

- **Components\UI\Program +15 dirs** (3 cross-edges)
- **Services +4 dirs** (1 cross-edges)
- **Components\Pages\Programs +18 dirs** (1 cross-edges)

## How to Explore

```
get_communities with id: "community-163"
smart_context with task: "understand Components\Layout +6 dirs", format: "gcx"
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
