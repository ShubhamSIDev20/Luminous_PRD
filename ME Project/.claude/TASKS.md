# TASKS.md — Task Index
> Updated: 2026-08-21T11:15+05:30
> Full detail for any task lives in `tasks/{filename}` — this file only indexes and describes.

---

## 🔥 Active
| ID | Task | File | Started | Agent |
|----|------|------|---------|-------|
| T-24 | Full battery-test flow, hardware run, against a real Secondary over RPMsg — partially proven 2026-08-19 (registration + CPU3 pin live), not yet end-to-end | [tasks/2026-08-11_full-battery-test-hardware-run.md](tasks/2026-08-11_full-battery-test-hardware-run.md) | 2026-08-11 | Developer |
| T-60 | Run the isolcpus kernel provisioning runbook on board `172.16.18.167` — CPU3 pin works today but isn't kernel-isolated yet | [tasks/2026-08-19_isolcpus-kernel-provisioning.md](tasks/2026-08-19_isolcpus-kernel-provisioning.md) | 2026-08-19 | Developer |
| T-62 | Hardware-verify Secondary 1 multi-channel (1-4) registration + CAN-FD SET_VALUES coalescing on board `172.16.18.167` | [tasks/2026-08-19_hardware-verify-multichannel-secondary1.md](tasks/2026-08-19_hardware-verify-multichannel-secondary1.md) | 2026-08-19 | Developer |

## 📋 Backlog
| ID | Task | File | Priority | Notes |
|----|------|------|----------|-------|
| T-63 | Revert `me-primary/Dockerfile` to `scratch` before the next production release | [tasks/2026-08-20_revert-debug-image-to-scratch-before-release.md](tasks/2026-08-20_revert-debug-image-to-scratch-before-release.md) | 🔴 High | `0.0.3`/`:latest` (2026-08-21) is still the `debian:bookworm-slim` debug image — developer explicitly chose to keep it for now, will say when to revert |
| T-65 | Confirm the M7 CAN-echo root cause with whoever owns the M7 firmware | [tasks/2026-08-20_confirm-m7-echo-root-cause-with-firmware-owner.md](tasks/2026-08-20_confirm-m7-echo-root-cause-with-firmware-owner.md) | 🟡 Med | A53-side filter (ADR-38) in place; echo didn't recur after ADR-37, root cause unconfirmed |
| T-66 | Decide whether CCChg's SET_VALUES should ever carry a non-zero voltage | [tasks/2026-08-20_ccchg-voltage-always-zero.md](tasks/2026-08-20_ccchg-voltage-always-zero.md) | 🟢 Low | `step_decode.h` has no voltage field for CCChg at all today — may be correct for CC mode |
| T-45 | An unrecognised `0xEE` QueryID is acked before Core Logic rejects it | [tasks/2026-08-12_unrecognised-ee-queryid-acked-before-rejection.md](tasks/2026-08-12_unrecognised-ee-queryid-acked-before-rejection.md) | 🟡 Med | Zero impact while Web App sends only the six documented commands |
| T-44 | Audit every remaining `me_frame_expected_len()` entry against real captured frames | [tasks/2026-08-12_audit-frame-length-table-against-captures.md](tasks/2026-08-12_audit-frame-length-table-against-captures.md) | 🔴 High | Grep hardware logs for `RECOVERED, but the length table … needs fixing` |
| T-38 | Confirm the `0xBB` Q1 frame length against a captured hex dump | [tasks/2026-08-12_confirm-bb-q1-frame-length.md](tasks/2026-08-12_confirm-bb-q1-frame-length.md) | 🟡 Med | Downgraded by ADR-19's CRC backstop; folded into T-44 |
| T-39 | Decide whether `0xBB` Q2 (program metadata) needs a reply | [tasks/2026-08-12_decide-bb-q2-metadata-reply.md](tasks/2026-08-12_decide-bb-q2-metadata-reply.md) | 🟡 Med | Left open by choice |
| T-40 | Watch for `0xBB` Q4 CRC rejections on the next hardware run | [tasks/2026-08-12_watch-bb-q4-crc-rejections.md](tasks/2026-08-12_watch-bb-q4-crc-rejections.md) | 🔴 High | Q4 is now CRC-verified before storage |
| T-34 | Decide whether device-scoped `0xEE`/`0xAA` queries should bypass the circuit gate | [tasks/2026-08-11_device-scoped-queries-bypass-circuit-gate.md](tasks/2026-08-11_device-scoped-queries-bypass-circuit-gate.md) | 🟢 Low | Downgraded 2026-08-12 after T-33 closed |
| T-36 | Make the admission-control *gate* host-testable | [tasks/2026-08-12_make-admission-control-gate-host-testable.md](tasks/2026-08-12_make-admission-control-gate-host-testable.md) | 🟢 Low | `comm_thread.c` is LINUX-ONLY, excluded from host build |
| T-46 | Watch for a battery payload that is not 40 bytes | [tasks/2026-08-12_watch-battery-payload-not-40-bytes.md](tasks/2026-08-12_watch-battery-payload-not-40-bytes.md) | 🔴 High | ADR-21 refuses/NACKs short payloads |
| T-48 | Handle `0xAA` Q7–Q10 broadcast frames, or reject them deliberately | [tasks/2026-08-12_handle-aa-q7-q10-broadcast-frames.md](tasks/2026-08-12_handle-aa-q7-q10-broadcast-frames.md) | 🟡 Med | 2-byte broadcast header, currently mis-parsed but harmlessly dropped |
| T-25 | Confirm the `0xAA` battery/config field offsets — RESOLVED 2026-08-12 | [tasks/2026-08-07_confirm-aa-battery-config-field-offsets.md](tasks/2026-08-07_confirm-aa-battery-config-field-offsets.md) | 🟢 Low | Pointer only — split into T-47, T-48 |
| T-26 | Confirm whether `0xBB` Q1/Q2 are still sent — partly answered 2026-08-12 | [tasks/2026-08-07_confirm-bb-q1-q2-still-sent.md](tasks/2026-08-07_confirm-bb-q1-q2-still-sent.md) | 🟢 Low | Pointer only — Q2 tracked as T-39 |
| T-27 | Check `.bss` against the Docker memory limit | [tasks/2026-08-11_check-bss-against-docker-memory-limit.md](tasks/2026-08-11_check-bss-against-docker-memory-limit.md) | 🟡 Med | 261 MB `.bss`, needs `docker stats` check |
| T-29 | Session data on UDP 10001 | [tasks/2026-08-11_session-data-on-udp-10001.md](tasks/2026-08-11_session-data-on-udp-10001.md) | 🟡 Med | Path built end to end, no producer yet |
| T-18 | Set `--iface ethernet0` as the default | [tasks/2026-08-07_set-ethernet0-default-iface.md](tasks/2026-08-07_set-ethernet0-default-iface.md) | 🟢 Low | Confirmed on hardware |
| T-19 | Narrow or drop the Docker-bridge IP warning | [tasks/2026-08-07_narrow-drop-docker-bridge-warning.md](tasks/2026-08-07_narrow-drop-docker-bridge-warning.md) | 🟢 Low | Ask before changing — reverted once already |
| T-9 | Obtain the "Device Registration Data" sheet from the source Excel | [tasks/2026-08-06_obtain-device-registration-data-sheet.md](tasks/2026-08-06_obtain-device-registration-data-sheet.md) | 🟡 Med | Primary source not in workspace |
| T-10 | Implement the remaining `0xDD` queries: Q2 delete, Q3 discovery, Q4 IP config | [tasks/2026-08-07_implement-remaining-dd-queries.md](tasks/2026-08-07_implement-remaining-dd-queries.md) | 🟡 Med | Documented in `bm_device_registration_v5.0.md` |
| T-11 | Live data on UDP 10000 + session store on UDP 10001 — largely done in #4 | [tasks/2026-08-07_live-data-udp10000-session-store-udp10001.md](tasks/2026-08-07_live-data-udp10000-session-store-udp10001.md) | 🟢 Low | Pointer only — remainder tracked as T-29 |
| T-12 | Command groups `0xAA`/`0xBB`/`0xEE`/`0xA0` — first three done in #4 | [tasks/2026-08-07_command-groups-aa-bb-ee-a0.md](tasks/2026-08-07_command-groups-aa-bb-ee-a0.md) | 🟢 Low | Only `0xA0` calibration remains |
| T-3 | Decide whether the ME app ships as a Docker image rather than a bare binary | [tasks/2026-08-06_decide-docker-image-vs-bare-binary.md](tasks/2026-08-06_decide-docker-image-vs-bare-binary.md) | 🟡 Med | Effectively superseded by T-17/T-58 |
| T-4 | Record the board's IP / hostname + SSH auth method | [tasks/2026-08-06_record-board-ip-hostname-ssh-auth.md](tasks/2026-08-06_record-board-ip-hostname-ssh-auth.md) | 🟢 Low | Passed ad-hoc as `-BoardIP` each run |
| T-5 | Confirm which container base image the board actually has | [tasks/2026-08-06_confirm-container-base-image.md](tasks/2026-08-06_confirm-container-base-image.md) | 🟢 Low | `deploy.ps1` defaults to `debian:bookworm-slim` |

## ✅ Done
| ID | Task | File | Completed | Session |
|----|------|------|-----------|---------|
| T-64 | Cut a new GHCR release including ADR-34/35/36/37/38/39 | [tasks/2026-08-20_release-ghcr-version-with-registration-retry-fix.md](tasks/2026-08-20_release-ghcr-version-with-registration-retry-fix.md) | 2026-08-21 | #16 |
| T-61 | Secondary 1 registers all 4 channels (0x11-0x14) independently over one TCP connection | [tasks/2026-08-19_secondary1-registers-4-channels.md](tasks/2026-08-19_secondary1-registers-4-channels.md) | 2026-08-19 | #12 |
| T-59 | CAN-FD RPMsg round-trip latency log per Secondary, microseconds | [tasks/2026-08-19_can-fd-rpmsg-rtt-latency-log.md](tasks/2026-08-19_can-fd-rpmsg-rtt-latency-log.md) | 2026-08-19 | #11 |
| T-58 | CPU3 core isolation + production Dockerfile/docker-compose.yml/GHCR release | [tasks/2026-08-19_cpu3-core-isolation-cicd-release.md](tasks/2026-08-19_cpu3-core-isolation-cicd-release.md) | 2026-08-19 | #11 |
| T-57 | Real RPMsg CAN transport replacing the fabricated `can_mgr.c` responder | [tasks/2026-08-18_real-rpmsg-can-transport.md](tasks/2026-08-18_real-rpmsg-can-transport.md) | 2026-08-18/19 | #11 |
| T-17 | Dedicated production Docker image for `me_primary` | [tasks/2026-08-19_dedicated-production-docker-image.md](tasks/2026-08-19_dedicated-production-docker-image.md) | 2026-08-19 | #11 |
| T-56 | Delete merged feature branches; create `develop` | [tasks/2026-08-15_delete-merged-branches-create-develop.md](tasks/2026-08-15_delete-merged-branches-create-develop.md) | 2026-08-15 | #10 |
| T-55 | `git init` + PR-based merge workflow adopted | [tasks/2026-08-14_git-init-pr-merge-workflow.md](tasks/2026-08-14_git-init-pr-merge-workflow.md) | 2026-08-14/15 | #9/#10 |
| T-54 | `step_decode`/`can_frame`/`step_engine` implementation (T-28 done, differently than scoped) | [tasks/2026-08-14_step-decode-can-frame-step-engine-impl.md](tasks/2026-08-14_step-decode-can-frame-step-engine-impl.md) | 2026-08-14/15 | #10 |
| T-53 | Design + implementation plan for step execution | [tasks/2026-08-14_step-execution-design-plan.md](tasks/2026-08-14_step-execution-design-plan.md) | 2026-08-14 | #10 |
| T-47 | Battery Impedance and Energy Density confirmed `float` | [tasks/2026-08-12_battery-impedance-energy-density-float-confirmed.md](tasks/2026-08-12_battery-impedance-energy-density-float-confirmed.md) | 2026-08-12 | #9 |
| T-49 | Transcribe `Config Data Frame Format V6.0.xlsx` → `Ref Docs/bm_config_v6.0.md` | [tasks/2026-08-12_transcribe-config-workbook-to-markdown.md](tasks/2026-08-12_transcribe-config-workbook-to-markdown.md) | 2026-08-12 | #9 |
| T-50 | Store the complete 40-byte battery record per Secondary/Channel | [tasks/2026-08-12_store-complete-40-byte-battery-record.md](tasks/2026-08-12_store-complete-40-byte-battery-record.md) | 2026-08-12 | #9 |
| T-51 | `0xAA` frame-length table entries (Q1–Q4, Q5, Q6; Q7–Q10 deliberately 0) | [tasks/2026-08-12_aa-frame-length-table-entries.md](tasks/2026-08-12_aa-frame-length-table-entries.md) | 2026-08-12 | #9 |
| T-52 | One-shot `0xCC` frame after successful registration | [tasks/2026-08-12_post-registration-cc-frame.md](tasks/2026-08-12_post-registration-cc-frame.md) | 2026-08-12 | #9 |
| T-41 | Fix the `0xEE` Start BAD_CRC — Q1 carries a 4-byte Session ID | [tasks/2026-08-12_fix-ee-start-bad-crc-session-id.md](tasks/2026-08-12_fix-ee-start-bad-crc-session-id.md) | 2026-08-12 | #8 |
| T-42 | `me_frame_resolve_len()` — locate a frame boundary by CRC when the layout table is wrong | [tasks/2026-08-12_frame-resolve-len-crc-backstop.md](tasks/2026-08-12_frame-resolve-len-crc-backstop.md) | 2026-08-12 | #8 |
| T-43 | Respond to `0xAA` Q5 battery write and all six `0xEE` control commands | [tasks/2026-08-12_respond-aa-q5-and-ee-commands.md](tasks/2026-08-12_respond-aa-q5-and-ee-commands.md) | 2026-08-12 | #8 |
| T-37 | Answer the `0xBB` handshake | [tasks/2026-08-12_answer-bb-handshake.md](tasks/2026-08-12_answer-bb-handshake.md) | 2026-08-12 | #7 |
| T-32 | Per-circuit registration gate | [tasks/2026-08-12_per-circuit-registration-gate.md](tasks/2026-08-12_per-circuit-registration-gate.md) | 2026-08-12 | #6 |
| T-31 | Relocate `comm_thread.c/.h` into `src/threads/` | [tasks/2026-08-12_relocate-comm-thread-to-threads-dir.md](tasks/2026-08-12_relocate-comm-thread-to-threads-dir.md) | 2026-08-12 | #6 |
| T-23 | 4-thread base | [tasks/2026-08-11_4-thread-base-implementation.md](tasks/2026-08-11_4-thread-base-implementation.md) | 2026-08-11 | #4 |
| T-0 | Set up cross-compile → deploy → run pipeline for Torizon target | [tasks/2026-08-06_setup-cross-compile-deploy-pipeline.md](tasks/2026-08-06_setup-cross-compile-deploy-pipeline.md) | 2026-08-06 | #1 |
| T-1 | Verify `Hello World!` runs on the board under Docker | [tasks/2026-08-07_verify-hello-world-runs-on-board.md](tasks/2026-08-07_verify-hello-world-runs-on-board.md) | 2026-08-07 | #2 |
| T-2 | Define scope + requirements for the ME registration milestone | [tasks/2026-08-07_define-me-registration-scope.md](tasks/2026-08-07_define-me-registration-scope.md) | 2026-08-07 | #2 |
| T-13 | Rename `hello-world-bringup/` → `me-primary/` | [tasks/2026-08-07_rename-hello-world-bringup-to-me-primary.md](tasks/2026-08-07_rename-hello-world-bringup-to-me-primary.md) | 2026-08-07 | #2 |
| T-14 | Implement CRC-16/Modbus + registration frame pack/parse, test-first | [tasks/2026-08-07_implement-crc16-registration-frame.md](tasks/2026-08-07_implement-crc16-registration-frame.md) | 2026-08-07 | #2 |
| T-15 | Implement system init, comm thread, TCP client, UDP sockets, netinfo, logging | [tasks/2026-08-07_implement-sysinit-comm-thread-tcp-udp.md](tasks/2026-08-07_implement-sysinit-comm-thread-tcp-udp.md) | 2026-08-07 | #2 |
| T-16 | Write functional block diagram to `Docs/ME_Primary_Comm_Block_Diagram.md` | [tasks/2026-08-07_write-comm-block-diagram.md](tasks/2026-08-07_write-comm-block-diagram.md) | 2026-08-07 | #2 |
| T-6 | **Verify device registration end to end against the real Web Application — PASSED** | [tasks/2026-08-07_verify-device-registration-end-to-end.md](tasks/2026-08-07_verify-device-registration-end-to-end.md) | 2026-08-07 | #2 |
| T-7 | Confirm the real CRC byte order | [tasks/2026-08-07_confirm-real-crc-byte-order.md](tasks/2026-08-07_confirm-real-crc-byte-order.md) | 2026-08-07 | #2 |
| T-20 | **Send CRC high byte first (big-endian) — HARDWARE-VERIFIED** | [tasks/2026-08-10_send-crc-high-byte-first.md](tasks/2026-08-10_send-crc-high-byte-first.md) | 2026-08-10 | #3 |
| T-21 | **Treat response `0x02` as success and idle instead of re-registering — HARDWARE-VERIFIED** | [tasks/2026-08-10_treat-response-0x02-as-success.md](tasks/2026-08-10_treat-response-0x02-as-success.md) | 2026-08-10 | #3 |
| T-22 | Collapse dead `ME_COMM_REGISTERED`/`ME_COMM_MONITORING` into a live `ME_COMM_IDLE` | [tasks/2026-08-10_collapse-dead-comm-states.md](tasks/2026-08-10_collapse-dead-comm-states.md) | 2026-08-10 | #3 |

## ❌ Won't Do
| ID | Task | File | Decision Date |
|----|------|------|----------------|
| T-33 | Ask the Web App team which CircuitIDs it sends to this board | [tasks/2026-08-12_ask-webapp-team-circuit-ids.md](tasks/2026-08-12_ask-webapp-team-circuit-ids.md) | 2026-08-12 |
| T-35 | Escape hatch for admission control: `--register-circuits` / `--gate off` | [tasks/2026-08-12_escape-hatch-register-circuits-flag.md](tasks/2026-08-12_escape-hatch-register-circuits-flag.md) | 2026-08-12 |
| T-8 | Correct `WebAppDocs/ICD.md` §3.4 (5-byte response, no CRC — wrong) | [tasks/2026-08-07_correct-icd-md-response-format.md](tasks/2026-08-07_correct-icd-md-response-format.md) | 2026-08-07 |

## 🚫 Blocked
| ID | Task | File | Blocker | Since |
|----|------|------|---------|-------|
| — | *(none)* | — | — | — |

---

## 📌 Known-good configuration (hardware-verified 2026-08-10, session #3)

```bash
./me_primary --server 172.16.15.230 --iface ethernet0 --verbose
```

| Item | Value |
|---|---|
| Board (`ethernet0`) | `172.16.14.206` (DHCP — was `.209`, then `172.16.18.238`), MAC `00:14:2d:ef:86:e2` (Toradex OUI) |
| Web App host | `172.16.15.230` (was `172.16.14.244` on 2026-08-07 — **DHCP, re-check each session**) |
| Web App process | `BatteryTestingSystem`, listening `0.0.0.0:9999` |
| Container | `qflex-backend`, `NetworkMode=host` |
| CRC byte order | ✅ **big-endian, high byte first, BOTH directions — hardware-verified 2026-08-10** (ADR-9). Request CRC `0x369E` → `36 9E`; response CRC `0xBE15` → `BE 15`. Supersedes the LE reading from 2026-08-07. |
| Registration success values | ✅ **`0x01` and `0x02` both register; board then holds IDLE — hardware-verified 2026-08-10** (ADR-10). |

> **The board's and the server's IPs both move.** Every hardware-verified run so
> far has used a different pair. Read the actual `system init` line rather than
> trusting an address recorded here.

⚠️ `172.16.10.21` is **not** the Web App — it is another host on the LAN that
answers with an RST. The laptop's *Ethernet* address `192.168.0.10` matches the
legacy BTS server address in the discovery example and is a false trail while
the board is on the `172.16.x.x` network.

> **Note on T-6:** not "blocked" — nothing is missing on the laptop side. It
> requires network access to both the board and a running Web Application,
> which Claude Code does not have from this machine.
