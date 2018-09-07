//-----------------------------------------------------------------------------
// FILE:	    RunCommand.cs
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

using Neon.Common;
using Neon.Hive;
using Neon.IO;
using System.Diagnostics.Contracts;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>run</b> command.
    /// </summary>
    public class RunCommand : CommandBase
    {
        private const string usage = @"
Runs a shell command on the local workstation, in the context of environment
variables loaded from zero or more Ansible compatible YAML variable files.

USAGE:

    neon run [OPTIONS] [VARS1] VARS2...] -- CMD...

ARGUMENTS:

    VARS#                   - Path to a YAML (Ansible) variables file.
                              (This file may optionally be encrypted)
    --                      - Separates the command/args to be invoked
    CMD...                  - Command and arguments

OPTIONS:

    --vault-password-file=NAME - Optionally specifies the name of the password
                              file to be used to decrypt the variable files.
                              See the notes below discussing where password
                              files are located.

    --ask-vault-pass        - Optionally specifies that the user should
                              be prompted for the decryption password.

REMARKS:

This command works by reading variables from one or more YAML files in the 
order they appear on the command line, setting these as environment variables 
and then executing a command in the context of these environment variables.
The source variable files are formatted as Ansible compatible YAML, like:

    username: jeff
    password: super.dude
    mysql:
        username: dbuser
        password: dbpass

This will generate four environment variables plus [NEON_RUN_ENV].

    username=jeff
    password=super.dude
    mysql_username=dbuser
    mysql_password=dbpass
    NEON_RUN_ENV=PATH       ** see note below

Variable files can be encrypted using the [neon ansible vault encrypt]
command and then can be used by [neon run] and other [neon ansible]
commands.  Encryption passwords can be specified manually using a 
prompt by passing [--ask-vault-pass] or by passing the PATH to a
password file via [--vault-password-file=NAME].

Password files simply hold a password as a single line text.  [neon-cli]
expects password files to be located in a user-specific directory on your
workstation:

    %LOCALAPPDATA%\neonFORGE\neonhive\ansible\passwords     - for Windows
    ~/.neonforge/neonhive/ansible/passwords                 - for OSX

These folders are encrypted at rest for security.  You can use the 
[neon ansible password ...] commands to manage your passwords.

NOTE: Ansible variables with multi-line values WILL BE IGNORED when
      setting ENVIRONMENT variables.

NOTE: The [neon run ...] command cannot be run recursively.  For example,
      you can't have one run command execute a script that executes a 
      nested run command.  This is enforced by the presence of the 
      [NEON_RUN_ENV] environment variable which references a file with
      the environment variables loaded by the current [run] command.
";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "run" }; }
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

            if (Environment.GetEnvironmentVariable("NEON_RUN_ENV") != null)
            {
                Console.Error.WriteLine("*** ERROR: [neon run ...] cannot be executed recursively.");
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
            var runFolder    = Path.Combine(HiveHelper.GetRunFolder(), Guid.NewGuid().ToString("D"));
            var runEnvPath   = Path.Combine(runFolder, "__runenv.txt");
            var exitCode     = 1;

            try
            {
                // Create the temporary run folder and make it the current directory.

                Directory.CreateDirectory(runFolder);

                // We need to load variables from any files specified on the command line,
                // decrypting them as required.

                var allVars = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

                if (leftCommandLine.Arguments.Length > 0)
                {
                    bool    askVaultPass     = leftCommandLine.HasOption("--ask-vault-pass");
                    string  tempPasswordPath = null;
                    string  passwordName     = null;

                    try
                    {
                        if (askVaultPass)
                        {
                            // Note that [--ask-vault-pass] takes presidence over [--vault-password-file].

                            var password = NeonHelper.ReadConsolePassword("Vault password: ");

                            // We need to generate a temporary password file in the
                            // Ansible passwords folder so we can pass it to the
                            // [neon ansible decrypt -- ...] command.

                            var passwordsFolder = HiveHelper.GetAnsiblePasswordsFolder();
                            var guid            = Guid.NewGuid().ToString("D");

                            tempPasswordPath = Path.Combine(passwordsFolder, $"{guid}.tmp");
                            passwordName     = Path.GetFileName(tempPasswordPath);

                            File.WriteAllText(tempPasswordPath, password);
                        }
                        else
                        {
                            passwordName = leftCommandLine.GetOption("--vault-password-file");
                        }

                        if (!string.IsNullOrEmpty(passwordName))
                        {
                            AnsibleCommand.VerifyPassword(passwordName);
                        }

                        // Decrypt the variables files, add the variables to the environment
                        // and also to the [allVars] dictionary which we'll use below to
                        // create the run variables file.

                        foreach (var varFile in leftCommandLine.Arguments)
                        {
                            var varContents = File.ReadAllText(varFile);

                            if (varContents.StartsWith("$ANSIBLE_VAULT;"))
                            {
                                // The variable file is encrypted so we're going recursively invoke
                                // the following command to decrypt it:
                                //
                                //      neon ansible vault view -- --vault-password=NAME VARS-PATH
                                //
                                // This uses the password to decrypt the variables to STDOUT.

                                if (string.IsNullOrEmpty(passwordName))
                                {
                                    Console.Error.WriteLine($"*** ERROR: [{varFile}] is encrypted.  Use [--ask-vault-pass] or [--vault-password-file] to specify the password.");
                                    Program.Exit(1);
                                }

                                var result = Program.ExecuteRecurseCaptureStreams(
                                    new object[]
                                    {
                                        "ansible",
                                        "vault",
                                        "--",
                                        "view",
                                        $"--vault-password-file={passwordName}",
                                        varFile
                                    });

                                if (result.ExitCode != 0)
                                {
                                    Console.Error.Write(result.AllText);
                                    Program.Exit(result.ExitCode);
                                }

                                varContents = NeonHelper.StripAnsibleWarnings(result.OutputText);
                            }

                            // [varContents] now holds the decrypted variables formatted as YAML.
                            // We're going to parse this and set the appropriate environment
                            // variables.
                            //
                            // Note that we're going to ignore variables with multi-line values.

                            var yaml = new YamlStream();
                            var vars = new List<KeyValuePair<string, string>>();

                            try
                            {
                                yaml.Load(varContents);
                            }
                            catch (Exception e)
                            {
                                throw new HiveException($"Unable to parse YAML from decrypted [{varFile}]: {NeonHelper.ExceptionError(e)}", e);
                            }

                            if (yaml.Documents.FirstOrDefault() != null)
                            {
                                ParseYamlVariables(vars, (YamlMappingNode)yaml.Documents.First().RootNode);
                            }

                            foreach (var variable in vars)
                            {
                                if (variable.Value != null && variable.Value.Contains('\n'))
                                {
                                    continue;   // Ignore variables with multi-line values.
                                }

                                allVars[variable.Key] = variable.Value;
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

                // We need to generate the NEON_RUN_ENV file defining the environment variables
                // loaded by the command.  This file format is compatible with the Docker
                // [run] command's [--env-file=PATH] option and will be used by nested calls to
                // [neon] to pass these variables through to the tool container as required.

                Environment.SetEnvironmentVariable("NEON_RUN_ENV", runEnvPath);

                using (var runEnvWriter = new StreamWriter(runEnvPath, false, Encoding.UTF8))
                {
                    foreach (var item in allVars)
                    {
                        runEnvWriter.WriteLine($"{item.Key}={item.Value}");
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

                if (Directory.Exists(runFolder))
                {
                    Directory.Delete(runFolder, true);
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
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(shimability: DockerShimability.None, ensureConnection: false);
        }
    }
}
