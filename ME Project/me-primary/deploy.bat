```bat
@echo off
setlocal

REM ============================================================
REM deploy.bat
REM
REM Usage:
REM
REM   deploy.bat <TORIZON_IP> <SERVER_IP>
REM       -> Execute existing binary only
REM
REM   deploy.bat <TORIZON_IP> <SERVER_IP> copy
REM       -> Copy binary to Torizon
REM       -> Copy binary to Docker
REM       -> Set executable permission
REM       -> Execute application
REM
REM Examples:
REM
REM   deploy.bat 172.16.14.206 172.16.15.230
REM   deploy.bat 172.16.14.206 172.16.15.230 copy
REM ============================================================


REM ============================================================
REM Check Torizon IP
REM ============================================================

if "%~1"=="" (
    echo.
    echo ERROR: Torizon IP is missing.
    echo.
    echo Usage:
    echo   %~nx0 ^<TORIZON_IP^> ^<SERVER_IP^>
    echo   %~nx0 ^<TORIZON_IP^> ^<SERVER_IP^> copy
    echo.
    pause
    exit /b 1
)


REM ============================================================
REM Check Server IP
REM ============================================================

if "%~2"=="" (
    echo.
    echo ERROR: Server IP is missing.
    echo.
    echo Usage:
    echo   %~nx0 ^<TORIZON_IP^> ^<SERVER_IP^>
    echo   %~nx0 ^<TORIZON_IP^> ^<SERVER_IP^> copy
    echo.
    pause
    exit /b 1
)


set "TORIZON_IP=%~1"
set "SERVER_IP=%~2"
set "COPY_FLAG=%~3"


REM ============================================================
REM Local and Remote Paths
REM ============================================================

set "LOCAL_PATH=C:\Users\NNEVREKAR\OneDrive - Ador Powertron Ltd\Documents\Projects\MicroME\ME_PRD\ME Project\me-primary\bin\me_primary"

set "REMOTE_PATH=/home/torizon/ME_BTS/me-primary/"


REM ============================================================
REM If "copy" argument is provided
REM ============================================================

if /I "%COPY_FLAG%"=="copy" goto COPY_FILES

goto EXECUTE_APPLICATION


REM ============================================================
REM Step 1: Copy Binary from PC to Torizon
REM ============================================================

:COPY_FILES

echo.
echo ============================================================
echo Step 1: Copying me_primary to Torizon
echo Torizon IP : %TORIZON_IP%
echo ============================================================
echo.

pscp "%LOCAL_PATH%" torizon@%TORIZON_IP%:%REMOTE_PATH%

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: PSCP file copy failed!
    pause
    exit /b 1
)

echo.
echo Binary copied to Torizon successfully.


REM ============================================================
REM Step 2: Copy Binary from Torizon to Docker
REM ============================================================

echo.
echo ============================================================
echo Step 2: Copying me-primary into qflex-backend
echo ============================================================
echo.

plink -ssh torizon@%TORIZON_IP% "docker cp /var/rootdirs/home/torizon/ME_BTS/me-primary/ qflex-backend:/app/"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: docker cp failed!
    pause
    exit /b 1
)

echo.
echo Binary copied into Docker successfully.


REM ============================================================
REM Step 3: Set executable permission
REM ============================================================

echo.
echo ============================================================
echo Step 3: Setting executable permission
echo ============================================================
echo.

REM plink -ssh -t torizon@%TORIZON_IP% "docker exec qflex-backend /bin/bash -c 'chmod +x /app/me-primary/me_primary'"
SSH torizon@%TORIZON_IP% "docker exec qflex-backend /bin/bash -c 'chmod +x /app/me-primary/me_primary'"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: Unable to set executable permission!
    pause
    exit /b 1
)

echo.
echo Executable permission set successfully.


REM ============================================================
REM Step 4: Execute Application
REM ============================================================

:EXECUTE_APPLICATION

echo.
echo ============================================================
echo Executing me_primary
echo Torizon IP : %TORIZON_IP%
echo Server IP  : %SERVER_IP%
if /I "%COPY_FLAG%"=="copy" (
    echo Mode       : COPY + EXECUTE
) else (
    echo Mode       : EXECUTE ONLY
)
echo ============================================================
echo.


REM plink -ssh -t torizon@%TORIZON_IP% "docker exec -it qflex-backend /bin/bash -c './me-primary/me_primary --server %SERVER_IP% --verbose'"
SSH -t torizon@%TORIZON_IP% "docker exec -it qflex-backend /bin/bash -c './me-primary/me_primary --server %SERVER_IP% --verbose'"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: me_primary execution failed!
    pause
    exit /b 1
)


echo.
echo ============================================================
echo Completed successfully.
echo ============================================================

pause
```
