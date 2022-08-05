@echo off
REM Configures the environment variables required to build neonKUBE projects.
REM 
REM 	buildenv [ <source folder> ]
REM
REM Note that <source folder> defaults to the folder holding this
REM batch file.
REM
REM This must be [RUN AS ADMINISTRATOR].

echo ===========================================
echo * neonKUBE Build Environment Configurator *
echo ===========================================

REM Default NK_ROOT to the folder holding this batch file after stripping
REM off the trailing backslash.

set NK_ROOT=%~dp0 
set NK_ROOT=%NK_ROOT:~0,-2%

if not [%1]==[] set NK_ROOT=%1

if exist %NK_ROOT%\neonKUBE.sln goto goodPath
echo The [%NK_ROOT%\neonKUBE.sln] file does not exist.  Please pass the path
echo to the neonKUBE solution folder.
goto done

:goodPath 

REM Set NK_REPOS to the parent directory holding the neonFORGE repositories.

pushd "%NK_ROOT%\.."
set NK_REPOS=%cd%
popd 

REM Some scripts need to know the developer's GitHub username:

echo.
set /p NEON_GITHUB_USER="Enter your GitHub username: "

echo.
echo Configuring...
echo.

REM Configure the environment variables.

set NK_TOOLBIN=%NK_ROOT%\ToolBin
set NK_BUILD=%NK_ROOT%\Build
set NK_CACHE=%NK_ROOT%\Build-cache
set NK_SNIPPETS=%NK_ROOT%\Snippets
set NK_TEST=%NK_ROOT%\Test
set NK_TEMP=C:\Temp
set NK_ACTIONS_ROOT=%NC_REPOS%\neonCLOUD\Automation\actions
set NK_CODEDOC=%NK_ROOT%\..\nforgeio.github.io
set NK_SAMPLES_CADENCE=%NK_ROOT%\..\cadence-samples
set DOTNETPATH=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
set MSBUILDPATH=C:\Program Files\Microsoft Visual Studio\2022\Community\Msbuild\Current\Bin\MSBuild.exe
set NEON_CLUSTER_TESTING=1

REM Persist the environment variables.

setx NEON_GITHUB_USER "%NEON_GITHUB_USER%" /M                 > nul
setx NK_REPOS "%NK_REPOS%" /M                                 > nul
setx NK_ROOT "%NK_ROOT%" /M                                   > nul
setx NK_TOOLBIN "%NK_TOOLBIN%" /M                             > nul
setx NK_BUILD "%NK_BUILD%" /M                                 > nul
setx NK_CACHE "%NK_CACHE%" /M                                 > nul
setx NK_SNIPPETS "%NK_SNIPPETS%" /M                           > nul
setx NK_TEST "%NK_TEST%" /M                                   > nul
setx NK_TEMP "%NK_TEMP%" /M                                   > nul
setx NK_ACTIONS_ROOT "%NK_ACTIONS_ROOT%" /M                   > nul
setx NK_CODEDOC "%NK_CODEDOC%" /M                             > nul
setx NK_SAMPLES_CADENCE "%NK_SAMPLES_CADENCE%" /M             > nul
setx NEON_CLUSTER_TESTING "%NEON_CLUSTER_TESTING%" /M         > nul

setx DOTNETPATH "%DOTNETPATH%" /M                             > nul
setx MSBUILDPATH "%MSBUILDPATH%" /M                           > nul
setx DOTNET_CLI_TELEMETRY_OPTOUT 1 /M                         > nul
setx DEV_WORKSTATION 1 /M                                     > nul
setx OPENSSL_CONF "%NK_ROOT%\External\OpenSSL\openssl.cnf" /M > nul

REM Make sure required folders exist.

if not exist "%NK_TEMP%" mkdir "%NK_TEMP%"
if not exist "%NK_TOOLBIN%" mkdir "%NK_TOOLBIN%"
if not exist "%NK_BUILD%" mkdir "%NK_BUILD%"
if not exist "%NK_BUILD%\neon" mkdir "%NK_BUILD%\neon"

REM Configure the PATH.
REM
REM Note that some tools like PuTTY and 7-Zip may be installed as
REM x86 or x64 to different directories.  We'll include commands that
REM attempt to add both locations to the path and [pathtool] is
REM smart enough to only add directories that actually exist.

%NK_TOOLBIN%\pathtool -dedup -system -add "%NK_BUILD%"
%NK_TOOLBIN%\pathtool -dedup -system -add "%NK_BUILD%\neon"
%NK_TOOLBIN%\pathtool -dedup -system -add "%NK_TOOLBIN%"
%NK_TOOLBIN%\pathtool -dedup -system -add "%NK_ROOT%\External\OpenSSL"
%NK_TOOLBIN%\pathtool -dedup -system -add "%DOTNETPATH%"
%NK_TOOLBIN%\pathtool -dedup -system -add "C:\cygwin64\bin"
%NK_TOOLBIN%\pathtool -dedup -system -add "%ProgramFiles%\7-Zip"
%NK_TOOLBIN%\pathtool -dedup -system -add "%ProgramFiles(x86)%\7-Zip"
%NK_TOOLBIN%\pathtool -dedup -system -add "%ProgramFiles%\PuTTY"
%NK_TOOLBIN%\pathtool -dedup -system -add "%ProgramFiles(x86)%\PuTTY"
%NK_TOOLBIN%\pathtool -dedup -system -add "%ProgramFiles%\WinSCP"
%NK_TOOLBIN%\pathtool -dedup -system -add "%ProgramFiles(x86)%\WinSCP"
%NK_TOOLBIN%\pathtool -dedup -system -add "C:\Go"
%NK_TOOLBIN%\pathtool -dedup -system -add "C:\Program Files (x86)\HTML Help Workshop"

REM Configure the neonKUBE program folder and add it to the PATH.

if not exist "%ProgramFiles%\neonKUBE" mkdir "%ProgramFiles%\neonKUBE"
%NK_TOOLBIN%\pathtool -dedup -system -add "%ProgramFiles%\neonKUBE"

REM Remove obsolete paths if they exist.

%NK_TOOLBIN%\pathtool --dedup -del "%NK_TOOLBIN%\OpenSSL"

REM Configure the neonKUBE kubeconfig path (as a USER environment variable).

set KUBECONFIG=%USERPROFILE%\.kube\admin.conf
reg add HKCU\Environment /v KUBECONFIG /t REG_EXPAND_SZ /d %USERPROFILE%\.kube\config /f > /nul

REM Install the KubeOps project templates: https://buehler.github.io/dotnet-operator-sdk/docs/templates.html

dotnet new --install KubeOps.Templates::* > /nul

REM Perform additional implementation in via Powershell.

pwsh -File "%NK_ROOT%\buildenv.ps1"

:done
echo.
echo ============================================================================================
echo * Be sure to close and reopen Visual Studio and any command windows to pick up the changes *
echo ============================================================================================
pause
