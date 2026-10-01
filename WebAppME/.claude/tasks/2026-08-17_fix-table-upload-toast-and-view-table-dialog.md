# T-15 — Fix table-file upload success-toast-shown-as-error + ProgramEditor "view table" dialog

**Status:** ✅ Done (build-verified, not yet visually confirmed in-browser)
**Session:** #9 ([sessions/2026-08-17_0000_table-upload-toast-and-view-table-dialog-fixes.md](../sessions/2026-08-17_0000_table-upload-toast-and-view-table-dialog-fixes.md))

## Problem
User-reported (live testing):
1. Table Files page: uploading a valid `.txt` file shows "File uploaded and validated successfully" but styled/routed as an **error** toast.
2. Program Editor: for a TABLE-operator step, the "view table" eye-icon button does nothing — no dialog appears.

## Root Cause
1. `FileManagerService.SaveFileAsync` returned `CommonResponse<bool>.Fail(...)` instead of `.Ok(...)` on its success path, so `result.Success == false` and the UI's error branch fired.
2. `StepRow.razor`'s `OnViewTable` handler was a dead stub (`Console.WriteLine` only, marked `// TODO`) — never wired to `ProgramEditor.razor`, unlike the working `OnViewProducer` pattern it sits next to.

## Fix
- `Services/FileManagerService.cs`: success path now returns `.Ok(true, "...")`.
- `StepRow.razor`: `OnViewTable` promoted from local stub to an `EventCallback<string>` parameter, bubbling to the parent.
- `ProgramEditor.razor`: added `HandleViewTable` + a new read-only table-file viewer `Dialog` (mirrors the existing PRODUCER viewer dialog), reading content via `FileManagerService.ReadFileContent`.

## Verification
`dotnet build` — 0 errors. Not yet confirmed live in-browser.
