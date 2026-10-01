# Deploy and run the ME Primary Board application on the Verdin iMX8M Plus.
#
# Copies the static aarch64 binary to the board over scp, then runs it inside a
# container on the board's existing Docker Engine.
#
# --network host is REQUIRED, not optional. The registration frame carries this
# board's IP and MAC as payload. On Docker's default bridge the program would
# read the container's virtual interface (172.17.x.x with a synthetic MAC) and
# register the board at an address the Web Application can never reach.

param(
    [Parameter(Mandatory = $true)][string]$BoardIP,
    [Parameter(Mandatory = $true)][string]$ServerIP,
    [string]$BoardUser   = "torizon",
    [string]$RemotePath  = "/home/torizon/me_primary",
    [string]$DockerImage = "debian:bookworm-slim",
    [int]$Port           = 9999,
    [string]$Iface       = "eth0",
    [int]$Device         = 1,
    [int]$Secondary      = 1,
    [int]$Channel        = 1,
    [string]$DeviceName  = "BTS-600",
    # Big-endian: CRC 0xADBE goes on the wire as "AD BE". Mirrors
    # ME_CRC_ORDER_DEFAULT in src/proto/crc16.h - change both together.
    [ValidateSet("le", "be")][string]$CrcOrder = "be",
    [int]$Timeout        = 5000,
    [switch]$DebugLog,
    [switch]$SkipCopy
)

$ErrorActionPreference = "Stop"

$localBin = Join-Path $PSScriptRoot "bin\me_primary"
if (-not (Test-Path $localBin)) {
    throw "Binary not found at $localBin - run build.ps1 first."
}

if (-not $SkipCopy) {
    Write-Output "Copying $localBin to ${BoardUser}@${BoardIP}:$RemotePath ..."
    scp $localBin "${BoardUser}@${BoardIP}:$RemotePath"
    if ($LASTEXITCODE -ne 0) { throw "scp failed (exit $LASTEXITCODE)" }
}

$appArgs = @(
    "--server", $ServerIP
    "--port", $Port
    "--iface", $Iface
    "--device", $Device
    "--secondary", $Secondary
    "--channel", $Channel
    "--name", "'$DeviceName'"
    "--crc-order", $CrcOrder
    "--timeout", $Timeout
)
if ($DebugLog) { $appArgs += "--verbose" }

$appArgLine = $appArgs -join " "

$remoteCmd = @(
    "chmod +x $RemotePath"
    "docker run --rm -i --network host -v ${RemotePath}:/me_primary:ro $DockerImage /me_primary $appArgLine"
) -join " && "

Write-Output ""
Write-Output "Running on the board (--network host, image $DockerImage) ..."
Write-Output "  target Web Application: ${ServerIP}:${Port}"
Write-Output "  press Ctrl-C to stop"
Write-Output ""

ssh -t "${BoardUser}@${BoardIP}" $remoteCmd
$rc = $LASTEXITCODE

Write-Output ""
if ($rc -eq 0) {
    Write-Output "Application exited cleanly (device was registered)."
} else {
    Write-Output "Application exited with code $rc (device was NOT registered)."
    Write-Output "See README.md 'Troubleshooting' for what to check next."
}
exit $rc
