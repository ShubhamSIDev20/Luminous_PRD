# Cross-build the ME Primary Board application for the Verdin iMX8M Plus.
#
# Produces a STATIC aarch64 ELF so the binary is decoupled from whatever glibc
# ships in the Docker image on the board (see project ADR on static linking).

param(
    [string]$OutDir = "$PSScriptRoot\bin"
)

$ErrorActionPreference = "Stop"

$overridePath = Join-Path (Split-Path $PSScriptRoot -Parent) ".embedded-override.json"
$toolchainBin = $null
if (Test-Path $overridePath) {
    $override = Get-Content $overridePath -Raw | ConvertFrom-Json
    $toolchainBin = $override.toolchains.'aarch64-linux-gnu'
}
if (-not $toolchainBin) {
    throw "aarch64-linux-gnu toolchain path not found in $overridePath"
}

$gcc = Join-Path $toolchainBin "aarch64-none-linux-gnu-gcc.exe"
if (-not (Test-Path $gcc)) {
    throw "Cross-compiler not found at $gcc"
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$sources = @(
    "$PSScriptRoot\src\main.c"
    "$PSScriptRoot\src\sys_init.c"
    "$PSScriptRoot\src\app_queues.c"
    "$PSScriptRoot\src\msg.c"
    "$PSScriptRoot\src\threads\comm_thread.c"
    "$PSScriptRoot\src\threads\core_logic.c"
    "$PSScriptRoot\src\threads\data_mgr.c"
    "$PSScriptRoot\src\threads\can_mgr.c"
    "$PSScriptRoot\src\threads\post_reg.c"
    "$PSScriptRoot\src\store\circuit_store.c"
    "$PSScriptRoot\src\store\circuit_registry.c"
    "$PSScriptRoot\src\proto\crc16.c"
    "$PSScriptRoot\src\proto\reg_frame.c"
    "$PSScriptRoot\src\proto\program_chain.c"
    "$PSScriptRoot\src\proto\frame_router.c"
    "$PSScriptRoot\src\proto\ack_frame.c"
    "$PSScriptRoot\src\proto\program_frame.c"
    "$PSScriptRoot\src\proto\control_frame.c"
    "$PSScriptRoot\src\proto\battery_frame.c"
    "$PSScriptRoot\src\proto\realtime_frame.c"
    "$PSScriptRoot\src\proto\can_frame.c"
    "$PSScriptRoot\src\proto\rpmsg_frame.c"
    "$PSScriptRoot\src\proto\step_decode.c"
    "$PSScriptRoot\src\exec\step_engine.c"
    "$PSScriptRoot\src\net\tcp_client.c"
    "$PSScriptRoot\src\net\udp_sock.c"
    "$PSScriptRoot\src\platform\netinfo.c"
    "$PSScriptRoot\src\platform\rpmsg_link.c"
    "$PSScriptRoot\src\util\log.c"
    "$PSScriptRoot\src\util\msgq.c"
    "$PSScriptRoot\src\util\channel_list.c"
)

foreach ($s in $sources) {
    if (-not (Test-Path $s)) { throw "Missing source file: $s" }
}

$out = Join-Path $OutDir "me_primary"

Write-Output "Compiling ME Primary for aarch64 (static) ..."
# gnu11, not c11: struct ifreq / IFNAMSIZ / IFF_UP live behind _DEFAULT_SOURCE,
# which strict ISO mode switches off.
& $gcc -std=gnu11 -O2 -Wall -Wextra -Werror `
    -I"$PSScriptRoot\src" `
    -static -pthread `
    -o $out @sources
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)" }

$readelf = Join-Path $toolchainBin "aarch64-none-linux-gnu-readelf.exe"
if (Test-Path $readelf) {
    Write-Output "--- ELF header ---"
    & $readelf -h $out | Select-String "Machine|Class|Type"
}

Write-Output "Built: $out"
