@echo off
REM ---------------------------------------------------------------------------
REM FILE:         neon-nuget-local.cmd
REM CONTRIBUTOR:  Jeff Lill
REM COPYRIGHT:    Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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

REM Publishes DEBUG builds of the NeonForge Nuget packages to the local
REM file system at: %NF_BUILD%\nuget.

powershell -file "%NF_ROOT%\ToolBin\neon-nuget-local.ps1" %*