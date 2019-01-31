@echo on
REM Configures the environment variables required to build neonFORGE projects.
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

if exist %NF_ROOT%\neonFORGE.sln goto goodPath
echo The [%NF_ROOT%\neonFORGE.sln] file does not exist.  Please pass the path
echo to the Neon solution folder.
goto done

:goodPath

REM Configure the environment variables.

set NF_TOOLBIN=%NF_ROOT%\ToolBin
set NF_BUILD=%NF_ROOT%\Build
set NF_TEMP=C:\Temp
set DOTNETPATH=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
set WINSDKPATH=C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6 Tools\x64

REM Persist the environment variables.

setx NF_ROOT "%NF_ROOT%" /M
setx NF_TOOLBIN "%NF_TOOLBIN%" /M
setx NF_BUILD "%NF_BUILD%" /M
setx NF_TEMP "%NF_TEMP%" /M

setx DOTNETPATH "%DOTNETPATH%" /M
setx DEV_WORKSTATION 1 /M
setx OPENSSL_CONF "%NF_TOOLBIN%\OpenSSL\openssl.cnf" /M

REM Make sure required folders exist.

if not exist "%NF_TEMP%" mkdir "%NF_TEMP%"
if not exist "%NF_TOOLBIN%" mkdir "%NF_TOOLBIN%"
if not exist "%NF_ROOT%\Build" mkdir "%NF_ROOT%\Build"

REM Configure the PATH.
REM
REM Note that some tools like PuTTY and 7-Zip may be installed as
REM x86 or x64 to different directories.  We'll include commands that
REM attempt to add both locations to the path and the [pathtool] is
REM smart enough to only add directories that actually exist.

%NF_TOOLBIN%\pathtool -dedup -system -add "%NF_BUILD%"
%NF_TOOLBIN%\pathtool -dedup -system -add "%NF_TOOLBIN%"
%NF_TOOLBIN%\pathtool -dedup -system -add "%NF_TOOLBIN%\OpenSSL"
%NF_TOOLBIN%\pathtool -dedup -system -add "%DOTNETPATH%"
%NF_TOOLBIN%\pathtool -dedup -system -add "%ProgramFiles%\7-Zip"
%NF_TOOLBIN%\pathtool -dedup -system -add "%ProgramFiles(x86)%\7-Zip"
%NF_TOOLBIN%\pathtool -dedup -system -add "%ProgramFiles%\PuTTY"
%NF_TOOLBIN%\pathtool -dedup -system -add "%ProgramFiles(x86)%\PuTTY"
%NF_TOOLBIN%\pathtool -dedup -system -add "%ProgramFiles%\WinSCP"
%NF_TOOLBIN%\pathtool -dedup -system -add "%ProgramFiles(x86)%\WinSCP"

REM Configure the neonKUBE program folder and add it to the PATH.

if not exist "%ProgramFiles%\neonKUBE" mkdir "%ProgramFiles%\neonKUBE"
%NF_TOOLBIN%\pathtool -dedup -system -add "%ProgramFiles%\neonKUBE"

REM Configure the neonKUBE kubeconfig path (as a USER environment variable).

set KUBECONFIG=%USERPROFILE%\.kube\admin.conf
reg add HKCU\Environment /v KUBECONFIG /t REG_EXPAND_SZ /d %USERPROFILE%\.kube\config /f 

:done
pause
