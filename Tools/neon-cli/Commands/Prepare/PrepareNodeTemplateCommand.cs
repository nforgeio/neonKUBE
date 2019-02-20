//-----------------------------------------------------------------------------
// FILE:	    PrepareCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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

REMARKS:

NOTE: Most users will never need to use this command.

This command is used to configure a machine with a mostly virgin Ubuntu-18.04 
installation so that it will be ready for use in a neonKUBE cluster.

Requirements:

    * The virtual machine must have been prepared with a fresh Ubuntu
      server installation as described in:
      
        Ubuntu-18.04 Hyper-V Template.docx
        Ubuntu-18.04 XenServer Template.docx

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
        public override string[] ExtendedOptions => new string[] { "--hyperv", "--xenserver" };

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

            var hyperv    = commandLine.HasOption("--hyperv");
            var xenserver = commandLine.HasOption("--xenserver");

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

            using (var server = Program.CreateNodeProxy<string>("vm-template", address, ipAddress, appendToLog: false))
            {
                Console.WriteLine("Connecting...");
                server.WaitForBoot();

                // $todo(jeff.lill):
                //
                // It would be really nice to automate this step, but this needs to
                // be done without [SshProxy] because that class depends on sudo
                // password prompting already being disabled.
#if TODO
                // Disable sudo password prompts.

                var sudoPromptScript =
$@"#!/bin/bash

cat <<EOF >> /tmp/sudo-disable-prompt
echo ""%sudo    ALL=NOPASSWD: ALL"" > /etc/sudoers.d/nopasswd
EOF

chmod 770 /tmp/sudo-disable-prompt
echo {Program.MachinePassword} | sudo -S /tmp/sudo-disable-prompt
rm /tmp/sudo-disable-prompt
";

                Console.WriteLine("Disabling sudo password prompts...");
                server.SudoCommand(CommandBundle.FromScript(sudoPromptScript));
#endif

                // Install required packages.

                Console.WriteLine("Installing packages...");
                server.SudoCommand("apt-get update");
                server.SudoCommand("apt-get install -yq zip secure-delete");

                // Disable SWAP by editing [/etc/fstab] to remove the [/swap.img] line.

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

                Console.WriteLine("Disabling SWAP...");
                server.UploadText("/etc/fstab", sbFsTab, permissions: "644", owner: "root:root");

                // We need to relocate the [sysadmin] UID/GID to 1234 so we
                // can create the [container] user and group at 1000.  We'll
                // need to create a temporary user with root permissions to
                // delete and then recreate the [sysadmin] account.

                Console.WriteLine("Creating [container] user...");

                var tempUserScript =
$@"#!/bin/bash

# Create the [temp] user.

useradd --uid 5000 --create-home --groups root temp
echo 'temp:{Program.MachinePassword}' | chpasswd
adduser temp sudo

# Create the minimum set of folders required by [SshProxy].

mkdir -f /home/temp/.exec
chown temp:temp /home/temp/.exec

mkdir -f /home/temp/.download
chown temp:temp /home/temp/.download

mkdir -f /home/temp/.upload
chown temp:temp /home/temp/.upload
";
                server.SudoCommand(CommandBundle.FromScript(tempUserScript));
            }

            // We need to reconnect with the new temporary account so
            // we can relocate the [sysadmin] user to its new UID.

            Program.MachineUsername = "temp";

            using (var server = Program.CreateNodeProxy<string>("vm-template", address, ipAddress, appendToLog: false))
            {
                Console.WriteLine("Reconnecting...");
                server.WaitForBoot();

                var sysadminUserScript =
$@"#!/bin/bash

# Relocate the user ID:

usermod -u {KubeConst.SysAdminUID} {KubeConst.SysAdminUser}
groupmod -g {KubeConst.SysAdminGID} {KubeConst.SysAdminUser}

# Update all file references to the UID:

find / -user 1000 -exec chown -h {KubeConst.SysAdminUser}{{}} \;
find / -group 1000 -exec chgrp -h {KubeConst.SysAdminGID}{{}} \;
";
                Console.WriteLine("Relocating the [sysadmin] user...");
                server.SudoCommand(CommandBundle.FromScript(sysadminUserScript));
            }

            // We need to reconnect again with [sysadmin] so we can remove
            // the [temp] user and create the [container] user and then
            // wrap things up.

            Program.MachineUsername = KubeConst.SysAdminUser;

            using (var server = Program.CreateNodeProxy<string>("vm-template", address, ipAddress, appendToLog: false))
            {
                Console.WriteLine("Reconnecting...");
                server.WaitForBoot();

                // Remove the [temp] user.

                Console.WriteLine("Removing the [temp] user...");
                server.SudoCommand($"userdel --remove temp");

                // Create the [container] user with no home directory.  This
                // means that the [container] user will have no chance of
                // logging into the machine.

                Console.WriteLine("Creating the [container] user...");
                server.SudoCommand($"useradd --uid {KubeConst.ContainerUID} --no-create-home {KubeConst.ContainerUser}");

                if (hyperv)
                {
                    // Configure the Linux guest integration services.

                    var guestServicesScript =
@"#!/bin/bash
cat <<EOF >> /etc/initramfs-tools/modules
hv_vmbus
hv_storvsc
hv_blkvsc
hv_netvsc
EOF

apt-get install -yq linux-virtual linux-cloud-tools-virtual linux-tools-virtual
update-initramfs -u
";
                    Console.WriteLine("Installing guest integration services...");
                    server.SudoCommand(CommandBundle.FromScript(guestServicesScript));
                }

                // Clean cached packages, DHCP leases, and zero the disk so
                // the image will compress better.

                var cleanScript =
@"#!/bin/bash
apt-get clean
rm -rf /var/lib/dhcp/*
sfill -fllz /
";
                Console.WriteLine("Cleaning up...");
                server.SudoCommand(CommandBundle.FromScript(cleanScript));

                // Shut the the VM down so the user can compress and upload
                // the disk image.

                Console.WriteLine("Shutting down...");
                server.Shutdown();

                Console.WriteLine();
                Console.WriteLine("*** Node template is ready ***");
            }
        }
    }
}
