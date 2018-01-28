//-----------------------------------------------------------------------------
// FILE:	    ShellCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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

using YamlDotNet.RepresentationModel;

using Neon.Cluster;
using Neon.Common;
using Neon.IO;
using System.Diagnostics.Contracts;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>shell</b> command.
    /// </summary>
    public class ShellCommand : CommandBase
    {
        private const string usage = @"
Runs a shell command on the local workstation, in the context of environment
variables loaded from zero or more Ansible compatible YAML variable files.

USAGE:

    neon shell [---vault-password-file=PATH] [--ask-vault-pass] [VARS1] VARS2...] -- CMD...

ARGUMENTS:

    VARS#                   - Path to a YAML (Ansible) variables file.
                              (This file may optionally be encrypted)
    --                      - Indicates the start of the command/args
                              to be invoked
    CMD...                  - Command and arguments

OPTIONS:

    --vault-password-file=PATH - Optionally specifies the path to the password
                              file to be used to decrypt the variable files.
                              See the notes below discussing where password
                              files are located.

    --ask-vault-pass        - Optionally specifies that the user should
                              be prompted for the decryption password.

NOTES:

This command works by reading variables from one or more YAMLfiles, setting
these as environment variables and then executing a command in the context
of these environment variables.  The variable files are formatted as Ansible
compatible YAML, like:

    username: jeff
    password: super.dude
    mysql:
        username: dbuser
        password: dbpass

This defines two simple passwords and two passwords in a dictionary.
This will generate these environment variables:

    username=jeff
    password=super.dude
    mysql_username=dbuser
    mysql_password=dbpass

Variable files can be encrypted using the [neon ansible vault encrypt]
command and then can be used by [neon shell] and other [neon ansible]
commands.  Encryption passwords can be specified manually using a 
prompt by passing [--ask-vault-pass] or by passing the PATH to a
password file via [--vault-password-file=PATH].

Password files simply hold a password as a single line text.  [neon-cli]
expects password files to be located in a user-specific directory on your
workstation:

    %LOCALAPPDATA%\neonFORGE\neoncluster\ansible\passwords  - for Windows
    ~/.neonforge/neoncluster/ansible/passwords              - for OSX

These folders are encrypted at rest for security.  You can use the 
[neon ansible password ...] commands to manage your passwords.
";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "shell" }; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--vault-password-file", "--ask-vault-pass" }; }
        }

        /// <inheritdoc/>
        public override string SplitItem
        {
            get { return "--"; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            if (commandLine.HasHelpOption)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            if (commandLine.Arguments.Length < 2)
            {
                Console.WriteLine(usage);
                Program.Exit(1);
            }

            var commandSplit     = Program.CommandLine.Split();
            var leftCommandLine  = commandSplit.Left.Shift(1);
            var rightCommandLine = commandSplit.Right;

            if (rightCommandLine == null || rightCommandLine.Arguments.Length == 0)
            {
                Console.Error.WriteLine("*** ERROR: Expecting a [--] argument followed by a shell command.");
                Program.Exit(1);
            }

            var orgDirectory = Directory.GetCurrentDirectory();
            var shellFolder  = Path.Combine(NeonClusterHelper.GetShellFolder(), Guid.NewGuid().ToString("D"));
            var exitCode     = 1;

            try
            {
                // Create the temporary shell folder and make it the current directory.

                Directory.CreateDirectory(shellFolder);

                // We need to load variables from any files specified on the command line,
                // decrypting them as required.

                if (leftCommandLine.Arguments.Length > 0)
                {
                    bool    askVaultPass     = leftCommandLine.HasOption("--ask-vault-pass");
                    string  tempPasswordPath = null;
                    string  passwordPath     = null;

                    try
                    {
                        if (askVaultPass)
                        {
                            // Note that [--ask-vault-pass] takes presidence over [--vault-password-file].

                            var password = NeonHelper.ReadConsolePassword("Vault password: ");

                            // We need to generate a temporary password file in the
                            // Ansible passwords folder so we can pass it to the
                            // [neon ansible decrypt -- ...] command.

                            var passwordsFolder = NeonClusterHelper.GetAnsiblePasswordsFolder();
                            var guid            = Guid.NewGuid().ToString("D");

                            tempPasswordPath = Path.Combine(passwordsFolder, $"{guid}.tmp");
                            passwordPath     = tempPasswordPath.Substring(passwordsFolder.Length + 1);

                            File.WriteAllText(tempPasswordPath, password);
                        }
                        else
                        {
                            var passwordFile = leftCommandLine.GetOption("--vault-password-file");

                            if (!string.IsNullOrEmpty(passwordFile))
                            {
                                passwordPath = Path.Combine(NeonClusterHelper.GetAnsiblePasswordsFolder(), passwordFile);
                            }
                        }

                        foreach (var varFile in leftCommandLine.Arguments)
                        {
                            var varContents = File.ReadAllText(varFile, Encoding.UTF8);

                            if (varContents.StartsWith("$ANSIBLE_VAULT;"))
                            {
                                // The variable file is encrypted we're going recursively invoke
                                // the following command to decrypt it:
                                //
                                //      neon ansible vault decrypt -- --vault-password-file PASSWORD-PATH --output - VARS-PATH
                                //
                                // This uses the password to decrypt the variables to STDOUT.

                                if (string.IsNullOrEmpty(passwordPath))
                                {
                                    Console.Error.WriteLine($"*** ERROR: [{varFile}] is encrypted.  Use [--ask-vault-pass] or [--vault-password-file] to specify the password.");
                                    Program.Exit(1);
                                }

                                var result = NeonHelper.ExecuteCaptureStreams(
                                    "dotnet",
                                    new object[]
                                    {
                                        NeonHelper.GetAssemblyPath(Assembly.GetEntryAssembly()),
                                        "ansible",
                                        "vault",
                                        "--",
                                        "decrypt",
                                        $"--vault-password-file={Path.GetFileName(passwordPath)}",
                                        "--output=-",
                                        varFile
                                    });

                                if (result.ExitCode != 0)
                                {
                                    Console.Error.Write(result.AllText);
                                    Program.Exit(result.ExitCode);
                                }

                                varContents = result.OutputText;

                                // $hack(jeff.lill):
                                //
                                // The [ansible-vault decrypt --output=- FILE] command writes the decrypted
                                // data to STDOUT as expected but then follows this with the line:
                                //
                                //      Decryption successful
                                //
                                // I've reported this bug to Ansible as:
                                //
                                //      https://github.com/ansible/ansible/issues/35424
                                //
                                // I'm going to workaround this by stripping off the last line
                                // if it's "Decryption successful".

                                var badLine = "Decryption successful\r\n";
                                var badPos  = varContents.LastIndexOf(badLine);

                                if (badPos == varContents.Length - badLine.Length)
                                {
                                    varContents = varContents.Substring(0, badPos);
                                }
                            }

                            // [varContents] now holds the decrypted variables formatted as YAML.
                            // We're going to parse this and set the appropriate environment
                            // variables.

                            var yaml = new YamlStream();
                            var vars = new List<KeyValuePair<string, string>>();

                            yaml.Load(new StringReader(varContents));
                            ParseYamlVariables(vars, (YamlMappingNode)yaml.Documents.First().RootNode);

                            foreach (var variable in vars)
                            {
                                Environment.SetEnvironmentVariable(variable.Key, variable.Value);
                            }
                        }
                    }
                    finally
                    {
                        if (tempPasswordPath != null && File.Exists(tempPasswordPath))
                        {
                            File.Delete(tempPasswordPath);  // Don't need this any more.
                        }
                    }
                }

                // Execute the command in the appropriate shell for the current workstation.

                var sbCommand = new StringBuilder();

                foreach (var arg in rightCommandLine.Items)
                {
                    if (sbCommand.Length > 0)
                    {
                        sbCommand.Append(' ');
                    }

                    if (arg.Contains(' '))
                    {
                        sbCommand.Append("\"" + arg + "\"");
                    }
                    else
                    {
                        sbCommand.Append(arg);
                    }
                }

                exitCode = NeonHelper.ExecuteShell(sbCommand.ToString());
            }
            finally
            {
                // Restore the current directory.

                Directory.SetCurrentDirectory(orgDirectory);

                // Cleanup

                if (Directory.Exists(shellFolder))
                {
                    Directory.Delete(shellFolder, true);
                }
            }

            Program.Exit(exitCode);
        }

        /// <summary>
        /// Recursive parses the variables in a <see cref="YamlNode"/> and 
        /// adds the variable names and values to a list. 
        /// </summary>
        /// <param name="variables">Th target variables list.</param>
        /// <param name="yamlNode">The YAML node.</param>
        /// <param name="prefix">The variable name prefix (for nested variable definitions).</param>
        private void ParseYamlVariables(List<KeyValuePair<string, string>> variables, YamlNode yamlNode, string prefix = "")
        {
            switch (yamlNode.NodeType)
            {
                case YamlNodeType.Scalar:

                    var scalarNode = (YamlScalarNode)yamlNode;

                    variables.Add(new KeyValuePair<string, string>(prefix, scalarNode.Value));
                    break;

                case YamlNodeType.Mapping:

                    var mappingNode = (YamlMappingNode)yamlNode;

                    foreach (var child in mappingNode.Children)
                    {
                        var name = prefix;

                        if (!string.IsNullOrEmpty(name))
                        {
                            name += "_";
                        }

                        ParseYamlVariables(variables, child.Value, name + child.Key);
                    }
                    break;

                default:

                    // We're going to ignore YAML aliases and arrays

                    break;
            }
        }

        /// <inheritdoc/>
        public override ShimInfo Shim(DockerShim shim)
        {
            return new ShimInfo(isShimmed: false, ensureConnection: false);
        }
    }
}
