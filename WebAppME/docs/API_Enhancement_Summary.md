# API Enhancement Summary

**Date:** 2026-09-03  
**Project:** BatteryTestingSystem  
**Status:** Proposed Design

> **⚠️ Superseded (2026-09-04):** `ChannelNumber` and `ChannelRange` were removed from `CommonRequest` after this was written — `ChannelList` is now the only channel field. See `.claude/agent-memory/api.md` for the current behavior.

---

## Key Findings

### 1. Missing DBC Files Endpoint ❌

**Current State:**
- ✅ `POST /api/Device/GetPrograms` - Lists all test programs
- ✅ `POST /api/Device/GetBatteries` - Lists all battery configurations  
- ❌ `POST /api/Device/GetDbcFiles` - **Does not exist**

**Impact:**
- MCP tools and API clients cannot discover available DBC files programmatically
- Must rely on battery-specific lookups or manual knowledge of IDs

**Solution:** Add `GetDbcFiles` endpoint following existing patterns

---

### 2. Repetitive Channel Requests 📦

**Current Problem:**
To send a program to channels 1-64 requires 64 identical request objects:

```json
[
  {"DeviceID": 1, "ChannelNumber": 1, "ProgramId": 5, "BatteryId": 3, "dbcId": 2},
  {"DeviceID": 1, "ChannelNumber": 2, "ProgramId": 5, "BatteryId": 3, "dbcId": 2},
  // ... 62 more identical objects
]
```

**Impact:**
- Large payloads (64x overhead)
- Error-prone manual construction
- Poor developer experience
- Unnecessary network bandwidth

**Solution:** Add channel range/list support to `CommonRequest`

---

## Proposed Enhancements

### Enhancement 1: Add DBC Files Listing

**New Endpoint:**
```
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
    }
  ]
}
```

**Implementation Steps:**
1. Add `GetAllAsync()` to `IDbcService` interface
2. Implement in `DbcService` and `DbcRepository`
3. Add controller endpoint in `DeviceController`
4. Add MCP tool in `DeviceMcpTools`

**Effort:** ~2 hours  
**Risk:** Low (no breaking changes)

---

### Enhancement 2: Channel Range/List Support

**Enhanced CommonRequest Model:**
```csharp
public class CommonRequest
{
    public int DeviceID { get; set; }
    public int SecondaryBoardNumber { get; set; } = 1;
    public int ChannelNumber { get; set; }  // Existing (backward compatible)
    
    // NEW: Range notation (e.g., "1-64" or "1-8,17-24")
    public string? ChannelRange { get; set; }
    
    // NEW: Explicit list (e.g., [1,2,5,10])
    public List<int>? ChannelList { get; set; }
    
    public int? ProgramId { get; set; }
    public int? BatteryId { get; set; }
    public int? dbcId { get; set; } = 0;
}
```

**Priority Resolution:**
1. If `ChannelList` provided → use explicit list
2. Else if `ChannelRange` provided → parse and expand range
3. Else use single `ChannelNumber` (existing behavior)

**Usage Examples:**

```json
// Single channel (existing - still works)
{"DeviceID": 1, "ChannelNumber": 5, "ProgramId": 10}

// Range notation (NEW)
{"DeviceID": 1, "ChannelRange": "1-64", "ProgramId": 10, "BatteryId": 3}

// Multiple ranges (NEW)
{"DeviceID": 1, "ChannelRange": "1-8,17-24,33-40", "ProgramId": 10}

// Explicit list (NEW)
{"DeviceID": 1, "ChannelList": [1,2,5,10,15], "ProgramId": 10}
```

**Before vs After:**

| Current (64 objects) | Enhanced (1 object) |
|---------------------|---------------------|
| 64 identical JSON objects | 1 object with `"ChannelRange": "1-64"` |
| ~2KB payload | ~80 bytes payload |
| Error-prone construction | Simple, clear syntax |

**Implementation Steps:**
1. Add `ChannelRange` and `ChannelList` fields to `CommonRequest`
2. Create `ChannelExpander` utility with range parsing logic
3. Update controller endpoints to expand before processing
4. Add validation (max 128 channels per request)
5. Update MCP tools to support new parameters

**Effort:** ~4-6 hours  
**Risk:** Medium (requires careful expansion logic, but backward compatible)

---

## Migration Path

### ✅ Zero Breaking Changes

Both enhancements are **fully backward compatible**:

| Existing Pattern | Still Works? | Notes |
|-----------------|--------------|-------|
| Single `ChannelNumber` | ✅ Yes | Unchanged behavior |
| Array of requests | ✅ Yes | Each processed individually |
| No DBC endpoint calls | ✅ Yes | New endpoint is additive |

**Adoption Strategy:**
- Existing clients continue working without any changes
- New clients can adopt enhanced patterns immediately
- MCP tools gain better ergonomics for bulk operations

---

## Benefits

### Developer Experience
- **90% payload reduction** for bulk operations (1 object vs 64)
- **Simpler client code** - natural "1-64" syntax
- **Better error messages** - validation at expansion time
- **API discoverability** - DBC files now listable

### Performance
- **Smaller payloads** - less network bandwidth
- **Faster serialization** on client side
- **Same server performance** - expansion once, then existing logic

### Maintainability
- **Backward compatible** - no breaking changes
- **Centralized logic** - expansion in `ChannelExpander`
- **Testable** - expansion isolated and unit-testable

---

## Implementation Plan

### Phase 1: DBC Files Endpoint
**Effort:** 2 hours | **Risk:** Low

- [ ] Add `GetAllAsync()` to `IDbcService`
- [ ] Implement in `DbcService.cs`
- [ ] Add `GetDbcFiles` endpoint in `DeviceController.cs`
- [ ] Add `GetDbcFiles` tool in `DeviceMcpTools.cs`

### Phase 2: Channel Range Support
**Effort:** 4-6 hours | **Risk:** Medium

- [ ] Add `ChannelRange`/`ChannelList` to `CommonRequest`
- [ ] Create `ChannelExpander` utility
- [ ] Update controller endpoints (SendProgram, Start, Stop, etc.)
- [ ] Add validation (max 128 channels)

### Phase 3: MCP Tools Update
**Effort:** 2 hours | **Risk:** Low

- [ ] Update MCP tool signatures
- [ ] Add usage examples to descriptions
- [ ] Test with Claude Desktop integration

### Phase 4: Testing & Documentation
**Effort:** 3-4 hours | **Risk:** Medium

- [ ] Unit tests for `ChannelExpander`
- [ ] Integration tests with 64-channel simulator
- [ ] Backward compatibility validation
- [ ] API documentation updates

**Total Estimated Effort:** 11-14 hours

---

## Recommendation

✅ **Implement both enhancements sequentially** (Phase 1 → 2 → 3 → 4)

**Rationale:**
- Both address real pain points identified in API review
- Minimal risk due to backward compatibility
- Clear improvement in developer experience
- Reasonable time investment (~2 days)

**Next Steps:**
1. Review and approve this proposal
2. Create implementation tasks in task tracker
3. Start with Phase 1 (low-risk, quick win)
4. Validate with simulator testing before production deployment

---

## Files Modified

### New Files:
- `Utils/ChannelExpander.cs` - Channel range parsing utility

### Modified Files:
- `Models/ViewModels/CommonRequest.cs` - Add ChannelRange/ChannelList
- `Services/Interfaces/IServices.cs` - Add GetAllAsync() to IDbcService
- `Services/Implementations/DbcService.cs` - Implement GetAllAsync()
- `Repositories/Implementations/DbcRepository.cs` - Add repository method
- `Controllers/DeviceController.cs` - Add GetDbcFiles, update endpoints
- `MCP/DeviceMcpTools.cs` - Add GetDbcFiles, update tool signatures

### Test Files:
- `BatteryTestingSystem.Tests/Utils/ChannelExpanderTests.cs` (new)

---

## Full Technical Details

See `docs/API_Enhancement_Design.md` for complete implementation details including:
- Detailed code examples for `ChannelExpander` utility
- Complete API request/response examples
- Unit test specifications
- Risk mitigation strategies
- Alternative design considerations

---

**Questions or Concerns?**
Review the full design document or discuss specific implementation details with the development team.
