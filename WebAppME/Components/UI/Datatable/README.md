# DataTable Component

Generic, self-contained Blazor data table with built-in Excel export.

## Files

| File | Purpose |
|------|---------|
| `DataTable.razor` | Main generic component |
| `ColumnDef.razor` | Child column definition |
| `DataTableModels.cs` | Supporting types |
| `ExamplePage.razor` | Full working example |

## Setup

**1. Copy the 3 component files** into your project, e.g. `Components/DataTable/`.

**2. Add NuGet package** (required for Excel export):
```
dotnet add package DocumentFormat.OpenXml
```

**3. Add to `_Imports.razor`:**
```razor
@using UI.Components.DataTable
```

---

## Usage

### Client-side (default)

```razor
<DataTable TItem="MyModel"
           Title="My Data"
           Data="@_list"
           Search="true"
           ExcelExport="true"
           ExcelFileName="my-data">
    <Columns>
        <ColumnDef TItem="MyModel" Key="id" Header="ID"
                   SearchSelector="@(r => r.Id.ToString())"
                   ExcelValueSelector="@(r => r.Id.ToString())">
            <CellTemplate Context="row">@row.Id</CellTemplate>
        </ColumnDef>
        <ColumnDef TItem="MyModel" Key="name" Header="Name"
                   SearchSelector="@(r => r.Name)"
                   ExcelValueSelector="@(r => r.Name)">
            <CellTemplate Context="row">@row.Name</CellTemplate>
        </ColumnDef>
    </Columns>
</DataTable>
```

### Server-side

```razor
<DataTable TItem="OrderDto"
           Title="Orders"
           ServerSide="true"
           DataSource="@FetchOrders"
           Search="true"
           ExcelExport="true">
    <Columns>...</Columns>
</DataTable>

@code {
    private async Task<DataTableResult<OrderDto>> FetchOrders(DataTableRequest req)
    {
        var result = await _service.GetPagedAsync(req.Page, req.PageSize, req.Search);
        return new DataTableResult<OrderDto>
        {
            Rows      = result.Items,
            TotalRows = result.Total
        };
    }
}
```

---

## DataTable Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Title` | `string` | `"Data"` | Toolbar title |
| `Subtitle` | `string?` | `null` | Appended after row count |
| `Data` | `List<TItem>` | `[]` | Client-side data |
| `DataSource` | `Func<DataTableRequest, Task<DataTableResult<TItem>>>?` | `null` | Server-side fetcher |
| `ServerSide` | `bool` | `false` | Enable server-side mode |
| `Search` | `bool` | `false` | Show search input |
| `IsLoading` | `bool` | `false` | Show streaming badge |
| `ExcelExport` | `bool` | `false` | Show built-in Excel export button |
| `ExcelFileName` | `string` | `"export"` | Download filename (without extension) |
| `ExcelSheetName` | `string` | `"Sheet1"` | Worksheet name |
| `IsFullscreen` | `bool` | `false` | Fullscreen icon state |
| `OnFullscreenToggle` | `EventCallback` | — | Shows fullscreen button when set |
| `OnRowClick` | `EventCallback<TItem>` | — | Row click handler |
| `RowSelected` | `Func<TItem, bool>?` | `null` | Row highlight predicate |
| `Columns` | `RenderFragment?` | — | `<ColumnDef>` children |
| `ActionButtons` | `RenderFragment?` | `null` | Extra toolbar buttons |
| `HeaderContent` | `RenderFragment?` | `null` | Content between title and tools |

## ColumnDef Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Key` | `string` | *(required)* | Unique column identifier |
| `Header` | `string` | *(required)* | Header label text |
| `HeaderColor` | `string?` | `null` | CSS color for header label |
| `Align` | `string` | `"left"` | `"left"` \| `"right"` \| `"center"` |
| `Visible` | `bool` | `true` | Initial visibility |
| `CellTemplate` | `RenderFragment<TItem>?` | `null` | Cell render template |
| `SearchSelector` | `Func<TItem, string>?` | `null` | Client-side search string |
| `ExcelValueSelector` | `Func<TItem, string>?` | `null` | Excel cell value (falls back to SearchSelector) |

## Excel Export

- Exports **all filtered rows** (not just the current page) in client-side mode
- Exports **current page only** in server-side mode (server owns the full dataset)
- Only **visible columns** in current order are exported
- **Bold header row**, auto column widths (estimated from data)
- Filename: `{ExcelFileName}_{yyyyMMdd_HHmmss}.xlsx`
- No JS file needed — uses a tiny inline JS blob for the download trigger
- Requires `DocumentFormat.OpenXml` NuGet package

## Column Visibility & Reorder

Click the **columns icon button** (clipboard icon) in the toolbar to open the panel.

- **Checkbox** — toggle column visibility
- **Drag the ⠿ handle** — reorder columns (HTML5 drag, no JS library)

Changes are reflected immediately in both the table and Excel export.

## CSS Variables

The component uses these CSS custom properties with fallbacks for use without a design system:

```css
--card, --card-foreground, --background, --foreground,
--border, --muted, --muted-foreground, --accent, --accent-foreground,
--primary, --primary-foreground, --secondary, --secondary-foreground,
--radius
```
