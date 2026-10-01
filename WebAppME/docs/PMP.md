# Project Management Plan (PMP)
**Document ID:** BTS-PMP-001  **Version:** 0.5  **Date:** May 2026  **Status:** Approved

---

## 1. Project Overview

| Item | Details |
|---|---|
| Project Name | Battery Testing System (BTS) |
| Current Version | 0.5 |
| Project Type | Embedded + Web Application |
| Start Date | December 2025 |
| Current Release | May 2026 |
| Team Size | Small (1–3 developers) |

---

## 2. Project Scope

### In Scope
- Blazor Server web application for hardware control and monitoring
- TCP/UDP hardware communication protocol implementation
- Per-session SQLite database management
- CI/CD pipeline (GitHub Actions → ghcr.io)
- Docker containerized deployment

### Out of Scope
- BTS hardware firmware development
- Mobile native applications
- Cloud-hosted data storage

---

## 3. Development Lifecycle

BTS follows an **Iterative Development** model with continuous delivery via GitHub Actions.

```
Requirements → Design → Implementation → Test → Deploy
      ↑_______________________________________|
              (iterative cycles)
```

### Milestones

| Milestone | Version | Date | Status |
|---|---|---|---|
| Initial architecture + DB schema | 0.1 | Dec 2025 | ✅ Done |
| TCP registration + real-time UDP | 0.2 | Jan 2026 | ✅ Done |
| Program upload + control commands | 0.3 | Feb 2026 | ✅ Done |
| DBC CAN + session recording | 0.4 | Mar 2026 | ✅ Done |
| Audit, ACL, calibration, UI polish | 0.5 | May 2026 | ✅ Done |
| Reporting, advanced analytics | 0.6 | TBD | 🔄 Planned |

---

## 4. Work Breakdown Structure (WBS)

```
BTS Project
├── 1. Infrastructure
│   ├── 1.1 Database setup + migrations
│   ├── 1.2 Docker + CI/CD pipeline
│   └── 1.3 Logging + monitoring
├── 2. Hardware Communication
│   ├── 2.1 TCP registration (ChannelManager)
│   ├── 2.2 UDP real-time listener
│   ├── 2.3 UDP session store listener
│   └── 2.4 SADP broadcast (DeviceDiscovery)
├── 3. Business Logic
│   ├── 3.1 Program management
│   ├── 3.2 Battery & session management
│   ├── 3.3 Calibration
│   └── 3.4 DBC CAN file management
├── 4. UI / Presentation
│   ├── 4.1 Layout + navigation
│   ├── 4.2 Real-time dashboard
│   ├── 4.3 Device discovery UI
│   ├── 4.4 Program editor
│   └── 4.5 Reporting
└── 5. Quality & Documentation
    ├── 5.1 Unit testing
    ├── 5.2 Integration testing
    └── 5.3 CMMI documentation
```

---

## 5. Roles and Responsibilities

| Role | Responsibility |
|---|---|
| Project Lead / Architect | Architecture decisions, hardware protocol, CI/CD |
| Full-Stack Developer | Blazor UI, services, repositories |
| QA / Tester | Test plans, verification, bug tracking |

---

## 6. Communication Plan

| Activity | Frequency | Participants | Medium |
|---|---|---|---|
| Development sync | Daily | Dev team | Stand-up / chat |
| Sprint review | Bi-weekly | All | Meeting |
| Release review | Per release | All + stakeholders | Meeting |
| Issue tracking | Continuous | All | GitHub Issues |

---

## 7. Tools

| Tool | Purpose |
|---|---|
| GitHub | Source control, CI/CD, releases |
| GitHub Actions | Automated build, test, deploy |
| ghcr.io | Docker image registry |
| Visual Studio 2022 | IDE |
| Docker Desktop | Local container testing |
| Serilog | Logging |
| GitHub Issues | Bug/feature tracking |
