//-----------------------------------------------------------------------------
// FILE:	    HiveGetCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using Neon.Common;
using Neon.Hive;
using System;
using System.Linq;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>hive get</b> command.
    /// </summary>
    public class HiveGetCommand : CommandBase
    {
        private const string usage = @"
Writes a specified value from the currently logged in hive to the
standard output.  Global hive values as well as node specific ones
can be obtained.

USAGE:

    neon hive get VALUE
    neon hive get NODE.VALUE

ARGUMENTS:

    VALUE       - identifies the desired value
    NODE        - optionally names a specific node

HIVE IDENTIFIERS:

    allow-unit-testing          - enables HiveFixture unit testing (bool)
    create-date-utc             - hive creation date (UTC)
    disable-auto-unseal         - disables automatic Vault unsealing (bool)
    hivemq-settings-app         - HiveMQ [app] account settings
    hivemq-settings-bootstrap   - HiveMQ bootstrap settings
    hivemq-settings-neon        - HiveMQ [neon] account settings
    hivemq-settings-sysadmin    - HiveMQ [sysadmin] account settings
    log-retention-days          - number of days of logs to retain
    registries                  - lists the Docker registries and credentials
    setup-pending               - indicates whether hive setup is in progress
    sshkey-client-pem           - client SSH private key (PEM format)
    sshkey-client-ppk           - client SSH private key (PPK format)
    sshkey-fingerprint          - SSH host key fingerprint
    ssh-password                - hive node admin password
    ssh-username                - hive node admin username
    uuid                        - Hive UUID
    vault-token                 - Vault root token
    version                     - Hive software version

NODE IDENTIFIERS:

    ip                          - internal hive IP address
    role                        - role: manager or worker
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "hive", "get" }; }
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

            var hiveLogin = Program.ConnectHive();
            var hive      = new HiveProxy(hiveLogin);

            if (commandLine.Arguments.Length != 1)
            {
                Console.Error.WriteLine("*** ERROR: VALUE-EXPR expected.");
                Console.Error.WriteLine();
                Console.Error.WriteLine(usage);
                Program.Exit(1);
            }

            var valueExpr = commandLine.Arguments[0];

            if (valueExpr.Contains('.'))
            {
                // Node expression.

                var fields   = valueExpr.Split(new char[] { '.' }, 2);
                var nodeName = fields[0].ToLowerInvariant();
                var value    = fields[1];

                if (string.IsNullOrEmpty(nodeName) || string.IsNullOrEmpty(value))
                {
                    Console.Error.WriteLine("*** ERROR: VALUE-EXPR is not valid.");
                    Program.Exit(1);
                }

                var node = hive.Definition.Nodes.SingleOrDefault(n => n.Name == nodeName);

                if (node == null)
                {
                    Console.Error.WriteLine($"*** ERROR: Node [{nodeName}] is not present.");
                    Program.Exit(1);
                }

                switch (value.ToLowerInvariant())
                {
                    case "ip":

                        Console.Write(node.PrivateAddress);
                        break;

                    case "role":

                        Console.Write(node.Role);
                        break;

                    default:

                        Console.Error.WriteLine($"*** ERROR: Unknown value [{value}].");
                        Program.Exit(1);
                        break;
                }
            }
            else
            {
                // Hive expression.

                valueExpr = valueExpr.ToLowerInvariant();

                switch (valueExpr)
                {
                    case HiveGlobals.UserAllowUnitTesting:

                        if (hive.Globals.TryGetBool(HiveGlobals.UserAllowUnitTesting, out var allowUnitTesting))
                        {
                            Console.Write(allowUnitTesting ? "true" : "false");
                        }
                        else
                        {
                            Console.Error.WriteLine($"*** ERROR: Hive setting [{valueExpr}] does not exist.");
                            Program.Exit(1);
                        }
                        break;

                    case HiveGlobals.CreateDateUtc:

                        if (hive.Globals.TryGetString(HiveGlobals.CreateDateUtc, out var createDate))
                        {
                            Console.Write(createDate);
                        }
                        else
                        {
                            Console.Error.WriteLine($"*** ERROR: Hive setting [{valueExpr}] does not exist.");
                            Program.Exit(1);
                        }
                        break;

                    case HiveGlobals.UserDisableAutoUnseal:

                        if (hive.Globals.TryGetBool(HiveGlobals.UserDisableAutoUnseal, out var disableAutoUnseal))
                        {
                            Console.Write(disableAutoUnseal ? "true" : "false");
                        }
                        else
                        {
                            Console.Error.WriteLine($"*** ERROR: Hive setting [{valueExpr}] does not exist.");
                            Program.Exit(1);
                        }
                        break;

                    case HiveGlobals.UserLogRetentionDays:

                        if (hive.Globals.TryGetInt(HiveGlobals.UserDisableAutoUnseal, out var logRetentionDays))
                        {
                            Console.Write(logRetentionDays);
                        }
                        else
                        {
                            Console.Error.WriteLine($"*** ERROR: Hive setting [{valueExpr}] does not exist.");
                            Program.Exit(1);
                        }
                        break;

                    case "registries":

                        var registries = hive.Registry.List();

                        // Special-case the Docker public registry if it's not
                        // set explicitly.

                        if (!registries.Exists(r => HiveHelper.IsDockerPublicRegistry(r.Registry)))
                        {
                            registries.Add(
                                new RegistryCredentials()
                                {
                                    Registry = HiveConst.DockerPublicRegistry
                                });
                        }

                        var maxRegistryLength = registries.Max(r => r.Registry.Length);

                        foreach (var registry in registries)
                        {
                            var spacer      = new string(' ', maxRegistryLength - registry.Registry.Length);
                            var credentials = string.Empty;

                            if (!string.IsNullOrEmpty(registry.Username))
                            {
                                credentials = $"{registry.Username}/{registry.Password ?? string.Empty}";
                            }

                            Console.WriteLine($"{registry.Registry}{spacer} - {credentials}");
                        }
                        break;

                    case HiveGlobals.SetupPending:

                        if (!hive.Globals.TryGetBool(HiveGlobals.UserDisableAutoUnseal, out var setupPending))
                        {
                            setupPending = true;
                        }

                        Console.Write(setupPending ? "true" : "false");
                        break;

                    case "sshkey-fingerprint":

                        Console.Write(hiveLogin.SshHiveHostKeyFingerprint);
                        break;

                    case "sshkey-client-pem":

                        Console.Write(hiveLogin.SshClientKey.PrivatePEM);
                        break;

                    case "sshkey-client-ppk":

                        Console.Write(hiveLogin.SshClientKey.PrivatePPK);
                        break;

                    case "ssh-password":

                        Console.Write(hiveLogin.SshPassword);
                        break;

                    case "ssh-username":

                        Console.Write(hiveLogin.SshUsername);
                        break;

                    case HiveGlobals.HiveMQSettingsApp:

                        if (hive.Globals.TryGetString(HiveGlobals.HiveMQSettingsBootstrap, out var hiveMQAppSettings))
                        {
                            Console.Write(hiveMQAppSettings);
                        }
                        else
                        {
                            Console.Error.WriteLine($"*** ERROR: Hive setting [{valueExpr}] does not exist.");
                            Program.Exit(1);
                        }
                        break;

                    case HiveGlobals.HiveMQSettingsBootstrap:

                        if (hive.Globals.TryGetString(HiveGlobals.HiveMQSettingsBootstrap, out var bootstrapSettings))
                        {
                            Console.Write(bootstrapSettings);
                        }
                        else
                        {
                            Console.Error.WriteLine($"*** ERROR: Hive setting [{valueExpr}] does not exist.");
                            Program.Exit(1);
                        }
                        break;

                    case HiveGlobals.HiveMQSettingsNeon:

                        if (hive.Globals.TryGetString(HiveGlobals.HiveMQSettingsBootstrap, out var hiveMQNeonSettings))
                        {
                            Console.Write(hiveMQNeonSettings);
                        }
                        else
                        {
                            Console.Error.WriteLine($"*** ERROR: Hive setting [{valueExpr}] does not exist.");
                            Program.Exit(1);
                        }
                        break;

                    case HiveGlobals.HiveMQSettingSysadmin:

                        if (hive.Globals.TryGetString(HiveGlobals.HiveMQSettingsBootstrap, out var hiveMQSysadminSettings))
                        {
                            Console.Write(hiveMQSysadminSettings);
                        }
                        else
                        {
                            Console.Error.WriteLine($"*** ERROR: Hive setting [{valueExpr}] does not exist.");
                            Program.Exit(1);
                        }
                        break;

                    case HiveGlobals.Uuid:

                        if (hive.Globals.TryGetString(HiveGlobals.Uuid, out var uuid))
                        {
                            Console.Write(uuid);
                        }
                        else
                        {
                            Console.Error.WriteLine($"*** ERROR: Hive setting [{valueExpr}] does not exist.");
                            Program.Exit(1);
                        }
                        break;

                    case "vault-token":

                        if (string.IsNullOrEmpty(hiveLogin.VaultCredentials.RootToken))
                        {
                            Console.WriteLine("*** ERROR: The current hive login does not have ROOT privileges.");
                            Program.Exit(1);
                        }

                        Console.Write(hiveLogin.VaultCredentials.RootToken);
                        break;

                    case HiveGlobals.Version:

                        if (hive.Globals.TryGetString(HiveGlobals.Version, out var version))
                        {
                            Console.Write(version);
                        }
                        else
                        {
                            Console.Error.WriteLine($"*** ERROR: Hive setting [{valueExpr}] does not exist.");
                            Program.Exit(1);
                        }
                        break;

                    default:

                        try
                        {
                            if (hive.Globals.TryGetString(valueExpr, out var value))
                            {
                                Console.Write(value);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.Error.WriteLine($"*** ERROR: {e.Message}].");
                            Program.Exit(1);
                        }
                        break;
                }
            }
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(shimability: DockerShimability.Optional, ensureConnection: true);
        }
    }
}
