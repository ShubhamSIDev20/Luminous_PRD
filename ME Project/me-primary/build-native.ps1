# Host build for the ME Primary Board protocol unit tests.
#
# Builds and runs the PURE protocol modules (proto/, util/) against the test
# suite using the native MinGW compiler. The socket, thread and netinfo layers
# are Linux-specific and are deliberately NOT built here - they are exercised
# only on the board.
#
# A passing run here proves the byte layout and CRC are correct. It proves
# nothing about aarch64 codegen, Torizon, Docker or the network path.

param(
    [string]$OutDir = "$PSScriptRoot\bin",
    [switch]$NoRun
)

$ErrorActionPreference = "Stop"

$overridePath = Join-Path (Split-Path $PSScriptRoot -Parent) ".embedded-override.json"
$gccDir = $null
if (Test-Path $overridePath) {
    $override = Get-Content $overridePath -Raw | ConvertFrom-Json
    $gccDir = $override.toolchains.'x86_64-w64-mingw32'
}
if (-not $gccDir) {
    throw "x86_64-w64-mingw32 toolchain path not found in $overridePath"
}

$gcc = Join-Path $gccDir "gcc.exe"
if (-not (Test-Path $gcc)) {
    throw "Native compiler not found at $gcc"
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

# Host-portable sources only.
$sources = @(
    "$PSScriptRoot\src\proto\crc16.c"
    "$PSScriptRoot\src\proto\reg_frame.c"
    "$PSScriptRoot\src\proto\program_chain.c"
    "$PSScriptRoot\src\proto\frame_router.c"
    "$PSScriptRoot\src\proto\ack_frame.c"
    "$PSScriptRoot\src\proto\program_frame.c"
    "$PSScriptRoot\src\proto\control_frame.c"
    "$PSScriptRoot\src\proto\battery_frame.c"
    "$PSScriptRoot\src\proto\realtime_frame.c"
    "$PSScriptRoot\src\proto\step_decode.c"
    "$PSScriptRoot\src\proto\can_frame.c"
    "$PSScriptRoot\src\proto\rpmsg_frame.c"
    "$PSScriptRoot\src\exec\step_engine.c"
    "$PSScriptRoot\src\store\circuit_store.c"
    "$PSScriptRoot\src\store\circuit_registry.c"
    "$PSScriptRoot\src\msg.c"
    "$PSScriptRoot\src\util\log.c"
    "$PSScriptRoot\src\util\msgq.c"
    "$PSScriptRoot\src\util\channel_list.c"
    "$PSScriptRoot\tests\test_crc16.c"
    "$PSScriptRoot\tests\test_reg_frame.c"
    "$PSScriptRoot\tests\test_log.c"
    "$PSScriptRoot\tests\test_msgq.c"
    "$PSScriptRoot\tests\test_program_chain.c"
    "$PSScriptRoot\tests\test_frame_router.c"
    "$PSScriptRoot\tests\test_ack_frame.c"
    "$PSScriptRoot\tests\test_program_frame.c"
    "$PSScriptRoot\tests\test_control_frame.c"
    "$PSScriptRoot\tests\test_battery_frame.c"
    "$PSScriptRoot\tests\test_realtime_frame.c"
    "$PSScriptRoot\tests\test_step_decode.c"
    "$PSScriptRoot\tests\test_can_frame.c"
    "$PSScriptRoot\tests\test_step_engine.c"
    "$PSScriptRoot\tests\test_rpmsg_frame.c"
    "$PSScriptRoot\tests\test_circuit_store.c"
    "$PSScriptRoot\tests\test_circuit_registry.c"
    "$PSScriptRoot\tests\test_channel_list.c"
    "$PSScriptRoot\tests\test_main.c"
) | Where-Object { Test-Path $_ }

$out = Join-Path $OutDir "me_primary_tests.exe"

# -DME_PROGRAM_BUF_SIZE=65536 : the real 2 MB x 64 circuits is 128 MB of .bss.
# Linux only reserves that; Windows commits it. The host tests exercise the
# logic, not the capacity, so they run against a small buffer.
Write-Output "Compiling protocol unit tests (native x86_64) ..."
& $gcc -std=c11 -O1 -g -Wall -Wextra -Werror -pthread `
    -DME_PROGRAM_BUF_SIZE=65536 `
    -o $out @sources
if ($LASTEXITCODE -ne 0) { throw "Test build failed (exit $LASTEXITCODE)" }

Write-Output "Built: $out"

if (-not $NoRun) {
    Write-Output ""
    & $out
    if ($LASTEXITCODE -ne 0) { throw "UNIT TESTS FAILED (exit $LASTEXITCODE)" }
}
