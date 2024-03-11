@echo off
REM Configures the environment variables required to build NEONKUBE projects.
REM 
REM 	buildenv [ <source folder> ]
REM
REM Note that <source folder> defaults to the folder holding this
REM batch file.
REM
REM This must be [RUN AS ADMINISTRATOR].

echo ===========================================
echo * NEONKUBE Build Environment Configurator *
echo ===========================================

REM Default NK_ROOT to the folder holding this batch file after stripping
REM off the trailing backslash.

set NK_ROOT=%~dp0 
set NK_ROOT=%NK_ROOT:~0,-2%

if not [%1]==[] set NK_ROOT=%1

if exist %NK_ROOT%\neonKUBE.sln goto goodPath
echo The [%NK_ROOT%\neonKUBE.sln] file does not exist.  Please pass the path
echo to the NEONKUBE solution folder.
goto done

:goodPath 

REM Set NK_REPOS to the parent directory holding the NEONFORGE repositories.

pushd "%NK_ROOT%\.."
set NK_REPOS=%cd%
popd 

REM Configure the environment variables.

set NK_TOOLBIN=%NK_ROOT%\ToolBin
set NK_BUILD=%NK_ROOT%\Build
set NK_CACHE=%NK_ROOT%\Build-cache
set NK_SNIPPETS=%NK_ROOT%\Snippets
set NK_TEST=%NK_ROOT%\Test
set NK_TEMP=C:\Temp
set NK_ACTIONS_ROOT=%NF_REPOS%\neonCLOUD\Automation\actions
set NEON_CLUSTER_TESTING=1

REM Persist the environment variables.

setx NK_REPOS "%NK_REPOS%" /M                         > nul
setx NK_ROOT "%NK_ROOT%" /M                           > nul
setx NK_TOOLBIN "%NK_TOOLBIN%" /M                     > nul
setx NK_BUILD "%NK_BUILD%" /M                         > nul
setx NK_CACHE "%NK_CACHE%" /M                         > nul
setx NK_SNIPPETS "%NK_SNIPPETS%" /M                   > nul
setx NK_TEST "%NK_TEST%" /M                           > nul
setx NK_TEMP "%NK_TEMP%" /M                           > nul
setx NK_ACTIONS_ROOT "%NK_ACTIONS_ROOT%" /M           > nul
setx NEON_CLUSTER_TESTING "%NEON_CLUSTER_TESTING%" /M > nul

setx DOTNET_CLI_TELEMETRY_OPTOUT 1 /M                 > nul

REM Make sure required folders exist.

if not exist "%NK_TEMP%" mkdir "%NK_TEMP%"
if not exist "%NK_TOOLBIN%" mkdir "%NK_TOOLBIN%"
if not exist "%NK_BUILD%" mkdir "%NK_BUILD%"
if not exist "%NK_BUILD%\neon" mkdir "%NK_BUILD%\neon"

REM Configure the PATH.

pathtool -dedup -system -add "%NK_BUILD%"
pathtool -dedup -system -add "%NK_BUILD%\neon"
pathtool -dedup -system -add "%NK_TOOLBIN%"
pathtool -dedup -system -add "%NK_ROOT%\External\OpenSSL"

REM Configure the NEONKUBE program folder and add it to the PATH.

if not exist "%ProgramFiles%\neonKUBE" mkdir "%ProgramFiles%\neonKUBE"
pathtool -dedup -system -add "%ProgramFiles%\neonKUBE"

REM Remove obsolete paths if they exist.

pathtool --dedup -del "%NK_TOOLBIN%\OpenSSL"

REM Configure the NEONKUBE kubeconfig path (as a USER environment variable).

set KUBECONFIG=%USERPROFILE%\.kube\admin.conf
reg add HKCU\Environment /v KUBECONFIG /t REG_EXPAND_SZ /d %USERPROFILE%\.kube\config /f > /nul

REM Perform additional implementation via Powershell.

pwsh -f "%NK_ROOT%\buildenv.ps1"

:done
echo.
echo ============================================================================================
echo * Be sure to close and reopen Visual Studio and any command windows to pick up the changes *
echo ============================================================================================
