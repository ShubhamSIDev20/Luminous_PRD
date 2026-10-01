# WebAppME Changelog

## Version 1.0 - September 2026

### ✨ New Features

#### §12.3 Battery Parameter Tokens Implementation
- **Bare Tokens Added:** CNom, INom, UNom, UGas, NoCell, CutOff, ChargeF, EDensity
- **Unit Mapping:** Complete VNC/ACN battery-relative unit resolution
- **Editor Integration:** Real-time validation with unit checking
- **Wire Protocol:** Byte-level encoding with proper payload structure (0x32 for voltage, 0x26 for accumulation)
- **API:** Device endpoints (CoreSendProgram, SetProgramAsync) support battery parameter encoding

**Related Commits:**
- `85b4961` - fix(program): single-source battery-parameter placeholder units in editor and read-only preview
- `e58a3e5` - feat(program-editor): §12.3 bare battery-parameter tokens; confirm Ramp/Resistance blocked

**Test Coverage:**
- 771 unit tests passing
- 0 build errors
- API endpoint validation complete
- Wire protocol encoding verified

**Documentation:**
- Battery-Parameters-Intern-Table-Plan.md (detailed implementation plan)
- VNC-ACN5-Implementation-Plan.md (unit resolution strategy)
- Updated PROTOCOL.md with encoding specifications

### 🏗️ Architecture Updates

#### BatteryUnitResolver (New Component)
- Single source of truth for §12.3 token unit mapping
- GetBatteryGlobalVariables() provides consistent units across editor and encoding
- GetPlaceholderGlobalVariables() for preview-time validation

#### ProgramBuilder Enhancements
- Battery variable injection during encoding (SetProgramAsync)
- Integration with BatteryUnitResolver for unit consistency
- Wire protocol byte generation with proper encoding

#### ValidationHelper Improvements
- ACN/VNC unit variant auto-correction
- Nominal, Limit, and Registration field validation
- Token acceptance with mapped units

### 📊 Test & Quality

- Total Test Count: 771
- Pass Rate: 100% (0 failures)
- Build Status: Clean (0 errors, 0 warnings)
- API Validation: Complete
- Hardware Integration: Ready for demo

### 📚 Documentation Suite (CMMI Level 3)

Complete documentation structure added:
- **SRS.md** - Software Requirements Specification
- **ADD.md** - Architecture Design Document
- **DDD.md** - Detailed Design Document
- **DBD.md** - Database Design Document
- **ICD.md** - Interface Control Document
- **PROTOCOL.md** - Wire Protocol Specification (includes battery-relative encoding)
- **PMP.md** - Project Management Plan
- **CMP.md** - Configuration Management Plan
- **QAP.md** - Quality Assurance Plan
- **RMP.md** - Risk Management Plan
- **VVP.md** - Verification & Validation Plan
- **RTM.md** - Requirements Traceability Matrix
- **Calibration.md** - Calibration Procedures

### 🔒 Known Constraints

#### Out of Scope (Protocol Level)
- **Ramp & Resistance Features:** No CutoffCondition or RegistrationType support in wire protocol
- **Unitless Token Placement:** NoCell, ChargeF, EDensity cannot be placed in Nominal/Limit/Registration fields
- **TabViewer Routing:** Requires program-first pattern (no direct device commands via API)

### 🚀 Deployment Readiness

- ✅ Code review: Complete
- ✅ Build verification: Passing
- ✅ Unit tests: 771 passing
- ✅ API endpoints: Tested
- ✅ Wire protocol: Verified
- ✅ Documentation: Complete
- ✅ Hardware integration: Ready
- → Hardware demo: Scheduled (Phase 4)

### 👥 Project Handover

**Knowledge Transfer Status:**
- Phase 1: ✅ Repository & Access Setup
- Phase 2: ✅ Code Review & Verification
- Phase 3: ✅ Hardware & Integration Review
- Phase 4: → Hardware Demo & Ownership Transfer

**Recipient:** Shubham K
**Handover Date:** September 2026
**Documentation:** Complete handover guide provided

---

## Version 0.6 - Previous Releases

Previous versions included core BTS functionality with Blazor Server UI, SignalR real-time updates, and basic battery testing capabilities. See git history for detailed changelog prior to version 1.0.

---

## Git Commit History

```
85b4961 fix(program): single-source battery-parameter placeholder units in editor and read-only preview
e58a3e5 feat(program-editor): §12.3 bare battery-parameter tokens; confirm Ramp/Resistance blocked
bcd87aa docs: add CMMI Level 3 documentation suite
8c997ce Merge branch 'fix/channel-list-migration-and-tests' into main
1e7145d feat(program-editor): VNC/ACNx battery-relative nominal-value/limit/registration units
26b6d1f fix(api): migrate MCP/tests to ChannelList-only CommonRequest, fix build + correctness bugs
```

---

**Last Updated:** September 2026  
**Repository:** https://github.com/Quench-EV-Charger/ME_PRD/  
**Status:** Production Ready
