---
name: gortex-components-layout-22-dirs
description: "Work in the Components\Layout +22 dirs area — 152 symbols across 46 files (86% cohesion)"
---

# Components\Layout +22 dirs

152 symbols | 46 files | 86% cohesion

## When to Use

Use this skill when working on files in:
- `Components\Layout\AppLayout.razor`
- `Components\Layout\LayoutModels.cs`
- `Components\Layout\MainLayout.razor`
- `Components\Layout\Navbar.razor`
- `Components\UI\Button\Button.razor`
- `Components\UI\Checkbox\Checkbox.razor`
- `Components\UI\ContextMenu\ContextMenu.razor`
- `Components\UI\ContextMenu\ContextMenuContent.razor`
- `Components\UI\ContextMenu\ContextMenuItem.razor`
- `Components\UI\ContextMenu\ContextMenuTrigger.razor`
- `Components\UI\Dashboard\CircuitActionBar.razor`
- `Components\UI\Dashboard\DbcValuePanel.razor`
- `Components\UI\Dashboard\TransferDialog.razor`
- `Components\UI\Dashboard\TransferResultsDialog.razor`
- `Components\UI\DataViewer\BmsChart.razor`
- `Components\UI\DataViewer\BmsDashboard.razor`
- `Components\UI\DataViewer\BmsDataTable.razor`
- `Components\UI\Dialog\ConfirmDialog.razor`
- `Components\UI\Dialog\Dialog.razor`
- `Components\UI\Dialog\DialogTrigger.razor`
- `Components\UI\Form\FormCheckbox.razor`
- `Components\UI\Form\FormInputText.razor`
- `Components\UI\Form\FormTextArea.razor`
- `Components\UI\Input\INumber.razor`
- `Components\UI\Input\Input.razor`
- `Components\UI\Pagination\PaginationLink.razor`
- `Components\UI\Program\StepRow.razor`
- `Components\UI\RadioGroup\RadioGroup.razor`
- `Components\UI\RadioGroup\RadioGroupItem.razor`
- `Components\UI\ScrollArea\InfiniteScrollComponent.razor`
- `Components\UI\ScrollArea\Infinite_Scroll_Demo.razor`
- `Components\UI\Search\Search.razor`
- `Components\UI\Switch\Switch.razor`
- `Components\UI\Tabs\Tabs.razor`
- `Components\UI\Tabs\TabsTrigger.razor`
- `Components\UI\TextArea\TextArea.razor`
- `Components\UI\Themes\ThemeEnums.cs`
- `Components\UI\Themes\ThemeProvider.razor`
- `Components\UI\Toast\Toast.razor`
- `Components\UI\TreeView\TreeNode.razor`
- `Components\UI\TreeView\TreeView.razor`
- `Components\UI\TreeView\TreeViewContextMenu.razor`
- `Components\UI\TreeView\TreeViewContextMenuItem.razor`
- `Middleware\ExceptionHandlerMiddleware.cs`
- `Models\DTOs\ResponseDTOs.cs`
- `Models\ViewModels\PageChangeRequest.cs`

## Key Files

| File | Symbols |
|------|---------|
| `Components\Layout\AppLayout.razor` | CurrentTheme, Notifications |
| `Components\Layout\LayoutModels.cs` | Read, Timestamp, Message, NotificationItem, Id |
| `Components\Layout\MainLayout.razor` | _notifications, firstRender, SaveNotificationsToIndexedDb, HandleThemeChange, OnAfterRenderAsync, ... |
| `Components\Layout\Navbar.razor` | HandleNotificationClick, CurrentTheme, Navigate, id, navMenuItem, ... |
| `Components\UI\Button\Button.razor` | OnClickHandler, e |
| `Components\UI\Checkbox\Checkbox.razor` | HandleClick |
| `Components\UI\ContextMenu\ContextMenu.razor` | ContextMenu, Close |
| `Components\UI\ContextMenu\ContextMenuContent.razor` | ContextMenu |
| `Components\UI\ContextMenu\ContextMenuItem.razor` | HandleClick, ContextMenu |
| `Components\UI\ContextMenu\ContextMenuTrigger.razor` | ContextMenu |
| `Components\UI\Dashboard\CircuitActionBar.razor` | actionId, HandleAction, disabled |
| `Components\UI\Dashboard\DbcValuePanel.razor` | ToggleDock, ClosePanel, HandleDataChanged, Dispose |
| `Components\UI\Dashboard\TransferDialog.razor` | Close |
| `Components\UI\Dashboard\TransferResultsDialog.razor` | handleClose |
| `Components\UI\DataViewer\BmsChart.razor` | HandleFullscreenClick |
| `Components\UI\DataViewer\BmsDashboard.razor` | page, ConfirmDownloadExistingAsync, HandleRegLabelSelected, label, pageSize, ... |
| `Components\UI\DataViewer\BmsDataTable.razor` | GoToPage, OnExportClick, PrevPage, page, NextPage, ... |
| `Components\UI\Dialog\ConfirmDialog.razor` | Open, Confirm, Cancel |
| `Components\UI\Dialog\Dialog.razor` | IsOpen, Close, Toggle |
| `Components\UI\Dialog\DialogTrigger.razor` | HandleClick |
| `Components\UI\Form\FormCheckbox.razor` | HandleChange, e |
| `Components\UI\Form\FormInputText.razor` | e, HandleChange |
| `Components\UI\Form\FormTextArea.razor` | HandleChange, e |
| `Components\UI\Input\INumber.razor` | e, OnInputHandler |
| `Components\UI\Input\Input.razor` | OnChangeHandler, e |
| `Components\UI\Pagination\PaginationLink.razor` | HandleClick |
| `Components\UI\Program\StepRow.razor` | HandleLastColumnKeyDown, HandleFirstColumnKeyDown, e, e |
| `Components\UI\RadioGroup\RadioGroup.razor` | value, SetValue |
| `Components\UI\RadioGroup\RadioGroupItem.razor` | HandleClick |
| `Components\UI\ScrollArea\InfiniteScrollComponent.razor` | RefreshAsync, SetViewMode, mode, OnItemsPerPageChangedHandler |
| `Components\UI\ScrollArea\Infinite_Scroll_Demo.razor` | RefreshData |
| `Components\UI\Search\Search.razor` | SelectItem, item |
| `Components\UI\Switch\Switch.razor` | HandleClick |
| `Components\UI\Tabs\Tabs.razor` | SetValue, value |
| `Components\UI\Tabs\TabsTrigger.razor` | HandleClick |
| `Components\UI\TextArea\TextArea.razor` | e, OnInput |
| `Components\UI\Themes\ThemeEnums.cs` | Future, Tech, ThemeMode, ShieldOS, Light, ... |
| `Components\UI\Themes\ThemeProvider.razor` | Mode, mode, SetTheme, ToggleTheme |
| `Components\UI\Toast\Toast.razor` | HandleClose |
| `Components\UI\TreeView\TreeNode.razor` | HandleContextMenuAction, action, HandleDoubleClick, HandleClick |
| `Components\UI\TreeView\TreeView.razor` | HandleNodeDoubleClick, HandleNodeClick, item, item |
| `Components\UI\TreeView\TreeViewContextMenu.razor` | HandleClose |
| `Components\UI\TreeView\TreeViewContextMenuItem.razor` | HandleClick |
| `Middleware\ExceptionHandlerMiddleware.cs` | _environment, environment, HandleExceptionAsync, _logger, UseCustomExceptionHandler, ... |
| `Models\DTOs\ResponseDTOs.cs` | ErrorDescription, ErrorResponse, State, ErrorUri, Error |
| `Models\ViewModels\PageChangeRequest.cs` | PageChangeRequest |

## Connected Communities

- **Components\Pages\Programs +18 dirs** (1 cross-edges)

## How to Explore

```
get_communities with id: "community-201"
smart_context with task: "understand Components\Layout +22 dirs", format: "gcx"
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
