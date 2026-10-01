// DataTableModels.cs
// All supporting types for the generic <DataTable<TItem>> component.
// No external dependencies — just Microsoft.AspNetCore.Components.
using Microsoft.AspNetCore.Components;

namespace BatteryTestingSystem.Components.UI.Datatable
{
    // ─── Server-side paging / search contract ────────────────────────────────

    /// Passed to the DataSource function on every page / search change.
    public class DataTableRequest
    {
        public int    Page     { get; set; } = 1;
        public int    PageSize { get; set; } = 50;
        public string Search   { get; set; } = string.Empty;
    }

    /// Returned by the DataSource function.
    public class DataTableResult<TItem>
    {
        public List<TItem> Rows      { get; set; } = [];
        public int         TotalRows { get; set; }
    }

    // ─── Column definition — built by <ColumnDef<TItem>> children ────────────

    public class ColumnDefinition<TItem>
    {
        /// Stable unique key used for drag-drop and visibility tracking.
        public string Key { get; set; } = Guid.NewGuid().ToString("N")[..8];

        /// Header label shown in the table and in the column panel.
        public string Header { get; set; } = string.Empty;

        /// Optional CSS color applied to the header cell only.
        public string? HeaderColor { get; set; }

        /// Cell text alignment: "left" | "right" | "center"
        public string Align { get; set; } = "left";

        /// Whether this column is visible (toggled by the column panel).
        public bool Visible { get; set; } = true;

        /// Display order — lower numbers render first.
        public int Order { get; set; }

        /// Optional render fragment replacing the header text (e.g. a select-all checkbox).
        public RenderFragment? HeaderTemplate { get; set; }

        /// Blazor render fragment for the cell. Context = TItem row.
        public RenderFragment<TItem>? CellTemplate { get; set; }

        /// Returns a searchable string from the row (used for client-side search).
        public Func<TItem, string>? SearchSelector { get; set; }

        /// Returns the plain-text value written into the Excel cell.
        /// Falls back to SearchSelector if not set. If neither, cell is blank.
        public Func<TItem, string>? ExcelValueSelector { get; set; }
    }

    // ─── Internal interface so DataTable<TItem> can call typed grouping without knowing TKey ─

    public interface IGroupRenderer<TItem>
    {
        bool DefaultExpanded { get; }
        string GetGroupKey(TItem row);
        RenderFragment RenderGroupHeader(GroupHeaderContext<TItem> ctx);
    }

    // ─── Grouping definition — optional, set via GroupBy= parameter ─────────────

    /// Describes how DataTable should group its rows.
    /// TKey is the group key type (e.g. DbcMessage, string).
    public class GroupDefinition<TItem, TKey> : IGroupRenderer<TItem>
    {
        /// Extract the group key from a row.
        public Func<TItem, TKey> KeySelector { get; set; } = _ => default!;

        /// Render the group header row. Receives GroupHeaderContext.
        /// Must emit a full <tr> element.
        public RenderFragment<GroupHeaderContext<TItem, TKey>>? GroupHeaderTemplate { get; set; }

        /// Render each child row inside a group.
        /// When null, DataTable falls back to its standard flat <tr> rendering
        /// (all ColumnDef CellTemplates, zebra stripe, selection highlight etc.).
        public RenderFragment<GroupRowContext<TItem>>? GroupRowTemplate { get; set; }

        /// Whether all groups start expanded (default: true).
        public bool DefaultExpanded { get; set; } = true;

        // ── IGroupRenderer<TItem> implementation ─────────────────────────────

        string IGroupRenderer<TItem>.GetGroupKey(TItem row)
        {
            var key = KeySelector(row);
            return key is null ? "__null__" : key.GetHashCode().ToString();
        }

        RenderFragment IGroupRenderer<TItem>.RenderGroupHeader(GroupHeaderContext<TItem> ctx)
        {
            if (GroupHeaderTemplate == null) return _ => { };

            // RawKey is the first TItem row of the group.
            // We call our own KeySelector to get the actual TKey — no cast, no null.
            TKey typedKey = ctx.RawKey is TItem firstRow
                ? KeySelector(firstRow)
                : default!;

            var typedCtx = new GroupHeaderContext<TItem, TKey>
            {
                Key          = typedKey,
                Items        = ctx.Items,
                IsExpanded   = ctx.IsExpanded,
                ColCount     = ctx.ColCount,
                ToggleExpand = ctx.ToggleExpand
            };

            return GroupHeaderTemplate(typedCtx);
        }
    }

    /// Untyped context passed from DataTable into IGroupRenderer — no TKey needed.
    public class GroupHeaderContext<TItem>
    {
        /// The actual group key object (e.g. DbcMessage). Cast as needed.
        public object? RawKey { get; set; }
        public List<TItem> Items { get; set; } = [];
        public bool IsExpanded { get; set; }
        public int ColCount { get; set; }
        public Action ToggleExpand { get; set; } = () => { };
    }

    /// Typed context passed into GroupHeaderTemplate inside GroupDefinition<TItem,TKey>.
    public class GroupHeaderContext<TItem, TKey>
    {
        public TKey Key { get; set; } = default!;
        public List<TItem> Items { get; set; } = [];
        public bool IsExpanded { get; set; }
        public int ColCount { get; set; }
        public Action ToggleExpand { get; set; } = () => { };
    }

    public class GroupRowContext<TItem>
    {
        public TItem Item { get; set; } = default!;
        public int RowIndex { get; set; }
    }

    // ─── Column context — cascaded from DataTable, columns register here ──────

    public class ColumnContext<TItem>
    {
        private readonly List<ColumnDefinition<TItem>> _cols = [];

        public IReadOnlyList<ColumnDefinition<TItem>> All => _cols;

        /// Visible columns in current order — used for rendering.
        public IEnumerable<ColumnDefinition<TItem>> Visible =>
            _cols.Where(c => c.Visible).OrderBy(c => c.Order);

        /// All columns ordered — used for the column-visibility panel.
        public IEnumerable<ColumnDefinition<TItem>> AllOrdered =>
            _cols.OrderBy(c => c.Order);

        /// Called by each <ColumnDef> on initialisation.
        public void Register(ColumnDefinition<TItem> col)
        {
            if (_cols.All(c => c.Key != col.Key))
            {
                col.Order = _cols.Count;
                _cols.Add(col);
            }
        }

        /// Shift column `fromKey` to the position of `toKey`, sliding others.
        public void Move(string fromKey, string toKey)
        {
            var from = _cols.FirstOrDefault(c => c.Key == fromKey);
            var to   = _cols.FirstOrDefault(c => c.Key == toKey);
            if (from == null || to == null || from.Key == to.Key) return;

            int a = from.Order, b = to.Order;
            if (a < b)
                foreach (var c in _cols.Where(c => c.Order > a && c.Order <= b)) c.Order--;
            else
                foreach (var c in _cols.Where(c => c.Order >= b && c.Order < a)) c.Order++;

            from.Order = b;
        }
    }
}
