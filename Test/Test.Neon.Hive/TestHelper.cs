//-----------------------------------------------------------------------------
// FILE:	    TestHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Hive;
using Neon.IO;
using Neon.Xunit;
using Neon.Xunit.Hive;

using Xunit;

namespace TestHive
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
        /// Returns the fully qualified path to the encrypted Ansible neonHIVE 
        /// test <b>secrets.yaml</b> file.
        /// </summary>
        /// <returns>The file path.</returns>
        public static string AnsibleSecretsPath => Path.GetFullPath(Path.Combine(Environment.GetEnvironmentVariable("NF_ROOT"), "Test", "secrets.yaml"));

        /// <summary>
        /// Returns name of the <b>neon-git</b> Ansible password file used
        /// to encrypt secret files included in the source repository.
        /// </summary>
        public const string AnsiblePasswordFile = "neon-git";
    }
}
