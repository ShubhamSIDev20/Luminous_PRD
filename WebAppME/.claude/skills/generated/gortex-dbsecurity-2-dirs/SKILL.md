---
name: gortex-dbsecurity-2-dirs
description: "Work in the DbSecurity +2 dirs area — 77 symbols across 6 files (92% cohesion)"
---

# DbSecurity +2 dirs

77 symbols | 6 files | 92% cohesion

## When to Use

Use this skill when working on files in:
- `Components\UI\Datatable\DataTable.razor`
- `DbSecurity\EncryptionAttribute.cs`
- `DbSecurity\IColumnEncryptionService.cs`
- `DbSecurity\IColumnEncryptionServiceWithSalt.cs`
- `DbSecurity\ModelBuilderEncryptionExtensions.cs`
- `Services\DbcParser.cs`

## Key Files

| File | Symbols |
|------|---------|
| `Components\UI\Datatable\DataTable.razor` | OnInitializedAsync, DataTableViewState |
| `DbSecurity\EncryptionAttribute.cs` | NotEncryptedAttribute, EncryptedAttribute, EncryptedSearchableAttribute |
| `DbSecurity\IColumnEncryptionService.cs` | Decrypt, _key, ColumnEncryptionService, IColumnEncryptionService, Decrypt, ... |
| `DbSecurity\IColumnEncryptionServiceWithSalt.cs` | Iterations, cipherText, plainText, _masterKey, IvSize, ... |
| `DbSecurity\ModelBuilderEncryptionExtensions.cs` | enc, plainValue, SafeDecryptSalted, T, _log, ... |
| `Services\DbcParser.cs` | filePath, Parse |

## Connected Communities

- **Components\Pages\Programs +18 dirs** (6 cross-edges)
- **Components\UI\Program +15 dirs** (2 cross-edges)
- **Services · ParseLines** (1 cross-edges)

## How to Explore

```
get_communities with id: "community-199"
smart_context with task: "understand DbSecurity +2 dirs", format: "gcx"
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
