# Session — Table-file upload toast + ProgramEditor "view table" dialog fixes
> Date: 2026-08-17 | Agent: Claude (Opus 5)

## Goal
Fix two user-reported bugs:
1. Table Files upload shows "File uploaded and validated successfully" as a **red error toast** instead of a green success toast.
2. In the Program Editor, the TABLE operator's "View table" (eye icon) button does nothing.

## What Was Found

### Bug 1 — success message rendered as error toast
`Services/FileManagerService.cs::SaveFileAsync` — on the successful-save path (after `File.WriteAllTextAsync`), it returned:
```csharp
return CommonResponse<bool>.Fail("File uploaded and validated successfully");
```
`.Fail(...)` sets `Success = false`. `Components/Pages/Programs/TableFileManager.razor::UploadFile()` does:
```csharp
if (result.Success) Toast.Success(result.Message); else Toast.Error(result.Message);
```
So the success text always rendered through the `else` branch as a destructive/red toast. `Toast.razor` itself was fine — no styling bug, purely a wrong factory-method call.

### Bug 2 — TABLE view button dialog not working
`Components/UI/Program/NominalValuesEditor.razor` renders an "Eye" button for `OperatorConstants.TABLE` that calls `OnViewTable.InvokeAsync(...)`. That callback was wired from `Components/UI/Program/StepRow.razor` to a **local stub**:
```csharp
private void OnViewTable(string tableName)
{
    // TODO: Implement your table viewing logic here
    Console.WriteLine($"View table: {tableName}");
}
```
It never bubbled up to `ProgramEditor.razor`, so no dialog ever opened (compare with the sibling `OnViewProducer` callback, which *does* bubble up and drives the working PRODUCER viewer dialog).

## What Was Changed
- `Services/FileManagerService.cs` — `SaveFileAsync` success path now returns `CommonResponse<bool>.Ok(true, "File uploaded and validated successfully")`.
- `Components/UI/Program/StepRow.razor` — removed the `OnViewTable` local stub; added `[Parameter] public EventCallback<string> OnViewTable { get; set; }` so the click event bubbles up to the parent (same pattern as `OnViewProducer`).
- `Components/Pages/Programs/ProgramEditor.razor`:
  - Wired `OnViewTable="@HandleViewTable"` onto `<StepRow>`.
  - Added a new read-only "TABLE File Viewer Dialog" (same `Dialog`/`DialogContent` pattern as the existing PRODUCER viewer dialog), showing file content in a `<pre>` block.
  - Added `HandleViewTable(string fileName)`: calls `FileManagerService.ReadFileContent(fileName)` (static, already globally `@using`d via `_Imports.razor`), shows `Toast.Error` on failure, else opens the new dialog with the content.

## Verification
`dotnet build BatteryTestingSystem.csproj` — 0 errors, 387 pre-existing warnings (unrelated to this change).
⚠️ Not yet visually verified in-browser by the user.

## Files Changed
- `Services/FileManagerService.cs`
- `Components/UI/Program/StepRow.razor`
- `Components/Pages/Programs/ProgramEditor.razor`

## Next Agent Should Do
- Have the user confirm in-browser: (1) uploading a valid `.txt` table file now shows a green success toast, (2) in Program Editor, a TABLE-operator step with a selected table file shows a working eye button that opens a read-only content dialog.
