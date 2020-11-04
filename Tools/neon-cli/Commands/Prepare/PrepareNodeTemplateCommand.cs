//-----------------------------------------------------------------------------
// FILE:	    PrepareCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using Neon.HyperV;
using Neon.IO;
using Neon.Kube;
using Neon.Net;
using Neon.SSH;
using Neon.XenServer;

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

    --host-password - Specifies the password required to login into the
                      machine hosting the VM used to create the template.
                      This is required for [--xenserver] mode.

    --vm-name       - Identifies the VM being used to generate the node
                      template.  This is required for [--xenserver] mode.

    --update        - Applies any distribution updates to the template. 

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
        public override string[] ExtendedOptions => new string[] { "--hyperv", "--xenserver", "--host-address", "--host-password", "--vm-name", "--update" };

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override bool NeedsSshCredentials(CommandLine commandLine) => true;

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
            var vmName        = commandLine.GetOption("--vm-name");
            var hostAddress   = commandLine.GetOption("--host-address");
            var hostPassword  = commandLine.GetOption("--host-password");
            var update        = commandLine.GetFlag("--update");
            var hostIpAddress = (IPAddress)null;

            if (xenserver)
            {
                if (string.IsNullOrEmpty(hostAddress))
                {
                    Console.Error.WriteLine("**** ERROR: [--host-address] must be specified for [--xenserver].");
                    Program.Exit(1);
                }

                if (string.IsNullOrEmpty(hostPassword))
                {
                    Console.Error.WriteLine("**** ERROR: [--host-password] must be specified for [--xenserver].");
                    Program.Exit(1);
                }

                if (string.IsNullOrEmpty(vmName))
                {
                    Console.Error.WriteLine("**** ERROR: [--vm-name] must be specified for [--xenserver].");
                    Program.Exit(1);
                }

                if (!NetHelper.TryParseIPv4Address(hostAddress, out hostIpAddress))
                {
                    Console.Error.WriteLine($"**** ERROR: [{hostAddress}] is not a valid IP address.");
                    Program.Exit(1);
                }

                // Ensure that the named VM actually exists.

                using (var xenHost = new XenClient(hostAddress, "root", hostPassword, name: hostAddress))
                {
                    var response = xenHost.InvokeItems("vm-list");

                    var vm = response.Items.Where(item => item["name-label"] == vmName).SingleOrDefault();

                    if (vm == null)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"*** ERROR: Cannot locate VM [{vmName}].");
                        Program.Exit(1);
                    }
                }
            }

            var address = commandLine.Arguments.ElementAtOrDefault(0);

            if (string.IsNullOrEmpty(address))
            {
                Console.Error.WriteLine("**** ERROR: ADDRESS argument is required.");
                Program.Exit(1);
            }

            if (!NetHelper.TryParseIPv4Address(address, out var ipAddress))
            {
                Console.Error.WriteLine($"**** ERROR: [{address}] is not a valid IP address.");
                Program.Exit(1);
            }

            // Prepare the template.

            Console.WriteLine();
            Console.WriteLine($"** Prepare {vmHost} VM Template ***");
            Console.WriteLine();

            using (var node = Program.CreateNodeProxy<NodeDefinition>("node-template", ipAddress, appendToLog: false))
            {
                KubeHelper.InitializeNode(node, Program.MachinePassword, update, message => Console.WriteLine(message));

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
                node.SudoCommand(CommandBundle.FromScript(guestServicesScript), RunOptions.FaultOnError);

                // Delete the pre-installed [/etc/netplan/*] files and add [no-dhcp.yaml]
                // which will effectively disable the network on first boot from the template.

                Console.WriteLine("Network:  disable DHCP");

                var initNetPlanScript =
$@"
rm /etc/netplan/*

cat <<EOF > /etc/netplan/no-dhcp.yaml
# This file is used to disable the network when a new VM created from
# a template is booted.  The [neon-node-prep] service handles network
# provisioning in conjunction with the cluster prepare step.
#
# Cluster prepare inserts a virtual DVD disc with a script that
# handles the network configuration which [neon-node-prep] will
# execute.

network:
  version: 2
  renderer: networkd
  ethernets:
    eth0:
      dhcp4: no
EOF
";
                node.SudoCommand(CommandBundle.FromScript(initNetPlanScript), RunOptions.FaultOnError);

                // We're going to disable [cloud-init] because we couldn't get it to work with
                // the NoCloud datasource.  There were just too many moving parts and it was 
                // really hard to figure out what [cloud-init] was doing or not doing.  Mounting
                // a DVD with a script that [neon-node-prep] executes is just as flexible
                // and is much easier to understand.

                Console.WriteLine("Disable:  [cloud-init]");

                var disableCloudInitScript =
$@"
touch /etc/cloud/cloud-init.disabled
";
                node.SudoCommand(CommandBundle.FromScript(disableCloudInitScript), RunOptions.FaultOnError);

                // We're going to stop and mask the [snapd.service] because we don't want it
                // automatically updating stuff.

                Console.WriteLine("Disable:  [snapd.service]");

                node.Status = "disable: [snapd.service]";

                var disableSnapScript =
@"
# Stop and mask [snapd.service] if it's not already masked.

systemctl status --no-pager snapd.service

if [ $? ]; then
    systemctl stop snapd.service
    systemctl mask snapd.service
fi
";
                node.SudoCommand(CommandBundle.FromScript(disableSnapScript), RunOptions.FaultOnError);

                // Install and configure the [neon-node-prep] service.  This is a simple script
                // that is configured to run as a oneshot systemd service before networking is
                // started.  This is currently used to configure the node's static IP address
                // configuration on first boot, so we don't need to rely on DHCP (which may not
                // be available in some environments).
                //
                // [neon-node-prep] is intended to run the first time a node is booted after
                // being created from a template.  It checks to see if a special ISO with a
                // configuration script named [neon-node-prep.sh] is inserted into the VMs DVD
                // drive and when present, the script will be executed and the [/etc/neon-node-prep]
                // file will be created to indicate that the service no longer needs to do this for
                // subsequent reboots.
                //
                // NOTE: The script won't create the [/etc/neon-node-prep] when the script
                //       ISO doesn't exist for debugging purposes.

                Console.WriteLine("Install:  [neon-node-prep] service");

                var neonNodePrepScript =
$@"# Ensure that the neon binary folder exists.

mkdir -p {KubeNodeFolders.Bin}

# Create the systemd unit file.

cat <<EOF > /etc/systemd/system/neon-node-prep.service

[Unit]
Description=neonKUBE one-time node preparation service 
After=systemd-networkd.service

[Service]
Type=oneshot
ExecStart={KubeNodeFolders.Bin}/neon-node-prep.sh
RemainAfterExit=false
StandardOutput=journal+console

[Install]
WantedBy=multi-user.target
EOF

# Create the service script.

cat <<EOF > {KubeNodeFolders.Bin}/neon-node-prep.sh
#!/bin/bash
#------------------------------------------------------------------------------
# FILE:	        neon-node-prep.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
#
# Licensed under the Apache License, Version 2.0 (the ""License"");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an ""AS IS"" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
#
# This script is run early during node boot before the netork is configured
# as a poor man's way for neonKUBE cluster setup to configure the network
# without requiring DHCP.  Here's how this works:
#
#       1. neonKUBE cluster setup creates a node VM from a template.
#
#       2. Setup creates a temporary ISO (DVD) image with a script named 
#          [neon-node-prep.sh] on it and uploads this to the Hyper-V
#          or XenServer host machine.
#
#       3. Setup inserts the VFD into the VM's DVD drive and starts the VM.
#
#       4. The VM boots, eventually running this script (via the
#          [neon-node-prep] service).
#
#       5. This script checks whether a DVD is present, mounts
#          it and checks it for the [neon-node-prep.sh] script.
#
#       6. If the DVD and script file are present, this service will
#          execute the script via Bash, peforming any required custom setup.
#          Then this script creates the [/etc/neon-node-prep] file which 
#          prevents the service from doing anything during subsequent node 
#          reboots.
#
#       7. The service just exits if the DVD and/or script file are 
#          not present.  This shouldn't happen in production but is useful
#          for script debugging.

# Run the prep script only once.

if [ -f /etc/neon-node-prep ] ; then
    echo ""INFO: Machine is already prepared.""
    exit 0
fi

# Check for the DVD and prep script.

mkdir -p /media/neon-node-prep

if [ ! $? ] ; then
    echo ""ERROR: Cannot create DVD mount point.""
    rm -rf /media/neon-node-prep
    exit 1
fi

mount /dev/dvd /media/neon-node-prep

if [ ! $? ] ; then
    echo ""WARNING: No DVD is present.""
    rm -rf /media/neon-node-prep
    exit 0
fi

if [ ! -f /media/neon-node-prep/neon-node-prep.sh ] ; then
    echo ""WARNING: No [neon-node-prep.sh] script is present on the DVD.""
    rm -rf /media/neon-node-prep
    exit 0
fi

# The script file is present so execute it.  Note that we're
# passing the path where the DVD is mounted as a parameter.

echo ""INFO: Running [neon-node-prep.sh]""
bash /media/neon-node-prep/neon-node-prep.sh /media/neon-node-prep

# Unmount the DVD and cleanup.

echo ""INFO: Cleanup""
umount /media/neon-node-prep
rm -rf /media/neon-node-prep

# Disable any future node prepping.

touch /etc/neon-node-prep
EOF

chmod 744 {KubeNodeFolders.Bin}/neon-node-prep.sh

# Configure [neon-node-prep] to start at boot.

systemctl enable neon-node-prep
systemctl daemon-reload
";
                node.SudoCommand(CommandBundle.FromScript(neonNodePrepScript), RunOptions.FaultOnError);

                // Virtualization host specific initialization.

                if (hyperv)
                {
                    // Clean cached packages, DHCP leases, and then zero the disk so
                    // the image will compress better.

                    var cleanScript =
@"#!/bin/bash
cloud-init clean
apt-get clean
rm -rf /var/lib/dhcp/*
sfill -fllz /
";
                    Console.WriteLine("Clean:    VM");
                    node.SudoCommand(CommandBundle.FromScript(cleanScript), RunOptions.FaultOnError);
                 
                    // Shut the the VM down so the user can compress and upload
                    // the disk image.

                    Console.WriteLine("Shutdown: VM");
                    node.Shutdown();
                }
                else if (xenserver)
                {
                    // The Hyper-V guest integration service delays booting on XenServer for 90 seconds,
                    // which is super annoying.  This service isn't necessary of course, so we're going
                    // to disable it.

                    Console.WriteLine($"Disable:  hv-kvp-daemon.service");
                    node.SudoCommand("systemctl disable hv-kvp-daemon.service");

                    // Establish an SSH connection to the XenServer host so we'll
                    // be able to mount and eject the XenServer tools ISO to the VM.

                    Program.MachinePassword = hostPassword;

                    using (var xenHost = new XenClient(hostAddress, "root", hostPassword, name: hostAddress, logFolder: KubeHelper.LogFolder))
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
mkdir -p /media/guest-tools
mount /dev/dvd /media/guest-tools
/media/guest-tools/Linux/install.sh -n
eject /dev/dvd
rm -rf /media/guest-tools
";
                        Console.WriteLine("Install:  XenServer Tools");
                        node.SudoCommand(CommandBundle.FromScript(guestToolsScript), RunOptions.FaultOnError);

                        // Eject the CD/DVD at the host level (we're going to ignore any errors 
                        // in case the [eject] in the script above already ejected the disk at
                        // the host level).

                        xenHost.Invoke("vm-cd-eject", $"uuid={vmUuid}");

                        // Cleanup and shutdown.

                        var cleanupScript =
@"
cloud-init clean
apt-get clean
rm -rf /var/lib/dhcp/*
sfill -fllz /
";
                        Console.WriteLine("Cleanup:  VM");
                        node.SudoCommand(CommandBundle.FromScript(cleanupScript), RunOptions.FaultOnError);

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
