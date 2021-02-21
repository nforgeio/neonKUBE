@echo off
REM Configures the environment variables required to build neonKUBE projects.
REM 
REM 	buildenv [ <source folder> ]
REM
REM Note that <source folder> defaults to the folder holding this
REM batch file.
REM
REM This must be [RUN AS ADMINISTRATOR].

REM Default NF_ROOT to the folder holding this batch file after stripping
REM off the trailing backslash.

set NF_ROOT=%~dp0 
set NF_ROOT=%NF_ROOT:~0,-2%

if not [%1]==[] set NF_ROOT=%1

if exist %NF_ROOT%\neonKUBE.sln goto goodPath
echo The [%NF_ROOT%\neonKUBE.sln] file does not exist.  Please pass the path
echo to the Neon solution folder.
goto done

:goodPath 

REM Set NF_REPOS to the parent directory holding the neonFORGE repositories.

pushd "%NF_ROOT%\.."
set NF_REPOS=%cd%
popd 

REM Configure the environment variables.

set NF_TOOLBIN=%NF_ROOT%\ToolBin
set NF_BUILD=%NF_ROOT%\Build
set NF_CACHE=%NF_ROOT%\Build-cache
set NF_SNIPPETS=%NF_ROOT%\Snippets
set NF_TEST=%NF_ROOT%\Test
set NF_TEMP=C:\Temp
set NF_CODEDOC=%NF_ROOT%\..\nforgeio.github.io
set NF_SAMPLES_CADENCE=%NF_ROOT%\..\cadence-samples
set NEON_SKIPSLOWTESTS=1
set DOTNETPATH=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
set WINSDKPATH=C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6 Tools\x64

REM Persist the environment variables.

setx NF_REPOS "%NF_REPOS%" /M
setx NF_ROOT "%NF_ROOT%" /M
setx NF_TOOLBIN "%NF_TOOLBIN%" /M
setx NF_BUILD "%NF_BUILD%" /M
setx NF_CACHE "%NF_CACHE%" /M
setx NF_SNIPPETS "%NF_SNIPPETS%" /M
setx NF_TEST "%NF_TEST%" /M
setx NF_TEMP "%NF_TEMP%" /M
setx NF_CODEDOC "%NF_CODEDOC%" /M
setx NF_SAMPLES_CADENCE "%NF_SAMPLES_CADENCE%" /M
setx NEON_SKIPSLOWTESTS %NEON_SKIPSLOWTESTS% /M
setx DOTNET_CLI_TELEMETRY_OPTOUT 1 /M

setx DOTNETPATH "%DOTNETPATH%" /M
setx DEV_WORKSTATION 1 /M
setx OPENSSL_CONF "%NF_ROOT%\External\OpenSSL\openssl.cnf" /M

REM Make sure required folders exist.

if not exist "%NF_TEMP%" mkdir "%NF_TEMP%"
if not exist "%NF_TOOLBIN%" mkdir "%NF_TOOLBIN%"
if not exist "%NF_BUILD%" mkdir "%NF_BUILD%"

REM Configure the PATH.
REM
REM Note that some tools like PuTTY and 7-Zip may be installed as
REM x86 or x64 to different directories.  We'll include commands that
REM attempt to add both locations to the path and [pathtool] is
REM smart enough to only add directories that actually exist.

%NF_TOOLBIN%\pathtool -dedup -system -add "%NF_BUILD%"
%NF_TOOLBIN%\pathtool -dedup -system -add "%NF_TOOLBIN%"
%NF_TOOLBIN%\pathtool -dedup -system -add "%NF_ROOT%\External\OpenSSL"
%NF_TOOLBIN%\pathtool -dedup -system -add "%DOTNETPATH%"
%NF_TOOLBIN%\pathtool -dedup -system -add "C:\cygwin64\bin"
%NF_TOOLBIN%\pathtool -dedup -system -add "%ProgramFiles%\7-Zip"
%NF_TOOLBIN%\pathtool -dedup -system -add "%ProgramFiles(x86)%\7-Zip"
%NF_TOOLBIN%\pathtool -dedup -system -add "%ProgramFiles%\PuTTY"
%NF_TOOLBIN%\pathtool -dedup -system -add "%ProgramFiles(x86)%\PuTTY"
%NF_TOOLBIN%\pathtool -dedup -system -add "%ProgramFiles%\WinSCP"
%NF_TOOLBIN%\pathtool -dedup -system -add "%ProgramFiles(x86)%\WinSCP"
%NF_TOOLBIN%\pathtool -dedup -system -add "C:\Go"

REM Configure the neonKUBE program folder and add it to the PATH.

if not exist "%ProgramFiles%\neonKUBE" mkdir "%ProgramFiles%\neonKUBE"
%NF_TOOLBIN%\pathtool -dedup -system -add "%ProgramFiles%\neonKUBE"

REM Remove obsolete paths if they exist.

%NF_TOOLBIN%\pathtool --dedup -del "%NF_TOOLBIN%\OpenSSL"

REM Configure the neonKUBE kubeconfig path (as a USER environment variable).

set KUBECONFIG=%USERPROFILE%\.kube\admin.conf
reg add HKCU\Environment /v KUBECONFIG /t REG_EXPAND_SZ /d %USERPROFILE%\.kube\config /f 

:done
@echo "============================================================================================"
@echo "* Be sure to close and reopen Visual Studio and any command windows to pick up the changes *"
@echo "============================================================================================"
pause
