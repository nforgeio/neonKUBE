//-----------------------------------------------------------------------------
// FILE:	    PrepareCommand.cs
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
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Kube;
using Neon.XenServer;
using Remotion.Linq.Parsing.Structure.IntermediateModel;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>prepare</b> command.
    /// </summary>
    public class PrepareCommand : CommandBase
    {
        private const string usage = @"
Performs basic cluster provisioning and management.

USAGE:

    neon prepare node-template OPTION ADDRESS

ARGUMENTS:

    ADDRESS         - IP address of the template VM.

OPTIONS:

    One of these options must be specified:

    --hyperv        - Initialize for Hyper-V
    --xenserver     - Initialize for XenServer

    Other options:

    --host-address  - Specifies the IP address for the machine hosting
                      the VM being used to create the template.  This is
                      required for [--xenserver] mode.

    --host-username - Specifies the username required to login into the
                      machine hosting the VM used to create the template.
                      This defaults to [root].

    --host-password - Specifies the password required to login into the
                      machine hosting the VM used to create the template
                      this is required for [--xenserver] mode.

    --vm-name       - Identifies the VM being used to generate the node
                      template.  This defaults to the name specified in
                      the setup instructions:

                          ""xenserver-ubuntu-neon""

    --upgrade       - Upgrades an existing template VM to the latest 
                      Ubuntu distribution bits.

REMARKS:

NOTE: This command is indended for internal use.

This command is used to configure a machine with a mostly virgin Ubuntu
installation so that it will be ready for use in a neonKUBE cluster.

Requirements:

    * The virtual machine must have been prepared with a fresh Ubuntu
      server installation as described in:
      
        Ubuntu-##.## Hyper-V Template.docx
        Ubuntu-##.## XenServer Template.docx

    * The VM [sysadmin] user credentials must be known (the password
      defaults to [sysadmin0000].

    * [sudo] password prompting must be disabled.

Simply execute this command with the VM's IP address to perform the
remaining configuration and then follow the remaining manual steps 
to obtain, compress, and upload the VM disk image as the cluster 
node template.
";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "prepare", "node-template" }; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions => new string[] { "--hyperv", "--xenserver", "--host-address", "--host-username", "--host-password", "--vm-name", "--upgrade" };

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            if (commandLine.Arguments.Count() == 0)
            {
                Help();
                Program.Exit(0);
            }

            var hyperv        = commandLine.HasOption("--hyperv");
            var xenserver     = commandLine.HasOption("--xenserver");
            var vmHost        = hyperv ? "Hyper-V" : "XenServer";
            var vmName        = commandLine.GetOption("--vm-name", "xenserver-ubuntu-neon");
            var hostAddress   = commandLine.GetOption("--host-address");
            var hostUsername  = commandLine.GetOption("--host-username", "root");
            var hostPassword  = commandLine.GetOption("--host-password");
            var upgrade       = commandLine.GetFlag("--upgrade");
            var hostIpAddress = (IPAddress)null;

            if (!upgrade)
            {
                if (!hyperv && !xenserver)
                {
                    Console.Error.WriteLine("**** ERROR: One of [--hyperv] or [--xenserver] must be specified.");
                    Program.Exit(1);
                }
                else if (hyperv && xenserver)
                {
                    Console.Error.WriteLine("**** ERROR: Only one of [--hyperv] or [--xenserver] can be specified.");
                    Program.Exit(1);
                }
            }

            if (xenserver && !upgrade)
            {
                if (string.IsNullOrEmpty(hostAddress))
                {
                    Console.Error.WriteLine("**** ERROR: [--host-address] must be specified for [--xenserver].");
                    Program.Exit(1);
                }

                if (string.IsNullOrEmpty(hostUsername))
                {
                    Console.Error.WriteLine("**** ERROR: [--host-username] must be specified for [--xenserver].");
                    Program.Exit(1);
                }

                if (string.IsNullOrEmpty(hostPassword))
                {
                    Console.Error.WriteLine("**** ERROR: [--host-password] must be specified for [--xenserver].");
                    Program.Exit(1);
                }

                if (!IPAddress.TryParse(hostAddress, out hostIpAddress))
                {
                    Console.Error.WriteLine($"**** ERROR: [{hostAddress}] is not a valid IP address.");
                    Program.Exit(1);
                }
            }

            var address = commandLine.Arguments.ElementAtOrDefault(0);

            if (string.IsNullOrEmpty(address))
            {
                Console.Error.WriteLine("**** ERROR: ADDRESS argument is required.");
                Program.Exit(1);
            }

            if (!IPAddress.TryParse(address, out var ipAddress))
            {
                Console.Error.WriteLine($"**** ERROR: [{address}] is not a valid IP address.");
                Program.Exit(1);
            }

            Program.MachineUsername = Program.CommandLine.GetOption("--machine-username", "sysadmin");
            Program.MachinePassword = Program.CommandLine.GetOption("--machine-password", "sysadmin0000");

            Covenant.Assert(Program.MachineUsername == KubeConst.SysAdminUser);

            // Handle upgrade only here.

            if (upgrade)
            {
                Console.WriteLine();
                Console.WriteLine($"** Upgrade {vmHost} VM Template ***");
                Console.WriteLine();

                using (var server = Program.CreateNodeProxy<string>("vm-template", address, ipAddress, appendToLog: false))
                {
                    Console.WriteLine($"Login:    [{KubeConst.SysAdminUser}]");
                    server.WaitForBoot();

                    Console.WriteLine("Run:      apt-get update");
                    server.SudoCommand("apt-get update");

                    Console.WriteLine("Run:      apt-get dist-upgrade -yq");
                    server.SudoCommand("apt-get dist-upgrade -yq");

                    Console.WriteLine("Run:      apt-get clean");
                    server.SudoCommand("apt-get clean");

                    Console.WriteLine("Run:      rm -rf /var/lib/dhcp/*");
                    server.SudoCommand("rm -rf /var/lib/dhcp/*");

                    Console.WriteLine("Run:      sfill -fllz /");
                    server.SudoCommand("sfill -fllz /");

                    Console.WriteLine("Shutdown: VM");
                    server.Shutdown();
                }

                Console.WriteLine();
                Console.WriteLine("*** Node template is upgraded ***");
                return;
            }

            // Perform the full template initialization.

            Console.WriteLine();
            Console.WriteLine($"** Prepare {vmHost} VM Template ***");
            Console.WriteLine();

            using (var server = Program.CreateNodeProxy<string>("vm-template", address, ipAddress, appendToLog: false))
            {
                // Disable sudo password prompts.

                Console.WriteLine("Disable:  [sudo] password");
                server.DisableSudoPrompt(Program.MachinePassword);
            }

            using (var server = Program.CreateNodeProxy<string>("vm-template", address, ipAddress, appendToLog: false))
            {
                Console.WriteLine($"Login:    [{KubeConst.SysAdminUser}]");
                server.WaitForBoot();

                // Install required packages:

                Console.WriteLine("Install:  packages");
                server.SudoCommand("apt-get update", RunOptions.FaultOnError);
                server.SudoCommand("apt-get install -yq --allow-downgrades zip secure-delete", RunOptions.FaultOnError);

                // Disable SWAP by editing [/etc/fstab] to remove the [/swap.img] line:

                var sbFsTab = new StringBuilder();

                using (var reader = new StringReader(server.DownloadText("/etc/fstab")))
                {
                    foreach (var line in reader.Lines())
                    {
                        if (!line.Contains("/swap.img"))
                        {
                            sbFsTab.AppendLine(line);
                        }
                    }
                }

                Console.WriteLine("Disable:  SWAP");
                server.UploadText("/etc/fstab", sbFsTab, permissions: "644", owner: "root:root");

                // We need to relocate the [sysadmin] UID/GID to 1234 so we
                // can create the [container] user and group at 1000.  We'll
                // need to create a temporary user with root permissions to
                // delete and then recreate the [sysadmin] account.

                Console.WriteLine("Create:   [temp] user");

                var tempUserScript =
$@"#!/bin/bash

# Create the [temp] user.

useradd --uid 5000 --create-home --groups root temp
echo 'temp:{Program.MachinePassword}' | chpasswd
adduser temp sudo
chown temp:temp /home/temp
";
                server.SudoCommand(CommandBundle.FromScript(tempUserScript), RunOptions.FaultOnError);
                Console.WriteLine($"Logout");
            }

            // We need to reconnect with the new temporary account so
            // we can relocate the [sysadmin] user to its new UID.

            Program.MachineUsername = "temp";

            using (var server = Program.CreateNodeProxy<string>("vm-template", address, ipAddress, appendToLog: false))
            {
                Console.WriteLine($"Login:    [temp]");
                server.WaitForBoot(createHomeFolders: true);

                // Beginning with Ubuntu 20.04 we're seeing systemd/(sd-pam) processes 
                // hanging around for a while for the [temp] process which prevents us 
                // from deleting the [temp] user below.  We're going to handle this by
                // killing any [temp] user processes first.

                Console.WriteLine("Kill:     [sysadmin] processes");
                server.SudoCommand("pkill -u sysadmin");

                // Relocate the [sysadmin] user to from [uid=1000:gid=1000} to [1234:1234]:

                var sysadminUserScript =
$@"#!/bin/bash

# Update all file references from the old to new [sysadmin]
# user and group IDs:

find / -group 1000 -exec chgrp -h {KubeConst.SysAdminGroup} {{}} \;
find / -user 1000 -exec chown -h {KubeConst.SysAdminUser} {{}} \;

# Relocate the [sysadmin] UID and GID:

groupmod --gid {KubeConst.SysAdminGID} {KubeConst.SysAdminGroup}
usermod --uid {KubeConst.SysAdminUID} --gid {KubeConst.SysAdminGID} --groups root,sysadmin,sudo {KubeConst.SysAdminUser}
";

                Console.WriteLine("Relocate: [sysadmin] user IDs");
                server.SudoCommand(CommandBundle.FromScript(sysadminUserScript), RunOptions.FaultOnError);
                Console.WriteLine($"Logout");
            }

            // We need to reconnect again with [sysadmin] so we can remove
            // the [temp] user, create the [container] user and then
            // wrap things up.  Beginning with Ubuntu 20.04 we're seeing
            // [systemd/(sd-pam)] processes hanging around for a while for
            // the [temp] user which prevents us from deleting the [temp]
            // user below.
            //
            // We're going to work around this be rebooting by killing all
            // [sysadmin] processes.

            Program.MachineUsername = KubeConst.SysAdminUser;

            using (var server = Program.CreateNodeProxy<string>("vm-template", address, ipAddress, appendToLog: false))
            {
                Console.WriteLine($"Login:    [{KubeConst.SysAdminUser}]");
                server.WaitForBoot();

                // Ensure that the owner and group for files in the [sysadmin]
                // home folder are correct.

                Console.WriteLine("Set:      [sysadmin] home folder owner");
                server.SudoCommand($"chown -R {KubeConst.SysAdminUser}:{KubeConst.SysAdminGroup} .*", RunOptions.FaultOnError);

                // Beginning with Ubuntu 20.04 we're seeing systemd/(sd-pam) processes 
                // hanging around for a while for the [temp] process which prevents us 
                // from deleting the [temp] user below.  We're going to handle this by
                // killing any [temp] user processes first.

                Console.WriteLine("Kill:     [temp] user processes");
                server.SudoCommand("pkill -u temp");

                // Remove the [temp] user.

                Console.WriteLine("Remove:   [temp] user");
                server.SudoCommand($"rm -rf /home/temp", RunOptions.FaultOnError);

                // Create the [container] user with no home directory.  This
                // means that the [container] user will have no chance of
                // logging into the machine.

                Console.WriteLine("Create:   [container] user", RunOptions.FaultOnError);
                server.SudoCommand($"useradd --uid {KubeConst.ContainerUID} --no-create-home {KubeConst.ContainerUser}", RunOptions.FaultOnError);

                // Configure the Linux guest integration services.

                var guestServicesScript =
@"#!/bin/bash
cat <<EOF >> /etc/initramfs-tools/modules
hv_vmbus
hv_storvsc
hv_blkvsc
hv_netvsc
EOF

apt-get install -yq --allow-downgrades linux-virtual linux-cloud-tools-virtual linux-tools-virtual
update-initramfs -u
";
                Console.WriteLine("Install:  guest integration services");
                server.SudoCommand(CommandBundle.FromScript(guestServicesScript), RunOptions.FaultOnError);

                // Virtualization host specific initialization.

                if (hyperv)
                {
                    // Clean cached packages, DHCP leases, and then zero the disk so
                    // the image will compress better.

                    var cleanScript =
@"#!/bin/bash
apt-get clean
rm -rf /var/lib/dhcp/*
sfill -fllz /
";
                    Console.WriteLine("Clean:    VM");
                    server.SudoCommand(CommandBundle.FromScript(cleanScript), RunOptions.FaultOnError);
                 
                    // Shut the the VM down so the user can compress and upload
                    // the disk image.

                    Console.WriteLine("Shutdown: VM");
                    server.Shutdown();
                }
                else if (xenserver)
                {
                    // The Hyper-V guest integration service delays booting on XenServer for 90 seconds,
                    // which is super annoying.  This service isn't necessary of course, so we're going
                    // to disable it.

                    Console.WriteLine($"Disable:  hv-kvp-daemon.service");
                    server.SudoCommand("systemctl disable hv-kvp-daemon.service");

                    // Establish an SSH connection to the XenServer host so we'll
                    // be able to mount and eject the XenServer tools ISO to the VM.

                    using (var xenHost = new XenClient(hostAddress, hostUsername, hostPassword, name: hostAddress, logFolder: KubeHelper.LogFolder))
                    {
                        // Ensure that the XenServer host version is [7.5.0].  This is the minimum host version
                        // supported by neonKUBE clusters and it's important that node templates be created on
                        // this exact version because templates created on later XenServer versions will not be
                        // backwards compatible with older host software.

                        Console.WriteLine($"Check:    XenServer host configuration");

                        var hostInfo = xenHost.GetHostInfo();

                        if (hostInfo.Version != SemanticVersion.Parse("7.5.0"))
                        {
                            Console.WriteLine();
                            Console.WriteLine($"*** ERROR: XenServer host version is [v{hostInfo.Version}].  Only [v7.5.0] is supported.");
                            Program.Exit(1);
                        }

                        //-------------------------------------
                        // Install the XenServer tools.

                        // Identify the tools storage repo holding the guest tools ISO.  We're going to
                        // assume that XenServer hosts have only one tool repository.

                        Console.WriteLine($"Insert:   [guest-tools.iso] as DVD");

                        var response = xenHost.SafeInvokeItems("sr-list", "is-tools-sr=true");

                        if (response.Items.Count == 0)
                        {
                            Console.WriteLine();
                            Console.WriteLine($"*** ERROR: Cannot locate tools storage repository.");
                            Program.Exit(1);
                        }
                        else if (response.Items.Count > 1)
                        {
                            Console.WriteLine();
                            Console.WriteLine($"*** ERROR: XenServer reports [{response.Items.Count}] tools storage repositories.");
                            Console.WriteLine($"           Only one SR is expected.");
                            Program.Exit(1);
                        }

                        var toolsSrUuid = response.Items.First()["uuid"];

                        // Identify the [guest-tools.iso] disk in the SR.

                        response = xenHost.InvokeItems("vdi-list", $"sr-uuid={toolsSrUuid}");

                        var toolsVdi = response.Items.SingleOrDefault(item => item["name-label"] == "guest-tools.iso");

                        if (toolsVdi == null)
                        {
                            Console.WriteLine();
                            Console.WriteLine($"*** ERROR: Cannot locate [guest-tools.iso] in [srUuid={toolsSrUuid}].");
                            Program.Exit(1);
                        }

                        // Identify the VM being used to create the template.

                        response = xenHost.InvokeItems("vm-list");

                        var vm = response.Items.Where(item => item["name-label"] == vmName).SingleOrDefault();

                        if (vm == null)
                        {
                            Console.WriteLine();
                            Console.WriteLine($"*** ERROR: Cannot locate VM [{vmName}].");
                            Program.Exit(1);
                        }

                        var vmUuid = vm["uuid"];

                        // Eject any disk that's already in the DVD drive.

                        xenHost.Invoke("vm-cd-eject", $"uuid={vmUuid}");

                        // Insert the [guest-tools.iso] disk in the CD/DVD drive attached to the VM.

                        xenHost.SafeInvoke("vm-cd-insert", $"uuid={vmUuid}", "cd-name=guest-tools.iso");

                        // Mount the ISO in the VM and install the tools.

                        var guestToolsScript =
@"
mount /dev/dvd /mnt
/mnt/Linux/install.sh -n
eject /dev/dvd
";
                        Console.WriteLine("Install:  XenServer Tools");
                        server.SudoCommand(CommandBundle.FromScript(guestToolsScript), RunOptions.FaultOnError);

                        // Eject the CD/DVD at the host level (we're going to ignore any errors 
                        // in case the [eject] in the script above already ejected the disk at
                        // the host level).

                        xenHost.Invoke("vm-cd-eject", $"uuid={vmUuid}");

                        // Cleanup and shutdown.

                        var cleanupScript =
@"
apt-get clean
rm -rf /var/lib/dhcp/*
sfill -fllz /
";
                        Console.WriteLine("Cleanup:  VM");
                        server.SudoCommand(CommandBundle.FromScript(cleanupScript), RunOptions.FaultOnError);

                        Console.WriteLine("Shutdown: VM");
                        xenHost.SafeInvoke("vm-shutdown", $"uuid={vmUuid}");
                    }
                }

                Console.WriteLine();
                Console.WriteLine("*** Node template is ready ***");
            }

            Program.Exit(0);
        }
    }
}
