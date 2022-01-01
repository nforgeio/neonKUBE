//-----------------------------------------------------------------------------
// FILE:	    Program.Dotnet.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Neon.Common;

namespace NeonBuild
{
    public static partial class Program
    {
        /// <summary>
        /// Executes the <b>dotnet</b> tool passing the <paramref name="commandLine"/> while
        /// limiting the environment variables passed to the <b>dotnet</b> tool to avoid
        /// conflicts.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        public static void Dotnet(CommandLine commandLine)
        {
            // It appears that the [dotnet] command and/or [msbuild] configures some environment
            // variables to transmit state to processes it starts.  This is preventing us from 
            // running [dotnet] within a build target as we need to do sometimes.
            //
            // The workaround is to invoke [neon-build dotnet ...] and have this command remove
            // any environment variables outside the norm and then execute the [dotnet] command
            // without those variables.

            const string allowedVariableNames =
@"
ALLUSERSPROFILE
APPDATA
architecture
architecture_bits
CommonProgramFiles
CommonProgramFiles(x86)
CommonProgramW6432
COMPUTERNAME
ComSpec
DEV_WORKSTATION
DOTNETPATH
DOTNET_CLI_TELEMETRY_OPTOUT
DriverData
GITHUB_PAT
GITHUB_USERNAME
GIT_INSTALL_ROOT
GOPATH
HOME
HOMEDRIVE
HOMEPATH
JAVA_HOME
LOCALAPPDATA
NUMBER_OF_PROCESSORS
OS
Path
PATHEXT
POWERSHELL_DISTRIBUTION_CHANNEL
PROCESSOR_ARCHITECTURE
PROCESSOR_IDENTIFIER
PROCESSOR_LEVEL
PROCESSOR_REVISION
ProgramData
ProgramFiles
ProgramFiles(x86)
ProgramW6432
PUBLIC
SystemDrive
SystemRoot
TEMP
USERDOMAIN
USERDOMAIN_ROAMINGPROFILE
USERNAME
USERPROFILE
windir
";
            var allowedVariables = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            using (var reader = new StringReader(allowedVariableNames))
            {
                foreach (var line in reader.Lines())
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    allowedVariables.Add(line.Trim());
                }
            }

            foreach (string environmentVariable in Environment.GetEnvironmentVariables().Keys)
            {
                if (!allowedVariables.Contains(environmentVariable))
                {
                    Environment.SetEnvironmentVariable(environmentVariable, null);
                }
            }

            Program.Exit(NeonHelper.Execute("dotnet", commandLine.Items.Skip(1).ToArray()));
        }
    }
}
