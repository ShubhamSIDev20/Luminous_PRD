# T-6: Verify device registration end to end against the real Web Application — PASSED
> Created: 2026-08-07 | Status: Done (2026-08-07, Session #2)
> File: `tasks/2026-08-07_verify-device-registration-end-to-end.md`

---

## Description
Developer confirmed the device registered against the real Web Application
(`BatteryTestingSystem`, listening `0.0.0.0:9999`). Root cause of the earlier
failure: the board was pointed at the wrong host (`172.16.10.21` — some
other machine on the LAN that answered with an RST). The Web App runs on the
developer's laptop, whose Wi-Fi address is `172.16.14.244`.

## Progress Log
- **2026-08-07** (Session #2, ~19:55 IST): PASSED —
  `./me_primary --server 172.16.14.244 --iface ethernet0 --verbose`.

## Related
- Relates to: ADR-4, ADR-5
