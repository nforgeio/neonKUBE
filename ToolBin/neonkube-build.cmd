@echo off
REM ---------------------------------------------------------------------------
REM FILE:         neonkube-build.cmd
REM CONTRIBUTOR:  Jeff Lill
REM COPYRIGHT:    Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
REM
REM Licensed under the Apache License, Version 2.0 (the "License");
REM you may not use this file except in compliance with the License.
REM You may obtain a copy of the License at
REM
REM     http://www.apache.org/licenses/LICENSE-2.0
REM
REM Unless required by applicable law or agreed to in writing, software
REM distributed under the License is distributed on an "AS IS" BASIS,
REM WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
REM See the License for the specific language governing permissions and
REM limitations under the License.

REM Performs a clean build of the neonKUBE solution and publishes binaries
REM to the [$/build] folder.  This can also optionally build the neonKUBE
REM Desktop installer.
REM
REM USAGE: neonkube-build [OPTIONS]
REM
REM OPTIONS:
REM
REM     -debug      - Builds the DEBUG version (this is the default)
REM     -release    - Builds the RELEASE version
REM     -nobuild    - Don't build the solution; just publish
REM     -installer  - Builds installer binaries

powershell -file "%NF_ROOT%\ToolBin\neonkube-build.ps1" %*
