# 2. Prevent Sleep / Hibernate / Idle Shutdown (Critical — Read Before Installing)

← [1. Prerequisites](01-prerequisites.md) | Next: [3. IIS Installation →](03-iis-installation.md)

---

## 2.0 Why this section exists — read this first

This application receives **UDP registration packets** from test hardware in the background, and test programs can run **continuously for months** with no operator interaction. Two completely separate mechanisms can silently stop that traffic, and both must be fixed — fixing only one is not enough:

1. **The Windows host goes to sleep or hibernates.** The moment the OS suspends, the network stack goes down. Any UDP registration packet a device sends during that window is **lost with no retry** — there is no "catch up" when the machine wakes. The device then shows as unregistered until its next periodic re-send, which can be a long wait, or may require a manual reset on the hardware side.
2. **IIS idles out or recycles the application pool**, even while Windows itself stays awake. IIS's default settings will stop the worker process after 20 minutes of no HTTP traffic (browser-facing traffic — background UDP listening doesn't count as "activity" to IIS) and will also periodically recycle it every ~29 hours regardless of activity. Either one kills the in-process background listener the same as a full app restart would. This is covered in [§3.9 of the IIS doc](03-iis-installation.md#39-prevent-iis-from-idling-out-the-application-pool-critical) — do not skip it.

**Both of these must be disabled on every install.** A site that only fixes Windows sleep but leaves IIS's default idle timeout in place will still silently drop registrations every 20 minutes of UI inactivity.

## 2.1 Set the power plan to High Performance and never sleep

Run in an **elevated PowerShell**:
```powershell
# Switch to the High Performance power plan
powercfg /setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c

# Never sleep, on AC or battery
powercfg /change standby-timeout-ac 0
powercfg /change standby-timeout-dc 0

# Never turn off the display (cosmetic, but keep it consistent — some sites lock the screen based on display-off timers)
powercfg /change monitor-timeout-ac 0
powercfg /change monitor-timeout-dc 0

# Never hibernate
powercfg /change hibernate-timeout-ac 0
powercfg /change hibernate-timeout-dc 0

# Disable hibernation entirely (also frees the disk space reserved for hiberfil.sys, and removes an
# accidental trigger — e.g. a laptop lid close defaulting to Hibernate instead of Do Nothing, see §2.3)
powercfg /hibernate off
```

```
🖼️ [SCREENSHOT — `powercfg /list` output showing the High Performance plan marked as active (*)]
```

Confirm via the GUI as a second check — **Settings → System → Power & Sleep** (or **Control Panel → Power Options** on Server) — both "Screen" and "Sleep" dropdowns must read **Never**, for both "On battery power" and "When plugged in" if the machine has a battery:

```
🖼️ [SCREENSHOT — Settings > System > Power & Sleep, both "Screen" and "Sleep" dropdowns set to "Never"]
```

## 2.2 Verify the settings actually took effect

```powershell
powercfg /query SCHEME_CURRENT SUB_SLEEP
```
Look for `Current AC Power Setting Index` and `Current DC Power Setting Index` under **Sleep after** / **Hibernate after** / **Allow hybrid sleep** — all should read `0x00000000` (never).

```
🖼️ [SCREENSHOT — terminal output of `powercfg /query SCHEME_CURRENT SUB_SLEEP` with all timeouts at 0x00000000]
```

## 2.3 Laptop-specific settings (skip this section on a desktop/server tower)

If the install machine is a laptop, sleep can also be triggered by the lid or the power button — `powercfg` alone won't cover these:

1. **Control Panel → Power Options → "Choose what closing the lid does"**
   - Set **"When I close the lid"** to **Do nothing**, for both "On battery" and "Plugged in"
   - Set **"When I press the power button"** to **Do nothing** or **Shut down** (never **Sleep**/**Hibernate**) — agree with the customer which is safer for their site

```
🖼️ [SCREENSHOT — "System Settings" dialog, lid-close and power-button dropdowns all set away from Sleep/Hibernate]
```

2. **Disable USB selective suspend** (can otherwise power down a USB-connected device adapter mid-test):
   ```powershell
   powercfg /setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0
   powercfg /setdcvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0
   powercfg /setactive SCHEME_CURRENT
   ```
   Or via GUI: **Power Options → Change plan settings → Change advanced power settings → USB settings → USB selective suspend setting** → **Disabled** (both AC and battery).

```
🖼️ [SCREENSHOT — Advanced power settings dialog, "USB selective suspend setting" expanded showing Disabled]
```

## 2.4 Network adapter power management — this is the one most likely to actually cause the UDP loss

Windows can power down a network adapter to save energy independently of the sleep settings above — and if it powers down the adapter the registration packets arrive on, they're dropped exactly the same way a full sleep would drop them. **Check this on every NIC the device traffic can arrive on.**

1. Open **Device Manager → Network adapters**
2. Right-click the relevant adapter (the one on the device/hardware network, and the primary NIC if unsure which one) → **Properties**
3. Go to the **Power Management** tab
4. **Uncheck** "Allow the computer to turn off this device to save power"
5. Repeat for every network adapter on the machine — if there are multiple NICs and you're not sure which one carries device traffic, do this for all of them

```
🖼️ [SCREENSHOT — Network adapter Properties > Power Management tab, "Allow the computer to turn off this device to save power" UNCHECKED]
```

## 2.5 Windows Update automatic restarts

Windows Update can force a reboot mid-test-run regardless of the sleep settings above. Mitigate with one (or both) of:
- **Settings → Windows Update → Advanced options → Active hours**: set active hours to cover the site's actual working/test window so Windows avoids auto-restarting during it (this only delays, it does not fully prevent, a forced restart if updates have been pending too long)
- Agree a fixed maintenance window with the customer for applying updates manually, and disable/defer automatic restart via Group Policy if the site has a domain (`Configure Automatic Updates` / `No auto-restart with logged on users for scheduled automatic updates installations`) — this requires domain admin involvement; loop in the customer's IT if applicable

```
🖼️ [SCREENSHOT — Windows Update > Advanced options > Active hours, configured to span the site's test window]
```

## 2.6 Recommended (not strictly required): UPS / power-loss protection

Since test runs can span months, a brief power outage can be as damaging as a sleep setting mistake. If the customer doesn't already have one, recommend a UPS for the host machine sized for a graceful shutdown window, and confirm Windows is set to a safe action (e.g. hibernate-if-critical-battery is irrelevant since hibernate is off per §2.1 — for a desktop on UPS, confirm the UPS software is configured to trigger an orderly `shutdown` command, not left to just cut power, if the outage outlasts the battery).

## 2.7 IIS also needs its own settings — don't stop here

The steps above only cover the **operating system**. IIS has an entirely separate idle-timeout and recycling mechanism that can stop the application even while Windows stays fully awake. This is covered in **[§3.9 of the IIS Installation doc](03-iis-installation.md#39-prevent-iis-from-idling-out-the-application-pool-critical)** — complete it as part of [3. IIS Installation](03-iis-installation.md), immediately after creating the application pool.

## 2.8 Verification checklist for this section

- [ ] `powercfg /list` shows High Performance as the active scheme
- [ ] `powercfg /query SCHEME_CURRENT SUB_SLEEP` shows all timeouts at `0x00000000`
- [ ] Settings → Power & Sleep shows "Never" for Screen and Sleep (both power sources if applicable)
- [ ] (Laptop only) Lid-close and power-button actions do not sleep/hibernate; USB selective suspend disabled
- [ ] Every network adapter's Power Management tab has "Allow the computer to turn off this device" unchecked
- [ ] Windows Update active hours (or a maintenance-window agreement) is in place
- [ ] You've continued on to [§3.9](03-iis-installation.md#39-prevent-iis-from-idling-out-the-application-pool-critical) for the IIS-side settings — this section alone is not sufficient

---
← [1. Prerequisites](01-prerequisites.md) | Next: [3. IIS Installation →](03-iis-installation.md)
