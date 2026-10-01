---
name: gortex-components-ui-windowmanager-1-dirs-windowmodel
description: "Work in the Components\UI\WindowManager +1 dirs · WindowModel area — 94 symbols across 5 files (98% cohesion)"
---

# Components\UI\WindowManager +1 dirs · WindowModel

94 symbols | 5 files | 98% cohesion

## When to Use

Use this skill when working on files in:
- `Components\Layout\WindowLayout.razor`
- `Components\UI\WindowManager\TaskManager.razor`
- `Components\UI\WindowManager\Window.razor`
- `Components\UI\WindowManager\WindowManager.razor`
- `Components\UI\WindowManager\WindowModel.cs`

## Key Files

| File | Symbols |
|------|---------|
| `Components\Layout\WindowLayout.razor` | GetAllWindows |
| `Components\UI\WindowManager\TaskManager.razor` | Windows, GetStatusBadgeClass, state |
| `Components\UI\WindowManager\Window.razor` | GetWindowClass, e, OnInitialized, StartDrag, model |
| `Components\UI\WindowManager\WindowManager.razor` | window, RestoreWindow, id, SetTaskbarDisplayMode, id, ... |
| `Components\UI\WindowManager\WindowModel.cs` | IconName, IconWithText, WindowStateData, State, Maximized, ... |

## How to Explore

```
get_communities with id: "community-191"
smart_context with task: "understand Components\UI\WindowManager +1 dirs · WindowModel", format: "gcx"
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
