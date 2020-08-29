//-----------------------------------------------------------------------------
// FILE:	    TestContext.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Windows;
using Neon.Xunit;

using Xunit;

namespace Neon.Xunit
{
    /// <summary>
    /// Holds information like settings and test files for unit tests.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is pretty easy to use.  Simply construct an instance, perform any
    /// desired initialization such as reading files (potentionally encypted) and
    /// configuring variables/settings.  Then your tests can reference this via
    /// the static <see cref="TestContext.Current"/> property.
    /// </para>
    /// <para>
    /// You'll generally construct one of these instances at the beginning of your
    /// test method or within a test fixture.  Only one <see cref="TestContext"/>
    /// may be active at any given time, so remember to call <see cref="Dispose"/>
    /// when your test run is commplete.
    /// </para>
    /// <para>
    /// </para>
    /// </remarks>
    public sealed class TestContext : IDisposable
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly char[] equalArray = new char[] { '=' };

        private static object syncRoot = new object();

        /// <summary>
        /// Returns the cuurent <see cref="TestContext"/> or <c>null</c>.
        /// </summary>
        public static TestContext Current { get; private set; }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if another <see cref="TestContext"/> already exists.</exception>
        /// <remarks>
        /// <note>
        /// Only one <see cref="TestContext"/> instance may exist at any time.
        /// </note>
        /// </remarks>
        public TestContext()
        {
            lock (syncRoot)
            {
                if (Current != null)
                {
                    throw new InvalidOperationException($"Another [{nameof(TestContext)}] already exists.  You'll need to dispose that one before creating another.");
                }

                Current = this;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            lock (syncRoot)
            {
                Current = null;
            }
        }

        /// <summary>
        /// Returns a case senstive dictionary mapping setting names to object values.
        /// You can use this to pass settings and other information to tests.
        /// </summary>
        public Dictionary<string, object> Settings { get; private set; } = new Dictionary<string, object>();

        /// <summary>
        /// Returns a case sensitive dictionary mapping file names to byte arrays 
        /// with the file contents.  You can use this to pass file data to tests.
        /// </summary>
        public Dictionary<string, byte[]> Files { get; private set; } = new Dictionary<string, byte[]>();

        /// <summary>
        /// Encrypts a file or directory when supported by the underlying operating system
        /// and file system.  Currently, this only works on non-HOME versions of Windows
        /// and NTFS file systems.  This fails silently.
        /// </summary>
        /// <param name="path">The file or directory path.</param>
        /// <returns><c>true</c> if the operation was successful.</returns>
        private bool EncryptFile(string path)
        {
            try
            {
                return Win32.EncryptFile(path);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Looks up a password from the <b>~/.neonkube/passwords</b> folder.
        /// </summary>
        /// <param name="passwordName">The password name.</param>
        /// <returns>The password value.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the password doesn't exist.</exception>
        private string LookupPassword(string passwordName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(passwordName), nameof(passwordName));

            string neonKubeFolder;

            if (NeonHelper.IsWindows)
            {
                neonKubeFolder = Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), ".neonkube");

                Directory.CreateDirectory(neonKubeFolder);

                try
                {
                    EncryptFile(neonKubeFolder);
                }
                catch
                {
                    // Encryption is not available on all platforms (e.g. Linux, OS/X, Windows Home, or non-NTFS
                    // file systems).  Secrets won't be encrypted for these situations.
                }
            }
            else if (NeonHelper.IsLinux || NeonHelper.IsOSX)
            {
                neonKubeFolder = Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".neonkube");

                Directory.CreateDirectory(neonKubeFolder);
            }
            else
            {
                throw new NotImplementedException();
            }

            var path = Path.Combine(neonKubeFolder, "passwords");

            if (!File.Exists(path))
            {
                throw new KeyNotFoundException(passwordName);
            }

            return File.ReadAllText(path).Trim();
        }

        /// <summary>
        /// <para>
        /// Loads settings formatted as <c>NAME=VALUE</c> from a text file into the
        /// <see cref="Settings"/> dictionary.  The file will be decrypted using
        /// <see cref="NeonVault"/> if necessary.
        /// </para>
        /// <note>
        /// Blank lines and lines beginning with '#' will be ignored.
        /// </note>
        /// </summary>
        /// <param name="path">The input file path.</param>
        /// <param name="passwordProvider">
        /// Optionally specifies the password provider function to be used to locate the
        /// password required to decrypt the source file when necessary.  This defaults
        /// to looking for the password inb <b>~/.neonkube/passwords</b> when 
        /// <paramref name="passwordProvider"/> is <c>null</c>.
        /// </param>
        /// <exception cref="FileNotFoundException">Thrown if the file doesn't exist.</exception>
        /// <exception cref="FormatException">Thrown for file formatting problems.</exception>
        public void LoadSettings(string path, Func<string, string> passwordProvider = null)
        {
            passwordProvider = passwordProvider ?? LookupPassword;

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"File not found: {path}");
            }

            var vault = new NeonVault(passwordProvider);
            var bytes = vault.Decrypt(path);

            using (var ms = new MemoryStream(bytes))
            {
                using (var reader = new StreamReader(ms))
                {
                    var lineNumber = 1;

                    foreach (var rawLine in reader.Lines())
                    {
                        var line = rawLine.Trim();

                        if (line.Length == 0 || line.StartsWith("#"))
                        {
                            continue;
                        }

                        var fields = line.Split(equalArray, 2);

                        if (fields.Length != 2)
                        {
                            throw new FormatException($"[{path}:{lineNumber}]: Invalid input: {line}");
                        }

                        var name  = fields[0].Trim();
                        var value = fields[1].Trim();

                        if (name.Length == 0)
                        {
                            throw new FormatException($"[{path}:{lineNumber}]: Setting name cannot be blank.");
                        }

                        Settings[name] = value;
                    }
                }
            }
        }

        /// <summary>
        /// <para>
        /// Loads environment variables formatted as <c>NAME=VALUE</c> from a text file into environment
        /// variables.  The file will be decrypted using <see cref="NeonVault"/> if necessary.
        /// </para>
        /// <note>
        /// Blank lines and lines beginning with '#' will be ignored.
        /// </note>
        /// </summary>
        /// <param name="path">The input file path.</param>
        /// <param name="passwordProvider">
        /// Optionally specifies the password provider function to be used to locate the
        /// password required to decrypt the source file when necessary.  This defaults
        /// to looking for the password inb <b>~/.neonkube/passwords</b> when 
        /// <paramref name="passwordProvider"/> is <c>null</c>.
        /// </param>
        /// <exception cref="FileNotFoundException">Thrown if the file doesn't exist.</exception>
        /// <exception cref="FormatException">Thrown for file formatting problems.</exception>
        public void LoadEnvironment(string path, Func<string, string> passwordProvider = null)
        {
            passwordProvider = passwordProvider ?? LookupPassword;

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"File not found: {path}");
            }

            var vault = new NeonVault(passwordProvider);
            var bytes = vault.Decrypt(path);

            using (var ms = new MemoryStream(bytes))
            {
                using (var reader = new StreamReader(ms))
                {
                    var lineNumber = 1;

                    foreach (var rawLine in reader.Lines())
                    {
                        var line = rawLine.Trim();

                        if (line.Length == 0 || line.StartsWith("#"))
                        {
                            continue;
                        }

                        var fields = line.Split(equalArray, 2);

                        if (fields.Length != 2)
                        {
                            throw new FormatException($"[{path}:{lineNumber}]: Invalid input: {line}");
                        }

                        var name  = fields[0].Trim();
                        var value = fields[1].Trim();

                        if (name.Length == 0)
                        {
                            throw new FormatException($"[{path}:{lineNumber}]: Setting name cannot be blank.");
                        }

                        Environment.SetEnvironmentVariable(name, value);
                    }
                }
            }
        }

        /// <summary>
        /// Loads a file into the <see cref="Files"/> dictionary, using the file name
        /// (without the directory path) as the key.  The file will be decrypted via
        /// <see cref="NeonVault"/> as necessary.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="passwordProvider">
        /// Optionally specifies the password provider function to be used to locate the
        /// password required to decrypt the source file when necessary.  This defaults
        /// to looking for the password inb <b>~/.neonkube/passwords</b> when 
        /// <paramref name="passwordProvider"/> is <c>null</c>.
        /// </param>
        /// <exception cref="FileNotFoundException">Thrown if the file doesn't exist.</exception>
        /// <exception cref="FormatException">Thrown for file formatting problems.</exception>
        public void LoadFile(string path, Func<string, string> passwordProvider = null)
        {
            passwordProvider = passwordProvider ?? LookupPassword;

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"File not found: {path}");
            }

            var vault = new NeonVault(passwordProvider);
            var bytes = vault.Decrypt(path);

            Files[Path.GetFileName(path)] = bytes;
        }

        /// <summary>
        /// Returns the raw bytes for the named file from the <see cref="Files"/>
        /// dictionary.
        /// </summary>
        /// <param name="filename">The file name.</param>
        /// <returns>The file bytes.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the file doesn't exist.</exception>
        public byte[] GetFileBytes(string filename)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(filename), nameof(filename));

            return Files[filename];
        }

        /// <summary>
        /// Returns the text for the named file from the <see cref="Files"/>.
        /// dictionary.
        /// </summary>
        /// <param name="filename">The file name.</param>
        /// <param name="encoding">The encoding to be used (defaults to <see cref="Encoding.UTF8"/>).</param>
        /// <returns>The file text.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the file doesn't exist.</exception>
        public string GetFileText(string filename, Encoding encoding = null)
        {
            var bytes = GetFileBytes(filename);

            encoding = encoding ?? Encoding.UTF8;

            return encoding.GetString(bytes);
        }
    }
}
