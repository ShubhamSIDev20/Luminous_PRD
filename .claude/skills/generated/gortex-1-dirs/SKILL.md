---
name: gortex-1-dirs
description: "Work in the . +1 dirs area — 53 symbols across 7 files (97% cohesion)"
---

# . +1 dirs

53 symbols | 7 files | 97% cohesion

## When to Use

Use this skill when working on files in:
- ``
- `Requirements\ME_Primary_SRS_V0.1\tools\build_me_srs.py`
- `external-call::dep:openpyxl.Workbook`
- `external-call::dep:openpyxl.styles.Font`
- `external-call::dep:openpyxl.styles.PatternFill`
- `external-call::dep:openpyxl.utils.get_column_letter`
- `external-call::dep:openpyxl.worksheet.datavalidation.DataValidation`

## Key Files

| File | Symbols |
|------|---------|
| `` | count, split, join, remove, add, ... |
| `Requirements\ME_Primary_SRS_V0.1\tools\build_me_srs.py` | main, headers, spec, subtitle, r, ... |
| `external-call::dep:openpyxl.Workbook` | openpyxl.Workbook |
| `external-call::dep:openpyxl.styles.Font` | openpyxl.styles.Font |
| `external-call::dep:openpyxl.styles.PatternFill` | openpyxl.styles.PatternFill |
| `external-call::dep:openpyxl.utils.get_column_letter` | openpyxl.utils.get_column_letter |
| `external-call::dep:openpyxl.worksheet.datavalidation.DataValidation` | openpyxl.worksheet.datavalidation.DataValidation |

## Entry Points

- `Requirements\ME_Primary_SRS_V0.1\tools\build_me_srs.py::sheet_requirements`

## Connected Communities

- **ME_Primary_SRS_V0.1/tools +1 dirs** (4 cross-edges)
- **ME_Primary_SRS_V0.1/tools · n** (1 cross-edges)

## How to Explore

```
analyze(operation:"communities", id:"community-33")
explore(operation:"context", task:"understand . +1 dirs", format:"gcx")
relations(operation:"usages", target:{symbol:"Requirements\ME_Primary_SRS_V0.1\tools\build_me_srs.py::sheet_requirements"}, format:"gcx")
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
