# Frontend
> Updated: 2026-08-10T00:00:00Z
> Framework: Blazor Server (ASP.NET Core, Interactive Server rendering) | Styling: Not confirmed in scan — check `wwwroot/` for CSS framework | State: Component-local + injected services (no separate state library observed)

## Structure
```
Components/
├── Layout/          # Layout shells (nav, page chrome)
├── UI/               # Shared/reusable Blazor UI components
└── Pages/            # Route-level .razor pages, grouped by feature
    ├── Devices/      # DeviceList.razor, device detail/management pages
    ├── Batteries/    # Battery list/detail pages
    ├── Programs/     # ProgramList.razor, ProgramEditor.razor
    ├── Reports/      # Reports.razor — consumes IExportRepository directly
    ├── Settings/      # Users.razor, general settings
    ├── Home/         # Dashboard/landing (DashboardView.razor)
    └── Test/         # Test/dev pages
wwwroot/               # Static assets (css/js/images)
```

## Patterns
- Blazor Server with **Interactive Server** render mode (configured in `Program.cs`) — components run server-side, UI updates via SignalR circuit
- Pages under `Components/Pages/` call injected services directly (e.g. `IProgramServices`, `IBatteryServices`, `IDeviceChannelServices`, `IDbcService`, `ChannelManager`) rather than going through the REST API
- `Reports.razor` and `BmsDashboard.razor`-style pages inject `IExportRepository` directly — flagged in the clean-architecture design doc as a coupling risk to revisit if/when the API v2 split happens
- Live device data reaches the UI via server-side polling/streaming against `ChannelManager` (same path as `GetLiveData` / `GetLiveSSE` REST endpoints use)

## Key Components (partial — expand as encountered)
| Component | Location | What It Does |
|-----------|----------|-------------|
| `DeviceList.razor` | `Components/Pages/Devices/` | Lists devices, likely entry point for device management |
| `DashboardView.razor` | `Components/Pages/Home/` | Home/dashboard landing page |
| `ProgramEditor.razor` | `Components/Pages/Programs/` | Create/edit battery test programs |
| `ProgramList.razor` | `Components/Pages/Programs/` | List test programs |
| `Reports.razor` | `Components/Pages/Reports/` | Report viewing; direct `IExportRepository` consumer |
| `SchedulerPage.razor` | `Components/Pages/` (subfolder not confirmed) | Program scheduling UI |
| `Users.razor` | `Components/Pages/Settings/` | User management |

## Notes
- Do not assume a client-side JS framework (React/Vue/Angular) is present — this is server-rendered Blazor; most "frontend" logic is actually C# running server-side.
- If a CSS framework (Bootstrap, Tailwind, etc.) is confirmed later, record it here — not verified in this scan pass.