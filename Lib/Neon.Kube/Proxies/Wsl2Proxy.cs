//-----------------------------------------------------------------------------
// FILE:	    Wsl2Proxy.cs
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
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;
using Neon.SSH;

namespace Neon.Kube
{
    /// <summary>
    /// Handles interactions with our neonKUBE WSL2 distribution running on the
    /// local Windows workstation.  Note that this is not intended to be generally
    /// useful.
    /// </summary>
    public class Wsl2Proxy
    {
        //---------------------------------------------------------------------
        // Static members

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
        /// Attempts to enable WSL2 on the local Windows workstation.  The machine will
        /// need to be rebooted afterwards, when this succeeds. 
        /// </summary>
        /// <returns>
        /// <c>true</c> is WSL2 was able to be configured.  This will return <c>false</c> when
        /// this is not possible, i.e. when virtulaization is not enabled on the machine.
        /// </returns>
        public static bool EnableWsl2()
        {
            // $todo(jefflill): Need to implement this

            return true;
        }

        /// <summary>
        /// Lists the names of the installed WSL2 distributions, optionally limiting
        /// this to the running distributions.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<string> List(bool runningOnly = false)
        {
            var response = NeonHelper.ExecuteCapture("wsl.exe",
                new object[]
                {
                    "--list",
                    "--quiet",
                    runningOnly ? "--running" : null
                });

            response.EnsureSuccess();

            using (var reader = new StringReader(response.OutputText))
            {
                return reader.Lines().ToList();
            }
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

            var response = NeonHelper.ExecuteCapture("wsl.exe",
                new object[]
                {
                    "--import",
                    name,
                    targetFolder,
                    tarPath,
                    "--version", "2"
                });

            response.EnsureSuccess();
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

            var response = NeonHelper.ExecuteCapture("wsl.exe",
                new object[]
                {
                    "--export",
                    name,
                    tarPath
                });

            response.EnsureSuccess();
        }

        /// <summary>
        /// Terminates the named distribution if it exists and is running.
        /// </summary>
        /// <param name="name">Identifies the target WSL2 distribution.</param>
        public static void Terminate(string name)
        {
            if (List(runningOnly: true).Contains(name, StringComparer.InvariantCultureIgnoreCase))
            {
                var response = NeonHelper.ExecuteCapture("wsl.exe",
                    new object[]
                    {
                        "--terminate", name
                    });

                response.EnsureSuccess();
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
                var response = NeonHelper.ExecuteCapture("wsl.exe",
                    new object[]
                    {
                        "--unregister", name
                    });

                response.EnsureSuccess();
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private readonly string preparedStatePath = LinuxPath.Combine(KubeNodeFolders.State, "prepare", "wsl2");

        /// <summary>
        /// Constructs a proxy connected to a specific WSL2 distribution, starting the
        /// distribution by default of it's npot already running.
        /// </summary>
        /// <param name="name">Identifies the target WSL2 distribution.</param>
        /// <param name="user">Optionally connect as a non-root user.</param>
        /// <param name="noStart">Optionally leaves the distribution as not running if it's not already running.</param>
        /// <remarks>
        /// The <paramref name="user"/> passed will become the default user for subsequent
        /// proxy operations.  This may be overridden by for specific operations by specifying 
        /// a different user in the call.
        /// </remarks>
        public Wsl2Proxy(string name, string user = "root", bool noStart = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(user), nameof(user));

            if (!Exists(name))
            {
                throw new InvalidOperationException($"WSL2 distribution [{name}] does not exist.");
            }

            this.Name = name;
            this.User = user;

            if (!noStart)
            {
                Start();
            }
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
        /// <para>
        /// Returns the IPv4 address of the WSL2 distribution.  This is a private address
        /// reachable only from the host workstation and this address will likely change
        /// everytime the distribution is restarted.
        /// </para>
        /// <note>
        /// This returns <c>null</c> when the distribution hasn't been started.
        /// </note>
        /// </summary>
        public string Address { get; private set; }

        /// <summary>
        /// Determines whether the distribution is running.
        /// </summary>
        public bool IsRunning => List(runningOnly: true).Contains(Name, StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Indicates whether the distribution has already been prepared for neonKUBE.
        /// </summary>
        public bool IsPrepared
        {
            get => File.Exists(ToWindowsPath(preparedStatePath));

            set
            {
                SudoExecute("mkdir", "-p", LinuxPath.GetDirectoryName(preparedStatePath));
                SudoExecute("touch", preparedStatePath);
            }
        }

        /// <summary>
        /// Configures OpenSSH server to bind to the distribution's current private
        /// IPv4 address by adding a small config file at <b>[/etc/sshd_config/listen.conf]</b>.
        /// This is called whenever a distribution is started.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the distribution isn't running.</exception>
        /// <remarks>
        /// <para>
        /// This is required because OpenSSH server listens on [0.0.0.0:22] by default.  
        /// This will result in a port conflict when the Windows host machine is also 
        /// listening on port 22 or if an other WSL2 distro is listening there.
        /// </para>
        /// <para>
        /// To handle this, we need to bind OpenSSH to the distro's private [172.x.x.x] 
        /// address but this is complicated because this address may change across
        /// reboots.  We're going to manage this by via a [/etc/sshd_config/listen.conf]
        /// file that specifies IPv4 as the address family and the current distro
        /// IP address as the listing address.
        /// </para>
        /// </remarks>
        private void ConfigureOpenSSH()
        {
            var listenConf =
$@"# This file is regenerated whenever the WSL2 distribution is started by neonDESKTOP.
#
# This is required because OpenSSH server listens on [0.0.0.0:22] by default.  
# This will result in a port conflict when the Windows host machine is also 
# listening on port 22 or if an other WSL2 distro is listening there.
#
# To handle this, we need to bind OpenSSH to the distro's private [172.x.x.x] 
# address but this is complicated because this address may change across
# reboots.  We're going to manage this by via a [/etc/ssh/sshd_config.d/listen.conf]
# file that specifies IPv4 as the address family and the current distro
# IP address as the listing address.

# Restrict to listening on IPv4 addresses to prevent conflicts.

AddressFamily inet

# Listen on the WSL distribution's private IP address.  This will be
# updated whenever the distribution is started.

ListenAddress {this.Address}
";
            SudoExecute("mkdir",
                new object[]
                {
                    "sudo", "mkdir", "-p", "/etc/ssh/sshd_config.d"

                }).EnsureSuccess();

            UploadFile("/etc/ssh/sshd_config.d/listen.conf", listenConf, owner: "root", permissions: "644");
        }

        /// <summary>
        /// Runs the <b>ip address</b> to intitialize the distribution's private IP address.
        /// </summary>
        private void GetAddress()
        {
            // Execute [ip address] in the distribution and parse the output to extract
            // the distribution's IP address.  The output looks like somthing this:
            //
            //  1: lo: <LOOPBACK,UP,LOWER_UP> mtu 65536 qdisc noqueue state UNKNOWN group default qlen 1000
            //      link/loopback 00:00:00:00:00:00 brd 00:00:00:00:00:00
            //      inet 127.0.0.1/8 scope host lo
            //         valid_lft forever preferred_lft forever
            //      inet6 ::1/128 scope host
            //         valid_lft forever preferred_lft forever
            //  2: bond0: <BROADCAST,MULTICAST,MASTER> mtu 1500 qdisc noop state DOWN group default qlen 1000
            //      link/ether 9e:3b:88:2a:08:55 brd ff:ff:ff:ff:ff:ff
            //  3: dummy0: <BROADCAST,NOARP> mtu 1500 qdisc noop state DOWN group default qlen 1000
            //      link/ether 46:cb:71:39:ad:4a brd ff:ff:ff:ff:ff:ff
            //  4: eth0: <BROADCAST,MULTICAST,UP,LOWER_UP> mtu 1500 qdisc mq state UP group default qlen 1000
            //      link/ether 00:15:5d:c8:ee:64 brd ff:ff:ff:ff:ff:ff
            //      inet 172.25.250.51/20 brd 172.25.255.255 scope global eth0
            //         valid_lft forever preferred_lft forever
            //      inet6 fe80::215:5dff:fec8:ee64/64 scope link
            //         valid_lft forever preferred_lft forever
            //  5: sit0@NONE: <NOARP> mtu 1480 qdisc noop state DOWN group default qlen 1000
            //      link/sit 0.0.0.0 brd 0.0.0.0
            //
            // We're going to parse the address from the [inet...] line for the [eth0] interface.

            var response = Execute("ip", "address");

            response.EnsureSuccess();

            using (var reader = new StringReader(response.OutputText))
            {
                // Skip lines until we get to the [eth0] line.

                while (true)
                {
                    var line = reader.ReadLine();

                    if (line == null)
                    {
                        throw new KubeException("Cannot determine the WSL2 distribution address because the [eth0] interface is missing.");
                    }

                    if (line.Contains(" eth0: "))
                    {
                        break;
                    }
                }

                // Scan for the [inet] line and extract the IP address.

                while (true)
                {
                    var line = reader.ReadLine();

                    if (line == null || !line.StartsWith(" "))
                    {
                        throw new KubeException("Cannot determine the WSL2 distribution address because the [eth0] interface does not specify an [inet] address.");
                    }

                    line = line.Trim();

                    if (line.StartsWith("inet "))
                    {
                        var fields = line.Split(new char[] { ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);

                        Address = fields[1];
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// <para>
        /// Connects to a distribution, starting it if it's not already running.
        /// This also performs some initialization if required, including disabling
        /// SUDO password prompts. 
        /// </para>
        /// <note>
        /// You need to call this for <see cref="Address"/> to be initialized if
        /// you disabled start when constructing the instance even if the
        /// distribution is already running.
        /// </note>
        /// </summary>
        /// <returns>The IP address assigned to the distribution.</returns>
        public string Start()
        {
            if (IsPrepared)
            {
                // Get the distribution's private IP address.

                GetAddress();

                // Ensure that OpenSSH binds only to the private IP

                ConfigureOpenSSH();

                if (File.Exists(ToWindowsPath("/usr/sbin/start-systemd-namespace")))
                {
                    // The distro has already been prepared, so we can simply start
                    // systemd in its own namespace to get things rolling, if it is
                    // configured.  Note that this cannot be done as [root].

                    Covenant.Assert(User != "root", "WSL2 distro prepared for [systemd] cannot be started as [root].");

                    NeonHelper.ExecuteCapture("wsl.exe",
                        new object[]
                        {
                            "--distribution", Name,
                            "--user", User,
                            "--",
                            "source", "/usr/sbin/start-systemd-namespace"

                        }).EnsureSuccess();
                }
            }
            else
            {
                // Launch a sleep job that will run for a day to keep the
                // distro running long enough for [neon-image] to configure
                // systemd, completing the distro preparation.

                NeonHelper.ExecuteCapture("wsl.exe",
                    new object[]
                    {
                        "--distribution", Name,
                        "--",
                        "sleep", (int)TimeSpan.FromDays(1).TotalSeconds, "&"

                    }).EnsureSuccess();

                // We need to disable SUDO password prompts if we haven't already done
                // so for this distribution.  We're going to accomplish this by creating
                // a temporary script file on the Windows host side and and then executing
                // it in the distribution as root.

                // We need to do this as [root].

                var orgUser = User;

                User = "root";

                try
                {
                    using (var tempFile = new TempFile())
                    {
                        var homeFolder = HostFolders.Home(KubeConst.SysAdminUser);
                        var script =
$@"
cat <<EOF > {homeFolder}/sudo-disable-prompt
#!/bin/bash
echo ""%sudo    ALL=NOPASSWD: ALL"" > /etc/sudoers.d/nopasswd
echo ""Defaults    !requiretty""  > /etc/sudoers.d/notty
echo ""Defaults    visiblepw""   >> /etc/sudoers.d/notty

chown root /etc/sudoers.d/*
chmod 440 /etc/sudoers.d/*
EOF

chmod 770 {homeFolder}/sudo-disable-prompt

cat <<EOF > {homeFolder}/askpass
#!/bin/bash
echo {KubeConst.SysAdminPassword}
EOF
chmod 770 {homeFolder}/askpass

export SUDO_ASKPASS={homeFolder}/askpass

sudo -A {homeFolder}/sudo-disable-prompt
rm {homeFolder}/sudo-disable-prompt
rm {homeFolder}/askpass
";
                        ExecuteScript(script).EnsureSuccess();
                    }

                    // Touch the prepared state file to indicate that the distribution has been prepared
                    // by disabling SUDO password prompts, etc.

                    var setPreparedScript =
$@"
mkdir -p {KubeNodeFolders.State}
chmod 750 {KubeNodeFolders.State}
mkdir -p {LinuxPath.GetDirectoryName(preparedStatePath)}
touch {preparedStatePath}
";
                    SudoExecuteScript(setPreparedScript).EnsureSuccess();

                    // We need to remove SNAP before we configure SYSYEMD.

                    var removeSnapPath   = LinuxPath.Combine(KubeNodeFolders.State, "base", "remove-snap");
                    var removeSnapScript =
$@"
set -euo pipefail

apt-get purge snapd -yq

# Touch the [base/remove-snap] idempotent action ID for this operation.  
# This must match the action ID used within [NodeSshProxy.BaseRemoveSnap()].

mkdir -p {LinuxPath.GetDirectoryName(removeSnapPath)}
touch {removeSnapPath}
";
                    SudoExecuteScript(removeSnapScript).EnsureSuccess();

                    // Get the distribution's private IP address.

                    GetAddress();

                    // Ensure that OpenSSH binds only to the private IP.

                    ConfigureOpenSSH();
                }
                finally
                {
                    User = orgUser;
                }
            }

            return this.Address;
        }

        /// <summary>
        /// Terminates the distribution if it's running.
        /// </summary>
        public void Terminate()
        {
            Wsl2Proxy.Terminate(Name);
        }

        /// <summary>
        /// Executes a program within the distribution.
        /// </summary>
        /// <param name="path">The program path.</param>
        /// <param name="args">Optional arguments.</param>
        /// <returns>An <see cref="ExecuteResponse"/> with the command results.</returns>
        public ExecuteResponse Execute(string path, params object[] args)
        {
            return NeonHelper.ExecuteCapture("wsl.exe",
                new object[]
                {
                    "--distribution", Name,
                    "--user", User,
                    "--",
                    path,
                    args
                });
        }

        /// <summary>
        /// Executes a program within the distribution as SUDO.
        /// </summary>
        /// <param name="path">The program path.</param>
        /// <param name="args">Optional arguments.</param>
        /// <returns>An <see cref="ExecuteResponse"/> with the command results.</returns>
        public ExecuteResponse SudoExecute(string path, params object[] args)
        {
            return NeonHelper.ExecuteCapture("wsl.exe",
                new object[]
                {
                    "--distribution", Name,
                    "--user", User,
                    "--",
                    "sudo", path,
                    args
                });
        }

        /// <summary>
        /// Executes a bash script on the distribution.
        /// </summary>
        /// <param name="script">The script text.</param>
        /// <param name="args">Optional script arguments.</param>
        /// <returns>An <see cref="ExecuteResponse"/> with the command results.</returns>
        public ExecuteResponse ExecuteScript(string script, params object[] args)
        {
            using (var tempFile = new TempFile())
            {
                File.WriteAllText(tempFile.Path, NeonHelper.ToLinuxLineEndings(script));
                return Execute("bash", ToLinuxPath(tempFile.Path));
            }
        }

        /// <summary>
        /// Executes a bash script as SUDO on the distribution.
        /// </summary>
        /// <param name="script">The script text.</param>
        /// <param name="args">Optional script arguments.</param>
        /// <returns>An <see cref="ExecuteResponse"/> with the command results.</returns>
        public ExecuteResponse SudoExecuteScript(string script, params object[] args)
        {
            using (var tempFile = new TempFile())
            {
                File.WriteAllText(tempFile.Path, NeonHelper.ToLinuxLineEndings(script));
                return Execute("sudo", "bash", ToLinuxPath(tempFile.Path));
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
            Covenant.Requires<ArgumentNullException>(windowsPath.Length >= 3 && char.IsLetter(windowsPath[0]) && windowsPath[1] == ':' && windowsPath[2] == '\\', nameof(windowsPath));

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
        /// <param name="noLinuxLineEndings">Optionally disables conversion of Windows (CRLF) line endings to the Linux standard (LF).</param>
        public void UploadFile(string path, string text, string owner = null, string permissions = null, bool noLinuxLineEndings = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path), nameof(path));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(text), nameof(text));

            var windowsPath = ToWindowsPath(path);

            if (!noLinuxLineEndings)
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
