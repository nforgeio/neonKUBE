//-----------------------------------------------------------------------------
// FILE:	    FolderCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Hive;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>folder</b> command.
    /// </summary>
    public class FolderCommand : CommandBase
    {
        private const string usage = @"
Prints the fully qualified path to the specified neonHIVE workstation
folder or optionally opens the folder on your desktop.

USAGE:

    neon folder [--open] FOLDER

ARGUMENTS:

    FOLDER              - Specifies the desired folder, one of:

        ANSIBLE-ROLES   - Holds installed Ansible roles
        ANSIBLE-VAULT   - Holds Ansible password files
        LOGINS          - login information
        SETUP           - setup files
        TEMP            - temporary files
        VPN             - VPN files

OPTIONS:

    --open              - Open the folder rather than printing it
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "folder" }; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--open" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            if (commandLine.Arguments.Length == 0 || commandLine.HasHelpOption)
            {
                Help();
                Program.Exit(0);
            }

            string folder = commandLine.Arguments.First();
            string path;

            switch (folder.ToLowerInvariant())
            {
                case "ansible-roles":

                    path = HiveHelper.GetAnsibleRolesFolder();
                    break;

                case "ansible-vault":

                    path = HiveHelper.GetAnsiblePasswordsFolder();
                    break;

                case "logins":

                    path = HiveHelper.GetLoginFolder();
                    break;

                case "setup":

                    path = HiveHelper.GetVmTemplatesFolder();
                    break;

                case "temp":

                    path = Program.HiveTempFolder;
                    break;

                case "vpn":

                    path = HiveHelper.GetVpnFolder();
                    break;

                default:

                    Console.Error.WriteLine($"*** ERROR: Unexpected folder [{commandLine.Arguments.First()}].");
                    Program.Exit(1);
                    return;
            }

            if (commandLine.HasOption("--open"))
            {
                if (NeonHelper.IsWindows)
                {
                    Process.Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), $"\"{path}\"");
                }
                else if (NeonHelper.IsOSX)
                {
                    throw new NotImplementedException("$todo(jeff.lill): Implement this for OSX.");
                }
                else
                {
                    throw new NotSupportedException("[--open] option is not supported on this platform.");
                }
            }
            else
            {
                Console.Write(path);
            }
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(shimability: DockerShimability.None);
        }
    }
}
