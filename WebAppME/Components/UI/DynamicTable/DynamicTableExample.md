# Modular Dynamic Table Component

## 🎯 Features

✅ **Slot-Based Architecture** - Fully customizable with render fragments  
✅ **Client & Server-Side** - Support for both processing modes  
✅ **Optional Search** - Enable only when needed  
✅ **Optional Pagination** - Show/hide as required  
✅ **No Built-in Dialogs** - Handle forms externally  
✅ **Clean Syntax** - Intuitive component-based API  
✅ **Debounced Search** - Configurable search delay  
✅ **Loading States** - Built-in loading indicators  

---

## 📦 Basic Structure

```razor
<DynamicTable TItem="YourDTO"
              DataSource="@yourData"
              ColumnCount="5">
    
    <TitleContent>
        <!-- Your title and header buttons -->
    </TitleContent>

    <TableHeader>
        <!-- Your table column headers -->
    </TableHeader>

    <RowContent>
        <!-- Your table row cells - @context is the current item -->
    </RowContent>

</DynamicTable>
```

---

## 🔧 Component Parameters

### Core Parameters
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `DataSource` | `IEnumerable<TItem>?` | `null` | Client-side data source |
| `OnServerLoad` | `Func<TableRequest, Task<TableResponse<TItem>>>?` | `null` | Server-side data loader |
| `ColumnCount` | `int` | `1` | Number of columns (for colspan in empty state) |
| `ServerSideProcessing` | `bool` | `false` | Enable server-side processing |

### Search Parameters
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ShowSearch` | `bool` | `false` | Show search input |
| `SearchFilter` | `Func<TItem, string, bool>?` | `null` | Client-side filter function |
| `SearchPlaceholder` | `string` | `"Search..."` | Search input placeholder |
| `SearchDebounceMs` | `int` | `300` | Search debounce delay |
| `ShowSearchSummary` | `bool` | `true` | Show filtered results summary |

### Pagination Parameters
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ShowPagination` | `bool` | `true` | Show pagination controls |
| `PageSize` | `int` | `10` | Items per page |
| `PageSizeOptions` | `int[]` | `{10,25,50,100}` | Available page size options |

### Display Parameters
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `EmptyMessage` | `string` | `"No data available"` | Empty state message |
| `LoadingMessage` | `string` | `"Loading..."` | Loading state message |
| `ContainerClass` | `string` | `"p-6"` | Container CSS class |
| `SearchContainerClass` | `string` | `"w-full max-w-2xl"` | Search container CSS |
| `TableContainerClass` | `string` | `"overflow-x-auto"` | Table container CSS |

### Render Fragment Slots
| Slot | Type | Description |
|------|------|-------------|
| `TitleContent` | `RenderFragment?` | Title and action buttons area |
| `HeaderActions` | `RenderFragment?` | Additional filters/actions below title |
| `TableHeader` | `RenderFragment?` | Table column headers |
| `RowContent` | `RenderFragment<TItem>?` | Table row cells (use `@context`) |
| `NoDataContent` | `RenderFragment?` | Custom empty state |
| `FooterContent` | `RenderFragment?` | Footer area below table |

### Event Callbacks
| Event | Type | Description |
|-------|------|-------------|
| `OnPageChanged` | `EventCallback<TableRequest>` | Fired when page changes |
| `OnSearchChanged` | `EventCallback<string>` | Fired when search changes |

---

## 📖 Usage Patterns

### 1. Client-Side Processing (Simple)

```razor
<DynamicTable TItem="Product"
              DataSource="@products"
              ColumnCount="4"
              ShowSearch="true"
              SearchFilter="@((p, term) => p.Name.Contains(term))"
              ShowPagination="true">
    
    <TitleContent>
        <h1 class="text-2xl font-bold">Products</h1>
        <Button OnClick="@AddProduct">Add</Button>
    </TitleContent>

    <TableHeader>
        <TableHead>Name</TableHead>
        <TableHead>Price</TableHead>
        <TableHead>Stock</TableHead>
        <TableHead>Actions</TableHead>
    </TableHeader>

    <RowContent>
        <TableCell>@context.Name</TableCell>
        <TableCell>@context.Price.ToString("C")</TableCell>
        <TableCell>@context.Stock</TableCell>
        <TableCell>
            <Button OnClick="@(() => Edit(context))">Edit</Button>
        </TableCell>
    </RowContent>

</DynamicTable>
```

### 2. Server-Side Processing

```razor
<DynamicTable TItem="Order"
              ServerSideProcessing="true"
              OnServerLoad="@LoadOrdersFromServer"
              ColumnCount="5"
              ShowSearch="true"
              ShowPagination="true"
              PageSize="25">
    
    <TitleContent>
        <h1>Orders</h1>
    </TitleContent>

    <TableHeader>
        <TableHead>Order #</TableHead>
        <TableHead>Customer</TableHead>
        <TableHead>Date</TableHead>
        <TableHead>Amount</TableHead>
        <TableHead>Status</TableHead>
    </TableHeader>

    <RowContent>
        <TableCell>@context.OrderNumber</TableCell>
        <TableCell>@context.CustomerName</TableCell>
        <TableCell>@context.OrderDate.ToString("d")</TableCell>
        <TableCell>@context.Amount.ToString("C")</TableCell>
        <TableCell>@context.Status</TableCell>
    </RowContent>

</DynamicTable>

@code {
    private async Task<TableResponse<Order>> LoadOrdersFromServer(TableRequest request)
    {
        // Call your API
        var result = await OrderService.GetPagedAsync(
            page: request.Page,
            pageSize: request.PageSize,
            searchTerm: request.SearchTerm
        );

        return new TableResponse<Order>
        {
            Data = result.Items,
            TotalRecords = result.TotalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
```

### 3. Minimal Table (No Search, No Pagination)

```razor
<DynamicTable TItem="LogEntry"
              DataSource="@logs"
              ColumnCount="3"
              ShowSearch="false"
              ShowPagination="false">
    
    <TitleContent>
        <h2>Recent Activity</h2>
    </TitleContent>

    <TableHeader>
        <TableHead>Time</TableHead>
        <TableHead>User</TableHead>
        <TableHead>Action</TableHead>
    </TableHeader>

    <RowContent>
        <TableCell>@context.Timestamp.ToString("HH:mm")</TableCell>
        <TableCell>@context.UserName</TableCell>
        <TableCell>@context.Action</TableCell>
    </RowContent>

</DynamicTable>
```

### 4. With Custom Empty State

```razor
<DynamicTable TItem="Task"
              DataSource="@tasks"
              ColumnCount="4">
    
    <TitleContent>
        <h1>My Tasks</h1>
    </TitleContent>

    <TableHeader>
        <TableHead>Task</TableHead>
        <TableHead>Due Date</TableHead>
        <TableHead>Priority</TableHead>
        <TableHead>Status</TableHead>
    </TableHeader>

    <RowContent>
        <TableCell>@context.Title</TableCell>
        <TableCell>@context.DueDate.ToString("MMM dd")</TableCell>
        <TableCell>@context.Priority</TableCell>
        <TableCell>@context.Status</TableCell>
    </RowContent>

    <NoDataContent>
        <div class="text-center py-12">
            <Blazicon Svg="Lucide.CheckCircle" class="h-16 w-16 mx-auto text-green-500 mb-4" />
            <h3 class="text-lg font-semibold mb-2">All done!</h3>
            <p class="text-muted-foreground mb-4">You have no pending tasks</p>
            <Button OnClick="@CreateTask">Create New Task</Button>
        </div>
    </NoDataContent>

</DynamicTable>
```

### 5. With Header Actions (Filters)

```razor
<DynamicTable TItem="Employee"
              DataSource="@filteredEmployees"
              ColumnCount="5"
              ShowSearch="true"
              SearchFilter="@EmployeeSearchFilter">
    
    <TitleContent>
        <h1>Employees</h1>
        <Button OnClick="@AddEmployee">Add Employee</Button>
    </TitleContent>

    <HeaderActions>
        <div class="flex gap-2">
            <Select Value="@selectedDepartment" ValueChanged="@FilterByDepartment">
                <SelectTrigger class="w-[200px]">
                    <SelectValue placeholder="All Departments" />
                </SelectTrigger>
                <SelectContent>
                    <SelectItem value="">All Departments</SelectItem>
                    <SelectItem value="IT">IT</SelectItem>
                    <SelectItem value="HR">HR</SelectItem>
                    <SelectItem value="Sales">Sales</SelectItem>
                </SelectContent>
            </Select>

            <Select Value="@selectedStatus" ValueChanged="@FilterByStatus">
                <SelectTrigger class="w-[150px]">
                    <SelectValue placeholder="All Status" />
                </SelectTrigger>
                <SelectContent>
                    <SelectItem value="">All</SelectItem>
                    <SelectItem value="active">Active</SelectItem>
                    <SelectItem value="inactive">Inactive</SelectItem>
                </SelectContent>
            </Select>
        </div>
    </HeaderActions>

    <TableHeader>
        <TableHead>Name</TableHead>
        <TableHead>Email</TableHead>
        <TableHead>Department</TableHead>
        <TableHead>Status</TableHead>
        <TableHead>Actions</TableHead>
    </TableHeader>

    <RowContent>
        <TableCell>@context.Name</TableCell>
        <TableCell>@context.Email</TableCell>
        <TableCell>@context.Department</TableCell>
        <TableCell>
            <span class="badge @(context.IsActive ? "badge-success" : "badge-danger")">
                @(context.IsActive ? "Active" : "Inactive")
            </span>
        </TableCell>
        <TableCell>
            <Button OnClick="@(() => EditEmployee(context))">Edit</Button>
        </TableCell>
    </RowContent>

</DynamicTable>
```

### 6. With Footer Content

```razor
<DynamicTable TItem="Invoice"
              DataSource="@invoices"
              ColumnCount="5">
    
    <TitleContent>
        <h1>Invoices</h1>
    </TitleContent>

    <TableHeader>
        <TableHead>Invoice #</TableHead>
        <TableHead>Client</TableHead>
        <TableHead>Date</TableHead>
        <TableHead>Amount</TableHead>
        <TableHead>Status</TableHead>
    </TableHeader>

    <RowContent>
        <TableCell>@context.InvoiceNumber</TableCell>
        <TableCell>@context.ClientName</TableCell>
        <TableCell>@context.InvoiceDate.ToString("d")</TableCell>
        <TableCell>@context.Amount.ToString("C")</TableCell>
        <TableCell>@context.Status</TableCell>
    </RowContent>

    <FooterContent>
        <div class="flex justify-between items-center py-4 border-t">
            <span class="text-sm text-muted-foreground">
                Total Invoices: @invoices.Count
            </span>
            <div class="text-right">
                <div class="text-sm text-muted-foreground">Total Amount</div>
                <div class="text-xl font-bold">
                    @invoices.Sum(i => i.Amount).ToString("C")
                </div>
            </div>
        </div>
    </FooterContent>

</DynamicTable>
```

---

## 🔄 Server-Side Data Loading

### TableRequest Model
```csharp
public class TableRequest
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public string SearchTerm { get; set; } = string.Empty;
    public string SortColumn { get; set; } = string.Empty;
    public bool SortDescending { get; set; }
}
```

### TableResponse Model
```csharp
public class TableResponse<T>
{
    public IEnumerable<T> Data { get; set; } = Enumerable.Empty<T>();
    public int TotalRecords { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
```

### Server Implementation Example

```csharp
// In your service
public async Task<PaginatedResult<Product>> GetPagedAsync(
    int page, int pageSize, string searchTerm)
{
    var query = _context.Products.AsQueryable();

    // Apply search
    if (!string.IsNullOrEmpty(searchTerm))
    {
        query = query.Where(p => 
            p.Name.Contains(searchTerm) || 
            p.SKU.Contains(searchTerm));
    }

    var totalRecords = await query.CountAsync();

    // Apply pagination
    var items = await query
        .OrderBy(p => p.Name)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    return new PaginatedResult<Product>
    {
        Items = items,
        TotalCount = totalRecords,
        Page = page,
        PageSize = pageSize
    };
}

// In your component
private async Task<TableResponse<Product>> LoadProducts(TableRequest request)
{
    var result = await ProductService.GetPagedAsync(
        request.Page, 
        request.PageSize, 
        request.SearchTerm
    );

    return new TableResponse<Product>
    {
        Data = result.Items,
        TotalRecords = result.TotalCount,
        Page = result.Page,
        PageSize = result.PageSize
    };
}
```

---

## 🎨 Styling Examples

### Custom Search Style
```razor
<DynamicTable SearchContainerClass="w-full max-w-4xl mx-auto"
              ...>
```

### Custom Container
```razor
<DynamicTable ContainerClass="p-8 bg-gray-50 rounded-lg"
              TableContainerClass="shadow-md rounded-lg overflow-hidden"
              ...>
```

---

## 💡 Tips & Best Practices

1. **Always set `ColumnCount`** to match your table columns for proper empty state display
2. **Use `SearchFilter` for client-side**, `OnServerLoad` for server-side search
3. **Set `ServerSideProcessing="true"`** when using `OnServerLoad`
4. **Debounce is automatic** - no need to implement yourself
5. **Use `@context`** in `RowContent` to access the current item
6. **Custom slots are optional** - use only what you need
7. **Loading states are automatic** for server-side processing
8. **Page changes reset to page 1** on search to avoid empty results

---

## 🔧 Public Methods

Access via `@ref`:

```razor
<DynamicTable @ref="tableRef" ...>
</DynamicTable>

@code {
    private DynamicTable<Product> tableRef;

    private async Task RefreshTable()
    {
        await tableRef.Refresh();
    }

    private async Task ResetTable()
    {
        await tableRef.ResetPagination();
    }
}
```

Available methods:
- `Refresh()` - Reload data without changing pagination
- `ResetPagination()` - Reset to page 1 and reload

---

## 🚀 Quick Start Checklist

- [ ] Define your data model (`TItem`)
- [ ] Set `DataSource` (client) or `OnServerLoad` (server)
- [ ] Set correct `ColumnCount`
- [ ] Add `<TitleContent>` with title and actions
- [ ] Add `<TableHeader>` with column headers
- [ ] Add `<RowContent>` with cells using `@context`
- [ ] Enable `ShowSearch` if needed with `SearchFilter`
- [ ] Enable `ShowPagination` if needed
- [ ] Customize empty states with `<NoDataContent>` if desired