# Circuit Filter Redesign — Design Spec

**Date:** 2026-06-02  
**Status:** Approved

---

## Problem

The existing `CircuitFilter` component has three confirmed bugs and a UX problem that make it unusable at scale:

1. **Blank-grid bug** — `DashboardView` line 82 uses `filteredCircuits.Any() && Contains(...)` while lines 313/325 use `!filteredCircuits.Any() || Contains(...)`. An empty set causes all cards to disappear.
2. **Stale live-status filters** — status chips (Connection / Program / Circuit) are not re-evaluated as circuits change state in real time.
3. **Manual checkbox list doesn't scale** — showing 100+ checkboxes for all circuits in a popover is unmanageable.
4. **No DB-backed access enforcement** — `UserCircuitAccess` table and `IUserCircuitAccessService` exist and are fully implemented but the `LoadUserCircuitFilterAsync` call in `DashboardView` is commented out.
5. **Circuit assignment missing from user creation** — `SaveUserAsync` in `Users.razor` creates a user + assigns a role but does not assign circuits; circuit access is only settable via a separate dialog after creation.

---

## Design

### Two-layer model

| Layer | Name | Scope | Storage | Who controls |
|-------|------|-------|---------|--------------|
| 1 | **Access** | Which circuits this user *may* see | DB — `UserCircuitAccess` table | Admin assigns at create/edit time |
| 2 | **View preference** | Which of their access circuits to show right now | `ServerSessionStorageService` (Permanent=true → DB) | User sets from dashboard |

Layer 1 is enforced server-side on load; it cannot be bypassed via the UI.  
Layer 2 is a subset of layer 1 — the user can never see more circuits than their access set.

Status chips are a **computed display filter** on top of layer 2 — no stored state, recalculated every render, always live.

---

## Layer 1 — Access enforcement (`DashboardView`)

### Admin detection

```
bool isAdmin = CurrentUser.User?.IsInRole("Administrator") ?? false;
```

### Init flow

```
OnInitializedAsync:
  1. Load all circuits from CircuitManager (existing, unchanged)
  2. If isAdmin → _accessCircuits = all circuits
     Else         → call CircuitAccessService.GetByUserIdAsync(CurrentUser.UserId)
                    → _accessCircuits = returned set (empty = no assigned circuits)
  3. Load _visibleCircuits from session storage (see Layer 2)
  4. If nothing persisted → _visibleCircuits = _accessCircuits (show all access circuits)
```

### Grid render rule (replaces line 82)

```
Show card if:
  _visibleCircuits.Count == 0  (empty = show all access)
  OR _visibleCircuits.Contains((dev.DeviceID, dev.CircuitID))
AND
  active status chip matches (or chip == All)
```

The `filteredCircuits` field is replaced by `_accessCircuits` + `_visibleCircuits` (two separate fields).

---

## Layer 2 — View preference (`CircuitFilter` component)

### State

```csharp
// Circuits user may see (passed from parent, derived from DB)
List<(int DeviceId, int CircuitId, string Name)> AccessCircuits   // [Parameter]

// Circuits user chose to show (bidirectional binding)
HashSet<(int DeviceId, int CircuitId)> VisibleCircuits             // [Parameter] @bind

// Storage key
string _storageKey = $"{CurrentUser.UserId}_circuit_view_pref";
```

### Persistence

- On load: `sessionStorage.GetComponentState<CircuitViewPref>(_storageKey)`  
  — if null → default to showing all access circuits  
- On change: `sessionStorage.SetComponentState(_storageKey, state, Permanent: true)`  
  — `Permanent: true` writes through to DB via `IConfigStorageService`

### Popover layout (simplified)

```
┌─────────────────────────────────────────────┐
│ Filter Circuits          [X hidden] [Reset] │
├─────────────────────────────────────────────┤
│ [Search circuits...]                        │
├─────────────────────────────────────────────┤
│ □ Dev1 – C1   □ Dev1 – C2   □ Dev1 – C3    │
│ □ Dev1 – C4   ■ Dev2 – C1   ■ Dev2 – C2    │
│  ... (scrollable, max-h-60)                 │
├─────────────────────────────────────────────┤
│ [Select All]                    [Hide All]  │
└─────────────────────────────────────────────┘
```

- Only shows circuits from `AccessCircuits` (never the full system list)
- Search filters the list by device name or circuit ID
- Badge on toolbar button = count of hidden circuits (0 = no badge)
- "Reset" clears the preference (shows all access circuits)

---

## Layer 3 — Live status chips (toolbar, always visible)

Chips placed on the `DashboardView` toolbar inline with the filter button:

```
[All] [Idle] [Charge] [Discharge] [Running] [Error]
```

- Single-select; defaults to "All"
- No stored state — just a `CircuitStatus? _statusChip` field on `DashboardView`
- Grid render applies chip filter on top of visible circuits
- Chips show a count badge pulled from live circuit status (`circuits.Count(c => ...)`)
- Computed every render — always accurate

---

## Circuit assignment at user creation (`Users.razor`)

### Problem

`SaveUserAsync` creates a user and assigns a role but makes no circuit assignment call. The circuit access dialog is a separate post-creation step that admins may forget.

### Change

After the `UserManager.CreateAsync` + `AddToRoleAsync` calls succeed, immediately show the circuit assignment dialog for the new user (or inline the circuit assignment into the create form as a step).

**Chosen approach: open circuit access dialog immediately after create**, reusing the existing `OpenCircuitAccessDialog(newUser)` method. This is the lowest-risk change — no new UI, just removes the gap.

```csharp
// In SaveUserAsync, after successful create + role assignment:
if (isCreateMode)
{
    Toast.Success(...);
    await LoadUsersAsync();
    CloseDialog();
    await OpenCircuitAccessDialogAsync(newUser);  // <-- add this
}
```

For `Administrator` role users, skip the circuit assignment dialog (they have access to all circuits by definition).

---

## Files changed

| File | Change |
|------|--------|
| `Components/UI/Dashboard/CircuitFilter.razor` | Full rewrite — access-scoped list, search, persist view pref |
| `Components/Pages/Home/DashboardView.razor` | Layer 1 init, status chips on toolbar, fix grid render rule |
| `Components/Pages/Settings/Users.razor` | Open circuit access dialog after user creation (non-admin only) |

No new files, no DB migrations, no new services — all infrastructure already exists.

---

## Out of scope

- Real-time push of access changes (if admin re-assigns circuits while user is logged in, changes take effect on next page load)
- Per-device grouping in the circuit list (can be added later)