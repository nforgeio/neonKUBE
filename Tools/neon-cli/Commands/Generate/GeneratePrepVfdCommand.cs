//-----------------------------------------------------------------------------
// FILE:	    GeneratePrepVfdCommand.cs
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
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.ModelGen;
using Neon.Common;
using Neon.Kube;
using Neon.Net;
using Neon.SSH;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>generate prep-vfd</b> command.
    /// </summary>
    public class GeneratePrepVfdCommand : CommandBase
    {
        private const string usage = @"
Generates a floppy disk image file that can act as a template for the 
[neon-node-prep.sh] script that will be executed on first boot when
provisioning neonKUBE nodes on Hyper-V, XenServer, as well as other
virtialization environments.

USAGE:

    neon generate prep-vfd IP-ADDRESS VFD-PATH

ARGUMENTS:

    IP-ADDRESS      - Address of a running Ubuntu prep VM
    VFD-PATH        - Path to the output virtual floppy file (gzip+hex).

REMARKS:

This command is used rarely by neonKUBE maintainers for rebuilding
the [neon-node-prep] floppy image file.  This image file is actually
embedded directy into the [Neon.Kube.KubeHelper] class which handles
the writing data to the image as required.

The output file is actually compressed via GZIP and then encoded as
multi-line C# HEX bytes to make it easy to paste into source code.

You'll need to have an Ubuntu prep virtual machine already provisioned
and running.  Wou can do this by following these instructions

    Ubuntu-20.04 XenServer Template.docx

and stopping after rebooting the VM after Ubuntu setup.  You'll need
to pass the IP-ADDRESS for the VM.  This script connects with the standard
neonKUBE VM credentials creates the disk image and then writes the GZIP/HEX
output to VFD-PATH.

Right now, this command simply creates a file named [neon-node-prep.sh]
on the floppy and writes 100 512B blocks to the file, the first block 
filled with 0x01, the second with 0x02,... and the last block with 0x64
(100).  This allows the script file to hold 51,200 bytes.

The idea is that cluster prepare will update the [neon-node-prep.sh] file
by locating these blocks, filling them all with 0x0A (NEWLINE) bytes and
then go back and writing the data to the blocks in order.  This is a bit
of a hack that assumes a text file where NEWLINEs at the end don't matter.
";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "generate", "prep-vfd" };

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override async Task RunAsync(CommandLine commandLine)
        {
            if (commandLine.Arguments.Length != 2)
            {
                Help();
                Program.Exit(1);
            }

            var ipAddress   = NetHelper.ParseIPv4Address(commandLine.Arguments.ElementAtOrDefault(0));
            var vfdPath     = commandLine.Arguments.ElementAtOrDefault(1);
            var credentials = SshCredentials.FromUserPassword("sysadmin", "sysadmin0000");

            using (var server = new NodeSshProxy<string>("vm-fhd", ipAddress, credentials))
            {
                server.WaitForBoot();

                // Create the raw disk device.

                server.SudoCommand("dd bs=512 count=2880 if=/dev/zero of=/tmp/floppy.img");

                // Initialize the VFAT floppy image (with "cidata" as the volume label).

                server.SudoCommand("mkfs.vfat -n cidata /tmp/floppy.img");

                // Mount the drive and write the data file.

                server.SudoCommand("mkdir -p /media/floppy");
                server.SudoCommand("mount -o loop /tmp/floppy.img /media/floppy/");

                const int blockCount = 100;
                const int blockSize  = 512;

                var blocks = new byte[blockCount * blockSize];

                for (int block = 0; block < blockCount; block++)
                {
                    for (int i = 0; i < blockSize; i++)
                    {
                        blocks[block * blockSize + i] = (byte)(block + 1);
                    }
                }

                server.UploadBytes("/media/floppy/neon-node-prep.sh", blocks);
                server.SudoCommand("umount /media/floppy");

                // The [/tmp/floppy.img] file should hold the image now.  We'll
                // download it, GZIP and then convert it to C# HEX before writing
                // it to the output.

                var vfdImageBytes = server.DownloadBytes("/tmp/floppy.img");
                var vfdImageGzip  = NeonHelper.GzipBytes(vfdImageBytes);
                var sbHex         = new StringBuilder();
                var index         = 0;

                while (index < vfdImageGzip.Length)
                {
                    // We're going to write out lines with up to 30 bytes each.

                    var nextPos = index + 30;

                    if (nextPos > vfdImageGzip.Length)
                    {
                        nextPos = vfdImageGzip.Length;
                    }

                    for (int i = index; i < nextPos; i++)
                    {
                        sbHex.Append($"0x{NeonHelper.ToHex(vfdImageGzip[i])}, ");
                    }

                    sbHex.AppendLine();

                    index = nextPos;
                }

                File.WriteAllText(vfdPath, sbHex.ToString());

                // Cleanup

                server.SudoCommand("rm -rf /media/floppy");
                server.SudoCommand("rm /tmp/floppy.img");
            }

            Program.Exit(0);
            await Task.CompletedTask;
        }
    }
}
