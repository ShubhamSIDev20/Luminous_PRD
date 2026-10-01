# TextArea Component

A multi-line text input component with Tailwind CSS styling.

## Usage

### Basic Example

```razor
<TextArea Placeholder="Type your message here..." />
```

### With Two-Way Binding

```razor
@code {
    private string message = "";
}

<TextArea @bind-Value="message" Placeholder="Enter your comment..." />
<p>Character count: @message.Length</p>
```

### With Custom Rows

```razor
<TextArea Rows="10" Placeholder="Long form content..." />
```

### Disabled State

```razor
<TextArea Value="This is read-only content" Disabled="true" />
```

### With Label

```razor
<div class="space-y-2">
    <Label For="description">Description</Label>
    <TextArea Id="description" Placeholder="Enter a description..." />
</div>
```

### Full Form Example

```razor
<Card>
    <CardHeader>
        <CardTitle>Feedback Form</CardTitle>
        <CardDescription>Share your thoughts with us</CardDescription>
    </CardHeader>
    <CardContent Class="space-y-4">
        <div class="space-y-2">
            <Label For="name">Name</Label>
            <Input Id="name" Placeholder="Your name" />
        </div>
        <div class="space-y-2">
            <Label For="feedback">Feedback</Label>
            <TextArea Id="feedback" 
                      Placeholder="Tell us what you think..." 
                      Rows="5" />
        </div>
    </CardContent>
    <CardFooter>
        <Button>Submit Feedback</Button>
    </CardFooter>
</Card>
```

## API Reference

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Value` | `string?` | `null` | The textarea value |
| `ValueChanged` | `EventCallback<string>` | - | Callback when value changes |
| `Placeholder` | `string?` | `null` | Placeholder text |
| `Disabled` | `bool` | `false` | Disable the textarea |
| `ReadOnly` | `bool` | `false` | Make textarea read-only |
| `Rows` | `int` | `4` | Number of visible text rows |
| `Class` | `string?` | `null` | Additional CSS classes |
| `AdditionalAttributes` | `Dictionary<string, object>?` | `null` | Additional HTML attributes |

## Features

- ✅ Two-way data binding with `@bind-Value`
- ✅ Disabled and read-only states
- ✅ Customizable rows
- ✅ Placeholder support
- ✅ Focus ring styling
- ✅ Responsive design
- ✅ Tailwind CSS styling

## Styling

Default classes include:
```
flex min-h-[80px] w-full rounded-md border border-input bg-background 
px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground 
focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring 
focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50
```

Override with the `Class` parameter for custom styling.
