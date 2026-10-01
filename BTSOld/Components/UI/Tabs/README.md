# Tabs Component

A set of layered sections of content—known as tab panels—that are displayed one at a time.

## Components

- **Tabs**: The root component that manages tab state
- **TabsList**: Container for tab triggers
- **TabsTrigger**: Individual tab button
- **TabsContent**: Content panel for each tab

## Usage

### Basic Example

```razor
<Tabs DefaultValue="account">
    <TabsList>
        <TabsTrigger Value="account">Account</TabsTrigger>
        <TabsTrigger Value="password">Password</TabsTrigger>
    </TabsList>
    
    <TabsContent Value="account">
        <Card>
            <CardHeader>
                <CardTitle>Account</CardTitle>
                <CardDescription>
                    Make changes to your account here.
                </CardDescription>
            </CardHeader>
            <CardContent>
                <p>Account settings content goes here.</p>
            </CardContent>
        </Card>
    </TabsContent>
    
    <TabsContent Value="password">
        <Card>
            <CardHeader>
                <CardTitle>Password</CardTitle>
                <CardDescription>
                    Change your password here.
                </CardDescription>
            </CardHeader>
            <CardContent>
                <p>Password settings content goes here.</p>
            </CardContent>
        </Card>
    </TabsContent>
</Tabs>
```

### Controlled Tabs

```razor
@code {
    private string activeTab = "account";
}

<Tabs Value="@activeTab" ValueChanged="@((value) => activeTab = value)">
    <TabsList>
        <TabsTrigger Value="account">Account</TabsTrigger>
        <TabsTrigger Value="password">Password</TabsTrigger>
        <TabsTrigger Value="settings">Settings</TabsTrigger>
    </TabsList>
    
    <TabsContent Value="account">
        <p>Account content</p>
    </TabsContent>
    
    <TabsContent Value="password">
        <p>Password content</p>
    </TabsContent>
    
    <TabsContent Value="settings">
        <p>Settings content</p>
    </TabsContent>
</Tabs>

<p>Current tab: @activeTab</p>
```


### With Disabled Tab

```razor
<Tabs DefaultValue="tab1">
    <TabsList>
        <TabsTrigger Value="tab1">Tab 1</TabsTrigger>
        <TabsTrigger Value="tab2">Tab 2</TabsTrigger>
        <TabsTrigger Value="tab3" Disabled="true">Tab 3 (Disabled)</TabsTrigger>
    </TabsList>
    
    <TabsContent Value="tab1">Content 1</TabsContent>
    <TabsContent Value="tab2">Content 2</TabsContent>
    <TabsContent Value="tab3">Content 3</TabsContent>
</Tabs>
```

### Custom Styling

```razor
<Tabs DefaultValue="overview">
    <TabsList Class="bg-slate-100 dark:bg-slate-800">
        <TabsTrigger Value="overview" Class="data-[state=active]:bg-white">
            Overview
        </TabsTrigger>
        <TabsTrigger Value="analytics" Class="data-[state=active]:bg-white">
            Analytics
        </TabsTrigger>
    </TabsList>
    
    <TabsContent Value="overview" Class="mt-4 p-4 border rounded-lg">
        <p>Overview content with custom styling</p>
    </TabsContent>
    
    <TabsContent Value="analytics" Class="mt-4 p-4 border rounded-lg">
        <p>Analytics content with custom styling</p>
    </TabsContent>
</Tabs>
```


## API Reference

### Tabs

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ChildContent` | `RenderFragment?` | `null` | The content to render inside the tabs container |
| `DefaultValue` | `string?` | `null` | The default active tab value (uncontrolled) |
| `Value` | `string?` | `null` | The active tab value (controlled) |
| `ValueChanged` | `EventCallback<string>` | - | Callback fired when the active tab changes |
| `AdditionalAttributes` | `Dictionary<string, object>?` | `null` | Additional HTML attributes |

### TabsList

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ChildContent` | `RenderFragment?` | `null` | The tab triggers to render |
| `Class` | `string?` | `null` | Additional CSS classes |
| `AdditionalAttributes` | `Dictionary<string, object>?` | `null` | Additional HTML attributes |

**Default Classes:**
```
inline-flex h-10 items-center justify-center rounded-md bg-muted p-1 text-muted-foreground
```

### TabsTrigger

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Value` | `string` | *Required* | The value that identifies this tab |
| `ChildContent` | `RenderFragment?` | `null` | The content to display in the trigger |
| `Class` | `string?` | `null` | Additional CSS classes |
| `Disabled` | `bool` | `false` | Whether the tab is disabled |
| `AdditionalAttributes` | `Dictionary<string, object>?` | `null` | Additional HTML attributes |

**Default Classes:**
```
inline-flex items-center justify-center whitespace-nowrap rounded-sm px-3 py-1.5 text-sm 
font-medium ring-offset-background transition-all data-[state=active]:bg-background 
data-[state=active]:text-foreground data-[state=active]:shadow-sm focus-visible:outline-none 
focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 
disabled:pointer-events-none disabled:opacity-50
```

### TabsContent

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Value` | `string` | *Required* | The value that identifies this content panel |
| `ChildContent` | `RenderFragment?` | `null` | The content to display when tab is active |
| `Class` | `string?` | `null` | Additional CSS classes |
| `AdditionalAttributes` | `Dictionary<string, object>?` | `null` | Additional HTML attributes |

**Default Classes:**
```
mt-2 ring-offset-background focus-visible:outline-none focus-visible:ring-2 
focus-visible:ring-ring focus-visible:ring-offset-2
```

## Features

- ✅ Controlled and uncontrolled modes
- ✅ Keyboard navigation (cascading value pattern)
- ✅ Disabled tabs
- ✅ Custom styling support
- ✅ Accessible with proper ARIA attributes
- ✅ Active state management
- ✅ Tailwind CSS styling
- ✅ State synchronization between components

## Notes

- The `Tabs` component uses `CascadingValue` to pass state to child components
- Only the active `TabsContent` is rendered (conditional rendering)
- The `data-state` attribute is set for CSS styling purposes (active/inactive)
- Tab triggers automatically manage their active state through the parent Tabs component
- Use `DefaultValue` for uncontrolled tabs or `Value`/`ValueChanged` for controlled tabs

## Styling with Tailwind CSS

The component uses Tailwind utility classes. Key classes include:

- **TabsList**: `bg-muted`, `rounded-md`, `inline-flex`
- **TabsTrigger**: `data-[state=active]:bg-background`, `transition-all`
- **TabsContent**: `ring-offset-background`, `focus-visible:ring-2`

You can override these by passing custom classes through the `Class` parameter.

## Accessibility

- Uses semantic HTML (`button`, `div` with `role="tabpanel"`)
- Active tab triggers have `data-state="active"` attribute
- Tab content panels have `role="tabpanel"` and `tabindex="0"`
- Focus management with `focus-visible` styles
- Disabled state properly prevents interaction

## Migration from React

This component replicates the behavior of the React version using:
- `@radix-ui/react-tabs` → Blazor `CascadingValue` pattern
- React `forwardRef` → Blazor `@ref` and `AdditionalAttributes`
- React state hooks → Blazor `@code` block with state management
- Props spreading → `AdditionalAttributes` capture
