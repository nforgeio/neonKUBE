//-----------------------------------------------------------------------------
// FILE:	    TestHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube;
using Neon.IO;
using Neon.Xunit;
using Neon.Xunit.Kube;

using Xunit;

namespace TestKube
{
    /// <summary>
    /// Misc local unit test helpers.
    /// </summary>
    public static class TestHelper
    {
        /// <summary>
        /// <b>nhive/test</b> image test user name.
        /// </summary>
        public const string TestUsername = "test";

        /// <summary>
        /// <b>nhive/test</b> image test user ID.
        /// </summary>
        public const string TestUID = "5555";

        /// <summary>
        /// <b>nhive/test</b> image test group ID.
        /// </summary>
        public const string TestGID = "6666";

        /// <summary>
        /// Creates and optionally populates a temporary test folder with test files.
        /// </summary>
        /// <param name="files">
        /// The files to be created.  The first item in each tuple entry will be 
        /// the local file name and the second the contents of the file.
        /// </param>
        /// <returns>The <see cref="Xunit.TempFolder"/>.</returns>
        /// <remarks>
        /// <note>
        /// Ensure that the <see cref="Xunit.TempFolder"/> returned is disposed so it and
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
        /// <returns>The <see cref="Xunit.TempFolder"/>.</returns>
        /// <remarks>
        /// <note>
        /// Ensure that the <see cref="Xunit.TempFolder"/> returned is disposed so it and
        /// any files within will be deleted.
        /// </note>
        /// </remarks>
        public static TempFolder TempFolder(string filename, string data)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(filename));

            var folder = new TempFolder();

            File.WriteAllText(Path.Combine(folder.Path, filename), data ?? string.Empty);

            return folder;
        }

        /// <summary>
        /// Executes the <b>neon-cli</b>.
        /// </summary>
        /// <param name="args">The command arguments.</param>
        /// <returns>The <see cref="ExecuteResult"/>.</returns>
        public static ExecuteResult Neon(params object[] args)
        {
            // We're going to run the command from the NF_BUILD directory.

            var buildFolder = Environment.GetEnvironmentVariable("NF_BUILD");

            if (string.IsNullOrEmpty(buildFolder))
            {
                throw new Exception("The NF_BUILD environment variable is not defined.");
            }

            string neonPath;

            if (NeonHelper.IsWindows)
            {
                neonPath = Path.Combine(buildFolder, "neon.cmd");
            }
            else
            {
                neonPath = Path.Combine(buildFolder, "neon");
            }

            if (!File.Exists(neonPath))
            {
                throw new Exception($"The [neon-cli] executable does not exist at [{neonPath}].");
            }

            return NeonHelper.ExecuteCapture(neonPath, args);
        }
    }
}
