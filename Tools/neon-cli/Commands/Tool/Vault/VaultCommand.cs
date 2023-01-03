//-----------------------------------------------------------------------------
// FILE:	    VaultCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Common;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>vault</b> command.
    /// </summary>
    [Command]
    public class VaultCommand : CommandBase
    {
        private const string usage = @"
Manages encrypted files.

USAGE:

    neon tool vault
    neon tool vault create PATH [PASSWORD-NAME]
    neon tool vault decrypt SOURCE TARGET
    neon tool vault edit PATH
    neon tool vault encrypt PATH [--password=PASSWORD-NAME]
    neon tool vault encrypt SOURCE TARGET [PASSWORD-NAME]
    neon tool vault password-name PATH

REMARKS:

[neon] file encryption uses AES 256-bit encryption to secure files on disk
such that they may be persisted to source control repositories and other
locations that may not be very secure.  This is a nice way to distribute
sensitive files to developers, operators, and CI/CD build environments.

This works by referencing named passwords stored in a secure location.
These passwords are stored in the current user folder at:

    %USERPROFILE%\.neonkube\passwords   - on Windows
    $HOME/.neonkube/passwords           - on OS/X and Linux

These files are encrypted at rest when possible (e.g. for Windows 10/PRO+).
In the future, we may support two-factor authentication devices such as
YubiKey to harden this even more.

Each password file holds the password itself and the password file name
is used as the name to identify each password.  The file commands use
password names to explicitly or implictly identify the password to be
used to manage specific files.  For example:

    neon tool vault create foo.txt default

creates an encrypted file named [foo.txt] using the password named [default].
The [default] password must already exist in the current user's passwords
folder.  Passwords can be managed via:

    neon tool password export [PASSWORD-NAMES...] ZIPFILE
    neon tool password import ZIPFILE
    neon tool password rm PASSWORD-NAME
    neon tool set PASSWORD-NAME [PASSWORD]

where the [set/rm] commands provide for creating and deleting passwords and
the [export/import] commands provide a way to share passwords using encrypted
ZIP files.

A password name must be specified when creating or encrypting a file.  This
may be done by explicitly passing the password name on the command line
or implicitly by creating a [.password-name] file in the same directory as
the target file or in one of its ancestor directories.

[neon] will search the current and ancestor directores up to the file 
system root for the first [.password-name] file when no password name is 
explicitly specified.  The [.password-name] file simply holds the name 
of the password.  This is a convenient way to specified default passwords
for a project.

Vault commands like [create] and [edit] will decrypt and launch a text
editor so that the file can be edited.  The default platform editor will
be launched (NotePad.exe for Windows or Vim for OS/X and Linux).  You can
customize the editor by setting the EDITOR environment variable to the path
to the editor executable file.
";
        /// <inheritdoc/>
        public override string[] Words => new string[] { "tool", "vault" }; 

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override async Task RunAsync(CommandLine commandLine)
        {
            Console.WriteLine(usage);
            Program.Exit(0);
            await Task.CompletedTask;
        }
    }
}
