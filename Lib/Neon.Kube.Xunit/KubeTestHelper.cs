//-----------------------------------------------------------------------------
// FILE:	    KubeTestHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Xunit;
using Neon.Xunit;

using Xunit;

namespace Neon.Kube.Xunit
{
    /// <summary>
    /// Misc local unit test helpers.
    /// </summary>
    public static class KubeTestHelper
    {
        /// <summary>
        /// <b>ghcr.io/neonrelease/test</b> image test user name.
        /// </summary>
        public const string TestUsername = "test";

        /// <summary>
        /// <b>ghcr.io/neonrelease/test</b> image test user ID.
        /// </summary>
        public const string TestUID = "5555";

        /// <summary>
        /// <b>ghcr.io/neonrelease/test</b> image test group ID.
        /// </summary>
        public const string TestGID = "6666";

        /// <summary>
        /// Static constructor.
        /// </summary>
        static KubeTestHelper()
        {
            ClusterDefinitions =
                new Dictionary<string, string>()
                {
                    { "hyperv/tiny", HyperVClusters.Tiny },
                    { "hyperv/small", HyperVClusters.Small },
                    { "hyperv/large", HyperVClusters.Large },

                    { "xenserver/tiny", XenServerClusters.Tiny },
                    { "xenserver/small", XenServerClusters.Small },
                    { "xenserver/large", XenServerClusters.Large },
                };
        }

        /// <summary>
        /// Returns a dictionary mapping test cluster definitions keyed like <b>hyperv/tiny</b> or
        /// <b>xenserver/large</b> to the actual cluster definition text.  This is used so that
        /// maintainers can configure their <b>neon-assistant</b> profile to specify which cluster
        /// definition they wish to use for running unit tests in their environment.
        /// </summary>
        public static IReadOnlyDictionary<string, string> ClusterDefinitions { get; private set; }

        /// <summary>
        /// Creates and optionally populates a temporary test folder with test files.
        /// </summary>
        /// <param name="files">
        /// The files to be created.  The first item in each tuple entry will be 
        /// the local file name and the second the contents of the file.
        /// </param>
        /// <returns>The <see cref="TempFolder"/>.</returns>
        /// <remarks>
        /// <note>
        /// Ensure that the <see cref="TempFolder"/> returned is disposed so it and
        /// any files within will be deleted.
        /// </note>
        /// </remarks>
        public static TempFolder CreateTestFolder(params Tuple<string, string>[] files)
        {
            var folder = new TempFolder();

            if (files != null)
            {
                foreach (var file in files)
                {
                    File.WriteAllText(Path.Combine(folder.Path, file.Item1), file.Item2 ?? string.Empty);
                }
            }

            return folder;
        }

        /// <summary>
        /// Creates and populates a temporary test folder with a test file.
        /// </summary>
        /// <param name="data">The file name</param>
        /// <param name="filename">The file data.</param>
        /// <returns>The <see cref="TempFolder"/>.</returns>
        /// <remarks>
        /// <note>
        /// Ensure that the <see cref="TempFolder"/> returned is disposed so it and
        /// any files within will be deleted.
        /// </note>
        /// </remarks>
        public static TempFolder TempFolder(string filename, string data)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(filename), nameof(filename));

            var folder = new TempFolder();

            File.WriteAllText(Path.Combine(folder.Path, filename), data ?? string.Empty);

            return folder;
        }

        /// <summary>
        /// Starts and returns a <see cref="KubeTestManager"/> instance.  This will put <see cref="KubeHelper"/>
        /// into test mode.  You must dispose the instance before the tests complete to revert back
        /// to normal mode.
        /// </summary>
        /// <returns>The <see cref="KubeTestManager"/>.</returns>
        public static KubeTestManager KubeTestManager()
        {
            return new KubeTestManager();
        }

        /// <summary>
        /// Returns the path to the <b>neon-cli</b> executable.
        /// </summary>
        private static string NeonExePath
        {
            get
            {
                // We're going to run the command from the NK_BUILD directory.

                var buildFolder = Environment.GetEnvironmentVariable("NK_BUILD");

                if (string.IsNullOrEmpty(buildFolder))
                {
                    throw new Exception("The NK_BUILD environment variable is not defined.");
                }

                if (NeonHelper.IsWindows)
                {
                    return Path.Combine(buildFolder, "neon.cmd");
                }
                else
                {
                    return Path.Combine(buildFolder, "neon");
                }
            }
        }
    }
}
