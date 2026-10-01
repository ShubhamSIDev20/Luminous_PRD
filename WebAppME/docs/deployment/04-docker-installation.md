# 4. Docker Installation (Alternative Path)

← [3. IIS Installation](03-iis-installation.md) | Next: [5. Application Deployment →](05-application-deployment.md)

---

> ⚠️ **The current release does not ship a `Dockerfile` or `docker-compose.yml`.** Only follow this path if engineering has provided a container image/Dockerfile for this specific release, and the customer's IT explicitly wants containers over IIS. Otherwise use [3. IIS Installation](03-iis-installation.md) — it's the default and matches how the app is actually built.

> Also complete [2. Power & Uptime Settings](02-power-and-uptime-settings.md) for the Docker **host** machine — sleep/hibernate on the host drops the container's network stack exactly like it would for IIS. Docker itself has no equivalent of IIS's idle-timeout/recycling ([§3.9](03-iis-installation.md#39-prevent-iis-from-idling-out-the-application-pool-critical)) since `--restart unless-stopped` (used below) keeps the container running regardless of traffic — but the host OS sleep settings still fully apply.

## 4.1 Install Docker

**Windows Server / Windows 10-11:**
1. Enable WSL2 (Windows 10/11 only): `wsl --install` (elevated PowerShell), reboot
2. Install Docker Desktop from `https://www.docker.com/products/docker-desktop/`
3. Verify:
   ```powershell
   docker --version
   docker run hello-world
   ```

```
🖼️ [SCREENSHOT — Docker Desktop installer's final "Installation succeeded" screen]
```

```
🖼️ [SCREENSHOT — terminal output of `docker run hello-world` showing the success message]
```

**Linux server:**
```bash
curl -fsSL https://get.docker.com | sh
sudo systemctl enable --now docker
docker --version
```

## 4.2 Prepare persistent storage — keep it OFF the app's container path

Per the folder-layout rule in [§1.5](01-prerequisites.md#folder-layout-decision-read-this-first), the database and exported files must live on a bind mount to a host folder that is **independent of the container/image** — never inside the container's writable layer, or all data is lost the moment the container is recreated (which happens on every update).

```bash
mkdir -p /opt/bts/data
mkdir -p /opt/bts/backups
```
(Windows Docker Desktop equivalent: create `D:\BTS\Data` and `D:\BTS\Backups` on the host as in the IIS path, then mount them below.)

## 4.3 Build / load the image

If handed a Dockerfile:
```bash
docker build -t batterytestingsystem:latest .
```
If handed a pre-built image tarball:
```bash
docker load -i batterytestingsystem-image.tar
```

## 4.4 Run the container

```bash
docker run -d \
  --name batterytestingsystem \
  --restart unless-stopped \
  -p 80:8080 \
  -v /opt/bts/data:/app/data \
  -e ConnectionStrings__DefaultConnection="Data Source=/app/data/BtsAppdb.db" \
  -e AppSettings__Data="/app/data" \
  -e ASPNETCORE_ENVIRONMENT="Production" \
  batterytestingsystem:latest
```

- Adjust `-p 80:8080` to whatever host port the customer wants exposed, and confirm the container's actual internal listen port with engineering before go-live (`8080` here is a placeholder).
- The `-v /opt/bts/data:/app/data` mount is what keeps the database and files on host storage, independent of the container image — this is the Docker equivalent of the app/data separation used in the IIS path.
- `--restart unless-stopped` is what keeps the container (and any background UDP listener inside it) running across host reboots and crashes — don't drop this flag.

```
🖼️ [SCREENSHOT — `docker ps` output showing the container "Up" and the port mapping]
```

## 4.5 Verify

```bash
docker ps                              # container should show "Up"
docker logs -f batterytestingsystem    # tail startup logs, check for exceptions
curl http://localhost/                  # or open in a browser
```

```
🖼️ [SCREENSHOT — the app's login page loading successfully in a browser, pointed at the Docker host]
```

---
← [3. IIS Installation](03-iis-installation.md) | Next: [5. Application Deployment →](05-application-deployment.md) (Docker installs still need the config in [6. Configuration](06-configuration.md) and the DB steps in [7](07-database-setup.md)/[8](08-database-backup.md)/[9](09-database-restore.md) — those apply to both paths)
