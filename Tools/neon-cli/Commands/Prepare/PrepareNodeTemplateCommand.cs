//-----------------------------------------------------------------------------
// FILE:	    PrepareCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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

            Console.WriteLine();
            Console.WriteLine("** Prepare VM Template ***");
            Console.WriteLine();

            using (var server = Program.CreateNodeProxy<string>("vm-template", address, ipAddress, appendToLog: false))
            {
                // Disable sudo password prompts.

                Console.WriteLine("Disable [sudo] password");
                server.DisableSudoPrompt(Program.MachinePassword);
            }

            using (var server = Program.CreateNodeProxy<string>("vm-template", address, ipAddress, appendToLog: false))
            {
                Console.WriteLine($"Connect as [{KubeConst.SysAdminUser}]");
                server.WaitForBoot();

                // Install required packages:

                Console.WriteLine("Install packages");
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

                Console.WriteLine("Disable SWAP");
                server.UploadText("/etc/fstab", sbFsTab, permissions: "644", owner: "root:root");

                // We need to relocate the [sysadmin] UID/GID to 1234 so we
                // can create the [container] user and group at 1000.  We'll
                // need to create a temporary user with root permissions to
                // delete and then recreate the [sysadmin] account.

                Console.WriteLine("Create [temp] user");

                var tempUserScript =
$@"#!/bin/bash

# Create the [temp] user.

useradd --uid 5000 --create-home --groups root temp
echo 'temp:{Program.MachinePassword}' | chpasswd
adduser temp sudo
chown temp:temp /home/temp
";
                server.SudoCommand(CommandBundle.FromScript(tempUserScript), RunOptions.FaultOnError);
            }

            // We need to reconnect with the new temporary account so
            // we can relocate the [sysadmin] user to its new UID.

            Program.MachineUsername = "temp";

            using (var server = Program.CreateNodeProxy<string>("vm-template", address, ipAddress, appendToLog: false))
            {
                Console.WriteLine($"Connecting as [temp]");
                server.WaitForBoot(createHomeFolders: true);

                var sysadminUserScript =
$@"#!/bin/bash

# Update all file references from the old to new [sysadmin]
# user and group IDs:

find / -group 1000 -exec chgrp -h {KubeConst.SysAdminGroup} {{}} \;
find / -user 1000 -exec chown -h {KubeConst.SysAdminUser} {{}} \;

# Relocate the user ID:

groupmod --gid {KubeConst.SysAdminGID} {KubeConst.SysAdminGroup}
usermod --uid {KubeConst.SysAdminUID} --gid {KubeConst.SysAdminGID} --groups root,sysadmin,sudo {KubeConst.SysAdminUser}
";
                Console.WriteLine("Relocate [sysadmin] user");
                server.SudoCommand(CommandBundle.FromScript(sysadminUserScript), RunOptions.FaultOnError);
            }

            // We need to reconnect again with [sysadmin] so we can remove
            // the [temp] user and create the [container] user and then
            // wrap things up.

            Program.MachineUsername = KubeConst.SysAdminUser;

            using (var server = Program.CreateNodeProxy<string>("vm-template", address, ipAddress, appendToLog: false))
            {
                Console.WriteLine($"Connect as [{KubeConst.SysAdminUser}]");
                server.WaitForBoot();

                // Ensure that the owner and group for files in the [sysadmin]
                // home folder are correct.

                Console.WriteLine("Set [sysadmin] home folder owner");
                server.SudoCommand($"chown -R {KubeConst.SysAdminUser}:{KubeConst.SysAdminGroup} .*", RunOptions.FaultOnError);

                // Remove the [temp] user.

                Console.WriteLine("Remove [temp] user");
                server.SudoCommand($"userdel temp", RunOptions.FaultOnError);
                server.SudoCommand($"rm -rf /home/temp", RunOptions.FaultOnError);

                // Create the [container] user with no home directory.  This
                // means that the [container] user will have no chance of
                // logging into the machine.

                Console.WriteLine("Create [container] user", RunOptions.FaultOnError);
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
                Console.WriteLine("Install guest integration services");
                server.SudoCommand(CommandBundle.FromScript(guestServicesScript), RunOptions.FaultOnError);
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
                    Console.WriteLine("Clean up");
                    server.SudoCommand(CommandBundle.FromScript(cleanScript), RunOptions.FaultOnError);
                 
                    // Shut the the VM down so the user can compress and upload
                    // the disk image.

                    Console.WriteLine("Shut down");
                    server.Shutdown();

                    Console.WriteLine();
                    Console.WriteLine("*** Node template is ready ***");
                }
                else if (xenserver)
                {
                    // NOTE: We need to to install the XenCenter tools manually.

                    Console.WriteLine();
                    Console.WriteLine("*** IMPORTANT: You need to manually complete the remaining steps ***");
                }
            }

            Program.Exit(0);
        }
    }
}
