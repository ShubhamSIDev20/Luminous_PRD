# iMX8MP Deployment Guide

How to get `me_primary` running on a physical Verdin iMX8M Plus board, and
how to recover it when it isn't. Covers both the single-shot dev path
(`deploy.ps1`) and the persistent production path (`docker compose` on the
board) — they are **not the same deployment**, see §2.

Companion docs: `../ME_Primary_Comm_Block_Diagram.md` (protocol),
`2026-08-19-imx8mp-core-isolation-provisioning.md` (one-time CPU3 kernel
provisioning — do this first on a new board).

## 0. Before touching anything: re-verify the board IP

Board IPs on this network are DHCP-assigned and **move between sessions**.
Do not trust an IP from memory or a prior session's notes — confirm with the
developer or by checking board-side network config first. As of 2026-08-21
the test board is `172.16.15.19`; the Web Application server is
`172.16.18.164`. Both can change.

## 1. SSH access

Key-based auth is preferred. If the local machine's key isn't in the board's
`authorized_keys` (common after a board re-flash or a new dev machine), you'll
see:

```
Permission denied (publickey,password).
```

Ask the developer for the `torizon` user's password rather than guessing or
retrying. This is a lab/test board on an internal network — password auth is
acceptable here but should never be treated as a pattern for anything
customer-facing.

**On Windows, without WSL/OpenSSH-agent set up:** `sshpass` is typically not
installed, but PuTTY's `plink` usually is. Non-interactive password auth
with a pinned host key (avoids an interactive host-key prompt hanging a
scripted call):

```powershell
# First connection: capture the fingerprint plink/ssh reports, then pin it
plink -batch -hostkey "SHA256:<fingerprint-from-first-connect-attempt>" -pw <password> torizon@<board-ip> "echo ok"
```

Get the fingerprint safely (no prompt, no key material printed) via:

```bash
ssh -o BatchMode=yes -v torizon@<board-ip> exit 2>&1 | grep -i "key fingerprint"
```

or read it directly off the "host key is not cached" message plink prints on
first contact.

## 2. Two deployment paths — know which one you're using

| | `deploy.ps1` (repo root: `me-primary/`) | `docker compose` (on-board) |
|---|---|---|
| Purpose | Fast dev iteration, single run, watch it live | Persistent service, survives reboot/crash |
| Container lifecycle | Ephemeral (`docker run --rm -i`), dies on Ctrl-C | `restart: unless-stopped` |
| Image | `debian:bookworm-slim` (standing decision — kept as GHCR `:latest` debug image until developer says otherwise) | `ghcr.io/quench-ev-charger/me-primary:latest` |
| Binary delivery | `scp`'d fresh from `bin\me_primary` every run | Bind-mounted from a directory on the board (`./me_primary`) — update by copying a new binary over it and restarting the container, not by re-running compose |
| CLI args | `--port --iface --channel <n>` (singular, one channel) | `--core --channels 1,2,3,4` (plural, multi-channel) |
| Where it runs from | Your Windows machine, requires network path to the board | On the board itself over SSH |

**These two are not interchangeable and their CLI flags have drifted apart**
(`deploy.ps1` predates the multi-channel/core-isolation work). Don't assume
`deploy.ps1`'s exact arguments reflect what production runs — check the
board's actual `docker-compose.yml` (§3) for the current source of truth.

For quick "does my new build even boot" checks on real hardware, `deploy.ps1`
is still the right tool — see the main `CLAUDE.md` `## Commands` section.
For anything that needs to survive a board reboot or run unattended, use §3.

## 3. Production deployment (docker compose on the board)

The compose project lives on the board, not just in the repo — e.g.
`/home/torizon/ME-Primary/docker-compose.yml`. Confirm the actual path per
board; it is not guaranteed to match the repo's `me-primary/docker-compose.yml`
verbatim (container name, mounted binary path, and `--server` IP are
board-specific).

```bash
ssh torizon@<board-ip>
cat /home/torizon/ME-Primary/docker-compose.yml   # confirm --server IP, image, mount path
```

**Container naming is per-board-role, not a typo.** A board provisioned as
"secondary 1" in the fleet may run `container_name: me-secondary_1` even
though it's the same `me-primary` image/binary — the name reflects the
board's registration role (`--secondary N`), not the source repo name.

### Deploying a new binary build

```bash
scp bin/me_primary torizon@<board-ip>:/home/torizon/ME-Primary/me_primary
ssh torizon@<board-ip> "cd /home/torizon/ME-Primary && docker compose up -d --force-recreate"
```

### Checking current status

```bash
ssh torizon@<board-ip> "docker ps -a --format '{{.Names}}\t{{.Image}}\t{{.Status}}'"
```

Anything not `Up ...` needs investigation before assuming it's serving
traffic — `restart: unless-stopped` does **not** guarantee the container is
actually running (see §4).

### Confirming registration succeeded

```bash
ssh torizon@<board-ip> "docker logs --tail 40 <container-name>"
```

Look for one `DEVICE REGISTERED` block per channel, each showing the
expected circuit number (`0x11`–`0x14` for secondary 1, channels 1–4) and
`Server response : 0x01 (Registered)`. The app then settles into
`state: IDLE - registered, holding connection, watching TCP, UDP 10000/10001`.

## 4. Known failure mode: RPMsg device race on boot

**Symptom:** container shows `Exited (128)` and never recovers on its own,
even though `restart: unless-stopped` is set.

**Cause:** `docker-compose.yml` passes through a hardware device
(`/dev/ttyRPMSG30`), which is created by the M7 core loader
(`m7-loader` container) — not present at boot until the M7 firmware finishes
initializing. If the `me-primary`/`me-secondary_N` container's compose-managed
start is attempted before that device exists, Docker fails at **container
create time** with:

```
error gathering device information while adding custom device "/dev/ttyRPMSG30": no such file or directory
```

This is a *create*-time failure, not a *runtime crash* — Docker's restart
policy retries a few times on a fixed schedule and then gives up, leaving the
container `Exited` indefinitely rather than crash-looping visibly. There is
currently no automated recovery; a board reboot with no one to SSH in and
manually restart it will stay down until someone notices.

### Diagnosis

```bash
ssh torizon@<board-ip> "docker inspect <container-name> --format '{{.State.ExitCode}} {{.State.Error}}'"
ssh torizon@<board-ip> "ls -la /dev/ttyRPMSG*"
ssh torizon@<board-ip> "docker logs --tail 20 m7-loader"
```

Confirm `m7-loader` logs end with `M7 core started successfully!` and the
RPMsg tty device now exists.

### Recovery

Once the device exists, just bring the compose service back up:

```bash
ssh torizon@<board-ip> "cd /home/torizon/ME-Primary && docker compose up -d"
```

Then re-verify per §3 ("Confirming registration succeeded").

### Open item — not yet fixed

This is a startup-ordering gap, not a one-off fluke: nothing currently gates
the `me-*` compose service on `m7-loader` actually finishing. A durable fix
would be a compose `depends_on` + healthcheck on `m7-loader` (or a
`/dev/ttyRPMSG30`-exists wait loop before `docker compose up`), so an
unattended reboot self-heals instead of requiring manual SSH intervention.
Flag this to the developer before relying on unattended reboots in the field.

## 5. Verification checklist

- [ ] Board IP and server IP re-confirmed for this session (§0)
- [ ] `docker ps -a` shows the target container `Up`, not `Exited`/`Restarting`
- [ ] `/dev/ttyRPMSG30` (or whatever device the compose file names) exists
- [ ] Logs show one `DEVICE REGISTERED` per expected channel
- [ ] App reached `state: IDLE` (or later) — not stuck retrying registration
- [ ] If core isolation matters for this test: CPU3 pinning verified per
      `2026-08-19-imx8mp-core-isolation-provisioning.md` §6
