---
name: gortex-me-primary-srs-v0-1-tools-1-dirs
description: "Work in the ME_Primary_SRS_V0.1/tools +1 dirs area — 29 symbols across 3 files (92% cohesion)"
---

# ME_Primary_SRS_V0.1/tools +1 dirs

29 symbols | 3 files | 92% cohesion

## When to Use

Use this skill when working on files in:
- ``
- `Requirements\ME_Primary_SRS_V0.1\tools\me_srs_data.py`
- `Requirements\ME_Primary_SRS_V0.1\tools\me_srs_reqs.py`

## Key Files

| File | Symbols |
|------|---------|
| `` | re, load, join, setdefault, append, ... |
| `Requirements\ME_Primary_SRS_V0.1\tools\me_srs_data.py` | _build_trace |
| `Requirements\ME_Primary_SRS_V0.1\tools\me_srs_reqs.py` | phase, cat, app, R, typ, ... |

## Connected Communities

- **. +1 dirs** (1 cross-edges)

## How to Explore

```
analyze(operation:"communities", id:"community-37")
explore(operation:"context", task:"understand ME_Primary_SRS_V0.1/tools +1 dirs", format:"gcx")
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
