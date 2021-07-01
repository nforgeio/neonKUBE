//-----------------------------------------------------------------------------
// FILE:	    DeploymentHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.IO;
using Neon.Kube.Models.Headend;

namespace Neon.Deployment
{
    /// <summary>
    /// Deployment related defintions and utilities.
    /// </summary>
    public static class DeploymentHelper
    {
        /// <summary>
        /// Identifies the named pipe used to communicate with the Neon profile
        /// service running on the local workstation to query for user profile
        /// information as well as secrets.
        /// </summary>
        public const string NeonProfileServicePipe = "neon-profile-service";

        /// <summary>
        /// The HTTP Content-Type used for <see cref="Download"/> metadata.
        /// </summary>
        public const string DownloadContentType = "application/vnd+neonforge.download+json";

        /// <summary>
        /// Clears the Powershell command history.  It's possible that scripts and
        /// GitHub workflow runs may leave sensitive information in the command
        /// history which could become a security vunerability.
        /// </summary>
        public static void ClearPowershellHistory()
        {
            // Inspired by:
            //
            //      https://www.shellhacks.com/clear-history-powershell/

            using (var tempFile = new TempFile(suffix: ".ps1"))
            {
                File.WriteAllText(tempFile.Path, "Remove-Item (Get-PSReadlineOption).HistorySavePath");
                
                var exitCode = NeonHelper.ExecuteShell($"pwsh -f \"{tempFile.Path}\"");

                if (exitCode != 0)
                {
                    throw new ExecuteException(exitCode, "Command failed.");
                }
            }
        }
    }
}
