# Table Component

A responsive table component built with Tailwind CSS.

## Components

- **Table**: The root table wrapper with overflow handling
- **TableHeader**: Table header section (thead)
- **TableBody**: Table body section (tbody)
- **TableFooter**: Table footer section (tfoot)
- **TableRow**: Table row (tr)
- **TableHead**: Table header cell (th)
- **TableCell**: Table data cell (td)
- **TableCaption**: Table caption

## Usage

### Basic Example

```razor
<Table>
    <TableCaption>A list of your recent invoices.</TableCaption>
    <TableHeader>
        <TableRow>
            <TableHead Class="w-[100px]">Invoice</TableHead>
            <TableHead>Status</TableHead>
            <TableHead>Method</TableHead>
            <TableHead Class="text-right">Amount</TableHead>
        </TableRow>
    </TableHeader>
    <TableBody>
        <TableRow>
            <TableCell Class="font-medium">INV001</TableCell>
            <TableCell>Paid</TableCell>
            <TableCell>Credit Card</TableCell>
            <TableCell Class="text-right">$250.00</TableCell>
        </TableRow>
        <TableRow>
            <TableCell Class="font-medium">INV002</TableCell>
            <TableCell>Pending</TableCell>
            <TableCell>PayPal</TableCell>
            <TableCell Class="text-right">$150.00</TableCell>
        </TableRow>
    </TableBody>
    <TableFooter>
        <TableRow>
            <TableCell ColSpan="3">Total</TableCell>
            <TableCell Class="text-right">$400.00</TableCell>
        </TableRow>
    </TableFooter>
</Table>
```

### With Selection

```razor
<Table>
    <TableHeader>
        <TableRow>
            <TableHead>Select</TableHead>
            <TableHead>Name</TableHead>
            <TableHead>Email</TableHead>
        </TableRow>
    </TableHeader>
    <TableBody>
        <TableRow Selected="@row1Selected">
            <TableCell><Checkbox @bind-Checked="row1Selected" /></TableCell>
            <TableCell>John Doe</TableCell>
            <TableCell>john@example.com</TableCell>
        </TableRow>
        <TableRow Selected="@row2Selected">
            <TableCell><Checkbox @bind-Checked="row2Selected" /></TableCell>
            <TableCell>Jane Smith</TableCell>
            <TableCell>jane@example.com</TableCell>
        </TableRow>
    </TableBody>
</Table>

@code {
    private bool row1Selected = false;
    private bool row2Selected = false;
}
```

## API Reference

### Table
- `ChildContent`: Content to render inside the table
- `Class`: Additional CSS classes
- `AdditionalAttributes`: Additional HTML attributes

### TableHeader, TableBody, TableFooter
- `ChildContent`: Content (usually TableRow components)
- `Class`: Additional CSS classes
- `AdditionalAttributes`: Additional HTML attributes

### TableRow
- `ChildContent`: Content (usually TableHead or TableCell components)
- `Class`: Additional CSS classes
- `Selected`: Boolean to mark row as selected (adds `data-state="selected"`)
- `AdditionalAttributes`: Additional HTML attributes

### TableHead, TableCell
- `ChildContent`: Content to display in the cell
- `Class`: Additional CSS classes
- `AdditionalAttributes`: Additional HTML attributes

### TableCaption
- `ChildContent`: Caption text
- `Class`: Additional CSS classes
- `AdditionalAttributes`: Additional HTML attributes
