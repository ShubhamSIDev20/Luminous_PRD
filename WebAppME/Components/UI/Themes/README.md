# Theme System

The SafeEyeStream.UI library includes a flexible theme system with light and dark mode support.

## Components

- **ThemeProvider**: Root component that provides theme context
- **ThemeMode**: Enum for Light, Dark, and System modes
- **LightTheme.razor.css**: CSS custom properties for light theme
- **DarkTheme.razor.css**: CSS custom properties for dark theme

## Usage

### Basic Setup

Wrap your application with ThemeProvider:

```razor
@page "/"
@using SafeEyeStream.UI.Themes

<ThemeProvider Mode="@currentTheme" ModeChanged="@OnThemeChanged">
    <YourAppContent />
</ThemeProvider>

@code {
    private ThemeMode currentTheme = ThemeMode.Light;

    private void OnThemeChanged(ThemeMode mode)
    {
        currentTheme = mode;
        // Optionally save to local storage
    }
}
```

### Theme Toggle Button

```razor
<ThemeProvider @ref="themeProvider" Mode="@theme">
    <div class="p-4">
        <Button OnClick="@(() => themeProvider.ToggleTheme())">
            @(themeProvider.IsDark ? "🌙" : "☀️") Toggle Theme
        </Button>
        
        <Card>
            <CardHeader>
                <CardTitle>Theme Demo</CardTitle>
            </CardHeader>
            <CardContent>
                <p>Current theme: @(themeProvider.IsDark ? "Dark" : "Light")</p>
            </CardContent>
        </Card>
    </div>
</ThemeProvider>

@code {
    private ThemeProvider? themeProvider;
    private ThemeMode theme = ThemeMode.Light;
}
```

### Manual Theme Control

```razor
<ThemeProvider @ref="themeProvider">
    <div class="flex gap-2">
        <Button OnClick="@(() => themeProvider.SetTheme(ThemeMode.Light))">
            Light
        </Button>
        <Button OnClick="@(() => themeProvider.SetTheme(ThemeMode.Dark))">
            Dark
        </Button>
    </div>
    
    <YourContent />
</ThemeProvider>
```

### With Local Storage Persistence

```razor
@inject IJSRuntime JS

<ThemeProvider Mode="@currentTheme" ModeChanged="@SaveTheme">
    <YourAppContent />
</ThemeProvider>

@code {
    private ThemeMode currentTheme = ThemeMode.Light;

    protected override async Task OnInitializedAsync()
    {
        // Load theme from local storage
        var savedTheme = await JS.InvokeAsync<string>("localStorage.getItem", "theme");
        if (Enum.TryParse<ThemeMode>(savedTheme, out var theme))
        {
            currentTheme = theme;
        }
    }

    private async Task SaveTheme(ThemeMode mode)
    {
        currentTheme = mode;
        await JS.InvokeVoidAsync("localStorage.setItem", "theme", mode.ToString());
    }
}
```

## API Reference

### ThemeProvider

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ChildContent` | `RenderFragment?` | `null` | Content to wrap with theme |
| `Mode` | `ThemeMode` | `Light` | Current theme mode |
| `ModeChanged` | `EventCallback<ThemeMode>` | - | Callback when theme changes |
| `CustomThemeClass` | `string?` | `null` | Additional CSS classes |

### Properties

- `IsDark`: Returns true if current mode is Dark
- `IsLight`: Returns true if current mode is Light

### Methods

- `ToggleTheme()`: Toggles between light and dark modes
- `SetTheme(ThemeMode mode)`: Sets a specific theme mode

### ThemeMode Enum

```csharp
public enum ThemeMode
{
    Light,
    Dark,
    System  // For future OS-level theme detection
}
```

## CSS Custom Properties

The theme system uses CSS custom properties (variables) that are automatically applied based on the current theme.

### Available Variables

- `--background`: Main background color
- `--foreground`: Main text color
- `--card`: Card background
- `--card-foreground`: Card text color
- `--primary`: Primary button/accent color
- `--primary-foreground`: Primary button text
- `--secondary`: Secondary elements
- `--muted`: Muted/disabled elements
- `--accent`: Accent colors
- `--destructive`: Error/danger colors
- `--border`: Border colors
- `--input`: Input field colors
- `--ring`: Focus ring colors
- `--radius`: Border radius value

## Tailwind Configuration

Make sure your `tailwind.config.js` includes dark mode:

```javascript
module.exports = {
  darkMode: 'class', // Use class-based dark mode
  // ... rest of config
}
```

## Features

- ✅ Light and Dark themes
- ✅ Cascading theme context
- ✅ Toggle functionality
- ✅ Programmatic theme control
- ✅ CSS custom properties
- ✅ Tailwind CSS integration
- ✅ Persistent theme storage support
- ✅ System theme detection (future)

## Best Practices

1. **Wrap at root level**: Place ThemeProvider at your app's root for global access
2. **Save preferences**: Use local storage to persist user's theme choice
3. **Provide toggle**: Always give users a way to switch themes
4. **Test both modes**: Ensure all components look good in light and dark modes
5. **Use semantic colors**: Use CSS variables instead of hard-coded colors
6. **Respect accessibility**: Ensure sufficient contrast in both themes

## Example: Complete App Setup

```razor
@* App.razor or MainLayout.razor *@
@using SafeEyeStream.UI.Themes
@inject IJSRuntime JS

<ThemeProvider @ref="themeProvider" Mode="@currentTheme" ModeChanged="@OnThemeChanged">
    <div class="min-h-screen bg-background text-foreground">
        <header class="border-b">
            <div class="container mx-auto p-4 flex justify-between items-center">
                <h1 class="text-2xl font-bold">My App</h1>
                <Button Variant="ButtonVariant.Ghost" 
                        OnClick="@(() => themeProvider?.ToggleTheme())">
                    @(themeProvider?.IsDark == true ? "🌙" : "☀️")
                </Button>
            </div>
        </header>
        
        <main class="container mx-auto p-4">
            @Body
        </main>
    </div>
</ThemeProvider>

@code {
    private ThemeProvider? themeProvider;
    private ThemeMode currentTheme = ThemeMode.Light;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            var savedTheme = await JS.InvokeAsync<string?>("localStorage.getItem", "theme");
            if (!string.IsNullOrEmpty(savedTheme) && 
                Enum.TryParse<ThemeMode>(savedTheme, out var theme))
            {
                currentTheme = theme;
                StateHasChanged();
            }
        }
    }

    private async Task OnThemeChanged(ThemeMode mode)
    {
        currentTheme = mode;
        await JS.InvokeVoidAsync("localStorage.setItem", "theme", mode.ToString());
    }
}
```

This setup provides a complete theming solution for your Blazor application!
