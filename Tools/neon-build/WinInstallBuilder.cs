//-----------------------------------------------------------------------------
// FILE:        WinInstallBuilder.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Neon;
using Neon.Common;
using Neon.Windows;

namespace NeonBuild
{
    /// <summary>
    /// Builds the <b>ksetup</b> installer for Windows.
    /// </summary>
    public class WinInstallBuilder
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="setupHelper">The Kubernetes settup helper.</param>
        public WinInstallBuilder(KubeSetupHelper setupHelper)
        {
            Covenant.Requires<ArgumentNullException>(setupHelper != null, nameof(setupHelper));

            this.Helper = setupHelper;
        }

        /// <summary>
        /// Returns the setup helper.
        /// </summary>
        public KubeSetupHelper Helper { get; private set; }

        /// <summary>
        /// Builds the installer.
        /// </summary>
        public void Run()
        {
            //-----------------------------------------------------------------
            // Initialize

            Helper.LogLine("Initialize");
            Helper.LogLine("----------");

            // Ensure that the required components are downloaded into the cache.

            Helper.Download();

            // Add the directories holding the cached [kubectl.exe] files to the beginning 
            // of the PATH so we'll be sure to execute the correct versions.

            Helper.SetToolPath();

            // The Inno Setup script expects these environment variables:
            //
            //      NF_KUBE_VERSION     - The Kubernetes version
            //      NF_DESKTOP_VERSION  - The neonKUBE product version

            var desktopVersion = File.ReadLines(Path.Combine(Environment.GetEnvironmentVariable("NF_ROOT"), "neonKUBE-version.txt")).First().Trim();

            Environment.SetEnvironmentVariable("NF_KUBE_VERSION", Helper.KubeVersion);
            Environment.SetEnvironmentVariable("NF_DESKTOP_VERSION", desktopVersion);

            // Run the Inno Setup compiler.  Note that we're assuming that it's installed
            // to the default folder.

            Helper.LogLine("Building installer");

            const string innoCompilerPath = @"C:\Program Files (x86)\Inno Setup 5\ISCC.exe";

            var result = Helper.Execute(innoCompilerPath, $"\"{Path.Combine(Helper.SourceRepoFolder, "Desktop", "WinInstaller", "Setup.iss")}\"", ignoreError: true);

            if (result.ExitCode != 0)
            {
                Helper.LogError(result.AllText);
                Program.Exit(1);
            }
        }
    }
}
