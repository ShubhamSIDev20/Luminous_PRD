# 17. Report & Export Read-Back

> Part of the [BTS System Flow documentation](README.md) · [Document index](README.md)

---

Everything so far writes *into* session files. This chapter reads back *out* of them.

### 17.1 Interactive viewing

```
  OPERATOR: Reports -> pick a session
        |
        v
  ISqliteBulkDatabaseManager  (the same class that wrote the file)
        |
        +-- GetSessionAsync(filePath)          -> SessionRequest
        |     +-- FetchProgramAsync             (from configurationEntities "Program")
        |     +-- FetchBatteryAsync             ("Battery")
        |     +-- FetchDbcDatabaseAsync         ("Dbc" -- merged 3-port map)
        |     +-- FetchDbcSignalNamesAsync      (ordered names for column headers)
        |     +-- FetchProducerProgramsAsync    (PRODUCER sub-programs)
        |     +-- FetchTableFileDataAsync       (TABLE step source data)
        |     +-- FetchExpandedProgramStepsAsync(hardware step -> user step mapping)
        |
        +-- CountSessionRowsAsync(filePath, stepFilter?)     -> total, for paging
        +-- ReadSessionPageAsync(filePath, page, size, ...)  -> PagedResult<MeasurementData>
        +-- ReadChartSampleAsync(filePath, ...)              -> List<ChartPoint> (downsampled)
        +-- GetDistinctRegLabelsAsync(filePath)              -> REG label list
        +-- ReadRegLogPageAsync(filePath, label, page, ...)  -> PagedResult<RegLogRecord>
        |
        v
  Grid / chart render from the SESSION FILE ONLY -- the main DB is never
  consulted for measurement data. This is what makes an old session
  reproducible even after its program/battery rows were edited.
```

**Live sessions update in place:** `StoreUdpData` publishes each decoded batch on
`EventBusService` keyed by the session's `filePath` (Chapter 7, stage 3g), so an open
grid or chart for a *running* session receives new rows without re-reading the disk.

### 17.2 Async Excel export

Large sessions can run to millions of rows, so export is a Hangfire job, not a request.

```
  OPERATOR: Reports -> [Export]
        |
        v
  POST /api/Export/request        [Authorize]
        |
        v
  +--------------------------------------------------------------+
  | ExportController.RequestExport(dto)                           |
  |   create ExportRecord (Pending) via IExportRepository         |
  |   jobId = _hangfire.Enqueue<ExportJobService>(                |
  |               svc => svc.RunAsync(created.Id, None))          |
  |   return the record id immediately  (HTTP returns now)        |
  +--------------------------------+------------------------------+
                                   |
                     ~~~ Hangfire worker thread ~~~
                                   |
                                   v
  +--------------------------------------------------------------+
  | ExportJobService.RunAsync(exportRecordId, ct)                 |
  |   [AutomaticRetry(Attempts = 1)]  -- one retry only; a broken |
  |   session file will not be retried forever                    |
  |                                                               |
  |   scope = _sp.CreateScope()   (job runs outside any request)  |
  |   record = repo.GetByIdAsync(id)                              |
  |       null -> log warning, RETURN                             |
  |   repo.UpdateStatusAsync(id, Processing)                      |
  |                                                               |
  |   exportDir = <Data>/exports/   (created if missing)          |
  |   safeName  = SessionFilePath with / \ : -> _                 |
  |   fileName  = "export_<safeName>_<yyyyMMdd_HHmmss>.xlsx"      |
  |                                                               |
  |   session = dbMgr.GetSessionAsync(record.SessionFilePath)     |
  |   WriteExcelAsync(...)              --> two phases below      |
  |                                                               |
  |   repo.MarkReadyAsync(id, fullPath, size)                     |
  |                                                               |
  |   catch -> UpdateStatusAsync(id, Failed, ex.Message)          |
  |            delete the half-written .xlsx if it exists         |
  +--------------------------------+------------------------------+
                                   |
                                   v
  +--------------------------------------------------------------+
  | WriteExcelAsync -- deliberately TWO phases                    |
  |                                                               |
  |  PHASE 1 (ClosedXML, in memory)                               |
  |    BuildProgramSheet(wb, session.Program)                     |
  |    wb.Worksheets.Add("MeasurementData")   <- EMPTY placeholder|
  |                                              so the tab lands |
  |                                              at index 1       |
  |    BuildBatterySheet(wb, session.Battery)                     |
  |    BuildChartPlaceholderSheet(wb)                             |
  |    wb.SaveAs(outputPath)                                      |
  |    ... workbook disposed, file handle released ...            |
  |                                                               |
  |  PHASE 2 (OpenXML SDK, SAX streaming)                         |
  |    WriteMeasurementSheetSaxAsync(outputPath, sessionFilePath, |
  |                                  dbMgr, ct)                   |
  |    re-opens the file and streams measurement rows one at a    |
  |    time straight to the XML writer                            |
  |                                                               |
  |  WHY TWO PHASES: ClosedXML builds the whole workbook in RAM.  |
  |  That is fine for a program sheet and a battery sheet, and    |
  |  fatal for a multi-million-row measurement sheet. The SAX     |
  |  writer holds constant memory regardless of session size, so  |
  |  the small sheets get the convenient API and the big one gets |
  |  the streaming one.                                           |
  +--------------------------------+------------------------------+
                                   |
                                   v
  GET /api/Export/status/{id}   -> Pending | Processing | Ready | Failed
  GET /api/Export/list          -> all export records
  GET /api/Export/download/{id} -> the .xlsx file stream
  DELETE /api/Export/{id}?deleteFile=true|false
```

### 17.3 The full data round trip

```
  HARDWARE
     |  UDP 10001
     v
  ChannelManager.StoreUdpData          (decode, resolve session, route)
     |
     v
  ChannelCommandHandler.EnqueueForStore / StartStoreWorkerAsync
     |
     v
  SqliteBulkDatabaseManager.InsertRecordAsync
     |
     v
  +-------------------------------------------+
  |  sessions/<dd-MM-yyyy>/<SID>_<D>_<B>_<C>.db|
  |    Measurements | RegLogs | Program |      |
  |    Battery | configurationEntities         |
  +-------------------+-----------------------+
     |                            |
     | interactive                | export
     v                            v
  ReadSessionPageAsync        ExportJobService.RunAsync
  ReadChartSampleAsync            |
  ReadRegLogPageAsync             v
  GetSessionAsync             exports/export_<session>_<stamp>.xlsx
     |                            |
     v                            v
  Reports grid + charts      operator downloads the workbook
```

**Files involved:** `Services/Implementations/SqliteBulkDatabaseManager.cs:359-800`,
`Services/Implementations/ExportJobService.cs`, `Controllers/ExportController.cs`,
`Services/EventBusService.cs`, `Components/Pages/Reports/`

---


---

[⬅ Previous](16-scheduled-execution.md) · [⬅ Index](README.md) · [Next ➡](A-appendix-reference-tables.md)
