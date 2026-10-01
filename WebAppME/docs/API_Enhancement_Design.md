# API Enhancement Design Document

**Date:** 2026-09-03  
**Status:** Proposed Design  
**Author:** Claude Code Analysis

> **⚠️ Superseded (2026-09-04):** The implementation below shipped with `ChannelNumber` + `ChannelRange` + `ChannelList` (priority `ChannelList` > `ChannelRange` > `ChannelNumber`), but `ChannelNumber` and `ChannelRange` were subsequently **removed** from `CommonRequest`. `ChannelList` is now the only channel field (single-channel callers send a one-element list). See `.claude/agent-memory/api.md` and `.claude/CONTEXT/api/device.md` for the current behavior — this document is kept for historical design context only.

---

## Executive Summary

This document proposes two critical API enhancements for the BatteryTestingSystem:

1. **Add Missing DBC Files Endpoint** - Provide a `GetDbcFiles` endpoint to list all available DBC files
2. **Channel Range/List Support** - Allow API clients to specify channel ranges (e.g., "1-64") or lists instead of repetitive individual requests

---

## Problem Statement

### 1. Missing DBC Files Endpoint

**Current State:**
- Programs can be listed via `POST /api/Device/GetPrograms`
- Batteries can be listed via `POST /api/Device/GetBatteries`
- **DBC files cannot be listed** - no equivalent endpoint exists

**Impact:**
- API clients cannot discover available DBC files programmatically
- MCP tools cannot provide DBC file options to users
- UI components must rely on battery-specific lookups only

### 2. Repetitive Channel Requests

**Current State:**
```json
// To send a program to channels 1-64, client must send:
POST /api/Device/SendProgram
[
  {"DeviceID": 1, "ChannelNumber": 1, "ProgramId": 5, "BatteryId": 3, "dbcId": 2},
  {"DeviceID": 1, "ChannelNumber": 2, "ProgramId": 5, "BatteryId": 3, "dbcId": 2},
  // ... 62 more identical objects
]
```

**Impact:**
- Large payloads (64 channels = 64 objects)
- Verbose API calls from MCP tools and external clients
- Error-prone manual construction of request arrays
- Poor developer experience

---

## Proposed Solution

### Enhancement 1: Add DBC Files Listing Endpoint

#### API Endpoint

```http
POST /api/Device/GetDbcFiles
```

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "dbcFileRecordId": 1,
      "fileName": "Tesla_Model3_BMS.dbc",
      "description": "Tesla Model 3 Battery Management System",
      "batteryId": 3,
      "batteryName": "Li-ion",
      "uploadedBy": "admin",
      "uploadedAt": "2026-01-15T10:30:00Z"
    },
    {
      "dbcFileRecordId": 2,
      "fileName": "Nissan_Leaf_BMS.dbc",
      "description": "Nissan Leaf Battery Pack",
      "batteryId": null,
      "batteryName": null,
      "uploadedBy": "admin",
      "uploadedAt": "2026-02-20T14:15:00Z"
    }
  ],
  "message": "success!"
}
```

#### Implementation Steps

1. **Add interface method** in `IDbcService`:
```csharp
Task<CommonResponse<List<DbcFileRecordDto>>> GetAllAsync();
```

2. **Implement in `DbcService`**:
```csharp
public async Task<CommonResponse<List<DbcFileRecordDto>>> GetAllAsync()
{
    try
    {
        var dbcFiles = await _dbcRepository.GetAllAsync();
        return CommonResponse<List<DbcFileRecordDto>>.Ok(dbcFiles, "DBC files retrieved successfully");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to retrieve DBC files");
        return CommonResponse<List<DbcFileRecordDto>>.Fail($"Failed: {ex.Message}");
    }
}
```

3. **Add controller endpoint** in `DeviceController`:
```csharp
[HttpPost("GetDbcFiles")]
public async Task<IActionResult> GetDbcFiles()
{
    var result = await _dbcService.GetAllAsync();
    return Ok(result);
}
```

4. **Update MCP tools** in `DeviceMcpTools`:
```csharp
[McpServerTool]
[Description("List all DBC files registered in the system.")]
public async Task<string> GetDbcFiles()
{
    var result = await _dbcService.GetAllAsync();
    return System.Text.Json.JsonSerializer.Serialize(result);
}
```

---

### Enhancement 2: Channel Range/List Support

#### Design Options

**Option A: Enhanced CommonRequest (Backward Compatible)**

Add optional `ChannelRange` and `ChannelList` to `CommonRequest`:

```csharp
public class CommonRequest
{
    public int DeviceID { get; set; }
    public int SecondaryBoardNumber { get; set; } = 1;
    
    // Existing single-channel field (backward compatible)
    public int ChannelNumber { get; set; }
    
    // NEW: Range support (e.g., "1-64" or "1-8,17-24")
    public string? ChannelRange { get; set; }
    
    // NEW: Explicit list support (e.g., [1,2,5,10,15])
    public List<int>? ChannelList { get; set; }
    
    public int? ProgramId { get; set; }
    public int? BatteryId { get; set; }
    public int? dbcId { get; set; } = 0;
}
```

**Priority:**
1. If `ChannelList` is provided → expand to those channels
2. Else if `ChannelRange` is provided → parse and expand range
3. Else use single `ChannelNumber` (backward compatible)

**Usage Examples:**

```json
// Single channel (existing behavior)
{"DeviceID": 1, "ChannelNumber": 5, "ProgramId": 10}

// Range notation (NEW)
{"DeviceID": 1, "ChannelRange": "1-64", "ProgramId": 10, "BatteryId": 3, "dbcId": 2}

// Multiple ranges (NEW)
{"DeviceID": 1, "ChannelRange": "1-8,17-24,33-40", "ProgramId": 10}

// Explicit list (NEW)
{"DeviceID": 1, "ChannelList": [1,2,5,10,15,20], "ProgramId": 10}
```

---

**Option B: Separate Bulk Request Model (Clean Separation)**

Create dedicated bulk request models:

```csharp
public class BulkChannelRequest
{
    public int DeviceID { get; set; }
    public int SecondaryBoardNumber { get; set; } = 1;
    
    // Range or list - mutually exclusive
    public string? ChannelRange { get; set; }  // "1-64" or "1-8,17-24"
    public List<int>? ChannelList { get; set; } // [1,2,5,10]
    
    public int? ProgramId { get; set; }
    public int? BatteryId { get; set; }
    public int? dbcId { get; set; } = 0;
}
```

Add new endpoints:
- `POST /api/Device/SendProgramBulk`
- `POST /api/Device/StartBulk`
- `POST /api/Device/StopBulk`

---

#### Recommended Approach: **Option A (Enhanced CommonRequest)**

**Rationale:**
- ✅ **Backward compatible** - existing API clients continue working
- ✅ **Single endpoint** - no need to duplicate logic across `/SendProgram` and `/SendProgramBulk`
- ✅ **Flexible** - supports all three patterns (single, range, list)
- ✅ **Minimal code changes** - expansion logic centralized in one helper method

---

### Implementation Plan

#### Step 1: Update CommonRequest Model

```csharp
// Models/ViewModels/CommonRequest.cs
public class CommonRequest
{
    public int DeviceID { get; set; }
    public int SecondaryBoardNumber { get; set; } = 1;
    public int ChannelNumber { get; set; }
    
    /// <summary>
    /// Channel range in format "1-64" or "1-8,17-24,33-40"
    /// Mutually exclusive with ChannelList
    /// </summary>
    public string? ChannelRange { get; set; }
    
    /// <summary>
    /// Explicit list of channel numbers
    /// Mutually exclusive with ChannelRange
    /// </summary>
    public List<int>? ChannelList { get; set; }
    
    public int? ProgramId { get; set; }
    public int? BatteryId { get; set; }
    public int? dbcId { get; set; } = 0;
}
```

#### Step 2: Create Channel Expansion Helper

```csharp
// Utils/ChannelExpander.cs
public static class ChannelExpander
{
    /// <summary>
    /// Expands a single CommonRequest into multiple requests, one per channel
    /// </summary>
    public static List<CommonRequest> ExpandChannels(CommonRequest request)
    {
        var channels = GetChannelNumbers(request);
        
        // If only one channel, return as-is
        if (channels.Count == 1 && channels[0] == request.ChannelNumber)
            return new List<CommonRequest> { request };
        
        // Expand to multiple requests
        return channels.Select(ch => new CommonRequest
        {
            DeviceID = request.DeviceID,
            SecondaryBoardNumber = request.SecondaryBoardNumber,
            ChannelNumber = ch,
            ProgramId = request.ProgramId,
            BatteryId = request.BatteryId,
            dbcId = request.dbcId
        }).ToList();
    }
    
    /// <summary>
    /// Determines the list of channel numbers from a CommonRequest
    /// Priority: ChannelList > ChannelRange > ChannelNumber
    /// </summary>
    private static List<int> GetChannelNumbers(CommonRequest request)
    {
        // Priority 1: Explicit list
        if (request.ChannelList != null && request.ChannelList.Any())
            return request.ChannelList.OrderBy(c => c).ToList();
        
        // Priority 2: Range notation
        if (!string.IsNullOrWhiteSpace(request.ChannelRange))
            return ParseChannelRange(request.ChannelRange);
        
        // Priority 3: Single channel (backward compatible)
        return new List<int> { request.ChannelNumber };
    }
    
    /// <summary>
    /// Parses channel range notation into a list of channel numbers
    /// Examples: "1-64" → [1,2,3,...,64]
    ///           "1-8,17-24" → [1,2,3,4,5,6,7,8,17,18,19,20,21,22,23,24]
    /// </summary>
    private static List<int> ParseChannelRange(string range)
    {
        var channels = new HashSet<int>();
        
        foreach (var segment in range.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = segment.Trim();
            
            // Range: "1-64"
            if (trimmed.Contains('-'))
            {
                var parts = trimmed.Split('-', 2);
                if (int.TryParse(parts[0], out int start) && int.TryParse(parts[1], out int end))
                {
                    for (int i = start; i <= end; i++)
                        channels.Add(i);
                }
            }
            // Single: "5"
            else if (int.TryParse(trimmed, out int single))
            {
                channels.Add(single);
            }
        }
        
        return channels.OrderBy(c => c).ToList();
    }
}
```

#### Step 3: Update Core Methods to Use Expansion

```csharp
// Controllers/DeviceController.cs

[HttpPost("SendProgram")]
public async Task<IActionResult> SendProgram([FromBody] List<CommonRequest> requests)
{
    // Expand each request that has ChannelRange or ChannelList
    var expandedRequests = requests
        .SelectMany(req => ChannelExpander.ExpandChannels(req))
        .ToList();
    
    var messages = await CoreSendProgram(_cm, _programServices, _batteryServices, _dbcService, expandedRequests);
    return Ok(CommonResponse<List<string>>.Ok(messages, "Processed"));
}

[HttpPost("Start")]
public async Task<IActionResult> Start([FromBody] List<CommonRequest> requests)
{
    var expandedRequests = requests
        .SelectMany(req => ChannelExpander.ExpandChannels(req))
        .ToList();
    
    var messages = await CoreStart(_cm, expandedRequests);
    return Ok(CommonResponse<List<string>>.Ok(messages, "Processed"));
}

// Similar updates for Stop, Pause, Continue, GetLiveData, GetSessions, GetDevices
```

#### Step 4: Update MCP Tools

```csharp
// MCP/DeviceMcpTools.cs

[McpServerTool]
[Description("Send program, battery config, and DBC file to circuits. " +
             "Supports single channel, channel range (e.g., '1-64'), or channel list (e.g., [1,2,5,10]).")]
public async Task<string> SendProgram(
    [Description("Device ID")] int deviceId,
    [Description("Single circuit ID (when not using range or list)")] int? circuitId,
    [Description("Channel range (e.g., '1-64' or '1-8,17-24')")] string? channelRange,
    [Description("Explicit channel list (e.g., [1,2,5,10])")] List<int>? channelList,
    [Description("Program ID to load")] int programId,
    [Description("Battery ID (optional)")] int? batteryId,
    [Description("DBC file record ID (optional)")] int? dbcId)
{
    var request = new List<CommonRequest>
    {
        new CommonRequest
        {
            DeviceID = deviceId,
            SecondaryBoardNumber = 1,
            ChannelNumber = circuitId ?? 1,
            ChannelRange = channelRange,
            ChannelList = channelList,
            ProgramId = programId,
            BatteryId = batteryId,
            dbcId = dbcId
        }
    };
    
    var expandedRequests = request
        .SelectMany(req => ChannelExpander.ExpandChannels(req))
        .ToList();
    
    var messages = await DeviceController.CoreSendProgram(_cm, _programServices, _batteryServices, _dbcService, expandedRequests);
    return string.Join("\n", messages);
}
```

---

## Testing Strategy

### Unit Tests

```csharp
[Fact]
public void ParseChannelRange_SingleRange_ReturnsCorrectList()
{
    var result = ChannelExpander.ParseChannelRange("1-5");
    Assert.Equal(new[] { 1, 2, 3, 4, 5 }, result);
}

[Fact]
public void ParseChannelRange_MultipleRanges_ReturnsCorrectList()
{
    var result = ChannelExpander.ParseChannelRange("1-3,8-10,15");
    Assert.Equal(new[] { 1, 2, 3, 8, 9, 10, 15 }, result);
}

[Fact]
public void ExpandChannels_WithChannelList_ExpandsCorrectly()
{
    var request = new CommonRequest
    {
        DeviceID = 1,
        ChannelList = new List<int> { 1, 5, 10 },
        ProgramId = 20
    };
    
    var expanded = ChannelExpander.ExpandChannels(request);
    
    Assert.Equal(3, expanded.Count);
    Assert.All(expanded, r => Assert.Equal(20, r.ProgramId));
}
```

### Integration Tests

1. **Test backward compatibility**: Existing single-channel requests still work
2. **Test range expansion**: "1-64" generates 64 circuit operations
3. **Test MCP tools**: Verify channel range/list parameters work correctly
4. **Test with simulator**: Run with 64-channel simulator to verify bulk operations

---

## Migration Path

### Phase 1: Add DBC Files Endpoint (Low Risk)
- Add `GetDbcFiles` to `IDbcService`, `DbcService`, `DeviceController`, `DeviceMcpTools`
- No breaking changes
- Estimated effort: 2 hours

### Phase 2: Add Channel Range Support (Medium Risk)
- Add `ChannelRange` and `ChannelList` fields to `CommonRequest`
- Create `ChannelExpander` utility
- Update controller endpoints to expand requests
- Maintain backward compatibility (single `ChannelNumber` still works)
- Estimated effort: 4-6 hours

### Phase 3: Update MCP Tools (Low Risk)
- Update MCP tool signatures to accept `channelRange` and `channelList`
- Update descriptions and examples
- Estimated effort: 2 hours

### Phase 4: Testing & Documentation (Medium Effort)
- Write unit tests for `ChannelExpander`
- Integration tests with 64-channel simulator
- Update API documentation
- Estimated effort: 3-4 hours

---

## Benefits

### Developer Experience
- ✅ **90% reduction in API payload size** for bulk operations (1 object vs 64)
- ✅ **Simpler client code** - one request instead of 64 identical ones
- ✅ **Better DX for MCP/CLI tools** - natural "send to channels 1-64" syntax

### Performance
- ✅ **Smaller payloads** - less network bandwidth
- ✅ **Faster serialization/deserialization** on client side
- ✅ **Same server-side performance** - expansion happens once, then existing logic runs

### Maintainability
- ✅ **Backward compatible** - no breaking changes
- ✅ **Centralized logic** - expansion in one place (`ChannelExpander`)
- ✅ **Testable** - expansion logic isolated and unit-testable

---

## Risks & Mitigation

### Risk 1: Large Range Expansions
**Risk:** User sends "1-1000" → server OOM  
**Mitigation:** Add validation to limit max expanded channels (e.g., 128)

```csharp
if (channels.Count > 128)
    throw new ArgumentException("Cannot expand to more than 128 channels in one request");
```

### Risk 2: Ambiguous Requests
**Risk:** User sends both `ChannelRange` and `ChannelList`  
**Mitigation:** Clear priority order (List > Range > Number) + validation warning

```csharp
if (request.ChannelList != null && !string.IsNullOrWhiteSpace(request.ChannelRange))
    _logger.LogWarning("Both ChannelList and ChannelRange provided; using ChannelList");
```

### Risk 3: Backward Compatibility
**Risk:** Existing clients break  
**Mitigation:** New fields are optional; single `ChannelNumber` still works

---

## API Examples

### Before (Current State)

```json
POST /api/Device/SendProgram
[
  {"DeviceID": 1, "ChannelNumber": 1, "ProgramId": 10, "BatteryId": 3, "dbcId": 2},
  {"DeviceID": 1, "ChannelNumber": 2, "ProgramId": 10, "BatteryId": 3, "dbcId": 2},
  {"DeviceID": 1, "ChannelNumber": 3, "ProgramId": 10, "BatteryId": 3, "dbcId": 2},
  // ... 61 more objects
]
```

### After (Enhanced State)

```json
POST /api/Device/SendProgram
[
  {
    "DeviceID": 1,
    "ChannelRange": "1-64",
    "ProgramId": 10,
    "BatteryId": 3,
    "dbcId": 2
  }
]
```

Or with explicit list:

```json
POST /api/Device/SendProgram
[
  {
    "DeviceID": 1,
    "ChannelList": [1, 2, 5, 10, 15, 20],
    "ProgramId": 10,
    "BatteryId": 3,
    "dbcId": 2
  }
]
```

Or mixed (backward compatible):

```json
POST /api/Device/SendProgram
[
  {"DeviceID": 1, "ChannelRange": "1-32", "ProgramId": 10, "BatteryId": 3},
  {"DeviceID": 1, "ChannelRange": "33-64", "ProgramId": 20, "BatteryId": 5}
]
```

---

## Conclusion

The proposed enhancements address both identified gaps:

1. **Missing DBC Files Endpoint** - Straightforward addition, consistent with existing `GetPrograms` / `GetBatteries` patterns
2. **Channel Range/List Support** - Backward-compatible enhancement that dramatically improves API ergonomics for bulk operations

**Recommendation:** Implement both enhancements in sequence (Phase 1 → Phase 2 → Phase 3 → Phase 4) to minimize risk and validate each step independently.
