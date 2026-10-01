# iMX8MP Core Isolation — Board Provisioning Runbook

One-time, per physical Verdin iMX8M Plus board. Kernel boot configuration
cannot live in a Dockerfile, Compose file, or CI — it must be applied
directly on the board over SSH. Survives OTA updates because `fw_setenv`
writes to the U-Boot environment partition, not the OSTree-managed rootfs.

Companion design doc: `../specs/2026-08-19-core-isolation-cicd-design.md`.

## 1. Check current boot arguments

```bash
ssh torizon@<board-ip>
cat /proc/cmdline
sudo fw_printenv bootargs
```

## 2. Append isolation parameters

```bash
sudo fw_setenv bootargs "$(sudo fw_printenv -n bootargs) isolcpus=3 nohz_full=3 rcu_nocbs=3 irqaffinity=0,1,2"
```

| Flag | Meaning |
|---|---|
| `isolcpus=3` | Scheduler never auto-assigns any thread to CPU3 |
| `nohz_full=3` | Disables the periodic timer-tick interrupt on CPU3 |
| `rcu_nocbs=3` | Kernel RCU callbacks run elsewhere, not on CPU3 |
| `irqaffinity=0,1,2` | Hardware interrupts default to CPU0-2, stay off CPU3 |

## 3. Reboot and verify

```bash
sudo reboot
```

After reboot:

```bash
cat /proc/cmdline | grep isolcpus
cat /sys/devices/system/cpu/isolated        # expect: 3
cat /sys/devices/system/cpu/nohz_full        # expect: 3
```

## 4. Keep late-registering IRQs off CPU3

Some drivers set their own IRQ affinity after boot, overriding the
`irqaffinity` default.

```bash
sudo tee /usr/local/bin/redirect_irqs.sh > /dev/null <<'EOF'
#!/bin/bash
for irq_path in /proc/irq/*/smp_affinity_list; do
    echo "0-2" > "$irq_path" 2>/dev/null
done
echo "[INFO] All IRQs redirected away from CPU3"
EOF
sudo chmod +x /usr/local/bin/redirect_irqs.sh
sudo /usr/local/bin/redirect_irqs.sh
```

Make it persistent:

```bash
sudo tee /etc/systemd/system/redirect-irqs.service > /dev/null <<'EOF'
[Unit]
Description=Redirect IRQs away from isolated CPU3
After=multi-user.target

[Service]
Type=oneshot
ExecStart=/usr/local/bin/redirect_irqs.sh

[Install]
WantedBy=multi-user.target
EOF
sudo systemctl daemon-reload
sudo systemctl enable redirect-irqs.service
sudo systemctl start redirect-irqs.service
```

## 5. Log in to the private image registry

The `me-primary` image is private (GHCR, inherits the repo's visibility).
One-time login with a PAT that has `read:packages`:

```bash
docker login ghcr.io -u <github-username>
```

## 6. Confirm after deploying `me_primary`

Once `docker compose up -d` is running the new image (see
`me-primary/docker-compose.yml`):

```bash
docker logs me-primary | grep -E "running on CPU|NOT isolated"
```

Expect a line like `core logic thread: running on CPU3` and the absence of
the `NOT isolated` warning. If the warning appears, re-check step 3.

The image is `scratch`-based (no shell), so `docker exec -it ... bash` for
`taskset`/`ps` does not work anymore - run those from the **host** instead,
which is the correct place for them regardless (they inspect the process as
the host scheduler sees it):

```bash
# PID of the container's process, as the host sees it
docker inspect -f '{{.State.Pid}}' me-primary

# Thread IDs inside that process (host-side ps, not docker exec)
ps -eLf | grep <PID-from-above>

# Confirm the Core Logic thread's TID is pinned to CPU3 only (expect mask "8")
taskset -p <TID-of-core-logic-thread>

# Confirm CPU3 stays otherwise idle - only Core Logic's own work should show
mpstat -P ALL 1
```
