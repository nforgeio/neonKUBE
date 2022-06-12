//-----------------------------------------------------------------------------
// FILE:	    Wsl2Proxy.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Win32.SafeHandles;

using Neon.Common;
using Neon.IO;
using Neon.Net;

namespace Neon.WSL
{
    /// <summary>
    /// <b>INTERNAL USE ONLY:</b> Handles interactions with our neonKUBE WSL2 distribution
    /// running on the local Windows workstation.  Note that this is not intended to be 
    /// generally useful at this time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WSL2 distibutions are managed by the Microsoft <b>wsl.exe</b> command line
    /// tool.  This includes commands to import/export, register, and terminate WSL2
    /// Linux distributions as well as the ability to login and/or execute commands,
    /// </para>
    /// <para>
    /// <b>wsl.exe</b> seems to be primarily intended to be used by users
    /// performing interactive commands from within some sort of command shell like
    /// <b>cmd.exe</b>, Powershell, <b>cmdr.exe</b>, <b>ms-terminal</b>, etc.
    /// </para>
    /// <para>
    /// The <see cref="Wsl2Proxy"/> class wraps the <b>wsl.exe</b> tool such
    /// that Linux commands can be be invoked via code running on Windows.  We
    /// currently use this for managing WSL2 for a local neonDESKTOP cluster.
    /// </para>
    /// <para><b>Managing WSL2 Distros</b></para>
    /// <para>
    /// This class provides these <c>static</c> methods for managing distros:
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><see cref="Import(string, string, string)"/></term>
    ///     <description>
    ///     Imports a WSL2 distro from a TAR file.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="Export(string, string)"/></term>
    ///     <description>
    ///     Exports a WSL2 distro to a TAR file.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="Terminate(string)"/></term>
    ///     <description>
    ///     Terminates the named WSL2 disto.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="Unregister(string)"/></term>
    ///     <description>
    ///     Unregisters the named WSL2 distribution.  Note that you must
    ///     <see cref="Terminate(string)"/> it first when the distro is running.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="List(bool, bool)"/></term>
    ///     <description>
    ///     Lists registered distributions, optionally returning only the running
    ///     distros.
    ///     </description>
    /// </item>
    /// </list>
    /// <para><b>Executing Commands</b></para>
    /// <para>
    /// To start a WSL distro, you'll need to instantiate an instance via <c>new </c><see cref="Wsl2Proxy(string, string)"/>,
    /// passing the registered name of the distribution and optionally the Linux user name (defaults to <b>root</b>).  By default,
    /// the constructor logs into the distro using specified user name.
    /// </para>
    /// <para>
    /// This class provides several methods for executing commands within the distro:
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><see cref="Execute(string, object[])"/></term>
    ///     <description>
    ///     Executes a command as the current user.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="ExecuteAs(string, string, object[])"/></term>
    ///     <description>
    ///     Executes a command as a specific user.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="ExecuteScript(string)"/></term>
    ///     <description>
    ///     Executes a script as the current user.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="ExecuteScriptAs(string, string)"/></term>
    ///     <description>
    ///     Executes a script as a specific user.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="SudoExecute(string, object[])"/></term>
    ///     <description>
    ///     <b>sudo</b> executes a command as the current user.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="SudoExecuteAs(string, string, object[])"/></term>
    ///     <description>
    ///     <b>sudo</b> executes a command as a specific user.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="SudoExecuteScript(string)"/></term>
    ///     <description>
    ///     <b>sudo</b> executes a script as the current user.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="SudoExecuteScriptAs(string, string)"/></term>
    ///     <description>
    ///     <b>sudo</b> executes a script as the current user.
    ///     </description>
    /// </item>
    /// </list>
    /// <note>
    /// <b>IMPORTANT:</b> Do not depend on the executed commands sharing the same environment variables.
    /// Also, don't depend on the Linux <b>login</b> having been started.
    /// </note>
    /// <para><b>Managing Files</b></para>
    /// <para>
    /// WSL2 distro file management is pretty easy because Windows mounts its file system at <b>/mnt/DRIVE-LETTER/...</b> within the
    /// distro so Linux code can access them and the distro files are mounted on Windows at <b>//wsl$/DISTRO-NAME/...</b>.
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><see cref="ToLinuxPath(string)"/></term>
    ///     <description>
    ///     Maps a host Windows file system path to the equivalent path within the Linux distro.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="ToWindowsPath(string)"/></term>
    ///     <description>
    ///     Maps a Linux distro file system path to the equivalent path on the Windows host.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="UploadFile(string, string, string, string, bool)"/></term>
    ///     <description>
    ///     Uploads a file from the Windows host to the distro, optionally setting the
    ///     owner and permissions as well as optionally converting Windows style line 
    ///     endings (\r\n) to Linux (\n);
    ///     </description>
    /// </item>
    /// </list>
    /// <note>
    /// These file methods work when <see cref="Wsl2Proxy"/> instance regardless of whether
    /// the instance is logged into the distro or not.
    /// </note>
    /// </remarks>
    public sealed class Wsl2Proxy : IDisposable
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// The name of the root Linux user.
        /// </summary>
        public const string RootUser = "root";

        /// <summary>
        /// Returns <c>true</c> if WSL2 is enabled on the local Windows workstation.
        /// </summary>
        public static bool IsWsl2Enabled
        {
            get
            {
                // $todo(jefflill): Need to implement this

                return true;
            }
        }

        /// <summary>
        /// Lists the names of the installed WSL2 distributions, optionally limiting
        /// this to the running distributions.
        /// </summary>
        /// <param name="runningOnly">Optionally return just the running distribitions.</param>
        /// <param name="keepDefault">Optionally retain the <b>" (Default)"</b> substring identifying the default repo.</param>
        /// <returns>The list of WSL2 distributions.</returns>
        public static IEnumerable<string> List(bool runningOnly = false, bool keepDefault = false)
        {
            var response = NeonHelper.ExecuteCapture("wsl.exe",
                new object[]
                {
                    "--list",
                    !keepDefault ? "--quiet" : null,
                    runningOnly ? "--running" : null
                },
                outputEncoding: Encoding.Unicode);

            response.EnsureSuccess();

            using (var reader = new StringReader(response.OutputText))
            {
                if (keepDefault)
                {
                    return reader.Lines().Skip(1).ToList();
                }
                else
                {
                    return reader.Lines().ToList();
                }
            }
        }

        /// <summary>
        /// Returns the name of the default WSL2 distribution, if any.
        /// </summary>
        /// <returns>The name of the default distribution or <c>null</c>.</returns>
        public static string GetDefault()
        {
            var distro = List(keepDefault: true).SingleOrDefault(distro => distro.EndsWith(" (Default)"));

            if (distro != null)
            {
                distro = distro.Replace(" (Default)", string.Empty);
            }

            return distro;
        }

        /// <summary>
        /// Checks to see if a named distribution exists.
        /// </summary>
        /// <param name="name">The distribution name.</param>
        /// <returns><c>true</c> if the distribution exists.</returns>
        public static bool Exists(string name)
        {
            return List().Contains(name, StringComparer.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Imports a distribution from a TAR file.
        /// </summary>
        /// <param name="name">The new distribution's name.</param>
        /// <param name="tarPath">Path to the distribution input TAR file.</param>
        /// <param name="targetFolder">Path to the folder where the distribution image will be created.</param>
        public static void Import(string name, string tarPath, string targetFolder)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(tarPath), nameof(tarPath));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(targetFolder), nameof(targetFolder));

            NeonHelper.ExecuteCapture("wsl.exe",
                new object[]
                {
                    "--import",
                    name,
                    targetFolder,
                    tarPath,
                    "--version", "2"
                },
                outputEncoding: Encoding.Unicode).EnsureSuccess();
        }

        /// <summary>
        /// Exports a named distribution to a TAR file.
        /// </summary>
        /// <param name="name">The new distribution's name.</param>
        /// <param name="tarPath">Path to the distribution output TAR file.</param>
        public static void Export(string name, string tarPath)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(tarPath), nameof(tarPath));

            NeonHelper.DeleteFile(tarPath);

            NeonHelper.ExecuteCapture("wsl.exe",
                new object[]
                {
                    "--export",
                    name,
                    tarPath
                },
                outputEncoding: Encoding.Unicode).EnsureSuccess();
        }

        /// <summary>
        /// Terminates the named distribution if it exists and is running.
        /// </summary>
        /// <param name="name">Identifies the target WSL2 distribution.</param>
        public static void Terminate(string name)
        {
            if (List(runningOnly: true).Contains(name, StringComparer.InvariantCultureIgnoreCase))
            {
                NeonHelper.ExecuteCapture("wsl.exe",
                    new object[]
                    {
                        "--terminate", name
                    },
                    outputEncoding: Encoding.Unicode).EnsureSuccess();
            }
        }

        /// <summary>
        /// Terminates the named distribution (if it exists ans is running) and 
        /// then unregisters it with WSL2 effectively removing it.
        /// </summary>
        /// <param name="name">Identifies the target WSL2 distribution.</param>
        public static void Unregister(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            Terminate(name);

            if (List().Contains(name, StringComparer.InvariantCultureIgnoreCase))
            {
                NeonHelper.ExecuteCapture("wsl.exe",
                    new object[]
                    {
                        "--unregister", name
                    },
                    outputEncoding: Encoding.Unicode).EnsureSuccess();
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private Dictionary<string, string>  cachedOsRelease   = null;

        /// <summary>
        /// Constructs a proxy connected to a specific WSL2 distribution, starting the
        /// distribution by default of it's not already running.
        /// </summary>
        /// <param name="name">Identifies the target WSL2 distribution.</param>
        /// <param name="user">Optionally connect as a non-root user.</param>
        /// <remarks>
        /// The <paramref name="user"/> passed will become the default user for subsequent
        /// proxy operations.  This may be overridden by for specific operations by specifying 
        /// a different user in the call.
        /// </remarks>
        public Wsl2Proxy(string name, string user = RootUser)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(user), nameof(user));

            if (!Exists(name))
            {
                throw new InvalidOperationException($"WSL2 distribution [{name}] does not exist.");
            }

            this.Name = name;
            this.User = user;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Terminate();
        }

        /// <summary>
        /// Returns the distribution's name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Sppecifies the distribution user account to use for operations.  This will be
        /// initialized to the user passed to the constructor but may be changed afterwards
        /// to perform operations under other accounts.
        /// </summary>
        public string User { get; set; }

        /// <summary>
        /// Determines whether the distribution is running.
        /// </summary>
        public bool IsRunning => List(runningOnly: true).Contains(Name, StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Returns a dictionary with the properties loaded from the <b>/etc/os-release</b> file
        /// on the distribution.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The contents will look something like:
        /// </para>
        /// <code>
        /// NAME=Ubuntu
        /// VERSION=20.04.1 LTS (Focal Fossa)
        /// ID=ubuntu
        /// ID_LIKE=debian
        /// PRETTY_NAME=Ubuntu 20.04.1 LTS
        /// VERSION_ID=20.04
        /// HOME_URL=https://www.ubuntu.com/
        /// SUPPORT_URL=https://help.ubuntu.com/
        /// BUG_REPORT_URL=https://bugs.launchpad.net/ubuntu/
        /// PRIVACY_POLICY_URL=https://www.ubuntu.com/legal/terms-and-policies/privacy-policy
        /// VERSION_CODENAME=focal
        /// UBUNTU_CODENAME=focal
        /// </code>
        /// </remarks>
        public IDictionary<string, string> OSRelease
        {
            get
            {
                if (cachedOsRelease != null)
                {
                    return cachedOsRelease;
                }

                cachedOsRelease = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

                var response = SudoExecute("cat", "/etc/os-release").EnsureSuccess();

                using (var reader = new StringReader(response.OutputText))
                {
                    foreach (var line in reader.Lines())
                    {
                        var trimmedLine = line.Trim();

                        if (trimmedLine == string.Empty || trimmedLine.StartsWith("#"))
                        {
                            continue;
                        }

                        var equalPos = trimmedLine.IndexOf('=');

                        if (equalPos == -1)
                        {
                            continue;
                        }

                        var name  = trimmedLine.Substring(0, equalPos).Trim();
                        var value = trimmedLine.Substring(equalPos + 1).Replace("\"", string.Empty).Trim();

                        cachedOsRelease[name] = value;
                    }
                }

                return cachedOsRelease;
            }
        }

        /// <summary>
        /// Returns <c>true</c> for Debian/Ubuntu based distributions.
        /// </summary>
        public bool IsDebian
        {
            get
            {
                var osRelease = OSRelease;

                if (osRelease.TryGetValue("ID", out var id))
                {
                    if (id.Equals("debian", StringComparison.InvariantCultureIgnoreCase) ||
                        id.Equals("ubuntu", StringComparison.InvariantCultureIgnoreCase))
                    {
                        return true;
                    }
                }

                if (osRelease.TryGetValue("ID_LIKE", out var idLike))
                {
                    if (idLike.Equals("debian", StringComparison.InvariantCultureIgnoreCase) ||
                        idLike.Equals("ubuntu", StringComparison.InvariantCultureIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Optionally overrides the default user temp folder.  This is currently
        /// used by unit tests to verify that the class still works when a user
        /// has spaces in their Windows username which means that their temp
        /// folder path will also include spaces.
        /// </summary>
        internal string TempFolder { get; set; } = null;

        /// <summary>
        /// Determines whether a WSL2 IPv4 port is currently listening for connections.
        /// </summary>
        /// <param name="port">The port number.</param>
        /// <returns><c>true</c> if the port is available.</returns>
        /// <remarks>
        /// This is useful for ensuring that another distro hasn't started listening
        /// on a port that's going to conflict with this distro.  This can happen 
        /// because all WSL2 distros share the same network namespace.  This can also
        /// happen when a Windows process is listening on the port.
        /// </remarks>
        public bool IsPortListening(int port)
        {
            return SudoExecuteAs(RootUser, "lsof", $"-i4:{port}").ExitCode == 0;
        }

        /// <summary>
        /// Terminates the distribution if it's running.
        /// </summary>
        public void Terminate()
        {
            Wsl2Proxy.Terminate(Name);
        }

        /// <summary>
        /// <para>
        /// Executes a program within the distribution as the current user.
        /// </para>
        /// <note>
        /// The program will be executed within the current login session
        /// if there is one.
        /// </note>
        /// </summary>
        /// <param name="path">The program path.</param>
        /// <param name="args">Optional program arguments.</param>
        /// <returns>An <see cref="ExecuteResponse"/> with the command results.</returns>
        public ExecuteResponse Execute(string path, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path), nameof(path));

            return NeonHelper.ExecuteCapture("wsl.exe",
                new object[]
                {
                    "--distribution", this.Name,
                    "--user", this.User,
                    "--",
                    path,
                    args
                },
                outputEncoding: Encoding.UTF8);
        }

        /// <summary>
        /// Executes a program within the distribution as a specific user.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="path">The program path.</param>
        /// <param name="args">Optional program arguments.</param>
        /// <returns>An <see cref="ExecuteResponse"/> with the command results.</returns>
        public ExecuteResponse ExecuteAs(string user, string path, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(user), nameof(user));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path), nameof(path));

            return NeonHelper.ExecuteCapture("wsl.exe",
                new object[]
                {
                    "--distribution", this.Name,
                    "--user", user,
                    "--",
                    path,
                    args
                },
                outputEncoding: Encoding.UTF8);
        }

        /// <summary>
        /// <para>
        /// Executes a program within the distribution as the current user under SUDO.
        /// </para>
        /// <note>
        /// The program will be executed within the current login session
        /// if there is one.
        /// </note>
        /// </summary>
        /// <param name="path">The program path.</param>
        /// <param name="args">Optional program arguments.</param>
        /// <returns>An <see cref="ExecuteResponse"/> with the command results.</returns>
        public ExecuteResponse SudoExecute(string path, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path), nameof(path));

            return NeonHelper.ExecuteCapture("wsl.exe",
                new object[]
                {
                    "--distribution", this.Name,
                    "--user", this.User,
                    "--",
                    "sudo", path,
                    args
                },
                outputEncoding: Encoding.UTF8);
        }

        /// <summary>
        /// Executes a program within the distribution as a specifc user under SUDO.
        /// </summary>
        /// <param name="path">The program path.</param>
        /// <param name="user">The user.</param>
        /// <param name="args">Optional pprogram arguments.</param>
        /// <returns>An <see cref="ExecuteResponse"/> with the command results.</returns>
        public ExecuteResponse SudoExecuteAs(string user, string path, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(user), nameof(user));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path), nameof(path));

            return NeonHelper.ExecuteCapture("wsl.exe",
                new object[]
                {
                    "--distribution", this.Name,
                    "--user", user,
                    "--",
                    "sudo", path,
                    args
                },
                outputEncoding: Encoding.UTF8);
        }

        /// <summary>
        /// <para>
        /// Executes a bash script on the distribution as the current user.
        /// </para>
        /// <note>
        /// The script will be executed within the current login session
        /// if there is one.
        /// </note>
        /// </summary>
        /// <param name="script">The script text.</param>
        /// <returns>An <see cref="ExecuteResponse"/> with the command results.</returns>
        public ExecuteResponse ExecuteScript(string script)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(script), nameof(script));

            using (var tempFile = new TempFile(folder: TempFolder))
            {
                var linuxScriptPath = ToLinuxPath(tempFile.Path);

                File.WriteAllText(tempFile.Path, NeonHelper.ToLinuxLineEndings(script));

                return Execute("bash", linuxScriptPath);
            }
        }

        /// <summary>
        /// Executes a bash script on the distribution as a specific user.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="script">The script text.</param>
        /// <returns>An <see cref="ExecuteResponse"/> with the command results.</returns>
        public ExecuteResponse ExecuteScriptAs(string user, string script)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(user), nameof(user));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(script), nameof(user));

            using (var tempFile = new TempFile(folder: TempFolder))
            {
                File.WriteAllText(tempFile.Path, NeonHelper.ToLinuxLineEndings(script));

                return ExecuteAs(user, "bash", ToLinuxPath(tempFile.Path));
            }
        }

        /// <summary>
        /// <para>
        /// Executes a bash script as SUDO on the distribution as the current user.
        /// </para>
        /// <note>
        /// The script will be executed within the current login session
        /// if there is one.
        /// </note>
        /// </summary>
        /// <param name="script">The script text.</param>
        /// <returns>An <see cref="ExecuteResponse"/> with the command results.</returns>
        public ExecuteResponse SudoExecuteScript(string script)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(script), nameof(script));

            using (var tempFile = new TempFile(folder: TempFolder))
            {
                var linuxScriptPath = ToLinuxPath(tempFile.Path);

                File.WriteAllText(tempFile.Path, NeonHelper.ToLinuxLineEndings(script));

                return Execute("sudo", "bash", linuxScriptPath);
            }
        }

        /// <summary>
        /// Executes a bash script as SUDO on the distribution as a specific user.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="script">The script text.</param>
        /// <returns>An <see cref="ExecuteResponse"/> with the command results.</returns>
        public ExecuteResponse SudoExecuteScriptAs(string user, string script)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(user), nameof(user));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(script), nameof(script));

            using (var tempFile = new TempFile(folder: TempFolder))
            {
                File.WriteAllText(tempFile.Path, NeonHelper.ToLinuxLineEndings(script));

                return ExecuteAs(user, "sudo", "bash", ToLinuxPath(tempFile.Path));
            }
        }

        /// <summary>
        /// Maps a fully qualified filesystem path within the Linux distribution
        /// to the corresponding Windows filesystem path on the host machine.
        /// </summary>
        /// <param name="linuxPath">The fully qualified internal Linux path.</param>
        /// <returns>The corresponding Linux path.</returns>
        /// <remarks>
        /// <note>
        /// This assumes that the internal Linux path includes only characters
        /// supported by Windows.
        /// </note>
        /// </remarks>
        public string ToWindowsPath(string linuxPath)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(linuxPath), nameof(linuxPath));
            Covenant.Requires<ArgumentNullException>(linuxPath.First() == '/', nameof(linuxPath));

            return $@"\\wsl$\{Name}{linuxPath.Replace('/', '\\')}";
        }

        /// <summary>
        /// Maps a fully qualified Windows host filesystem path to the corresponding
        /// path within the Linux distribution.
        /// </summary>
        /// <param name="windowsPath">The fully qualified host Windows path.</param>
        /// <returns>The corresponding Windows host path.</returns>
        public string ToLinuxPath(string windowsPath)
        {
            // [hostPath] needs to look like: <drive>:\ ...

            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(windowsPath), nameof(windowsPath));
            Covenant.Requires<ArgumentException>(windowsPath.Length >= 3 && char.IsLetter(windowsPath[0]) && windowsPath[1] == ':' && windowsPath[2] == '\\', nameof(windowsPath));

            return $"/mnt/{char.ToLowerInvariant(windowsPath[0])}{windowsPath.Substring(2).Replace('\\', '/')}";
        }

        /// <summary>
        /// Creates a text file at the specifid path within the distribution.  The file will
        /// be created with the current <see cref="User"/> as the owner by default by this
        /// can be overridden.
        /// </summary>
        /// <param name="path">The target path.</param>
        /// <param name="text">The text to be written.</param>
        /// <param name="owner">Optionally overrides the current user when setting the file owner.</param>
        /// <param name="permissions">Optionally specifies the linux file permissions.</param>
        /// <param name="toLinuxText">Optionally convertes conversion of Windows (CRLF) line endings to the Linux standard (LF).</param>
        public void UploadFile(string path, string text, string owner = null, string permissions = null, bool toLinuxText = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path), nameof(path));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(text), nameof(text));

            var windowsPath = ToWindowsPath(path);

            if (toLinuxText)
            {
                text = NeonHelper.ToLinuxLineEndings(text);
            }

            File.WriteAllText(windowsPath, text);

            if (!string.IsNullOrEmpty(permissions))
            {
                SudoExecute("chown", owner ?? User, path).EnsureSuccess();
                SudoExecute("chgrp", owner ?? User, path).EnsureSuccess();  // We're assuming the user's group is the same as the user name
                SudoExecute("chmod", permissions, path).EnsureSuccess();
            }
        }
    }
}
