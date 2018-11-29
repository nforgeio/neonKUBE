//-----------------------------------------------------------------------------
// FILE:	    AnsibleCommand.cs
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

using ICSharpCode.SharpZipLib.Zip;

using Neon.Common;
using Neon.IO;
using Neon.Hive;
using Neon.Net;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>ansible</b> commands.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ansible is not supported on Windows and although it's possible to deploy Ansible
    /// on Mac OSX, we don't want to require it as a dependency to make the experience
    /// the same on Windows and Mac and also to simplify neonHIVE setup.  The <b>neon-cli</b>
    /// implements the <b>neon ansible...</b> commands to map files from the host operating
    /// system into a <b>nhive/neon-cli</b> container where Ansible is installed so any
    /// operations can be executed there.
    /// </para>
    /// <para>
    /// This command works by mapping the current client directory into the <b>neon-cli</b> 
    /// container at <b>/cwd</b> and then generating the hosts and host variables files at
    /// <b>/etc/ansible</b> in the container and then running <b>ansible ARGS</b>, <b>ansible-galaxy ARGS</b>
    /// <b>ansible-playbook ARGS</b>, or <b>ansible-vault ARGS</b> by passing the SSH client 
    /// certificate and any command line arguments after the "--".
    /// </para>
    /// <para>
    /// Variables are generated for each Docker label specified for each host node.  Each
    /// variable is prefixed by <b>label_</b> and all periods (.) in label names are converted
    /// to underscores (_).
    /// </para>
    /// <note>
    /// This command makes no attempt to map files referenced by command line arguments
    /// or options into the container other than to map the current directory from the
    /// workstation along with the Ansible roles and vault directories in the user folder.
    /// Any password file references will be assumed to be relative to the user's vault
    /// folder.
    /// </note>
    /// </remarks>
    public partial class AnsibleCommand : CommandBase
    {
        private const string usage = @"
USAGE:

    neon ansible exec   [OPTIONS] -- ARGS   - runs an adhoc command via:   ansible ARGS
    neon ansible galaxy [OPTIONS] -- ARGS   - manage ansible roles via:    ansible-galaxy ARGS
    neon ansible play   [OPTIONS] -- ARGS   - runs a playbook via:         ansible-playbook ARGS
    neon ansible vault  [OPTIONS] -- ARGS   - manages ansible secrets via: ansible-vault ARGS

    neon ansible config   ZIP-PATH          - returns Ansible configuration files to ZIP archive
    neon ansible password CMD ...           - password management
    neon ansible module   MODULE ARGS       - implements built-in neonHIVE Ansible modules

ARGUMENTS:

    ARGS                - Ansible compatible arguments
    CMD                 - Password command
    ZIP-PATH            - Path to the output ZIP archive

OPTIONS:

    --cwd=FOLDER        - Use FOLDER as the current working directory
    --env=NAME          - Makes a workstation environment variable available
                          to the Ansible command.
    --env=NAME=VALUE    - Sets an environment variable for Ansible commands

    You can specify multiple [--env] options.

NOTE: 

This command makes no attempt to map files referenced by command line arguments
or options into the container other than to map the current directory into the
container and to run the Ansible command within this mapped directory.  You can
use [--cwd=FOLDER] to override the current working directory when running the 
command.

Ansible roles are managed via the [neon ansible galaxy] command.  This 
command installs roles to the your workstation in a user specific folder:

    %LOCALAPPDATA%\neonFORGE\neonhive\ansible\roles     - for Windows
    ~/.neonforge/neonhive/ansible/roles                 - for OSX

Ansible vault passwords are managed via the [neon ansible vault] command which
manages secrets on your workstation in the user specific folder:

    %LOCALAPPDATA%\neonFORGE\neonhive\ansible\passwords - for Windows
    ~/.neonforge/neonhive/ansible/passwords             - for OSX
";

        private const string execHelp = @"
Performs ad hoc Ansible commands via [ansible] built into [neon-cli].

USAGE:

    neon ansible exec [OPTIONS] -- ARGS

ARGUMENTS:

    ARGS                - Any valid [ansible] options and arguments

OPTIONS:

    --cwd=FOLDER        - Use FOLDER as the current working directory

This command works by running the [ansible ARGS] command within a Docker 
container, mapping the current directory (or the [--cwd=FOLDER] directory)
into the container as the current directory there.  Any installed Ansible
roles or vault passwords are also mapped into the container.

The Ansible hosts will be set to the nodes in the current hive.  These
are organized into these predefined host groups:

    all             - all hive nodes
    hive            - all hive nodes
    managers        - manager nodes
    workers         - worker nodes
    swarm           - manager or worker nodes
    pets            - pet nodes
    ceph            - nodes hosting any Ceph storage service
    ceph-mds        - nodes hosting Ceph MDS service
    ceph-mon        - nodes hosting Ceph monitor service
    ceph-osd        - nodes hosting Ceph OSD service

The special reserved [swarm-manager] hostname can be used to target 
Docker Swarm related tasks as a healthy Swarm manager node.

Host variables will be generated for each hive node.  These will include
the variables used by Ansible to establish the SSH connections as well as
all of the node labels specified in the hive configuration.  The node
label names will be prefixed by ""neon_"" and will have all embedded periods
converted to underscores.
";

        private const string playHelp = @"
Runs Ansible playbooks via [ansible-playbook] built into [neon-cli].

USAGE:

    neon ansible play [OPTIONS] -- ARGS

ARGUMENTS:

    ARGS                - Any valid [ansible] options and arguments

OPTIONS:

    --cwd=FOLDER        - Use FOLDER as the current working directory

This command works by running the [ansible-playbook ARGS] command within a 
Docker  container, mapping the current directory (or the [--cwd=FOLDER] 
directory) into the container as the current directory there.  Any installed
Ansible roles or vault passwords are also mapped into the container.

The Ansible hosts will be set to the nodes in the current hive.  These
are organized into four groups:

    all             - all hive nodes
    managers        - manager nodes
    workers         - worker nodes
    swarm           - manager or worker nodes
    pets            - pet nodes
    ceph            - nodes hosting any Ceph storage service
    ceph-mds        - nodes hosting Ceph MDS service
    ceph-mon        - nodes hosting Ceph monitor service
    ceph-osd        - nodes hosting Ceph OSD service

The special reserved [swarm-manager] hostname can be used to target 
Docker Swarm related tasks as a healthy Swarm manager node.

Host variables will be generated for each hive node.  These will include
the variables used by Ansible to establish the SSH connections as well as
all of the node labels specified in the hive configuration.  The node
label names will be prefixed by ""neon_"" and will have all embedded periods
converted to underscores.
";

        private const string galaxyHelp = @"
Manages installed Ansible roles via [ansible-galaxy] built into [neon-cli].

USAGE:

    neon ansible galaxy [OPTIONS] -- ARGS

ARGUMENTS:

    ARGS                - Any valid [ansible] options and arguments

OPTIONS:

    --cwd=FOLDER        - Use FOLDER as the current working directory

Ansible roles are managed via the [neon ansible galaxy] command.  This 
command installs roles to the your workstation in a user specific folder:

    %LOCALAPPDATA%\neonFORGE\neonhive\ansible\roles     - for Windows
    ~/.neonforge/neonhive/ansible/roles                 - for OSX

The [neon ansible ...] commands map this folder into the Docker container
they create such that any installed roles will be available. 
";

        private const string vaultHelp = @"
Manages Ansible roles via [ansible-Vault] built into [neon-cli].

USAGE:

    neon ansible vault [OPTIONS] -- ARGS

ARGUMENTS:

    ARGS                - Any valid [ansible] options and arguments

OPTIONS:

    --editor=nano|vim|vi    - Specifies the editor to use for modifying
                              encrypted files.  This defaults to [nano].
    --vault=FOLDER          - Use FOLDER as the password file location.

You can use [--ask-vault-pass] so that Ansible commands prompt for 
passwords or use [--vault-password-file NAME] to specify the password.

Note that all password files must be located at a user specific folder
on your workstation and must be referenced without specifying a path:

    %LOCALAPPDATA%\neonFORGE\neonhive\ansible\passwords     - for Windows
    ~/.neonforge/neonhive/ansible/passwords                 - for OSX
";

        private const string passwordHelp = @"
Manages Ansible Vault passwords for neonHIVE.

USAGE:

    neon ansible password ls|list               - Lists passwords
    neon ansible password export ZIP [PATTERN]  - Exports passwords to ZIP archive
    neon ansible password folder [--open]       - Prints or opens password folder
    neon ansible password get NAME              - Displays a password   
    neon ansible password import ZIP            - Imports passwords from ZIP archive
    neon ansible password rm|remove NAME        - Removes a password
    neon ansible password set NAME              - Sets a secure generated password 
    neon ansible password set NAME VALUE        - Sets a password to a specific value
    neon ansible password set NAME -            - Sets a password from STDIN

ARGUMENTS:

    NAME        - Name of the password file
    VALUE       - Password value
    PATTERN     - Optionally selects exported passwords via wildcards
    ZIP         - ZIP file archive partj
    -           - Indicates that password is read from STDIN

OPTIONS:

    --open      - Indicates that the password folder should be opened
                  in a file explorer window.

Passwords are simple text files that hold passwords on a single line.  These
are stored in a user-specific folder at:

    %LOCALAPPDATA%\neonFORGE\neonhive\ansible\passwords     - for Windows
    ~/.neonforge/neonhive/ansible/passwords                 - for OSX
";

        private const string configHelp = @"
Writes the Ansible configuration, inventory, and variables files generated for 
the current hive to a ZIP archive.

USAGE:

    neon ansible config ZIP-PATH

ARGUMENTS:

    ZIP-PATH            - Path to the output ZIP archive

This command is handy when you're developing Ansible playbooks and scripts
for a hive.  Use this command to write a ZIP archive including the
inventory and variable files that will be used when Ansible commands
will be run on the hive.

You can open the returned ZIP archive to inspect these file.
";


        private const string moduleHelp = @"
WARNING: FOR INTERNAL USE ONLY

Implements built-in neonHIVE Ansible modules that can be invoked via
[neon ansible exec -- ARGS] or [neon ansible play -- ARGS].  This command
should never need to be called directly by hive operators.

USAGE:

    neon ansible module MODULE ARGS

ARGUMENTS:

    MODULE              - Identifies the built-in module (see list below)
    ARGS                - Module arguments passed by Ansible

MODULES:

    neon_certificate        - Manages hive TLS certificates
    neon_couchbase_import   - Imports Couchbase data
    neon_couchbase_index    - Manages Couchbase indexes
    neon_couchbase_query    - Executes a Couchbase query
    neon_dashboard          - Manages hive dashboards
    neon_docker_config      - Manages Docker configs
    neon_docker_login       - Manages Docker registry logins
    neon_docker_secret      - Manages Docker secrets
    neon_docker_service     - Manages Docker services
    neon_docker_stack       - Manages Docker stacks
    neon_globals            - Manages hive global settings
    neon_hive_dns           - Manages hive DNS host overrides
    neon_traffic_manager    - Manages hive traffic directors
    neon_queue              - Manager hive message queues
";

        private const string sshClientPrivateKeyPath = "/dev/shm/ansible/ssh-client.key";   // Path to the SSH private client key (on a container RAM drive)
        private const string mappedCurrentDirectory  = "/cwd";                              // Path to the current working directory mapped into the container
        private const string mappedRolesPath         = "/etc/ansible/mapped-roles";         // Path where external roles are mapped into the container
        private const string mappedPasswordsPath     = "/etc/ansible/mapped-passwords";     // Path where external Vault passwords are mapped into the container
        private const string mappedZipPath           = "/zip";                              // Path where the [config] command will write the zipped Ansible files
        private const string copiedPasswordsPath     = "/dev/shm/copied-passwords";         // Path where copies of external Vault passwords held in the container

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "ansible" }; }
        }

        /// <inheritdoc/>
        public override string SplitItem
        {
            get { return "--"; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--cwd", "--editor", "--open", "--env" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            if (commandLine.Arguments.Length <= 1)
            {
                Help();
                Program.Exit(0);
            }

            var login            = Program.HiveLogin;
            var commandSplit     = commandLine.Split(SplitItem);
            var leftCommandLine  = commandSplit.Left;
            var rightCommandLine = commandSplit.Right;
            var command          = leftCommandLine.Arguments.ElementAtOrDefault(1);

            if (leftCommandLine.HasHelpOption)
            {
                Help();
                Program.Exit(0);
            }

            // The [password] command operates in [no-shim] mode so we'll implement it here.

            if (command == "password")
            {
                var passwordCommandLine = leftCommandLine.Shift(2);

                if (leftCommandLine.HasHelpOption || passwordCommandLine.Arguments.Length == 0)
                {
                    Console.WriteLine(passwordHelp);
                    Program.Exit(0);
                }

                string  passwordsFolder = HiveHelper.GetAnsiblePasswordsFolder();
                string  passwordCommand = passwordCommandLine.Arguments.ElementAtOrDefault(0);
                string  passwordName    = passwordCommandLine.Arguments.ElementAtOrDefault(1);
                string  passwordValue   = passwordCommandLine.Arguments.ElementAtOrDefault(2);
                int     passwordCount   = 0;
                string  passwordPath;
                string  passwordPattern;
                string  zipPath;
                string  zipPassword;

                switch (passwordCommand)
                {
                    case "folder":

                        if (passwordCommandLine.HasOption("--open"))
                        {
                            if (NeonHelper.IsWindows)
                            {
                                Process.Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), $"\"{passwordsFolder}\"");
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
                            Console.Write(passwordsFolder);
                        }
                        break;

                    case "get":

                        if (passwordName == null)
                        {
                            Console.Error.WriteLine("[ansible password get NAME] command is missing the [NAME] argument.");
                            Program.Exit(1);
                        }

                        passwordPath = Path.Combine(passwordsFolder, passwordName);

                        if (!File.Exists(passwordPath))
                        {
                            Console.Error.WriteLine($"*** ERROR: Password file [{passwordPath}] does not exist.");
                            Program.Exit(1);
                        }

                        Console.Write(File.ReadAllText(passwordPath));
                        break;

                    case "export":

                        zipPath         = passwordCommandLine.Arguments.ElementAtOrDefault(1);
                        passwordPattern = passwordCommandLine.Arguments.ElementAtOrDefault(2);

                        if (!File.Exists(zipPath))
                        {
                            if (Directory.Exists(zipPath))
                            {
                                Console.Error.WriteLine($"*** ERROR: [{zipPath}] is a directory, not a file.");
                                Program.Exit(1);
                            }
                            else
                            {
                                Console.Error.WriteLine($"*** ERROR: [{zipPath}] file does not exist.");
                                Program.Exit(1);
                            }
                        }

                        if (string.IsNullOrEmpty(passwordPattern))
                        {
                            passwordPattern = "*";
                        }

                        if (string.IsNullOrEmpty(zipPath))
                        {
                            Console.Error.WriteLine("*** ERROR: ZIP-PATH argument is required.");
                            Program.Exit(1);
                        }

                    retryPassword:

                        zipPassword = NeonHelper.ReadConsolePassword("Enter Password:   ");

                        if (!string.IsNullOrEmpty(zipPassword) && zipPassword != NeonHelper.ReadConsolePassword("Confirm Password: "))
                        {
                            Console.WriteLine();
                            Console.WriteLine("The passwords don't match.  Please try again.");
                            Console.WriteLine();

                            goto retryPassword;
                        }

                        using (var zip = ZipFile.Create(zipPath))
                        {
                            if (!string.IsNullOrWhiteSpace(zipPassword))
                            {
                                zip.Password = zipPassword;
                            }

                            zip.BeginUpdate();

                            foreach (var path in Directory.GetFiles(passwordsFolder, passwordPattern, SearchOption.TopDirectoryOnly))
                            {
                                passwordCount++;
                                zip.Add(path, Path.GetFileName(path));
                            }

                            zip.CommitUpdate();
                        }

                        Console.WriteLine();
                        Console.WriteLine($"[{passwordCount}] passwords exported.");
                        break;

                    case "import":

                        zipPath = passwordCommandLine.Arguments.ElementAtOrDefault(1);

                        if (!File.Exists(zipPath))
                        {
                            if (Directory.Exists(zipPath))
                            {
                                Console.Error.WriteLine($"*** ERROR: [{zipPath}] is a directory, not a file.");
                                Program.Exit(1);
                            }
                            else
                            {
                                Console.Error.WriteLine($"*** ERROR: [{zipPath}] file does not exist.");
                                Program.Exit(1);
                            }
                        }

                        if (string.IsNullOrEmpty(zipPath))
                        {
                            Console.Error.WriteLine("*** ERROR: ZIP-PATH argument is required.");
                            Program.Exit(1);
                        }

                        zipPassword = NeonHelper.ReadConsolePassword("ZIP Password: ");

                        using (var input = new FileStream(zipPath, FileMode.Open, FileAccess.ReadWrite))
                        {
                            using (var zip = new ZipFile(input))
                            {
                                if (!string.IsNullOrWhiteSpace(zipPassword))
                                {
                                    zip.Password = zipPassword;
                                }

                                foreach (ZipEntry zipEntry in zip)
                                {
                                    if (!zipEntry.IsFile)
                                    {
                                        continue;
                                    }

                                    passwordCount++;

                                    using (var zipStream = zip.GetInputStream(zipEntry))
                                    {
                                        using (var passwordStream = new FileStream(Path.Combine(passwordsFolder, zipEntry.Name), FileMode.Create, FileAccess.ReadWrite))
                                        {
                                            zipStream.CopyTo(passwordStream);
                                        }
                                    }
                                }
                            }
                        }

                        Console.WriteLine();
                        Console.WriteLine($"[{passwordCount}] passwords imported.");
                        break;                    

                    case "ls":
                    case "list":

                        foreach (var file in Directory.GetFiles(passwordsFolder, "*.*", SearchOption.AllDirectories)
                            .OrderBy(f => f.ToLowerInvariant()))
                        {
                            Console.WriteLine(file.Substring(passwordsFolder.Length + 1));  // Strip off the Vault folder path.
                        }
                        break;

                    case "rm":
                    case "remove":

                        if (passwordName == null)
                        {
                            Console.Error.WriteLine("[ansible password rm NAME] command is missing the [NAME] argument.");
                            Program.Exit(1);
                        }

                        passwordPath = Path.Combine(passwordsFolder, passwordName);

                        if (!File.Exists(passwordPath))
                        {
                            Console.Error.WriteLine($"***ERROR: Password file [{passwordPath}] does not exist.");
                            Program.Exit(1);
                        }

                        File.Delete(passwordPath);
                        break;

                    case "set":

                        if (passwordName == null)
                        {
                            Console.Error.WriteLine("[ansible password set] command is missing the [NAME] argument.");
                            Program.Exit(1);
                        }

                        passwordPath = Path.Combine(passwordsFolder, passwordName);

                        if (passwordValue == null)
                        {
                            // Generate and set a secure password.

                            File.WriteAllText(passwordPath, NeonHelper.GetRandomPassword(20));
                        }
                        else if (passwordValue == "-")
                        {
                            // Read the password from standard input.

                            passwordValue = NeonHelper.ReadStandardInputText();

                            // Make sure we have a only single line of text.

                            passwordValue = passwordValue.Trim();

                            if (passwordValue.IndexOf('\n') != -1)
                            {
                                Console.Error.WriteLine($"*** ERROR: Password passed in STDIN cannot have one line of text.");
                                Program.Exit(1);
                            }

                            File.WriteAllText(passwordPath, passwordValue);
                        }
                        else
                        {
                            // Password value is passed on the command line.

                            File.WriteAllText(passwordPath, passwordValue.Trim());
                        }
                        break;

                    default:

                        Console.Error.WriteLine($"*** ERROR: Unexpected Ansible password  [{passwordCommand}]command.");
                        Program.Exit(1);
                        break;
                }

                return;
            }

            // Implement the rest of the commands.

            if (!HiveHelper.InToolContainer)
            {
                Console.Error.WriteLine($"*** ERROR: [neon ansible {command}] does not support [no-shim] mode.");
                Program.Exit(1);
            }

            var noAnsibleCommand = false;

            if (rightCommandLine == null)
            {
                rightCommandLine = new CommandLine();
                noAnsibleCommand = true;
            }

            // Change the current directory to the mapped external directory.

            Environment.CurrentDirectory = mappedCurrentDirectory;

            // The user's [.../ansible/passwords] workstation folder is mapped into the container
            // at [/etc/ansible/mapped-passwords].  For Windows at least, these files are mapped
            // in with execute permissions which Ansible doesn't like.  Ansible appears to believe
            // that executable password files are actually scripts that will return the password.
            //
            // I tried using [chmod] in the container to clear the executable permissions but it
            // didn't work (probably a Docker mapped file thing).
            //
            // The workaround is to copy all of the passwords in the container to [/dev/shm/passwords]
            // so that they'll have no execute permissions.  We'll shim the command such that the
            // internal container command will reference passwords in copy folder.

            Directory.CreateDirectory(copiedPasswordsPath);

            foreach (var passwordPath in Directory.GetFiles(mappedPasswordsPath, "*", SearchOption.TopDirectoryOnly))
            {
                var contents = File.ReadAllBytes(passwordPath);
                var target   = Path.Combine(copiedPasswordsPath, Path.GetFileName(passwordPath));

                File.WriteAllBytes(target, contents);
            }

            // Munge any [--vault-password-file=NAME] or [--vault-password-file NAME] options to use a 
            // path prefix that is relative to the internal password copies folder.  Note that
            // [--vault-password-file=NAME] may appear only once in the command line.

            for (int i = 0; i < rightCommandLine.Items.Length; i++)
            {
                var item = rightCommandLine.Items[i];

                if (item.StartsWith("--vault-password-file="))
                {
                    var passwordFile = item.Substring(item.IndexOf('=') + 1);

                    rightCommandLine.Items[i] = $"--vault-password-file={Path.Combine(copiedPasswordsPath, passwordFile)}";
                    break;
                }
                else if (item == "--vault-password-file")
                {
                    if (i + 1 >= rightCommandLine.Items.Length)
                    {
                        Console.Error.WriteLine("*** ERROR: Missing password file after [--vault-password-file] option.");
                        Program.Exit(1);
                    }

                    var passwordFile = rightCommandLine.Items[i + 1];

                    rightCommandLine.Items[i + 1] = Path.Combine(copiedPasswordsPath, passwordFile);
                    break;
                }
            }

            // We also need to munge any [--vault-id=ID@NAME] or [--vault-password-file ID@NAME] options to use a 
            // path prefix that is relative to the internal password copies folder.  Note that the ID is optional
            // and that [--vault-id] may appear multiple times in the command line.

            for (int i = 0; i < rightCommandLine.Items.Length; i++)
            {
                var item = rightCommandLine.Items[i];

                if (item.StartsWith("--vault-id="))
                {
                    var vaultId      = item.Substring(item.IndexOf('=') + 1);
                    var vaultIdParts = vaultId.Split('@', 2);

                    if (vaultIdParts.Length == 1)
                    {
                        rightCommandLine.Items[i] = Path.Combine(copiedPasswordsPath, vaultIdParts[0]);
                    }
                    else
                    {
                        rightCommandLine.Items[i] =$"{vaultIdParts[0]}@{Path.Combine(copiedPasswordsPath, vaultIdParts[1])}";
                    }
                }
                else if (item == "--vault-id")
                {
                    if (i + 1 >= rightCommandLine.Items.Length)
                    {
                        Console.Error.WriteLine("*** ERROR: Missing password file after [--vault-id] option.");
                        Program.Exit(1);
                    }

                    i++;    // Advance to the vault ID argument.

                    var vaultId      = rightCommandLine.Items[i];
                    var vaultIdParts = vaultId.Split('@', 2);

                    if (vaultIdParts.Length == 1)
                    {
                        rightCommandLine.Items[i] = Path.Combine(copiedPasswordsPath, vaultIdParts[0]);
                    }
                    else
                    {
                        rightCommandLine.Items[i] = $"{vaultIdParts[0]}@{Path.Combine(copiedPasswordsPath, vaultIdParts[1])}";
                    }
                }
            }

            // Execute the command.

            switch (command)
            {
                case "config":

                    if (leftCommandLine.HasHelpOption)
                    {
                        Console.WriteLine(configHelp);
                        Program.Exit(0);
                    }

                    var zipFileName = leftCommandLine.Arguments.ElementAtOrDefault(2);

                    if (zipFileName == null)
                    {
                        Console.Error.WriteLine("*** ERROR: ZIP-PATH argument is required.");
                        Program.Exit(1);
                    }

                    var zipPath = Path.Combine(mappedZipPath, zipFileName);

                    GenerateAnsibleConfig();
                    GenerateAnsibleFiles(login);

                    // Remove any existing ZIP file.

                    if (File.Exists(zipPath))
                    {
                        File.Delete(zipPath);
                    }

                    // Recursively ZIP the contents of the [/etc/ansible] directory.
                    // Note that we need to change the current directory so the ZIP
                    // file won't include the [/etc/ansible] directories.

                    Environment.CurrentDirectory = "/etc/ansible";

                    var result = NeonHelper.ExecuteCapture("zip", $"-r \"{zipPath}\" .");

                    if (result.ExitCode != 0)
                    {
                        Console.Error.WriteLine(result.ErrorText);
                        Program.Exit(result.ExitCode);
                    }
                    else
                    {
                        Console.WriteLine();
                        Console.WriteLine($"Ansible configuration written to [{zipFileName}]");
                    }
                    break;

                case "exec":

                    if (leftCommandLine.HasHelpOption || noAnsibleCommand)
                    {
                        Console.WriteLine(execHelp);
                        Program.Exit(0);
                    }

                    if (login.Definition.HiveNode.SshAuth != AuthMethods.Tls)
                    {
                        Console.Error.WriteLine($"*** ERROR: The [ansible exec] command requires that the hive nodes were deployed with [{nameof(HiveNodeOptions)}.{nameof(HiveNodeOptions.SshAuth)}.{nameof(AuthMethods.Tls)}].");
                        Program.Exit(1);
                    }

                    GenerateAnsibleConfig();
                    GenerateAnsibleFiles(login);
                    Program.Exit(NeonHelper.Execute("ansible", NeonHelper.NormalizeExecArgs("--user", login.SshUsername, "--private-key", sshClientPrivateKeyPath, rightCommandLine.Items)));
                    break;

                case "galaxy":

                    if (leftCommandLine.HasHelpOption || noAnsibleCommand)
                    {
                        Console.WriteLine(galaxyHelp);
                        Program.Exit(0);
                    }

                    GenerateAnsibleConfig();
                    Program.Exit(NeonHelper.Execute("ansible-galaxy", NeonHelper.NormalizeExecArgs(rightCommandLine.Items)));
                    break;

                case "module":

                    GenerateAnsibleConfig();
                    ExecuteModule(login, leftCommandLine.Shift(2));
                    break;

                case "play":

                    if (leftCommandLine.HasHelpOption || noAnsibleCommand)
                    {
                        Console.WriteLine(playHelp);
                        Program.Exit(0);
                    }

                    if (login.Definition.HiveNode.SshAuth != AuthMethods.Tls)
                    {
                        Console.Error.WriteLine($"*** ERROR: The [ansible play] command requires that the hive nodes were deployed with [{nameof(HiveNodeOptions)}.{nameof(HiveNodeOptions.SshAuth)}.{nameof(AuthMethods.Tls)}].");
                        Program.Exit(1);
                    }

                    GenerateAnsibleConfig();
                    GenerateAnsibleFiles(login);
                    Program.Exit(NeonHelper.Execute("ansible-playbook", NeonHelper.NormalizeExecArgs("--user", login.SshUsername, "--private-key", sshClientPrivateKeyPath, rightCommandLine.Items)));
                    break;

                case "vault":

                    if (leftCommandLine.HasHelpOption || noAnsibleCommand)
                    {
                        Console.WriteLine(vaultHelp);
                        Program.Exit(0);
                    }

                    var editor = leftCommandLine.GetOption("--editor", "nano");

                    switch (editor.ToLowerInvariant())
                    {
                        case "nano":

                            Environment.SetEnvironmentVariable("EDITOR", "/bin/nano");
                            break;

                        case "vim":

                            Environment.SetEnvironmentVariable("EDITOR", "/usr/bin/vim");
                            break;

                        case "vi":

                            Environment.SetEnvironmentVariable("EDITOR", "/usr/bin/vi");
                            break;

                        default:

                            Console.Error.WriteLine($"*** ERROR: [--editor={editor}] does not specify a known editor.  Specify one of: NANO, VIM, or VI.");
                            Program.Exit(1);
                            break;
                    }

                    GenerateAnsibleConfig();
                    Program.Exit(NeonHelper.Execute("ansible-vault", NeonHelper.NormalizeExecArgs(rightCommandLine.Items)));
                    break;

                default:

                    Console.Error.WriteLine($"*** ERROR: Unexpected Ansible [{command}] command.");
                    Program.Exit(1);
                    break;
            }
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            // For the Ansible Vault commands, we're going to map the directory
            // containing the target file into the container as the current working
            // directory and munge the file name argument such that it has no
            // directory path.  Note that the target file will be the last argument
            // in the command line.
            //
            // For all of the other commands, we need to map the current directory 
            // into the container.  Note that the [--cwd=FOLDER] command line option
            // will override the current directory if present.

            string command = shim.CommandLine.Arguments.ElementAtOrDefault(1);

            if (command == "vault")
            {
                var ansibleCommandLine = shim.CommandLine.Split(SplitItem).Right;

                if (ansibleCommandLine == null)
                {
                    ansibleCommandLine = new CommandLine();
                }

                if (ansibleCommandLine.Arguments.Length >= 2)
                {
                    var externalTarget = ansibleCommandLine.Arguments.Last();

                    shim.AddMappedFolder(new DockerShimFolder(Path.GetDirectoryName(Path.GetFullPath(externalTarget)), mappedCurrentDirectory, isReadOnly: false));

                    // Munge the target file path to be just the file name.

                    var fileName = Path.GetFileName(externalTarget);

                    if (fileName != externalTarget)
                    {
                        shim.ReplaceItem(externalTarget, Path.GetFileName(externalTarget));
                    }
                }
            }
            else
            {
                var externalCurrentDirectory = shim.CommandLine.GetOption("--cwd", Environment.CurrentDirectory);

                shim.AddMappedFolder(new DockerShimFolder(externalCurrentDirectory, mappedCurrentDirectory, isReadOnly: false));
            }

            // ...and also map the external Ansible roles and vault folders into the container.

            shim.AddMappedFolder(new DockerShimFolder(HiveHelper.GetAnsibleRolesFolder(), mappedRolesPath, isReadOnly: false));
            shim.AddMappedFolder(new DockerShimFolder(HiveHelper.GetAnsiblePasswordsFolder(), mappedPasswordsPath, isReadOnly: false));

            // ...finally, we need to verify that any password files specified by [--vault-password-file PATH] 
            // actually exist in the [neon-cli] ansible passwords folder.
            //
            // Note that this option can take two forms:
            //
            //      --vault-password-file=NAME
            //      --vault-password-file NAME

            var localPasswordFolder = HiveHelper.GetAnsiblePasswordsFolder();

            for (int index = 0; index < shim.CommandLine.Items.Length; index++)
            {
                var item = shim.CommandLine.Items[index];

                if (item == "--vault-password-file" && index + 1 < shim.CommandLine.Items.Length && !shim.CommandLine.Items[index + 1].StartsWith("-"))
                {
                    var passwordName = shim.CommandLine.Items[index + 1];

                    VerifyPassword(passwordName);
                }
                else if (item.StartsWith("--vault-password-file="))
                {
                    var passwordName = item.Substring("--vault-password-file=".Length);

                    VerifyPassword(passwordName);
                }
            }

            // Add any [--env] options to the shim so they can be passed to the container.

            var leftCommandLine = shim.CommandLine.Split(SplitItem).Left;

            foreach (var envOption in leftCommandLine.GetOptionValues("--env"))
            {
                shim.AddEnvironmentVariable(envOption);
            }

            // Note that we don't shim the [password] command and that also doesn't need
            // a hive connection.

            if (shim.CommandLine.Arguments.ElementAtOrDefault(1) == "password")
            {
                return new DockerShimInfo(shimability: DockerShimability.None, ensureConnection: false);
            }

            // For the [config] command, we're going to shim the folder
            // where the ZIP file is to be written to [/cwd] inside the
            // container and then strip the directory path off of the 
            // ZIP-FILE argument.

            if (shim.CommandLine.Arguments.ElementAtOrDefault(1) == "config")
            {
                var zipPath = shim.CommandLine.Arguments.ElementAtOrDefault(2);

                if (zipPath == null)
                {
                    // The ZIP-PATH argument isn't present, which is an error.
                    // We're going to go ahead and map the current directory
                    // to [/zip] and then let [neon] running in the container
                    // report the error.

                    shim.AddMappedFolder(new DockerShimFolder(Environment.CurrentDirectory, mappedZipPath, isReadOnly: false));
                }
                else
                {
                    var zipFolder = Path.GetDirectoryName(Path.GetFullPath(zipPath));
                    var zipFile   = Path.GetFileName(zipPath);

                    shim.AddMappedFolder(new DockerShimFolder(zipFolder, mappedZipPath, isReadOnly: false));
                    shim.ReplaceItem(zipPath, zipFile);
                }
            }

            // Determine which commands don't require hive connections.

            var ensureConnection = true;

            switch (command)
            {
                case "galaxy":
                case "password":
                case "vault":

                    ensureConnection = false;
                    break;
            }

            return new DockerShimInfo(shimability: DockerShimability.Required, ensureConnection: ensureConnection);
        }

        /// <summary>
        /// Verifies that a vault password exists and is valid.
        /// </summary>
        /// <param name="passwordName">The password name.</param>
        public static void VerifyPassword(string passwordName)
        {
            if (string.IsNullOrWhiteSpace(passwordName))
            {
                Console.Error.WriteLine($"**** ERROR: [--vault-password-file={passwordName}] is not valid.");
                Program.Exit(1);
            }

            if (Path.IsPathRooted(passwordName) || passwordName.IndexOfAny(new char[] { ':', '/', '\\' }) != -1)
            {
                Console.Error.WriteLine($"**** ERROR: [--vault-password-file={passwordName}] must be the name of the password file managed by [neon-cli].  This cannot include a file path.");
                Program.Exit(1);
            }

            if (!File.Exists(Path.Combine(HiveHelper.GetAnsiblePasswordsFolder(), passwordName)))
            {
                Console.Error.WriteLine($"*** ERROR: Password [{passwordName}] does not exist.");
                Program.Exit(1);
            }
        }

        /// <summary>
        /// Writes a modified Ansible configuration file to <b>/etc/ansible/ansible.cfg</b>.
        /// </summary>
        private void GenerateAnsibleConfig()
        {
            // We're going to set the [IN_NEON_ANSIBLE_COMMAND=1] environment 
            // variable so that built-in module code will be able to verify that
            // it's running in the context of another ansible command.

            Environment.SetEnvironmentVariable("IN_NEON_ANSIBLE_COMMAND", "1");

            // This is the default configuration installed by the Ansible package
            // with a few modifications.

            var config =
$@"# config file for ansible -- https://ansible.com/
# ===============================================

# nearly all parameters can be overridden in ansible-playbook
# or with command line flags. ansible will read ANSIBLE_CONFIG,
# ansible.cfg in the current working directory, .ansible.cfg in
# the home directory or /etc/ansible/ansible.cfg, whichever it
# finds first

[defaults]

# some basic default values...

#inventory      = /etc/ansible/hosts
#library        = /usr/share/my_modules/
#module_utils   = /usr/share/my_module_utils/
#remote_tmp     = ~/.ansible/tmp
#local_tmp      = ~/.ansible/tmp
#forks          = 5
#poll_interval  = 15
#sudo_user      = root
#ask_sudo_pass = True
#ask_pass      = True
#transport      = smart
#remote_port    = 22
#module_lang    = C
#module_set_locale = False

# plays will gather facts by default, which contain information about
# the remote system.
#
# smart - gather by default, but don't regather if already gathered
# implicit - gather by default, turn off with gather_facts: False
# explicit - do not gather by default, must say gather_facts: True
#gathering = implicit

# This only affects the gathering done by a play's gather_facts directive,
# by default gathering retrieves all facts subsets
# all - gather all subsets
# network - gather min and network facts
# hardware - gather hardware facts (longest facts to retrieve)
# virtual - gather min and virtual facts
# facter - import facts from facter
# ohai - import facts from ohai
# You can combine them using comma (ex: network,virtual)
# You can negate them using ! (ex: !hardware,!facter,!ohai)
# A minimal set of facts is always gathered.
#gather_subset = all

# some hardware related facts are collected
# with a maximum timeout of 10 seconds. This
# option lets you increase or decrease that
# timeout to something more suitable for the
# environment. 
# gather_timeout = 10

# additional paths to search for roles in, colon separated.  Note
# that the first folder is mapped into the Docker container from 
# the client workstation.  This will be the folder where [ansible-galaxy]
# will install new roles (since it's the first writeable folder in
# the list.
roles_path = {mappedRolesPath}:/etc/ansible/roles

# uncomment this to disable SSH key host checking
host_key_checking = False

# change the default callback, you can only have one 'stdout' type  enabled at a time.
#stdout_callback = skippy


## Ansible ships with some plugins that require whitelisting,
## this is done to avoid running all of a type by default.
## These setting lists those that you want enabled for your system.
## Custom plugins should not need this unless plugin author specifies it.

# enable callback plugins, they can output to stdout but cannot be 'stdout' type.
#callback_whitelist = timer, mail

# Determine whether includes in tasks and handlers are ""static"" by
# default. As of 2.0, includes are dynamic by default. Setting these
# values to True will make includes behave more like they did in the
# 1.x versions.
#task_includes_static = True
#handler_includes_static = True

# Controls if a missing handler for a notification event is an error or a warning
#error_on_missing_handler = True

# change this for alternative sudo implementations
#sudo_exe = sudo

# What flags to pass to sudo
# WARNING: leaving out the defaults might create unexpected behaviours
#sudo_flags = -H -S -n

# SSH timeout
#timeout = 10

# default user to use for playbooks if user is not specified
# (/usr/bin/ansible will use current user as default)
#remote_user = root

# logging is off by default unless this path is defined
# if so defined, consider logrotate
#log_path = /var/log/ansible.log

# default module name for /usr/bin/ansible
#module_name = command

# use this shell for commands executed under sudo
# you may need to change this to bin/bash in rare instances
# if sudo is constrained
#executable = /bin/sh

# if inventory variables overlap, does the higher precedence one win
# or are hash values merged together?  The default is 'replace' but
# this can also be set to 'merge'.
#hash_behaviour = replace

# by default, variables from roles will be visible in the global variable
# scope. To prevent this, the following option can be enabled, and only
# tasks and handlers within the role will see the variables there
#private_role_vars = yes

# list any Jinja2 extensions to enable here:
#jinja2_extensions = jinja2.ext.do,jinja2.ext.i18n

# if set, always use this private key file for authentication, same as
# if passing --private-key to ansible or ansible-playbook
#private_key_file = /path/to/file

# If set, configures the path to the Vault password file as an alternative to
# specifying --vault-password-file on the command line.
#vault_password_file = /path/to/vault_password_file

# format of string {{{{ ansible_managed }}}} available within Jinja2
# templates indicates to users editing templates files will be replaced.
# replacing {{file}}, {{host}} and {{uid}} and strftime codes with proper values.
#ansible_managed = Ansible managed: {{file}} modified on %Y-%m-%d %H:%M:%S by {{uid}} on {{host}}
# {{file}}, {{host}}, {{uid}}, and the timestamp can all interfere with idempotence
# in some situations so the default is a static string:
#ansible_managed = Ansible managed

# by default, ansible-playbook will display ""Skipping [host]"" if it determines a task
# should not be run on a host.  Set this to ""False"" if you don't want to see these ""Skipping""
# messages. NOTE: the task header will still be shown regardless of whether or not the
# task is skipped.
#display_skipped_hosts = True

# by default, if a task in a playbook does not include a name: field then
# ansible-playbook will construct a header that includes the task's action but
# not the task's args.  This is a security feature because ansible cannot know
# if the *module* considers an argument to be no_log at the time that the
# header is printed.  If your environment doesn't have a problem securing
# stdout from ansible-playbook (or you have manually specified no_log in your
# playbook on all of the tasks where you have secret information) then you can
# safely set this to True to get more informative messages.
#display_args_to_stdout = False

# by default (as of 1.3), Ansible will raise errors when attempting to dereference
# Jinja2 variables that are not set in templates or action lines. Uncomment this line
# to revert the behavior to pre-1.3.
#error_on_undefined_vars = False

# by default (as of 1.6), Ansible may display warnings based on the configuration of the
# system running ansible itself. This may include warnings about 3rd party packages or
# other conditions that should be resolved if possible.
# to disable these warnings, set the following value to False:
#system_warnings = True

# by default (as of 1.4), Ansible may display deprecation warnings for language
# features that should no longer be used and will be removed in future versions.
# to disable these warnings, set the following value to False:
deprecation_warnings = False

# (as of 1.8), Ansible can optionally warn when usage of the shell and
# command module appear to be simplified by using a default Ansible module
# instead.  These warnings can be silenced by adjusting the following
# setting or adding warn=yes or warn=no to the end of the command line
# parameter string.  This will for example suggest using the git module
# instead of shelling out to the git command.
# command_warnings = False


# set plugin path directories here, separate with colons
#action_plugins     = /usr/share/ansible/plugins/action
#cache_plugins      = /usr/share/ansible/plugins/cache
#callback_plugins   = /usr/share/ansible/plugins/callback
#connection_plugins = /usr/share/ansible/plugins/connection
#lookup_plugins     = /usr/share/ansible/plugins/lookup
#inventory_plugins  = /usr/share/ansible/plugins/inventory
#vars_plugins       = /usr/share/ansible/plugins/vars
#filter_plugins     = /usr/share/ansible/plugins/filter
#test_plugins       = /usr/share/ansible/plugins/test
#terminal_plugins   = /usr/share/ansible/plugins/terminal
#strategy_plugins   = /usr/share/ansible/plugins/strategy


# by default, ansible will use the 'linear' strategy but you may want to try
# another one
#strategy = free

# by default callbacks are not loaded for /bin/ansible, enable this if you
# want, for example, a notification or logging callback to also apply to
# /bin/ansible runs
#bin_ansible_callbacks = False


# don't like cows?  that's unfortunate.
# set to 1 if you don't want cowsay support or export ANSIBLE_NOCOWS=1
#nocows = 1

# set which cowsay stencil you'd like to use by default. When set to 'random',
# a random stencil will be selected for each task. The selection will be filtered
# against the `cow_whitelist` option below.
#cow_selection = default
#cow_selection = random

# when using the 'random' option for cowsay, stencils will be restricted to this list.
# it should be formatted as a comma-separated list with no spaces between names.
# NOTE: line continuations here are for formatting purposes only, as the INI parser
#       in python does not support them.
#cow_whitelist=bud-frogs,bunny,cheese,daemon,default,dragon,elephant-in-snake,elephant,eyes,\
#              hellokitty,kitty,luke-koala,meow,milk,moofasa,moose,ren,sheep,small,stegosaurus,\
#              stimpy,supermilker,three-eyes,turkey,turtle,tux,udder,vader-koala,vader,www

# don't like colors either?
# set to 1 if you don't want colors, or export ANSIBLE_NOCOLOR=1
#nocolor = 1

# if set to a persistent type (not 'memory', for example 'redis') fact values
# from previous runs in Ansible will be stored.  This may be useful when
# wanting to use, for example, IP information from one group of servers
# without having to talk to them in the same playbook run to get their
# current IP information.
#fact_caching = memory


# retry files
# When a playbook fails by default a .retry file will be created in ~/
# You can disable this feature by setting retry_files_enabled to False
# and you can change the location of the files by setting retry_files_save_path

#retry_files_enabled = False
#retry_files_save_path = ~/.ansible-retry

# squash actions
# Ansible can optimise actions that call modules with list parameters
# when looping. Instead of calling the module once per with_ item, the
# module is called once with all items at once. Currently this only works
# under limited circumstances, and only with parameters named 'name'.
#squash_actions = apk,apt,dnf,homebrew,pacman,pkgng,yum,zypper

# prevents logging of task data, off by default
#no_log = False

# prevents logging of tasks, but only on the targets, data is still logged on the master/controller
#no_target_syslog = False

# controls whether Ansible will raise an error or warning if a task has no
# choice but to create world readable temporary files to execute a module on
# the remote machine.  This option is False by default for security.  Users may
# turn this on to have behaviour more like Ansible prior to 2.1.x.  See
# https://docs.ansible.com/ansible/become.html#becoming-an-unprivileged-user
# for more secure ways to fix this than enabling this option.
#allow_world_readable_tmpfiles = False

# controls the compression level of variables sent to
# worker processes. At the default of 0, no compression
# is used. This value must be an integer from 0 to 9.
#var_compression_level = 9

# controls what compression method is used for new-style ansible modules when
# they are sent to the remote system.  The compression types depend on having
# support compiled into both the controller's python and the client's python.
# The names should match with the python Zipfile compression types:
# * ZIP_STORED (no compression. available everywhere)
# * ZIP_DEFLATED (uses zlib, the default)
# These values may be set per host via the ansible_module_compression inventory
# variable
#module_compression = 'ZIP_DEFLATED'

# This controls the cutoff point (in bytes) on --diff for files
# set to 0 for unlimited (RAM may suffer!).
#max_diff_size = 1048576

# This controls how ansible handles multiple --tags and --skip-tags arguments
# on the CLI.  If this is True then multiple arguments are merged together.  If
# it is False, then the last specified argument is used and the others are ignored.
# This option will be removed in 2.8.
#merge_multiple_cli_flags = True

# Controls showing custom stats at the end, off by default
#show_custom_stats = True

# Controls which files to ignore when using a directory as inventory with
# possibly multiple sources (both static and dynamic)
#inventory_ignore_extensions = ~, .orig, .bak, .ini, .cfg, .retry, .pyc, .pyo

# This family of modules use an alternative execution path optimized for network appliances
# only update this setting if you know how this works, otherwise it can break module execution
#network_group_modules=['eos', 'nxos', 'ios', 'iosxr', 'junos', 'vyos']

# When enabled, this option allows lookups (via variables like {{{{lookup('foo')}}}} or when used as
# a loop with `with_foo`) to return data that is not marked ""unsafe"". This means the data may contain
# jinja2 templating language which will be run through the templating engine.
# ENABLING THIS COULD BE A SECURITY RISK
#allow_unsafe_lookups = False

# set default errors for all plays
#any_errors_fatal = False

[inventory]
# enable inventory plugins, default: 'host_list', 'script', 'yaml', 'ini'
#enable_plugins = host_list, virtualbox, yaml, constructed

# ignore these extensions when parsing a directory as inventory source
#ignore_extensions = .pyc, .pyo, .swp, .bak, ~, .rpm, .md, .txt, ~, .orig, .ini, .cfg, .retry

# ignore files matching these patterns when parsing a directory as inventory source
#ignore_patterns=

# If 'true' unparsed inventory sources become fatal errors, they are warnings otherwise.
#unparsed_is_failed=False

[privilege_escalation]
#become=True
#become_method=sudo
#become_user=root
#become_ask_pass=False

[paramiko_connection]

# uncomment this line to cause the paramiko connection plugin to not record new host
# keys encountered.  Increases performance on new host additions.  Setting works independently of the
# host key checking setting above.
#record_host_keys=False

# by default, Ansible requests a pseudo-terminal for commands executed under sudo. Uncomment this
# line to disable this behaviour.
#pty=False

# paramiko will default to looking for SSH keys initially when trying to
# authenticate to remote devices.  This is a problem for some network devices
# that close the connection after a key failure.  Uncomment this line to
# disable the Paramiko look for keys function
#look_for_keys = False

# When using persistent connections with Paramiko, the connection runs in a
# background process.  If the host doesn't already have a valid SSH key, by
# default Ansible will prompt to add the host key.  This will cause connections
# running in background processes to fail.  Uncomment this line to have
# Paramiko automatically add host keys.
#host_key_auto_add = True

[ssh_connection]

# Adds the option to retry failed ssh executions if the failure is encountered 
# in ssh itself, not the remote command. This can be helpful if there are transient 
# network issues. Enabled by setting retries to an integer greater than 1. Defaults to 0.
retries = 4

# ssh arguments to use
# Leaving off ControlPersist will result in poor performance, so use
# paramiko on older platforms rather than removing it, -C controls compression use
#ssh_args = -C -o ControlMaster=auto -o ControlPersist=60s

# The base directory for the ControlPath sockets. 
# This is the ""%(directory)s"" in the control_path option
# 
# Example: 
# control_path_dir = /tmp/.ansible/cp
#control_path_dir = ~/.ansible/cp

# The path to use for the ControlPath sockets. This defaults to a hashed string of the hostname, 
# port and username (empty string in the config). The hash mitigates a common problem users 
# found with long hostames and the conventional %(directory)s/ansible-ssh-%%h-%%p-%%r format. 
# In those cases, a ""too long for Unix domain socket"" ssh error would occur.
#
# Example:
# control_path = %(directory)s/%%h-%%r
#control_path =

# Enabling pipelining reduces the number of SSH operations required to
# execute a module on the remote server. This can result in a significant
# performance improvement when enabled, however when using ""sudo:"" you must
# first disable 'requiretty' in /etc/sudoers
#
# By default, this option is disabled to preserve compatibility with
# sudoers configurations that have requiretty (the default on many distros).
#
#pipelining = False

# Control the mechanism for transferring files (old)
#   * smart = try sftp and then try scp [default]
#   * True = use scp only
#   * False = use sftp only
#scp_if_ssh = smart

# Control the mechanism for transferring files (new)
# If set, this will override the scp_if_ssh option
#   * sftp  = use sftp to transfer files
#   * scp   = use scp to transfer files
#   * piped = use 'dd' over SSH to transfer files
#   * smart = try sftp, scp, and piped, in that order [default]
#transfer_method = smart

# if False, sftp will not use batch mode to transfer files. This may cause some
# types of file transfer failures impossible to catch however, and should
# only be disabled if your sftp version has problems with batch mode
#sftp_batch_mode = False

[persistent_connection]

# Configures the persistent connection timeout value in seconds.  This value is
# how long the persistent connection will remain idle before it is destroyed.  
# If the connection doesn't receive a request before the timeout value 
# expires, the connection is shutdown. The default value is 30 seconds.
#connect_timeout = 30

# Configures the persistent connection retry timeout.  This value configures the
# the retry timeout that ansible-connection will wait to connect
# to the local domain socket. This value must be larger than the
# ssh timeout (timeout) and less than persistent connection idle timeout (connect_timeout).
# The default value is 15 seconds.
#connect_retry_timeout = 15

# The command timeout value defines the amount of time to wait for a command
# or RPC call before timing out. The value for the command timeout must
# be less than the value of the persistent connection idle timeout (connect_timeout)
# The default value is 10 second.
#command_timeout = 10

[accelerate]
#accelerate_port = 5099
#accelerate_timeout = 30
#accelerate_connect_timeout = 5.0

# The daemon timeout is measured in minutes. This time is measured
# from the last activity to the accelerate daemon.
#accelerate_daemon_timeout = 30

# If set to yes, accelerate_multi_key will allow multiple
# private keys to be uploaded to it, though each user must
# have access to the system via SSH to add a new key. The default
# is ""no"".
#accelerate_multi_key = yes

[selinux]
# file systems that require special treatment when dealing with security context
# the default behaviour that copies the existing context or uses the user default
# needs to be changed to use the file system dependent context.
#special_context_filesystems=nfs,vboxsf,fuse,ramfs,9p

# Set this to yes to allow libvirt_lxc connections to work without SELinux.
#libvirt_lxc_noseclabel = yes

[colors]
#highlight = white
#verbose = blue
#warn = bright purple
#error = red
#debug = dark gray
#deprecate = purple
#skip = cyan
#unreachable = red
#ok = green
#changed = yellow
#diff_add = green
#diff_remove = red
#diff_lines = cyan


[diff]
# Always print diff when running ( same as always running with -D/--diff )
# always = no

# Set how many context lines to show in diff
# context = 3
";
            File.WriteAllText("/etc/ansible/ansible.cfg", config);
        }

        /// <summary>
        /// Generates Ansible files including host inventory related files, modfying <b>/etc/hosts</b>
        /// and writing the private TLS client key.
        /// </summary>
        /// <param name="login">The hive login.</param>
        private void GenerateAnsibleFiles(HiveLogin login)
        {
            var hiveLogin = Program.ConnectHive();
            var hive      = new HiveProxy(hiveLogin);

            // IMPLEMENTATION NOTE:
            //
            // We need a clean way to target Docker Swarm related modules at a healthy
            // swarm manager node.  We're going to use [HiveHelper.GetReachableManager()]
            // to look for a healthy node (via pings) and then this to the inventory as
            // the [swarm-manager] host and to [/etc/hosts] as [health-swarm-manager] with
            // the manager's IP address.   We'll also generate related host variables.
            // 
            // If [HiveHelper.GetReachableManager()] fails, we'll create a host
            // for the first manager as a fallback.  In this case, any playbooks 
            // targeting the unhealthy first manager will fail with a timeout, which
            // is what the operator should expect.

            var swarmManager = hive.FirstManager;

            try
            {
                swarmManager = hive.GetReachableManager();
            }
            catch (HiveException)
            {
                // We didn't find a healthy manager so we'll just fallback
                // to the first manager as initialized above.
            }

            // Write the hive's SSH client private key to [/dev/shm/ssh-client.key],
            // which is on the container RAM drive for security.  Note that the key file
            // must be restricted to the ROOT account to be accepted by Ansible.

            Directory.CreateDirectory(Path.GetDirectoryName(sshClientPrivateKeyPath));
            File.WriteAllText(sshClientPrivateKeyPath, login.SshClientKey.PrivatePEM);
            NeonHelper.Execute("chmod", $"600 \"{sshClientPrivateKeyPath}\"");

            // We need to execute the Ansible command within the client workstation's current directory 
            // mapped into the container.

            Environment.CurrentDirectory = mappedCurrentDirectory;

            // Generate the Ansible inventory and variable files.  We're going to use the hive node
            // name for each host and then generate some standard Ansible variables and then generate a
            // variable for each host label.  These label variables will be prefixed by "label_" with the
            // label name appended and with any embedded periods converted to underscores.
            //
            // The hosts will be organized into four groups: managers, workers, pets, and swarm (where
            // swarm includes the managers and workers, but not the pets).

            const string ansibleConfigFolder = "/etc/ansible";
            const string ansibleVarsFolder   = ansibleConfigFolder + "/host_vars";

            Directory.CreateDirectory(ansibleConfigFolder);
            Directory.CreateDirectory(ansibleVarsFolder);

            // Generate the hosts file using the INI format.  Note that we'll be organizing these
            // into standard groups as well as groups explicitly assigned by node definitions.
            //
            // Note that we're going to special-case the [swarm-manager] host, which won't
            // belong to any groups.

            var groups = login.Definition.GetHostGroups(excludeAllGroup: true);

            using (var writer = new StreamWriter(new FileStream(Path.Combine(ansibleConfigFolder, "hosts"), FileMode.Create, FileAccess.ReadWrite), Encoding.ASCII))
            {
                // Special-case the implicit Docker Swarm manager node (not in a group).

                writer.WriteLine(HiveDefinition.VirtualSwarmManagerName);

                // Write the groups.

                foreach (var group in groups.OrderBy(g => g.Key))
                {
                    writer.WriteLine();
                    writer.WriteLine($"[{group.Key}]");

                    foreach (var node in group.Value)
                    {
                        writer.WriteLine(node.Name);
                    }
                }

                // Write the implicit [hive] group with all of the hive nodes.

                writer.WriteLine();
                writer.WriteLine($"[hive]");

                foreach (var node in hive.Definition.SortedNodes)
                {
                    writer.WriteLine(node.Name);
                }
            }

            // Generate host variable files as YAML.

            var swarmManagerNodes   = new List<NodeDefinition>();  // Create a list with a faked up Swarm manager node
            var swarmManagerNode    = NeonHelper.JsonClone(swarmManager.Metadata);
            var swarmManagerAddress = swarmManagerNode.PrivateAddress;

            swarmManagerNode.Name           = HiveDefinition.VirtualSwarmManagerName;
            swarmManagerNode.PrivateAddress = HiveDefinition.VirtualSwarmManagerName;  // This references a record we'll add to [/etc/hosts] below.

            swarmManagerNodes.Add(swarmManagerNode);

            foreach (var node in login.Definition.Nodes
                .Union(swarmManagerNodes))
            {
                using (var writer = new StreamWriter(new FileStream(Path.Combine(ansibleConfigFolder, "host_vars", node.Name), FileMode.Create, FileAccess.ReadWrite), Encoding.UTF8))
                {
                    writer.WriteLine("---");
                    writer.WriteLine("# Ansible variables:");
                    writer.WriteLine();
                    writer.WriteLine($"ansible_host: \"{node.PrivateAddress}\"");
                    writer.WriteLine($"ansible_port: \"{NetworkPorts.SSH}\"");
                    writer.WriteLine($"ansible_user: \"{login.SshUsername}\"");
                    writer.WriteLine($"ansible_ssh_private_key_file: \"{sshClientPrivateKeyPath}\"");
                    writer.WriteLine();
                    writer.WriteLine("# Docker node labels:");
                    writer.WriteLine();

                    foreach (var label in node.Labels.Standard
                        .Union(node.Labels.Custom)
                        .OrderBy(l => l.Key))
                    {
                        var name = "neon_" + label.Key.Replace('.', '_');   // Prefix by "neon_" and convert periods in label names to underscores

                        // We may need to escape the label value to be YAML/Ansible safe.
                        // Note that I'm going to just go ahead and quote all values for
                        // simplicity.

                        var value = label.Value != null ? label.Value.ToString() : "null";
                        var sb    = new StringBuilder();

                        sb.Append('"');

                        foreach (var ch in value)
                        {
                            switch (ch)
                            {
                                case '{':

                                    sb.Append("{{");
                                    break;

                                case '}':

                                    sb.Append("}}");
                                    break;

                                case '"':

                                    sb.Append("\\\"");
                                    break;

                                case '\\':

                                    sb.Append("\\\\");
                                    break;

                                case '\r':

                                    sb.Append("\\r");
                                    break;

                                case '\n':

                                    sb.Append("\\n");
                                    break;

                                case '\t':

                                    sb.Append("\\t");
                                    break;

                                default:

                                    sb.Append(ch);
                                    break;
                            }
                        }

                        sb.Append('"');

                        writer.WriteLine($"{name}: {sb}");
                    }
                }
            }

            // Generate the [/etc/ssh/ssh_known_hosts] file with the public SSH key of the hive
            // nodes so Ansible will be able to verify host identity when connecting via SSH.  
            // Note that all nodes share the same key.  This documented here:
            //
            //      http://man.openbsd.org/sshd.8

            var hostPublicKeyFields = login.SshHiveHostPublicKey.Split(" ");
            var hostPublicKey       = $"{hostPublicKeyFields[0]} {hostPublicKeyFields[1]}"; // Strip off the [user@host] field from the end (if present).

            using (var writer = new StreamWriter(new FileStream("/etc/ssh/ssh_known_hosts", FileMode.Create, FileAccess.ReadWrite), Encoding.ASCII))
            {
                foreach (var node in login.Definition.SortedNodes
                    .Union(swarmManagerNodes))
                {
                    writer.WriteLine($"# Node: {node.Name}");
                    writer.WriteLine($"{node.PrivateAddress} {hostPublicKey}");
                    writer.WriteLine();
                }
            }

            // Add the [swarm-master] record to [/etc/hosts].

            File.AppendAllText("/etc/hosts",
                NeonHelper.ToLinuxLineEndings(
$@"
# Identifies a (hopefully) healthy swarm manager node.

{swarmManagerAddress} {HiveDefinition.VirtualSwarmManagerName}
"));
        }
    }
}
