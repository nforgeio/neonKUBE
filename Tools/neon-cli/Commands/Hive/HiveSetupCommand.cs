//-----------------------------------------------------------------------------
// FILE:	    HiveSetupCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Hive;
using Neon.HiveMQ;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>hive setup</b> command.
    /// </summary>
    public class HiveSetupCommand : CommandBase
    {
        private const string usage = @"
Configures a neonHIVE as described in the hive definition file.

USAGE: 

    neon hive setup [OPTIONS] root@HIVE

ARGUMENTS:

    HIVE        - The hive name.

OPTIONS:

    --unredacted        - Runs Vault and other commands with potential
                          secrets without redacting logs.  This is useful 
                          for debugging hive setup  issues.  
                          Do not use for production hives.
";
        private const string        logBeginMarker  = "# HIVE-BEGIN-SETUP ############################################################";
        private const string        logEndMarker    = "# HIVE-END-SETUP ##############################################################";
        private const string        logFailedMarker = "# HIVE-END-SETUP-FAILED #######################################################";

        private string              hiveLoginPath;
        private HiveLogin           hiveLogin;
        private HiveProxy           hive;
        private string              managerNodeNames     = string.Empty;
        private string              managerNodeAddresses = string.Empty;
        private int                 managerCount         = 0;
        private bool                sshTlsAuth;

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "hive", "setup" }; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--unredacted" }; }
        }

        /// <inheritdoc/>
        public override bool NeedsSshCredentials(CommandLine commandLine)
        {
            return true;
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            if (Program.HiveLogin != null)
            {
                Console.Error.WriteLine("*** ERROR: You are logged into a hive.  You need to logout before setting up another.");
                Program.Exit(1);
            }

            if (commandLine.Arguments.Length < 1)
            {
                Console.Error.WriteLine("*** ERROR: [root@HIVE] argument is required.");
                Program.Exit(1);
            }

            var login = HiveHelper.SplitLogin(commandLine.Arguments[0]);

            if (!login.IsOK)
            {
                Console.Error.WriteLine($"*** ERROR: Invalid username/hive [{commandLine.Arguments[0]}].  Expected something like: USER@HIVE");
                Program.Exit(1);
            }

            var username = login.Username;
            var hiveName = login.HiveName;

            hiveLoginPath = Program.GetHiveLoginPath(username, hiveName);

            if (!File.Exists(hiveLoginPath))
            {
                Console.Error.WriteLine($"*** ERROR: Be sure to prepare the hive first using [neon hive prepare...].  File [{hiveLoginPath}] not found.");
                Program.Exit(1);
            }

            hiveLogin      = NeonHelper.JsonDeserialize<HiveLogin>(File.ReadAllText(hiveLoginPath));
            hiveLogin.Path = hiveLoginPath;

            if (!hiveLogin.SetupPending)
            {
                Console.Error.WriteLine($"*** ERROR: Hive [{hiveLogin.HiveName}] has already been setup.");
            }

            // $hack(jeff.lill):
            //
            // The [hive setup] command used to use the [Program.MachineUsername] and [Program.MachinePassword]
            // settings directly but we changes this to let the [hive prepare] command do that and persist the
            // credentials to the hive login so the setup command will use those.  We needed to do that because
            // Azure no longer accepts the default password "sysadmin0000" as not meeting their new complexity
            // requirements and also because cloud VMs will now be provisioned with secure passwords right out
            // of the box.
            //
            // Note that if [hiveLogin.SshProvisionPassword == NULL] that means that all of the nodes have
            // already been set to use the secure password so we'll use that instead.
            //
            // We're going to set the Program credentials to here so that we'll end up using the credentials
            // from the login.  This isn't super clean.

            Program.MachineUsername = hiveLogin.SshUsername;
            Program.MachinePassword = hiveLogin.SshProvisionPassword ?? hiveLogin.SshPassword;

            // Note that hive setup appends to existing log files.

            hive = new HiveProxy(hiveLogin, Program.CreateNodeProxy<NodeDefinition>, appendLog: true, useBootstrap: true, defaultRunOptions: RunOptions.LogOutput | RunOptions.FaultOnError);

            // We need to ensure that any necessary VPN connection is opened if we're
            // not provisioning on-premise or not running in the tool container.

            if (hiveLogin.Definition.Vpn.Enabled && 
                hiveLogin.Definition.Hosting.IsCloudProvider && 
                !HiveHelper.InToolContainer)
            {
                HiveHelper.VpnOpen(hiveLogin,
                    onStatus: message => Console.WriteLine($"*** {message}"),
                    onError: message => Console.Error.WriteLine($"*** ERROR {message}"));
            }

            // Emulate a hive connection so the [HiveHelper] methods will work.

            HiveHelper.OpenHive(hiveLogin);

            // Generate a string with the IP addresses of the management nodes separated
            // by spaces.  We'll need this when we initialize the management nodes.
            //
            // We're also going to select the management address that we'll use to for
            // joining regular nodes to the hive.  We'll use the first management
            // node when sorting in ascending order by name for this.

            foreach (var managerNodeDefinition in hive.Definition.SortedManagers)
            {
                managerCount++;

                if (managerNodeNames.Length > 0)
                {
                    managerNodeNames     += " ";
                    managerNodeAddresses += " ";
                }

                managerNodeNames     += managerNodeDefinition.Name;
                managerNodeAddresses += managerNodeDefinition.PrivateAddress.ToString();
            }

            // Configure global options.

            if (commandLine.HasOption("--unredacted"))
            {
                hive.SecureRunOptions = RunOptions.None;
            }

            // Perform the setup operations.

            var controller = 
                new SetupController<NodeDefinition>(new string[] { "hive", "setup", $"[{hive.Name}]" }, hive.Nodes)
                {
                    ShowStatus  = !Program.Quiet,
                    MaxParallel = Program.MaxParallel
                };

            controller.AddWaitUntilOnlineStep("connect");

            switch (hive.Definition.HiveNode.SshAuth)
            {
                case AuthMethods.Password:

                    sshTlsAuth = false;
                    break;

                case AuthMethods.Tls:

                    sshTlsAuth = true;
                    break;

                default:

                    throw new NotSupportedException($"Unsupported SSH authentication method [{hive.Definition.HiveNode.SshAuth}].");
            }

            if (sshTlsAuth)
            {
                controller.AddStep("ssh client cert", 
                    (node, stepDelay) =>
                    {
                        GenerateClientSshKey(node, stepDelay);
                    },
                    node => node == hive.FirstManager);
            }

            controller.AddStep("verify OS", 
                (node, stepDelay) =>
                {
                    Thread.Sleep(stepDelay);
                    CommonSteps.VerifyOS(node);
                });

            // Write the operation begin marker to all hive node logs.

            hive.LogLine(logBeginMarker);

            // We're going to configure the managers separately from the workers
            // because we need to be careful about when we reboot the managers
            // since this will also take down the VPN.  We're also going to 
            // reboot all of the managers together after common manager 
            // configuration is complete for the same reason.

            controller.AddStep("manager initialize",
                (node, stepDelay) =>
                {
                    ConfigureCommon(node, stepDelay);
                },
                node => node.Metadata.IsManager);

            controller.AddStep("manager restart",
                (node, stepDelay) =>
                {
                    node.InvokeIdempotentAction("setup/common-restart",
                        () =>
                        {
                            Thread.Sleep(stepDelay);
                            RebootAndWait(node);
                        });
                },
                node => node.Metadata.IsManager,
                noParallelLimit: true);

            controller.AddStep("manager config",
                (node, stepDelay) =>
                {
                    ConfigureManager(node, stepDelay);
                },
                node => node.Metadata.IsManager,
                stepStaggerSeconds: hive.Definition.Setup.StepStaggerSeconds);

            // Configure the workers and pets.

            if (hive.Workers.Count() > 0 || hive.Pets.Count() > 0)
            {
                var workerPetStepLabel = "worker/pet config";

                if (hive.Workers.Count() == 0)
                {
                    workerPetStepLabel = "pet config";
                }
                else if (hive.Pets.Count() == 0)
                {
                    workerPetStepLabel = "worker config";
                }

                controller.AddStep(workerPetStepLabel,
                    (node, stepDelay) =>
                    {
                        ConfigureCommon(node, stepDelay);
                        node.InvokeIdempotentAction("setup/common-restart", () => RebootAndWait(node));
                        ConfigureNonManager(node);
                    },
                    node => node.Metadata.IsWorker || node.Metadata.IsPet,
                    stepStaggerSeconds: hive.Definition.Setup.StepStaggerSeconds);
            }

            // Create the Swarm.

            controller.AddStep("swarm create",
                (node, stepDelay) =>
                {
                    CreateSwarm(node, stepDelay);
                },
                node => node == hive.FirstManager);

            controller.AddStep("swarm join",
                (node, stepDelay) =>
                {
                    JoinSwarm(node, stepDelay);
                }, 
                node => node != hive.FirstManager && !node.Metadata.IsPet);

            // Continue with the configuration unless we're just installing bare Docker.

            if (!hive.Definition.BareDocker)
            {
                if (hive.Definition.HiveFS.Enabled)
                {
                    controller.AddStep("ceph user",
                        (node, stepDelay) =>
                        {
                            // Note that we're creating the Ceph user and group on all cluster
                            // nodes when Ceph is enabled so that it'll be easier for an operator
                            // to manually move services around without needing to create these
                            // as well.

                            CephUser(node, stepDelay);
                        });

                    controller.AddStep("ceph packages",
                        (node, stepDelay) =>
                        {
                            CephPackages(node, stepDelay);
                        },
                        stepStaggerSeconds: hive.Definition.Setup.StepStaggerSeconds);

                    controller.AddGlobalStep("ceph settings", () => CephSettings());

                    controller.AddStep("ceph bootstrap",
                        (node, stepDelay) =>
                        {
                            CephBootstrap(node, stepDelay);
                        },
                        node => node.Metadata.Labels.CephMON);

                    controller.AddStep("ceph cluster",
                        (node, stepDelay) =>
                        {
                            CephCluster(node, stepDelay);
                        }, 
                        noParallelLimit: true);
                }

                controller.AddStep("networks",
                    (node, stepDelay) =>
                        {
                            ConfigureHiveNetworks(node, stepDelay);
                        }, 
                        node => node == hive.FirstManager);

                controller.AddStep("node labels",
                    (node, stepDelay) =>
                    {
                        AddNodeLabels(node);
                    },
                    n => n == hive.FirstManager);

                if (hive.Definition.Docker.RegistryCache)
                {
                    var registryCacheConfigurator = new RegistryCache(hive, hiveLoginPath);

                    controller.AddStep("registry cache",
                        (node, stepDelay) =>
                        {
                            Thread.Sleep(stepDelay);
                            registryCacheConfigurator.Configure(node);
                        });
                }

                if (hive.Definition.Docker.RegistryCache && hive.Definition.NodeDefinitions.Count > 1)
                {
                    // The hive deploys a local registry cache and we have multiple
                    // nodes, so we're going to pull images on the first manager first 
                    // so they get loaded into the hive's register's registry cache 
                    // and then pull for all of the other nodes in a subsequent step
                    // (so we don't slam the Internet connection and the package mirrors.

                    controller.AddStep("pull images to cache",
                        (node, stepDelay) =>
                        {
                            Thread.Sleep(stepDelay);
                            PullImages(node, pullAll: true);
                        },
                        node => node == hive.FirstManager);

                    controller.AddStep("pull images to nodes",
                        (node, stepDelay) =>
                        {
                            Thread.Sleep(stepDelay);
                            PullImages(node);
                        }, 
                        node => node != hive.FirstManager);
                }
                else
                {
                    // Just pull the images to all nodes in parallel if there's 
                    // no registry cache deployed.

                    controller.AddStep("pull images",
                        (node, stepDelay) =>
                        {
                            Thread.Sleep(stepDelay);
                            PullImages(node);
                        });
                }

                controller.AddGlobalStep("hive key/value",
                    () =>
                    {
                        HiveHelper.OpenHive(hive);

                        ConfigureVaultProxy();
                        ConfigureVault();
                        ConfigureConsul();
                        ConfigureSecrets();
                    });

                var hiveServices = new HiveServices(hive);

                controller.AddGlobalStep("hive services", () => hiveServices.Configure(hive.FirstManager));

                controller.AddStep("hive containers",
                    (node, stepDelay) =>
                    {
                        hiveServices.DeployContainers(node, stepDelay);
                    });

                if (hive.Definition.Log.Enabled)
                {
                    var logServices = new LogServices(hive);

                    controller.AddGlobalStep("log services", () => logServices.Configure(hive.FirstManager));

                    controller.AddStep("log containers",
                        (node, stepDelay) =>
                        {
                            logServices.DeployContainers(node, stepDelay);
                        });

                    controller.AddGlobalStep("kibana config", () => logServices.ConfigureKibana(hive.FirstManager));
                }

                controller.AddGlobalStep("dashboards", () => ConfigureDashboards());

                controller.AddStep("check managers", 
                    (node, stepDelay) =>
                    {
                        Thread.Sleep(stepDelay);
                        HiveDiagnostics.CheckManager(node, hive.Definition);
                    }, 
                    node => node.Metadata.IsManager);

                controller.AddStep("check workers/pets",
                    (node, stepDelay) =>
                    {
                        Thread.Sleep(stepDelay);
                        HiveDiagnostics.CheckWorkersOrPet(node, hive.Definition);
                    }, 
                    node => node.Metadata.IsWorker || node.Metadata.IsPet);

                if (hive.Definition.Log.Enabled)
                {
                    controller.AddGlobalStep("check logging", () => HiveDiagnostics.CheckLogServices(hive));
                }
            }

            // Change the root account's password to something very strong.  
            // This step should be very close to the last one so it will still be
            // possible to log into nodes with the old password to diagnose
            // setup issues.
            //
            // Note that the if statement verifies that we haven't already generated
            // the strong password in a previous setup run that failed.  This prevents
            // us from generating a new password, perhaps resulting in hive nodes
            // having different passwords.

            if (!hiveLogin.HasStrongSshPassword)
            {
                if (hive.Definition.HiveNode.PasswordLength > 0)
                {
                    hiveLogin.SshPassword          = NeonHelper.GetRandomPassword(hive.Definition.HiveNode.PasswordLength);
                    hiveLogin.HasStrongSshPassword = true;
                }
                else
                {
                    hiveLogin.SshPassword = Program.MachinePassword;
                }

                hiveLogin.Save();
            }

            if (hive.Definition.HiveNode.PasswordLength > 0)
            {
                // $todo(jeff.lill):
                //
                // Note that this step isn't entirely idempotent.  The problem happens
                // when the password change fails on one or more of the nodes and succeeds
                // on others.  This will result in SSH connection failures for the nodes
                // that had their passwords changes.
                //
                // A possible workaround would be to try both the provisioning and new 
                // password when connecting to nodes, but I'm going to defer this.
                //
                //      https://github.com/jefflill/NeonForge/issues/397

                controller.AddStep("strong password",
                    (node, stepDelay) =>
                    {
                        SetStrongPassword(node, stepDelay);
                    });
            }

            controller.AddGlobalStep("passwords set", () =>
                {
                    // This hidden step sets the SSH provisioning password to NULL to 
                    // indicate that the finaly password has been set for all of the nodes.

                    hiveLogin.SshProvisionPassword = null;
                    hiveLogin.Save();
                },
                quiet: true);

            controller.AddGlobalStep("ssh certs", () => ConfigureSshCerts());

            // This needs to be run last because it will likely disable
            // SSH username/password authentication which may block
            // connection attempts.
            //
            // It's also handy to do this last so it'll be possible to 
            // manually login with the original credentials to diagnose
            // setup issues.

            controller.AddStep("ssh secured",
                (node, stepDelay) =>
                {
                    ConfigureSsh(node, stepDelay);
                });

            controller.AddGlobalStep("finish up",
                () =>
                {
                    // Some services (like [neon-hive-manager]) don't perform some
                    // activities until hive setup has completed, so we'll indicate
                    // that we're done.

                    hive.Globals.Set(HiveGlobals.SetupPending, false);
                });

            // Start setup.

            if (!controller.Run())
            {
                // Write the operation end/failed to all hive node logs.

                hive.LogLine(logFailedMarker);

                Console.Error.WriteLine("*** ERROR: One or more configuration steps failed.");
                Program.Exit(1);
            }

            // Update the hive login file.

            hiveLogin.SetupPending = false;
            hiveLogin.IsRoot       = true;
            hiveLogin.Username     = HiveConst.RootUser;
            hiveLogin.Definition   = hive.Definition;

            if (hive.Definition.Vpn.Enabled)
            {
                // We don't need to save the certificate authority files any more
                // because they've been stored in the hive Vault.

                hiveLogin.VpnCredentials.CaZip = null;
            }

            hiveLogin.Save();

            // Write the operation end marker to all hive node logs.

            hive.LogLine(logEndMarker);

            Console.WriteLine($"*** Logging into [{HiveConst.RootUser}@{hive.Definition.Name}].");

            // Note that we're going to login via the VPN for cloud environments
            // but not for local hosting since the operator had to be on-premise
            // to have just completed hive setup.
            var currentLogin =
                new CurrentHiveLogin()
                {
                    Login  = $"{HiveConst.RootUser}@{hive.Definition.Name}",
                    ViaVpn = hiveLogin.Definition.Hosting.Environment != HostingEnvironments.Machine
                };

            currentLogin.Save();

            Console.WriteLine();
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            var commandLine = shim.CommandLine.Shift(Words.Length);

            if (commandLine.HasOption("--remove-templates"))
            {
                // We'll run the command in [no-shim] mode for this option.

                return new DockerShimInfo(shimability: DockerShimability.None);
            }

            if (Program.HiveLogin != null)
            {
                Console.Error.WriteLine("*** ERROR: You are logged into a hive.  You need to logout before setting up another.");
                Program.Exit(1);
            }

            // The argument should be the user's hive login.

            if (commandLine.Arguments.Length < 1)
            {
                Console.Error.WriteLine("*** ERROR: [root@HIVE] argument is required.");
                Program.Exit(1);
            }

            var login = HiveHelper.SplitLogin(commandLine.Arguments[0]);

            if (!login.IsOK)
            {
                Console.Error.WriteLine($"*** ERROR: Invalid username/hive [{commandLine.Arguments[0]}].  Expected something like: USER@HIVE");
                Program.Exit(1);
            }

            var username = login.Username;
            var hiveName = login.HiveName;

            var hiveLoginPath = Program.GetHiveLoginPath(username, hiveName);

            if (!File.Exists(hiveLoginPath))
            {
                Console.Error.WriteLine($"*** ERROR: Be sure to prepare the hive first using [neon hive prepare...].  File [{hiveLoginPath}] not found.");
                Program.Exit(1);
            }

            hiveLogin      = NeonHelper.JsonDeserialize<HiveLogin>(File.ReadAllText(hiveLoginPath));
            hiveLogin.Path = hiveLoginPath;

            hiveLogin.Definition.Validate();

            if (!hiveLogin.SetupPending)
            {
                Console.Error.WriteLine($"*** ERROR: Hive [{hiveName}] has already been setup.");
            }

            // Cloud deployments had their VPN configured when they were prepared
            // so we need to connect the VPN now so we can setup the nodes.  
            //
            // On-premise hives are always setup via local network connections
            // so we will be connecting the VPN. 

            if (hiveLogin.Definition.Vpn.Enabled &&
                hiveLogin.Definition.Hosting.IsCloudProvider)
            {
                HiveHelper.VpnOpen(hiveLogin,
                    onStatus: message => Console.WriteLine($"*** {message}"),
                    onError: message => Console.Error.WriteLine($"*** ERROR: {message}"));
            }

            // We're going to use WinSCP to convert the OpenSSH PEM formatted key
            // to the PPK format PuTTY/WinSCP require.  Note that this won't work
            // when the tool is running in a Docker Linux container.  We're going
            // to handle the conversion here as a post run action.

            shim.SetPostAction(
                exitCode =>
                {
                    if (exitCode != 0)
                    {
                        return;
                    }

                    var pemKeyPath = Path.Combine(Program.HiveTempFolder, Guid.NewGuid().ToString("D"));
                    var ppkKeyPath = Path.Combine(Program.HiveTempFolder, Guid.NewGuid().ToString("D"));

                    try
                    {
                        // Reload the login to pick up changes from the shimmed command.

                        hiveLogin      = HiveHelper.LoadHiveLogin(HiveConst.RootUser, hiveLogin.Definition.Name);
                        hiveLogin.Path = hiveLoginPath;

                        // Update the PuTTY/WinSCP key.

                        File.WriteAllText(pemKeyPath, hiveLogin.SshClientKey.PrivatePEM);

                        ExecuteResult result;

                        try
                        {
                            result = NeonHelper.ExecuteCapture("winscp.com", $@"/keygen ""{pemKeyPath}"" /comment=""{hiveLogin.Definition.Name} Key"" /output=""{ppkKeyPath}""");
                        }
                        catch (Win32Exception)
                        {
                            return; // Tolerate when WinSCP isn't installed.
                        }

                        if (result.ExitCode != 0)
                        {
                            Console.WriteLine(result.OutputText);
                            Console.Error.WriteLine(result.ErrorText);
                            Program.Exit(result.ExitCode);
                        }

                        hiveLogin.SshClientKey.PrivatePPK = File.ReadAllText(ppkKeyPath);

                        hiveLogin.Save();
                    }
                    finally
                    {
                        if (File.Exists(pemKeyPath))
                        {
                            File.Delete(pemKeyPath);
                        }

                        if (File.Exists(ppkKeyPath))
                        {
                            File.Delete(ppkKeyPath);
                        }
                    }
                });

            return new DockerShimInfo(shimability: DockerShimability.Optional, ensureConnection: false);
        }

        /// <summary>
        /// Basic configuration that will happen every time if DEBUG setup
        /// mode is ENABLED or else will be invoked idempotently (if that's 
        /// a word).
        /// </summary>
        /// <param name="node">The target hive node.</param>
        private void ConfigureBasic(SshProxy<NodeDefinition> node)
        {
            // Configure the node's environment variables.

            CommonSteps.ConfigureEnvironmentVariables(node, hive.Definition);

            // Upload the setup and configuration files.

            node.CreateHiveHostFolders();
            node.UploadConfigFiles(hive.Definition);
            node.UploadResources(hive.Definition);
        }

        /// <summary>
        /// Performs common node configuration.
        /// </summary>
        /// <param name="node">The target hive node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void ConfigureCommon(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            //-----------------------------------------------------------------
            // NOTE: 
            //
            // We're going to perform the following steps outside of the
            // idempotent check to make it easier to debug and modify 
            // scripts and tools when hive setup has been partially
            // completed.  These steps are implicitly idempotent and
            // complete pretty quickly.

            if (Program.Debug)
            {
                ConfigureBasic(node);
            }

            //-----------------------------------------------------------------
            // Ensure the following steps are executed only once.

            node.InvokeIdempotentAction("setup/common",
                () =>
                {
                    Thread.Sleep(stepDelay);

                    if (!Program.Debug)
                    {
                        ConfigureBasic(node);
                    }

                    // Ensure that the node has been prepared for setup.

                    CommonSteps.PrepareNode(node, hive.Definition);

                    // Create the [/mnt-data] folder if it doesn't already exist.  This folder
                    // is where we're going to host the Docker containers and volumes that should
                    // have been initialized to link to any data drives attached to the machine
                    // or simply be located on the OS drive.  This may not be initialized for
                    // some prepared nodes, so we'll create this on the OS drive if necessary.

                    if (!node.DirectoryExists("/mnt-data"))
                    {
                        node.SudoCommand("mkdir -p /mnt-data");
                    }

                    // Configure the APT proxy server settings early.

                    node.Status = "run: setup-apt-proxy.sh";
                    node.SudoCommand("setup-apt-proxy.sh");

                    // Perform basic node setup including changing the hostname.

                    UploadHostsFile(node);
                    UploadHostEnvFile(node);

                    node.Status = "run: setup-node.sh";
                    node.SudoCommand("setup-node.sh");

                    // Create and configure the internal hive self-signed certificates.

                    node.Status = "install: certs";

                    if (node.Metadata.IsManager)
                    {
                        // Configure the Vault service certificate (with private key).

                        node.SudoCommand("mkdir -p /etc/vault");
                        node.UploadText($"/etc/vault/vault.crt", hiveLogin.HiveCertificate.CertPem);
                        node.UploadText($"/etc/vault/vault.key", hiveLogin.HiveCertificate.KeyPem);
                        node.SudoCommand("chmod 600 /etc/vault/*");
                    }

                    node.SudoCommand("mkdir -p /etc/neon/certs");
                    node.SudoCommand("chmod 700 /etc/neon/certs");
                    node.UploadText($"/etc/neon/certs/hive.crt", hiveLogin.HiveCertificate.CertPem);
                    node.UploadText($"/etc/neon/certs/hive.key", hiveLogin.HiveCertificate.KeyPem);
                    node.SudoCommand("chmod 600 /etc/neon/certs/*");

                    // Configure the Consul service certificate (with private key).  Note that
                    // these need to be configured on all hive nodes because the manager runs
                    // the Consul service as the masters and the workers and pets run the
                    // service as a proxy.

                    node.SudoCommand("mkdir -p /etc/consul.d");
                    node.UploadText($"/etc/consul.d/consul.crt", hiveLogin.HiveCertificate.CertPem);
                    node.UploadText($"/etc/consul.d/consul.key", hiveLogin.HiveCertificate.KeyPem);
                    node.SudoCommand("chmod 600 /etc/consul.d/*");

                    // Upload the hive certificates (without private key) to all hive nodes
                    // so they'll be trusted implicitly.
                    //
                    // Note that uploading a registry cache certificiate even if that isn't
                    // currently enabled in the cluster definition so it'll be easy to upgrade 
                    // the hive later.

                    node.SudoCommand("mkdir -p /usr/local/share/ca-certificates");

                    var hiveName = hiveLogin.Definition.Name.ToLowerInvariant();

                    node.UploadText($"/usr/local/share/ca-certificates/hive-{hiveName}.crt", hiveLogin.HiveCertificate.CertPem);

                    node.SudoCommand("chmod 644 /usr/local/share/ca-certificates/*");
                    node.SudoCommand("update-ca-certificates");

                    // Tune Linux for SSDs, if enabled.

                    node.Status = "run: setup-ssd.sh";
                    node.SudoCommand("setup-ssd.sh");
                });
        }

        /// <summary>
        /// Reboots the nodes and waits until the package manager is ready.
        /// </summary>
        /// <param name="node">The hive node.</param>
        private void RebootAndWait(SshProxy<NodeDefinition> node)
        {
            node.Status = "restarting...";
            node.Reboot(wait: true);
        }

        /// <summary>
        /// Generates and uploads the <b>/etc/hosts</b> file for a node.
        /// </summary>
        /// <param name="node">The target node.</param>
        private void UploadHostsFile(SshProxy<NodeDefinition> node)
        {
            var sbHosts = new StringBuilder();

            var nodeAddress = node.PrivateAddress.ToString();
            var separator   = new string(' ', Math.Max(16 - nodeAddress.Length, 1));

            sbHosts.Append(
$@"
127.0.0.1	    localhost
{nodeAddress}{separator}{node.Name}
::1             localhost ip6-localhost ip6-loopback
ff02::1         ip6-allnodes
ff02::2         ip6-allrouters
");
            node.UploadText("/etc/hosts", sbHosts.ToString(), 4, Encoding.UTF8);
        }

        /// <summary>
        /// Generates and uploads the <b>/etc/neon/host-env</b> file for a node.
        /// </summary>
        /// <param name="node">The target node.</param>
        private void UploadHostEnvFile(SshProxy<NodeDefinition> node)
        {
            var sbEnvHost       = new StringBuilder();
            var vaultDirectLine = string.Empty;
            var consulDefs      = string.Empty;

            if (node.Metadata.IsManager)
            {
                vaultDirectLine = $"export VAULT_DIRECT_ADDR={hive.Definition.GetVaultDirectUri(node.Name)}";
            }

            if (hive.Definition.Consul.Tls)
            {
                consulDefs =
$@"
export CONSUL_HTTP_SSL=true
export CONSUL_HTTP_ADDR={hive.Definition.Hostnames.Consul}:{hive.Definition.Consul.Port}
export CONSUL_HTTP_FULLADDR=https://{hive.Definition.Hostnames.Consul}:{hive.Definition.Consul.Port}
";
            }
            else
            {
                consulDefs =
$@"
export CONSUL_HTTP_SSL=false
export CONSUL_HTTP_ADDR={hive.Definition.Hostnames.Consul}:{hive.Definition.Consul.Port}
export CONSUL_HTTP_FULLADDR=http://{hive.Definition.Hostnames.Consul}:{hive.Definition.Consul.Port}
";
            }

            sbEnvHost.AppendLine(
$@"#------------------------------------------------------------------------------
# FILE:         /etc/neon/host-env
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# This script can be mounted into containers that require extended knowledge
# about the hive and host node.  This will be generally be mounted to the container
# at [/etc/neon/host-env] such that the container entrypoint script can execute it.

# Define the hive and Docker host related environment variables.

export NEON_HIVE={hive.Definition.Name}
export NEON_DATACENTER={hive.Definition.Datacenter}
export NEON_ENVIRONMENT={hive.Definition.Environment}
export NEON_HOSTING={hive.Definition.Hosting.Environment.ToString().ToLowerInvariant()}
export NEON_NODE_NAME={node.Name}
export NEON_NODE_ROLE={node.Metadata.Role}
export NEON_NODE_IP={node.Metadata.PrivateAddress}
export NEON_NODE_SSD={node.Metadata.Labels.StorageSSD.ToString().ToLowerInvariant()}
export NEON_APT_PROXY={HiveHelper.GetPackageProxyReferences(hive.Definition)}

export VAULT_ADDR={hive.Definition.VaultProxyUri}
{vaultDirectLine}
{consulDefs}

# Define the hive-specific host names.

export HiveHostnames_Base=$NEON_HIVE.nhive.io
export HiveHostnames_RegistryCache=neon-registry-cache.$NEON_HIVE.nhive.io
export HiveHostnames_LogEsData=neon-log-esdata.$NEON_HIVE.nhive.io
export HiveHostnames_Consul=neon-consul.$NEON_HIVE.nhive.io
export HiveHostnames_Vault=neon-vault.$NEON_HIVE.nhive.io
export HiveHostnames_RabbitMQ=neon-hivemq.$NEON_HIVE.nhive.io
export HiveHostnames_UpdateHosts=neon-hosts-fixture-modify.$NEON_HIVE.nhive.io

if [ -d /mnt/host/ca-certificates ] ; then

    # This ensures that the hive certificates mounted from the host to
    # [/usr/local/share/ca-certificates] are trusted by the container.
    # We currently support the following tools if they exist within
    # the container:
    #
    #   update-ca-certificates: Debian, Ubuntu, Alpine,...
    #   
    #   update-ca-trust: RedHat, CentOS...
    #
    # Note that custom applications (like Java) may need additional
    # certificate initialization.  You can do this in your image
    # entrypoint script.

    if which update-ca-certificates ; then

        cp /mnt/host/ca-certificates/* /usr/local/share/ca-certificates
        update-ca-certificates

    elif which update-ca-trust ; then

        cp /mnt/host/ca-certificates/* /usr/share/pki/ca-trust-source/anchors/
        update-ca-trust force-enable
        update-ca-trust extract
    fi
fi
");
            node.UploadText($"{HiveHostFolders.Config}/host-env", sbEnvHost.ToString(), 4, Encoding.UTF8);
        }

        /// <summary>
        /// Generates the Consul configuration file for a hive node.
        /// </summary>
        /// <param name="node">The target hive node.</param>
        /// <returns>The configuration file text.</returns>
        private string GetConsulConfig(SshProxy<NodeDefinition> node)
        {
            var consulTls  = node.Hive.Definition.Consul.Tls;
            var consulDef  = node.Hive.Definition.Consul;
            var consulConf = new JObject();

            consulConf.Add("log_level", "info");
            consulConf.Add("datacenter", hive.Definition.Datacenter);
            consulConf.Add("node_name", node.Name);
            consulConf.Add("data_dir", "/mnt-data/consul");
            consulConf.Add("advertise_addr", node.Metadata.PrivateAddress.ToString());
            consulConf.Add("client_addr", "0.0.0.0");

            var ports = new JObject();

            ports.Add("http", consulTls ? -1 : 8500);
            ports.Add("https", consulTls ? 8500 : -1);
            ports.Add("dns", 8600);     // This is the default Consul DNS port.

            consulConf.Add("ports", ports);

            if (consulTls)
            {
                consulConf.Add("cert_file", "/etc/consul.d/consul.crt");
                consulConf.Add("key_file", "/etc/consul.d/consul.key");
            }

            consulConf.Add("ui", true);
            consulConf.Add("leave_on_terminate", false);
            consulConf.Add("skip_leave_on_interrupt", true);
            consulConf.Add("disable_remote_exec", true);
            consulConf.Add("domain", "hive");

            var recursors = new JArray();

            foreach (var nameserver in hive.Definition.Network.Nameservers)
            {
                recursors.Add(nameserver);
            }
        
            consulConf.Add("recursors", recursors);

            var dnsConfig  = new JObject();
            var serviceTtl = new JObject();

            if (consulDef.DnsMaxStale > 0)
            {
                dnsConfig.Add("allow_stale", true);
                dnsConfig.Add("max_stale", $"{consulDef.DnsMaxStale}s");
            }
            else
            {
                dnsConfig.Add("allow_stale", false);
                dnsConfig.Add("max_stale", "0s");
            }

            dnsConfig.Add("node_ttl", $"{consulDef.DnsTTL}s");

            serviceTtl.Add("*", $"{consulDef.DnsTTL}s");
            dnsConfig.Add("service_ttl", serviceTtl);
            consulConf.Add("dns_config", dnsConfig);

            if (node.Metadata.IsManager)
            {
                consulConf.Add("bootstrap_expect", hive.Definition.Managers.Count());

                var performance = new JObject();

                performance.Add("raft_multiplier", 1);

                consulConf.Add("performance", performance);
            }
            else
            {
                var managerAddresses = new JArray();

                foreach (var manager in hive.Managers)
                {
                    managerAddresses.Add(manager.Metadata.PrivateAddress.ToString());
                }

                consulConf.Add("retry_join", managerAddresses);
                consulConf.Add("retry_interval", "30s");
            }

            return consulConf.ToString(Formatting.Indented);
        }

        /// <summary>
        /// Returns the contents of the <b>/etc/docker/daemon.json</b> file to
        /// be provisioned on a node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <returns>The Docker settings as JSON text.</returns>
        private string GetDockerConfig(SshProxy<NodeDefinition> node)
        {
            var settings = new JObject();

#if USERNS_REMAP
            // $todo(jeff.lill): https://github.com/moby/moby/issues/37560
            if (node.Hive.Definition.Docker.UsernsRemap)
            {
                settings.Add("userns-remap", "default");
            }
#endif
            // Logging configuration.

            settings.Add("log-level", "info");
            settings.Add("log-driver", hive.Definition.Docker.LogDriver);

            var logOptions = new JObject();

            foreach (var option in hive.Definition.Docker.LogOptions.Split(';'))
            {
                if (string.IsNullOrWhiteSpace(option))
                {
                    continue;   // Ignore blank options.
                }

                var fields = option.Split(new char[] { '=' }, 2);

                if (fields.Length == 1)
                {
                    logOptions.Add(fields[0], string.Empty);    // Not sure if this is reallt necessary (but it probably won't hurt).
                }
                else
                {
                    logOptions.Add(fields[0], fields[1]);
                }
            }

            settings.Add("log-opts", logOptions);

            // Storage driver configuration.

            switch (Program.OSProperties.StorageDriver)
            {
                case DockerStorageDrivers.Aufs:

                    settings.Add("storage-driver", "aufs");
                    break;

                case DockerStorageDrivers.Overlay:

                    settings.Add("storage-driver", "overlay");
                    break;

                case DockerStorageDrivers.Overlay2:

                    settings.Add("storage-driver", "overlay2");
                    break;

                default:

                    throw new NotImplementedException($"Unsupported storage driver: {Program.OSProperties.StorageDriver}.");
            }

            // Configure experimental Docker behavior. 

            settings.Add("experimental", hive.Definition.Docker.Experimental);

            // Specify any registry caches followed by the Docker public registry.

            var registries = new JArray();

            if (!hive.Definition.BareDocker && hive.Definition.Docker.RegistryCache)
            {
                foreach (var manager in hive.Definition.SortedManagers)
                {
                    registries.Add($"https://{manager.Name}.{hive.Definition.Hostnames.RegistryCache}:{HiveHostPorts.DockerRegistryCache}");
                }
            }

            registries.Add($"https://{HiveConst.DockerPublicRegistry}");

            settings.Add("registry-mirrors", registries);

            // I believe this will set the default network MTU for all Docker networks.

            settings.Add("mtu", hive.Definition.Network.MTU);

            return NeonHelper.JsonSerialize(settings, Formatting.Indented);
        }

        /// <summary>
        /// Configures the required VPN return routes for on-premise hives 
        /// that enable VPN.
        /// </summary>
        /// <param name="node">The target hive node.</param>
        private void ConfigureVpnPoolRoutes(SshProxy<NodeDefinition> node)
        {
            if (!hive.Definition.Vpn.Enabled)
            {
                // Note that cloud deployments handle VPN return routing via routing tables setup
                // during hive preparation.

                return;
            }

            node.Status = "vpn return routes";

            // Since hive VPN traffic is coming throught the manager nodes and
            // isn't coming through the hive router directly, we need to setup
            // explict routes on each node that will direct packets back to the
            // manager where specific client VPN connections are terminated.
            //
            // We need to make the routes available for the current session and
            // also edit [/etc/network/interfaces] to add the routes during 
            // after a machine or networking restart.
            //
            // Note that even managers will need VPN return routes to the other
            // managers.

            // Create a list of route commands for the current node.

            var primaryInterface = node.GetNetworkInterface(node.PrivateAddress);
            var routeCommands    = new List<string>();

            foreach (var manager in hive.Managers.Where(m => m != node))
            {
                routeCommands.Add($"ip route add {manager.Metadata.VpnPoolSubnet} via {manager.PrivateAddress} dev {primaryInterface} || true");
            }

            // Execute the route commands on the node so they will be 
            // available immediately.

            foreach (var command in routeCommands)
            {
                node.SudoCommand(command);
            }

            // Read the existing [/etc/network/interfaces] file and strip out
            // any existing section that looks like:
            //
            //      # BEGIN-VPN-RETURN-ROUTES
            //        ...
            //      # END-VPN-RETURN-ROUTES
            //
            // Then append the a new section of the commands to the end of
            // the file with the [up] prefix and write it back to the node.

            var existingInterfaces = node.DownloadText("/etc/network/interfaces");
            var newInterfaces      = new StringBuilder();
            var inReturnSection    = false;

            foreach (var line in new StringReader(existingInterfaces).Lines())
            {
                if (line.StartsWith("# BEGIN-VPN-RETURN-ROUTES"))
                {
                    inReturnSection = true;
                    continue;
                }
                else if (line.StartsWith("# END-VPN-RETURN-ROUTES"))
                {
                    inReturnSection = false;
                    continue;
                }

                if (!inReturnSection)
                {
                    newInterfaces.AppendLine(line);
                }
            }

            if (routeCommands.Count > 0)
            {
                newInterfaces.AppendLine("# BEGIN-VPN-RETURN-ROUTES");

                foreach (var command in routeCommands)
                {
                    newInterfaces.AppendLine($"up {command}");
                }

                newInterfaces.AppendLine("# END-VPN-RETURN-ROUTES");
            }

            node.UploadText("/etc/network/interfaces", newInterfaces.ToString());
        }

        /// <summary>
        /// Completes manager node configuration.
        /// </summary>
        /// <param name="node">The target hive node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void ConfigureManager(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            node.InvokeIdempotentAction("setup/manager",
                () =>
                {
                    Thread.Sleep(stepDelay);

                    // Configure the APT package proxy on the managers
                    // and configure the proxy selector for all nodes.

                    node.Status = "run: setup-apt-proxy.sh";
                    node.SudoCommand("setup-apt-proxy.sh");

                    // Upgrade Linux packages if requested.  We're doing this after
                    // deploying the APT package proxy so it'll be faster.

                    switch (hive.Definition.HiveNode.Upgrade)
                    {
                        case OsUpgrade.Partial:

                            node.Status = "package upgrade (partial)";

                            node.SudoCommand("safe-apt-get upgrade -yq");
                            break;

                        case OsUpgrade.Full:

                            node.Status = "package upgrade (full)";

                            node.SudoCommand("safe-apt-get dist-upgrade -yq");
                            break;
                    }

                    // Check to see whether the upgrade requires a reboot and
                    // do that now if necessary.

                    // $todo(jeff.lill):
                    //
                    // This is probably an extra step for managers because we
                    // already restart managers after the [manager initialize]
                    // step.  Perhaps we could relocate manager upgrade to
                    // the initialize step and avoid this extra reboot.
                    //
                    // I'm not going to mess with this right now because there
                    // could be some impacts on VPN config etc.

                    if (node.FileExists("/var/run/reboot-required"))
                    {
                        node.Status = "reboot after update";
                        node.Reboot();
                    }

                    // Setup NTP.

                    node.Status = "run: setup-ntp.sh";
                    node.SudoCommand("setup-ntp.sh");

                    // Configure the VPN return routes.

                    ConfigureVpnPoolRoutes(node);

                    // Setup the Consul server and join it to the Consul cluster.

                    node.Status = "upload: consul.json";
                    node.SudoCommand("mkdir -p /etc/consul.d");
                    node.SudoCommand("chmod 770 /etc/consul.d");
                    node.UploadText("/etc/consul.d/consul.json", GetConsulConfig(node));

                    node.Status = "run: setup-consul-server.sh";
                    node.SudoCommand("setup-consul-server.sh", hive.Definition.Consul.EncryptionKey);

                    if (!hive.Definition.BareDocker)
                    {
                        // Bootstrap Consul cluster discovery.

                        node.InvokeIdempotentAction("setup/consul-bootstrap",
                            () =>
                            {
                                var discoveryTimer = new PolledTimer(TimeSpan.FromMinutes(2));

                                node.Status = "consul cluster bootstrap";

                                while (true)
                                {
                                    if (node.SudoCommand($"consul join {managerNodeAddresses}", RunOptions.None).ExitCode == 0)
                                    {
                                        break;
                                    }

                                    if (discoveryTimer.HasFired)
                                    {
                                        node.Fault($"Unable to form Consul cluster within [{discoveryTimer.Interval}].");
                                        break;
                                    }

                                    Thread.Sleep(TimeSpan.FromSeconds(5));
                                }
                            });

                        // Install Vault server.

                        node.Status = "run: setup-vault-server.sh";
                        node.SudoCommand("setup-vault-server.sh");
                    }

                    // Setup Docker

                    node.Status = "setup docker";

                    node.SudoCommand("mkdir -p /etc/docker");
                    node.UploadText("/etc/docker/daemon.json", GetDockerConfig(node));
                    node.SudoCommand("chmod 640 /etc/docker/daemon.json");

                    var dockerRetry = new LinearRetryPolicy(typeof(TransientException), maxAttempts: 5, retryInterval: TimeSpan.FromSeconds(5));

                    dockerRetry.InvokeAsync(
                        async () =>
                        {
                            var response = node.SudoCommand("setup-docker.sh", node.DefaultRunOptions & ~RunOptions.FaultOnError);

                            if (response.ExitCode != 0)
                            {
                                throw new TransientException(response.ErrorText);
                            }

                            await Task.CompletedTask;

                        }).Wait();

                    // Clean up any cached APT files.

                    node.Status = "clean up";
                    node.SudoCommand("safe-apt-get clean -yq");
                    node.SudoCommand("rm -rf /var/lib/apt/lists");
                });
        }

        /// <summary>
        /// Creates the initial swarm on the bootstrap manager node passed and 
        /// captures the manager and worker swarm tokens required to join additional
        /// nodes to the hive.
        /// </summary>
        /// <param name="bootstrapManager">The target bootstrap manager server.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void CreateSwarm(SshProxy<NodeDefinition> bootstrapManager, TimeSpan stepDelay)
        {
            if (hiveLogin.SwarmManagerToken != null && hiveLogin.SwarmWorkerToken != null)
            {
                return; // Swarm has already been created.
            }

            Thread.Sleep(stepDelay);

            bootstrapManager.Status = "create swarm";
            bootstrapManager.DockerCommand(RunOptions.FaultOnError, $"docker swarm init --advertise-addr {bootstrapManager.Metadata.PrivateAddress}:{hive.Definition.Docker.SwarmPort}");

            var response = bootstrapManager.DockerCommand(RunOptions.FaultOnError, $"docker swarm join-token manager");

            hiveLogin.SwarmManagerToken = ExtractSwarmToken(response.OutputText);

            response = bootstrapManager.DockerCommand(RunOptions.FaultOnError, $"docker swarm join-token worker");

            hiveLogin.SwarmWorkerToken = ExtractSwarmToken(response.OutputText);

            // Persist the swarm tokens into the hive login.

            hiveLogin.Save();
        }

        /// <summary>
        /// Extracts the Swarm token from a <b>docker swarm join-token [manager|worker]</b> 
        /// command.  The token returned can be used when adding additional nodes to the hive.
        /// </summary>
        /// <param name="commandResponse">The command response string.</param>
        /// <returns>The swarm token.</returns>
        private string ExtractSwarmToken(string commandResponse)
        {
            const string tokenOpt = "--token ";

            var startPos = commandResponse.IndexOf(tokenOpt);
            var errorMsg = $"Cannot extract swarm token from:\r\n\r\n{commandResponse}";

            if (startPos == -1)
            {
                throw new HiveException(errorMsg);
            }

            if (startPos == -1)
            {
                throw new HiveException(errorMsg);
            }

            startPos += tokenOpt.Length;

            // It looks like the format for output has changed.  Older releases
            // like [17.03-ce] have a backslash to continue the example command.
            // Newer versions have a space and additional arguments on the same
            // line.  We're going to handle both.

            var endPos = commandResponse.IndexOf("\\", startPos);

            if (endPos == -1)
            {
                endPos = commandResponse.IndexOfAny(new char[] { ' ', '\r', '\n' }, startPos);
            }

            if (endPos == -1)
            {
                throw new HiveException($"Cannot extract swarm token from:\r\n\r\n{commandResponse}");
            }

            return commandResponse.Substring(startPos, endPos - startPos).Trim();
        }

        /// <summary>
        /// Configures non-manager nodes like workers or individuals.
        /// </summary>
        /// <param name="node">The target hive node.</param>
        private void ConfigureNonManager(SshProxy<NodeDefinition> node)
        {
            node.InvokeIdempotentAction($"setup/{node.Metadata.Role}",
                () =>
                {
                    // Configure the APT package proxy on the managers
                    // and configure the proxy selector for all nodes.

                    node.Status = "run: setup-apt-proxy.sh";
                    node.SudoCommand("setup-apt-proxy.sh");

                    // Upgrade Linux packages if requested.  We're doing this after
                    // deploying the APT package proxy so it'll be faster.

                    switch (hive.Definition.HiveNode.Upgrade)
                    {
                        case OsUpgrade.Partial:

                            node.Status = "package upgrade (partial)";

                            node.SudoCommand("safe-apt-get upgrade -yq");
                            break;

                        case OsUpgrade.Full:

                            node.Status = "package upgrade (full)";

                            node.SudoCommand("safe-apt-get dist-upgrade -yq");
                            break;
                    }

                    // Check to see whether the upgrade requires a reboot and
                    // do that now if necessary.

                    if (node.FileExists("/var/run/reboot-required"))
                    {
                        node.Status = "reboot after update";
                        node.Reboot();
                    }

                    // Setup NTP.

                    node.Status = "run: setup-ntp.sh";
                    node.SudoCommand("setup-ntp.sh");

                    // Configure the VPN return routes.

                    ConfigureVpnPoolRoutes(node);

                    if (!hive.Definition.BareDocker)
                    {
                        // Setup the Consul proxy and join it to the Consul cluster.

                        node.Status = "upload: consul.json";
                        node.SudoCommand("mkdir -p /etc/consul.d");
                        node.SudoCommand("chmod 770 /etc/consul.d");
                        node.UploadText("/etc/consul.d/consul.json", GetConsulConfig(node));

                        node.Status = "run: setup-consul-proxy.sh";
                        node.SudoCommand("setup-consul-proxy.sh", hive.Definition.Consul.EncryptionKey);

                        // Join this node's Consul agent with the master(s).

                        node.InvokeIdempotentAction("setup/consul-join",
                            () =>
                            {
                                var discoveryTimer = new PolledTimer(TimeSpan.FromMinutes(5));

                                node.Status = "join consul cluster";

                                while (true)
                                {
                                    if (node.SudoCommand($"consul join {managerNodeAddresses}", RunOptions.None).ExitCode == 0)
                                    {
                                        break;
                                    }

                                    if (discoveryTimer.HasFired)
                                    {
                                        node.Fault($"Unable to join Consul cluster within [{discoveryTimer.Interval}].");
                                        break;
                                    }

                                    Thread.Sleep(TimeSpan.FromSeconds(5));
                                }
                            });
                    }

                    // Setup Docker.

                    node.Status = "setup docker";

                    node.SudoCommand("mkdir -p /etc/docker");
                    node.UploadText("/etc/docker/daemon.json", GetDockerConfig(node));

                    var dockerRetry = new LinearRetryPolicy(typeof(TransientException), maxAttempts: 5, retryInterval: TimeSpan.FromSeconds(5));

                    dockerRetry.InvokeAsync(
                        async () =>
                        {
                            var response = node.SudoCommand("setup-docker.sh", node.DefaultRunOptions & ~RunOptions.FaultOnError);

                            if (response.ExitCode != 0)
                            {
                                throw new TransientException(response.ErrorText);
                            }

                            await Task.CompletedTask;

                        }).Wait();

                    // Other initialization.

                    if (!hive.Definition.BareDocker)
                    {
                        // Configure Vault client.

                        node.Status = "run: setup-vault-client.sh";
                        node.SudoCommand("setup-vault-client.sh");
                    }

                    // Clean up any cached APT files.

                    node.Status = "clean up";
                    node.SudoCommand("safe-apt-get clean -yq");
                    node.SudoCommand("rm -rf /var/lib/apt/lists");
                });
        }

        /// <summary>
        /// Creates the standard hive overlay networks.
        /// </summary>
        /// <param name="manager">The manager node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void ConfigureHiveNetworks(SshProxy<NodeDefinition> manager, TimeSpan stepDelay)
        {
            Thread.Sleep(stepDelay);

            manager.InvokeIdempotentAction("setup/ingress-network",
                () =>
                {
                    // We need to delete and recreate the Docker ingress network so we can
                    // reduce the MTU from 1500 to 1492.  I believe this is what was causing
                    // timeouts and perhaps poor performance when hive nodes are deployed as
                    // Hyper-V and XEN virtual machines.

                    var ingressScript =
$@"
# Delete the [ingress] network.

docker network rm ingress << EOF
y
EOF

# Give the network a chance to actually be deleted.

sleep 10

# Recreate the [ingress] network with the new settings.

docker network create \
   --driver overlay \
   --ingress \
   --subnet={hive.Definition.Network.IngressSubnet} \
   --gateway={hive.Definition.Network.IngressGateway} \
   --opt com.docker.network.mtu={hive.Definition.Network.IngressMTU} \
   ingress
";
                    var bundle = new CommandBundle(". ./ingress.sh");

                    bundle.AddFile("ingress.sh", ingressScript, isExecutable: true);

                    manager.Status = "network: ingress MTU and subnet";
                    manager.SudoCommand(bundle);
                });

            manager.InvokeIdempotentAction("setup/neon-public-network",
                () =>
                {
                    manager.Status = "network: neon-public";
                    manager.DockerCommand(
                        "docker network create",
                        "--driver", "overlay",
                        "--subnet", hive.Definition.Network.PublicSubnet,
                        "--opt", "encrypt",
                        hive.Definition.Network.PublicAttachable ? "--attachable" : null,
                        HiveConst.PublicNetwork);
                });

            manager.InvokeIdempotentAction("setup/neon-private-network",
                () =>
                {
                    manager.Status = "network: neon-private";
                    manager.DockerCommand(
                        "docker network create",
                        "--driver", "overlay",
                        "--subnet", hive.Definition.Network.PrivateSubnet,
                        "--opt", "encrypt",
                        hive.Definition.Network.PrivateAttachable ? "--attachable" : null,
                        HiveConst.PrivateNetwork);
                });
        }

        /// <summary>
        /// Adds the node labels.
        /// </summary>
        /// <param name="manager">The manager node.</param>
        private void AddNodeLabels(SshProxy<NodeDefinition> manager)
        {
            manager.InvokeIdempotentAction("setup/node-labels",
                () =>
                {
                    manager.Status = "labeling";

                    foreach (var node in hive.Nodes.Where(n => n.Metadata.InSwarm))
                    {
                        var labelDefinitions = new List<string>();

                        labelDefinitions.Add($"{NodeLabels.LabelDatacenter}={hive.Definition.Datacenter.ToLowerInvariant()}");
                        labelDefinitions.Add($"{NodeLabels.LabelEnvironment}={hive.Definition.Environment.ToString().ToLowerInvariant()}");

                        foreach (var item in node.Metadata.Labels.Standard)
                        {
                            var value = item.Value;

                            if (value == null)
                            {
                                value = string.Empty;
                            }
                            else
                            {
                                value = value.ToString().ToLowerInvariant();
                            }

                            labelDefinitions.Add($"{item.Key.ToLowerInvariant()}={value}");
                        }

                        foreach (var item in node.Metadata.Labels.Custom)
                        {
                            var value = item.Value;

                            if (value == null)
                            {
                                value = string.Empty;
                            }
                            else
                            {
                                value = value.ToString();
                            }

                            labelDefinitions.Add($"{item.Key.ToLowerInvariant()}={value}");
                        }

                        if (labelDefinitions.Count == 0)
                        {
                            // This should never happen but it's better to be safe.

                            continue;
                        }

                        // Generate a script that adds the required labels to each in one shot.

                        var labelAdds = new StringBuilder();

                        foreach (var labelDefinition in labelDefinitions)
                        {
                            labelAdds.AppendWithSeparator($"--label-add \"{labelDefinition}\"");
                        }

                        var labelCommand = $"docker node update {labelAdds} \"{node.Name}\"";

                        // We occasionaly see [update out of sequence] errors from labeling operations.
                        // These seem to be transient, so we're going to retry a few times before
                        // actually giving up.

                        var retry = new LinearRetryPolicy(e => e is HiveException, maxAttempts: 10, retryInterval: TimeSpan.FromSeconds(5));

                        retry.InvokeAsync(
                            async () =>
                            {
                                var bundle = new CommandBundle("./set-labels.sh");

                                bundle.AddFile("set-labels.sh", labelCommand, isExecutable: true);

                                var response = manager.SudoCommand(bundle, RunOptions.Defaults & ~RunOptions.FaultOnError);

                                if (response.ExitCode != 0)
                                {
                                    throw new TransientException(response.ErrorSummary);
                                }

                                await Task.CompletedTask;

                            }).Wait();

                        // We're going to wait two seconds for the hive manager to propagate the
                        // changes in the hope of proactively avoiding the [update out of sequence] 
                        // errors

                        Thread.Sleep(TimeSpan.FromSeconds(2));
                    }
                });
        }

        /// <summary>
        /// Pulls common images to the node.
        /// </summary>
        /// <param name="node">The target hive node.</param>
        /// <param name="pullAll">
        /// Optionally specifies that all hive images should be pulled to the
        /// node regardless of the node properties.  This is used to pull images
        /// into the cache.
        /// </param>
        private void PullImages(SshProxy<NodeDefinition> node, bool pullAll = false)
        {
            node.InvokeIdempotentAction("setup/pull-images",
                () =>
                {
                    var images = new List<string>()
                    {
                        Program.ResolveDockerImage("nhive/ubuntu-16.04"),
                        Program.ResolveDockerImage("nhive/ubuntu-16.04-dotnet"),
                        Program.ResolveDockerImage(hive.Definition.Image.Proxy),
                        Program.ResolveDockerImage(hive.Definition.Image.ProxyVault)
                    };

                    if (node.Metadata.IsManager)
                    {
                        images.Add(Program.ResolveDockerImage(hive.Definition.Image.HiveManager));
                        images.Add(Program.ResolveDockerImage(hive.Definition.Image.ProxyManager));
                        images.Add(Program.ResolveDockerImage(hive.Definition.Image.Dns));
                        images.Add(Program.ResolveDockerImage(hive.Definition.Image.DnsMon));
                        images.Add(Program.ResolveDockerImage(hive.Definition.Image.SecretRetriever));
                    }

                    if (hive.Definition.Log.Enabled)
                    {
                        // All nodes pull these images:

                        images.Add(Program.ResolveDockerImage(hive.Definition.Image.LogHost));
                        images.Add(Program.ResolveDockerImage(hive.Definition.Image.Metricbeat));

                        // [neon-log-collector] only runs on managers.

                        if (pullAll || node.Metadata.IsManager)
                        {
                            images.Add(Program.ResolveDockerImage(hive.Definition.Image.LogCollector));
                        }

                        // [elasticsearch] only runs on designated nodes.

                        if (pullAll || node.Metadata.Labels.LogEsData)
                        {
                            images.Add(Program.ResolveDockerImage(hive.Definition.Image.Elasticsearch));
                        }
                    }

                    if (pullAll || node.Metadata.Labels.HiveMQ)
                    {
                        images.Add(Program.ResolveDockerImage(hive.Definition.Image.HiveMQ));
                    }

                    foreach (var image in images)
                    {
                        var command = $"docker pull {image}";

                        node.Status = $"run: {command}";
                        node.DockerCommand(command);
                    }
                });
        }

        /// <summary>
        /// Adds the node to the swarm cluster.
        /// </summary>
        /// <param name="node">The target hive node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void JoinSwarm(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            if (node == hive.FirstManager)
            {
                // This node is implictly joined to the hive.

                node.Status = "joined";
                return;
            }

            node.InvokeIdempotentAction("setup/swarm-join",
                () =>
                {
                    Thread.Sleep(stepDelay);

                    node.Status = "joining";

                    if (node.Metadata.IsManager)
                    {
                        node.DockerCommand(hive.SecureRunOptions | RunOptions.FaultOnError, $"docker swarm join --token {hiveLogin.SwarmManagerToken} {hive.FirstManager.Metadata.PrivateAddress}:2377");
                    }
                    else
                    {
                        // Must be a worker node.

                        node.DockerCommand(hive.SecureRunOptions | RunOptions.FaultOnError, $"docker swarm join --token {hiveLogin.SwarmWorkerToken} {hive.FirstManager.Metadata.PrivateAddress}:2377");
                    }
                });

            node.Status = "joined";
        }

        /// <summary>
        /// Returns systemd unit settings that will effectively have the unit
        /// try to restart forever on failure.
        /// </summary>
        private string UnitRestartSettings
        {
            get
            {
                return
@"# These settings configure the service to restart always after
# waiting 5 seconds between attempts for up to a 365 days (effectively 
# forever).  [StartLimitIntervalSec] is set to the number of seconds 
# in a year and [StartLimitBurst] is set to the number of 5 second 
# intervals in [StartLimitIntervalSec].

Restart=always
RestartSec=5
StartLimitIntervalSec=31536000 
StartLimitBurst=6307200";
            }
        }

        /// <summary>
        /// Installs the required Ceph Storage Cluster packages on the node without
        /// configuring them.
        /// </summary>
        /// <param name="node">The target hive node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void CephPackages (SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            if (!hive.Definition.HiveFS.Enabled)
            {
                return;
            }

            // IMPLEMENTATION NOTE:
            //
            // We're going to configure the Ceph release key, repository and packages
            // on all nodes to make it easier to reconfigure the Ceph cluster manually or 
            // via tools in the future.  This also installs [ceph-common] which is
            // required to actually mount the file system, which will also probably be 
            // required on all nodes.
            //
            // Note that the full Ceph install consumes about 411MB of disk space
            // where as the common components add about 100MB.  So the full install
            // consumes an additional 311MB.  I'm not going to worry about allowing
            // operators tune what's installed.  Perhaps something for the future.

            // $todo(jeff.lill): Consider allowing customization of which Ceph components are installed.

            node.InvokeIdempotentAction("setup/ceph-packages",
                () =>
                {
                    Thread.Sleep(stepDelay);

                    node.Status = "ceph package";

                    // Extract the Ceph release and version from the configuration.
                    // Note that the version is optional and is currently ignored.

                    var parts       = hive.Definition.HiveFS.Release.Split('/');
                    var cephRelease = parts[0].Trim().ToLowerInvariant();

                    // Configure the Ceph Debian package repositories and install
                    // the required packages.

                    // $todo(jeff.lill): 
                    //
                    // Do we need to pin to the Ceph version to avoid automatically updating 
                    // to a new release via [apt-get dist-upgrade]?

                    var linuxRelease = node.SudoCommand("lsb_release -sc").OutputText;

                    node.SudoCommand($"wget -q -O- https://download.ceph.com/keys/release.asc | sudo apt-key add -");
                    node.SudoCommand($"apt-add-repository \"deb https://download.ceph.com/debian-{cephRelease}/ {linuxRelease} main\"");
                    node.SudoCommand($"safe-apt-get update");
                    node.SudoCommand($"safe-apt-get install -yq ceph");

                    // We also need need support for extended file system attributes
                    // so we can set the maximum size in bytes and/or maximum number
                    // files in a directory via [setfattr] and [getfat].

                    node.SudoCommand($"safe-apt-get install -yq attr");

                    //---------------------------------------------------------
                    // The default Ceph service systemd unit files don't try very
                    // hard to restart after failures so we're going to upload
                    // systemd unit drop-in files with our changes.

                    string unitPath;

                    // Ceph-MDS

                    unitPath = "/etc/systemd/system/ceph-mds@.service";

                    node.UploadText(unitPath,
$@"[Unit]
Description=Ceph metadata server daemon
After=network-online.target local-fs.target time-sync.target
Wants=network-online.target local-fs.target time-sync.target
PartOf=ceph-mds.target

[Service]
LimitNOFILE=1048576
LimitNPROC=1048576
EnvironmentFile=-/etc/default/ceph
Environment=CLUSTER=ceph
ExecStart=/usr/bin/ceph-mds -f --cluster ${{CLUSTER}} --id %i --setuser ceph --setgroup ceph
ExecReload=/bin/kill -HUP $MAINPIDk
PrivateDevices=yes
ProtectHome=true
ProtectSystem=full
PrivateTmp=true
TasksMax=infinity

{UnitRestartSettings}

[Install]
WantedBy=ceph-mds.target
");
                    node.SudoCommand($"chmod 644 {unitPath}");

                    // Ceph-MGR

                    unitPath = "/etc/systemd/system/ceph-mgr@.service";

                    node.UploadText(unitPath,
$@"[Unit]
Description=Ceph cluster manager daemon
After=network-online.target local-fs.target time-sync.target
Wants=network-online.target local-fs.target time-sync.target
PartOf=ceph-mgr.target

[Service]
LimitNOFILE=1048576
LimitNPROC=1048576
EnvironmentFile=-/etc/default/ceph
Environment=CLUSTER=ceph

ExecStart=/usr/bin/ceph-mgr -f --cluster ${{CLUSTER}} --id %i --setuser ceph --setgroup ceph
ExecReload=/bin/kill -HUP $MAINPID

{UnitRestartSettings}

[Install]
WantedBy=ceph-mgr.target
");
                    node.SudoCommand($"chmod 644 {unitPath}");

                    // Ceph-MGRS

                    unitPath = "/etc/systemd/system/ceph-mgrs@.service";

                    node.UploadText(unitPath,
$@"[Unit]
Description=Ceph cluster manager daemon
After=network-online.target local-fs.target time-sync.target
Wants=network-online.target local-fs.target time-sync.target
PartOf=ceph-mgr.target

[Service]
LimitNOFILE=1048576
LimitNPROC=1048576
EnvironmentFile=-/etc/default/ceph
Environment=CLUSTER=ceph

ExecStart=/usr/bin/ceph-mgr -f --cluster ${{CLUSTER}} --id %i --setuser ceph --setgroup ceph
ExecReload=/bin/kill -HUP $MAINPID

{UnitRestartSettings}

[Install]
WantedBy=ceph-mgr.target
");
                    node.SudoCommand($"chmod 644 {unitPath}");
                    node.SudoCommand("systemctl daemon-reload");

                    // Ceph-MON

                    unitPath = "/etc/systemd/system/ceph-mon@.service";

                    node.UploadText(unitPath,
$@"[Unit]
Description=Ceph cluster monitor daemon

# According to:
#   http://www.freedesktop.org/wiki/Software/systemd/NetworkTarget
# these can be removed once ceph-mon will dynamically change network
# configuration.
After=network-online.target local-fs.target time-sync.target
Wants=network-online.target local-fs.target time-sync.target

PartOf=ceph-mon.target

[Service]
LimitNOFILE=1048576
LimitNPROC=1048576
EnvironmentFile=-/etc/default/ceph
Environment=CLUSTER=ceph
ExecStart=/usr/bin/ceph-mon -f --cluster ${{CLUSTER}} --id %i --setuser ceph --setgroup ceph
ExecReload=/bin/kill -HUP $MAINPID
PrivateDevices=yes
ProtectHome=true
ProtectSystem=full
PrivateTmp=true
TasksMax=infinity

{UnitRestartSettings}

[Install]
WantedBy=ceph-mon.target
");
                    node.SudoCommand($"chmod 644 {unitPath}");

                    // Ceph-OSD

                    unitPath = "/etc/systemd/system/ceph-osd@.service";

                    node.UploadText(unitPath,
$@"[Unit]
Description=Ceph object storage daemon osd.%i
After=network-online.target local-fs.target time-sync.target ceph-mon.target
Wants=network-online.target local-fs.target time-sync.target
PartOf=ceph-osd.target

[Service]
LimitNOFILE=1048576
LimitNPROC=1048576
EnvironmentFile=-/etc/default/ceph
Environment=CLUSTER=ceph
ExecStart=/usr/bin/ceph-osd -f --cluster ${{CLUSTER}} --id %i --setuser ceph --setgroup ceph
ExecStartPre=/usr/lib/ceph/ceph-osd-prestart.sh --cluster ${{CLUSTER}} --id %i
ExecReload=/bin/kill -HUP $MAINPID
ProtectHome=true
ProtectSystem=full
PrivateTmp=true
TasksMax=infinity

{UnitRestartSettings}

[Install]
WantedBy=ceph-osd.target
");
                    node.SudoCommand($"chmod 644 {unitPath}");

                    // Reload the systemd configuration changes.

                    node.SudoCommand("systemctl daemon-reload");
                });
        }

        /// <summary>
        /// Generates the Ceph related configuration settings as a global step.  This
        /// assumes that <see cref="CephPackages"/> has already been completed.
        /// </summary>
        private void CephSettings()
        {
            if (!hive.Definition.HiveFS.Enabled)
            {
                return;
            }

            if (hiveLogin.Ceph != null)
            {
                return; // We've already done this.
            }

            // IMPLEMENTATION NOTE:
            //
            // We're going to follow the steps keyring generation steps from the link
            // below to obtain the monitor, client admin, and OSD keyrings in addition
            // to the monitor map which we'll then persist in the cluser login so 
            // we'll be able to complete Ceph configuration in subsequent setup steps.
            //
            //      http://docs.ceph.com/docs/master/install/manual-deployment/

            // Use the first manager node to access the Ceph admin tools.

            var manager          = hive.FirstManager;
            var cephConfig       = new CephConfig();
            var tmpPath          = "/tmp/ceph";
            var monKeyringPath   = LinuxPath.Combine(tmpPath, "mon.keyring");
            var adminKeyringPath = LinuxPath.Combine(tmpPath, "admin.keyring");
            var osdKeyringPath   = LinuxPath.Combine(tmpPath, "osd.keyring");
            var monMapPath       = LinuxPath.Combine(tmpPath, "monmap");
            var runOptions       = RunOptions.Defaults | RunOptions.FaultOnError;

            hiveLogin.Ceph = cephConfig;

            cephConfig.Name = "ceph";
            cephConfig.Fsid = Guid.NewGuid().ToString("D");

            manager.SudoCommand($"mkdir -p {tmpPath}", RunOptions.Defaults | RunOptions.FaultOnError);

            // Generate an initial monitor keyring (we'll be adding additional keys below).

            manager.SudoCommand($"ceph-authtool --create-keyring {monKeyringPath} --gen-key -n mon. --cap mon \"allow *\"", runOptions);

            // Generate the client admin keyring. 

            manager.SudoCommand($"ceph-authtool --create-keyring {adminKeyringPath} --gen-key -n client.admin --set-uid=0 --cap mon \"allow *\" --cap osd \"allow *\" --cap mds \"allow *\" --cap mgr \"allow *\"", runOptions);
            manager.SudoCommand($"chmod 666 {adminKeyringPath}", runOptions);

            cephConfig.AdminKeyring = manager.DownloadText(adminKeyringPath);

            // Generate the bootstrap OSD keyring.

            manager.SudoCommand($"ceph-authtool --create-keyring {osdKeyringPath} --gen-key -n client.bootstrap-osd --cap mon \"profile bootstrap-osd\"", runOptions);
            manager.SudoCommand($"chmod 666 {osdKeyringPath}", runOptions);

            cephConfig.OSDKeyring = manager.DownloadText(osdKeyringPath);

            // Add the client admin and OSD bootstrap keyrings to the monitor keyring
            // and then download the result.

            manager.SudoCommand($"ceph-authtool {monKeyringPath} --import-keyring {adminKeyringPath}", runOptions);
            manager.SudoCommand($"ceph-authtool {monKeyringPath} --import-keyring {osdKeyringPath}", runOptions);
            manager.SudoCommand($"chmod 666 {monKeyringPath}", runOptions);

            cephConfig.MonitorKeyring = manager.DownloadText(monKeyringPath);

            // Generate the monitor map.

            var sbAddOptions = new StringBuilder();

            foreach (var monNode in hive.Nodes.Where(n => n.Metadata.Labels.CephMON))
            {
                sbAddOptions.AppendWithSeparator($"--add {monNode.Name} {monNode.PrivateAddress}");
            }

            manager.SudoCommand($"monmaptool --create {sbAddOptions} --fsid {cephConfig.Fsid} {monMapPath}", runOptions);
            manager.SudoCommand($"chmod 666 {monMapPath}", runOptions);

            cephConfig.MonitorMap = manager.DownloadBytes(monMapPath);

            // Make sure we've deleted any temporary files.

            manager.SudoCommand($"rm -r {tmpPath}", runOptions);

            // Persist the hive login so we'll have the keyrings and 
            // monitor map in case we have to restart setup.

            hiveLogin.Save();
        }

        /// <summary>
        /// Generates and uploads the Ceph configuration file to a node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="configOnly">Optionally specifies that only the <b>ceph.conf</b> file is to be uploaded.</param>
        private void UploadCephConf(SshProxy<NodeDefinition> node, bool configOnly = false)
        {
            node.Status = "ceph config";

            var sbHostNames     = new StringBuilder();
            var sbHostAddresses = new StringBuilder();

            foreach (var monitorNode in hive.Definition.SortedNodes.Where(n => n.Labels.CephMON))
            {
                sbHostNames.AppendWithSeparator(monitorNode.Name, ", ");
                sbHostAddresses.AppendWithSeparator(monitorNode.PrivateAddress, ", ");
            }

            var hiveSubnet =
                new HostingManagerFactory().IsCloudEnvironment(hive.Definition.Hosting.Environment)
                    ? hive.Definition.Network.CloudSubnet
                    : hive.Definition.Network.PremiseSubnet;

            node.SudoCommand("mkdir -p /etc/ceph");

            var mdsConf = string.Empty;

            if (node.Metadata.Labels.CephMDS)
            {
                mdsConf =
$@"
[mds.{node.Name}]
host = {node.Name}
mds cache memory limit = {(int)(node.Metadata.GetCephMDSCacheSize(hive.Definition) * HiveFSOptions.CacheSizeFudge)}
mds_standby_replay = true
";
            }

            node.UploadText("/etc/ceph/ceph.conf",
$@"[global]
fsid = {hiveLogin.Ceph.Fsid}
mgr initial modules = dashboard
mon initial members = {sbHostNames}
mon host = {sbHostAddresses}
mon health preluminous compat warning = false
public network = {hiveSubnet}
auth cluster required = cephx
auth service required = cephx
auth client required = cephx
osd journal size = {HiveDefinition.ValidateSize(hive.Definition.HiveFS.OSDJournalSize, hive.Definition.HiveFS.GetType(), nameof(hive.Definition.HiveFS.OSDJournalSize)) / NeonHelper.Mega}
osd pool default size = {hive.Definition.HiveFS.OSDReplicaCount}
osd pool default min size = {hive.Definition.HiveFS.OSDReplicaCountMin}
osd pool default pg num = {hive.Definition.HiveFS.OSDPlacementGroups}
osd pool default pgp num = {hive.Definition.HiveFS.OSDPlacementGroups}
osd crush chooseleaf type = 1
bluestore_cache_size = {(int)(node.Metadata.GetCephOSDCacheSize(hive.Definition) * HiveFSOptions.CacheSizeFudge) / NeonHelper.Mega}
{mdsConf}
");
            if (!configOnly)
            {
                var cephUser         = hive.Definition.HiveFS.Username;
                var adminKeyringPath = "/etc/ceph/ceph.client.admin.keyring";

                node.UploadText(adminKeyringPath, hiveLogin.Ceph.AdminKeyring);
                node.SudoCommand($"chown {cephUser}:{cephUser} {adminKeyringPath}");
                node.SudoCommand($"chmod 640 {adminKeyringPath}");
            }
        }

        /// <summary>
        /// Generates the Ceph manager service keyring for a node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <returns>The keyring.</returns>
        private string GetManagerKeyring(SshProxy<NodeDefinition> node)
        {
            var result = node.SudoCommand($"ceph auth get-or-create mgr.{node.Name} mon \"allow profile mgr\" osd \"allow *\" mds \"allow *\"");

            if (result.ExitCode == 0)
            {
                return result.OutputText;
            }
            else
            {
                // We shouldn't ever get here.

                return string.Empty;
            }
        }

        /// <summary>
        /// Generates the Ceph MDS service keyring for a node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <returns>The keyring.</returns>
        private string GetMDSKeyring(SshProxy<NodeDefinition> node)
        {
            var result = node.SudoCommand($"ceph auth get-or-create mds.{node.Name} mds \"allow *\" osd \"allow *\" mon \"allow rwx\"");

            if (result.ExitCode == 0)
            {
                return result.OutputText;
            }
            else
            {
                // We shouldn't ever get here.

                return string.Empty;
            }
        }

        /// <summary>
        ///Creates the Ceph service user and group.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void CephUser(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            node.InvokeIdempotentAction("setup/ceph-user",
                () =>
                {
                    var cephUser = hive.Definition.HiveFS.Username;

                    // Ensure that the Ceph lib folder exists because this acts as
                    // the HOME directory for the user.

                    node.SudoCommand($"mkdir -p /var/lib/ceph");

                    // Create the [ceph] group.

                    node.SudoCommand($"groupadd --system --force {cephUser}");

                    // Create the [ceph] user.

                    node.SudoCommand($"useradd --system -g ceph --home-dir /var/lib/ceph --comment \"Ceph storage service\" {cephUser}");
                });
        }

        /// <summary>
        /// Bootstraps the Ceph monitor/manager nodes.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void CephBootstrap(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            node.InvokeIdempotentAction("setup/ceph-bootstrap",
                () =>
                {
                    Thread.Sleep(stepDelay);

                    var cephUser = hive.Definition.HiveFS.Username;
                    var tempPath = "/tmp/ceph";

                    // Create a temporary folder.

                    node.SudoCommand($"mkdir -p {tempPath}");

                    // Upload the Ceph config file here because we'll need it below.

                    UploadCephConf(node, configOnly: true);

                    // Create the monitor data directory and load the monitor map and keyring.

                    node.Status = "ceph-mon config";

                    var monFolder = $"/var/lib/ceph/mon/{hiveLogin.Ceph.Name}-{node.Name}";
                    
                    node.SudoCommand($"mkdir -p {monFolder}");
                    node.SudoCommand($"chown {cephUser}:{cephUser} {monFolder}");
                    node.SudoCommand($"chmod 770 {monFolder}");

                    var monitorMapPath     = LinuxPath.Combine(tempPath, "monmap");
                    var monitorKeyringPath = LinuxPath.Combine(tempPath, "ceph.mon.keyring");

                    node.UploadBytes(monitorMapPath, hiveLogin.Ceph.MonitorMap);
                    node.UploadText(monitorKeyringPath, hiveLogin.Ceph.MonitorKeyring);
                    node.SudoCommandAsUser(cephUser, $"ceph-mon --mkfs -i {node.Name} --monmap {monitorMapPath} --keyring {monitorKeyringPath}");

                    // Upload the client admin keyring.

                    var adminKeyringPath = "/etc/ceph/ceph.client.admin.keyring";

                    node.UploadText(adminKeyringPath, hiveLogin.Ceph.AdminKeyring);
                    node.SudoCommand($"chown {cephUser}:{cephUser} {adminKeyringPath}");
                    node.SudoCommand($"chmod 640 {adminKeyringPath}");

                    // Indicate that we're done configuring the monitor and start it.

                    node.Status = "ceph-mon start";
                    node.SudoCommand($"touch /var/lib/ceph/mon/ceph-{node.Name}/done");
                    node.SudoCommand($"systemctl enable ceph-mon@{node.Name}");
                    node.SudoCommand($"systemctl start ceph-mon@{node.Name}");

                    // Wait for the monitor to indicate that it has started.

                    try
                    {
                        NeonHelper.WaitFor(
                            () =>
                            {
                                return GetCephClusterStatus(node).IsHealthy;
                            },
                            timeout: TimeSpan.FromSeconds(120),
                            pollTime: TimeSpan.FromSeconds(2));
                    }
                    catch (TimeoutException)
                    {
                        node.Fault("Timeout waiting for Ceph Monitor.");
                    }

                    // Configure and start the manager service.

                    node.Status = "ceph-mgr config";

                    var mgrFolder = $"/var/lib/ceph/mgr/ceph-{node.Name}";

                    node.SudoCommandAsUser(cephUser, $"mkdir -p {mgrFolder}");

                    var mgrKeyringPath = $"{mgrFolder}/keyring";

                    node.UploadText(mgrKeyringPath, GetManagerKeyring(node));
                    node.SudoCommand($"chown {cephUser}:{cephUser} {mgrKeyringPath}");
                    node.SudoCommand($"chmod 640 {mgrKeyringPath}");

                    node.Status = "ceph-mgr start";
                    node.SudoCommand($"systemctl enable ceph-mgr@{node.Name}");
                    node.SudoCommand($"systemctl start ceph-mgr@{node.Name}");

                    // Give the manager some time to start.

                    Thread.Sleep(TimeSpan.FromSeconds(15));

                    if (hive.Definition.Dashboard.HiveFS)
                    {
                        // Enable the Ceph dashboard.

                        node.Status = "ceph dashboard";

                        // Ceph versions after [luminous] require additional configuration
                        // to setup the TLS certificate and login credentials.

                        if (hive.Definition.HiveFS.Release != "luminous")
                        {
                            node.SudoCommand($"ceph config-key set mgr/dashboard/crt -i /etc/neon/certs/hive.crt");
                            node.SudoCommand($"ceph config-key set mgr/dashboard/key -i /etc/neon/certs/hive.key");
                            node.SudoCommand($"ceph dashboard set-login-credentials {HiveConst.DefaultUsername} {HiveConst.DefaultPassword}");
                            node.SudoCommand($"ceph config set mgr mgr/dashboard/server_port {HiveHostPorts.CephDashboard}");

                            // Restart the MGR to pick up the changes.

                            node.SudoCommand($"systemctl restart ceph-mgr@{node.Name}");
                        }
                    }

                    // Remove the temp folder.

                    node.SudoCommand($"rm -r {tempPath}");
                });
        }

        /// <summary>
        /// Summarizes the Ceph cluster status.
        /// </summary>
        private class CephClusterStatus
        {
            /// <summary>
            /// Indicates that the neonHIVE includes a Ceph cluster.
            /// </summary>
            public bool IsEnabled { get; set; }

            /// <summary>
            /// Indicates that the hive is healthy.
            /// </summary>
            public bool IsHealthy { get; set; }

            /// <summary>
            /// The number of Monitor servers configured for the hive.
            /// </summary>
            public int MONCount { get; set; }

            /// <summary>
            /// The number of OSD servers configured for the hive.
            /// </summary>
            public int OSDCount { get; set; }

            /// <summary>
            /// The current number of active OSD servers.
            /// </summary>
            public int OSDActiveCount { get; set; }

            /// <summary>
            /// The number of MDS servers configured for the hive.
            /// </summary>
            public int MDSCount { get; set; }

            /// <summary>
            /// The number of active MDS servers configured for the hive.
            /// </summary>
            public int MDSActiveCount { get; set; }

            /// <summary>
            /// Indicates that the <b>hiveFS</b> file system is ready.
            /// </summary>
            public bool IsHiveFsReady { get; set; }
        }

        /// <summary>
        /// Queries a node for the current Ceph cluster status.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <returns>The <cref cref="CephClusterStatus"/>.</returns>
        private CephClusterStatus GetCephClusterStatus(SshProxy<NodeDefinition> node)
        {
            var status = new CephClusterStatus();

            if (!hive.Definition.HiveFS.Enabled)
            {
                return status;
            }

            status.IsEnabled = true;

            // Get the Ceph cluster status as JSON to determine the number of
            // monitors as well as the number total and active OSD servers. 

            var result = node.SudoCommand("ceph status --format=json-pretty");

            if (result.ExitCode == 0)
            {
                var cephStatus = JObject.Parse(result.OutputText);

                var health   = (JObject)cephStatus.GetValue("health");
                var monMap   = (JObject)cephStatus.GetValue("monmap");
                var monArray = (JArray)monMap.GetValue("mons");
                var monCount = monArray.Count();
                var osdMap   = (JObject)cephStatus.GetValue("osdmap");
                var osdMap2  = (JObject)osdMap.GetValue("osdmap");

                status.IsHealthy      = (string)health.GetValue("status") == "HEALTH_OK";
                status.OSDCount       = (int)osdMap2.GetValue("num_osds");
                status.OSDActiveCount = (int)osdMap2.GetValue("num_up_osds");
            }

            // Get the Ceph MDS status as JSON to determine the number of
            // running MDS servers. 

            result = node.SudoCommand("ceph mds stat --format=json-pretty");

            if (result.ExitCode == 0)
            {
                var mdsStatus = JObject.Parse(result.OutputText);

                // This counts the number of MDS servers that are running
                // in cold standby mode.

                var fsMap    = (JObject)mdsStatus.GetValue("fsmap");
                var standbys = (JArray)fsMap.GetValue("standbys");

                foreach (JObject standby in standbys)
                {
                    var state = (string)standby.GetValue("state");

                    status.MDSCount++;

                    if (state.StartsWith("up:"))
                    {
                        status.MDSActiveCount++;
                    }
                }

                // This counts the number of MDS servers that are active
                // or are running in hot standby mode.

                var filesystems     = (JArray)fsMap.GetValue("filesystems");
                var firstFileSystem = (JObject)filesystems.FirstOrDefault();

                if (firstFileSystem != null)
                {
                    var mdsMap = (JObject)firstFileSystem.GetValue("mdsmap");
                    var info   = (JObject)mdsMap.GetValue("info");

                    foreach (var property in info.Properties())
                    {
                        var item  = (JObject)property.Value;
                        var state = (string)item.GetValue("state");

                        status.MDSCount++;

                        if (state.StartsWith("up:"))
                        {
                            status.MDSActiveCount++;
                        }
                    }
                }
            }

            // $hack(jeff.lill):
            //
            // Detect if the [hiveFS] file system is ready.  There's probably a
            // way to detect this from the JSON status above but I need to
            // move on to other things.  This is fragile because it assumes
            // that there's only one file system deployed.

            result = node.SudoCommand("ceph mds stat");

            if (result.ExitCode == 0)
            {
                status.IsHiveFsReady = result.OutputText.StartsWith("hivefs-") &&
                                       result.OutputText.Contains("up:active");
            }

            return status;
        }

        /// <summary>
        /// Configures the Ceph cluster by configuring and starting the OSD and MDS 
        /// services and then creating and mounting a CephFS file system.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void CephCluster(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            node.InvokeIdempotentAction("setup/ceph-osd",
                () =>
                {
                    Thread.Sleep(stepDelay);

                    node.Status = "ceph-osd config";

                    var cephUser = hive.Definition.HiveFS.Username;

                    // All nodes need the config file.

                    UploadCephConf(node);

                    // Configure OSD if enabled for this node.

                    if (node.Metadata.Labels.CephOSD)
                    {
                        node.Status = "ceph-osd start";
                        node.UploadText("/var/lib/ceph/bootstrap-osd/ceph.keyring", hiveLogin.Ceph.OSDKeyring);
                        node.SudoCommand($"ceph-volume lvm create --bluestore --data {node.Metadata.Labels.CephOSDDevice}");
                        node.Status = string.Empty;
                    }
                });

            // Wait for the hive OSD services to come up.  We're going to have
            // all nodes wait but only the first manager will report status to
            // the UX.

            var osdCount = hive.Definition.Nodes.Count(n => n.Labels.CephOSD);

            if (node == hive.FirstManager)
            {
                node.Status = $"OSD servers: [0 of {osdCount}] ready";
            }
            else
            {
                node.Status = string.Empty;
            }

            try
            {
                NeonHelper.WaitFor(
                    () =>
                    {
                        var clusterStatus = GetCephClusterStatus(node);

                        if (node == hive.FirstManager)
                        {
                            node.Status = $"OSD servers: [{clusterStatus.OSDActiveCount} of {osdCount}] ready";
                        }

                        return clusterStatus.OSDActiveCount == osdCount;
                    },
                    timeout: TimeSpan.FromSeconds(120),
                    pollTime: TimeSpan.FromSeconds(2));
            }
            catch (TimeoutException)
            {
                node.Fault("Timeout waiting for Ceph OSD servers.");
            }

            node.Status = string.Empty;

            // Configure MDS if enabled for this node.

            if (node.Metadata.Labels.CephMDS)
            {
                node.InvokeIdempotentAction("setup/ceph-mds",
                    () =>
                    {
                        node.Status = "ceph-msd";

                        var mdsFolder = $"/var/lib/ceph/mds/ceph-{node.Name}";

                        node.SudoCommand($"mkdir -p {mdsFolder}");

                        var mdsKeyringPath = $"{mdsFolder}/keyring";

                        node.UploadText(mdsKeyringPath, GetMDSKeyring(node));

                        node.Status = "ceph-mds start";
                        node.SudoCommand($"systemctl enable ceph-mds@{node.Name}");
                        node.SudoCommand($"systemctl start ceph-mds@{node.Name}");
                        node.Status = string.Empty;
                    });
            }

            // Wait for the hive MDS services to come up.  We're going to have
            // all nodes wait but only the first manager will report status to
            // the UX.

            var mdsCount = hive.Definition.Nodes.Count(n => n.Labels.CephMDS);

            if (node == hive.FirstManager)
            {
                node.Status = $"MDS servers: [0 of {mdsCount}] ready";
            }
            else
            {
                node.Status = string.Empty;
            }

            try
            {
                NeonHelper.WaitFor(
                    () =>
                    {
                        var clusterStatus = GetCephClusterStatus(node);

                        if (node == hive.FirstManager)
                        {
                            node.Status = $"MDS servers: [{clusterStatus.MDSActiveCount} of {mdsCount}] ready";
                        }

                        return clusterStatus.MDSActiveCount == mdsCount;
                    },
                    timeout: TimeSpan.FromSeconds(120),
                    pollTime: TimeSpan.FromSeconds(2));
            }
            catch (TimeoutException)
            {
                node.Fault("Timeout waiting for Ceph MDS servers.");
            }

            node.Status = string.Empty;

            // We're going to have the first manager create the [hivefs_data] and [hivefs_metadata] storage 
            // pools and then the [hiveFS] filesystem using those pools.  Then we'll have the first manager 
            // wait for the filesystem to be created.

            if (node == hive.FirstManager)
            {
                node.InvokeIdempotentAction("setup/hive-fs",
                    () =>
                    {
                        node.Status = "create file system";
                        node.SudoCommand($"ceph osd pool create hivefs_data {hive.Definition.HiveFS.OSDPlacementGroups}");
                        node.SudoCommand($"ceph osd pool create hivefs_metadata {hive.Definition.HiveFS.OSDPlacementGroups}");
                        node.SudoCommand($"ceph fs new hivefs hivefs_metadata hivefs_data");
                    });
            }

            // Wait for the file system to start.

            try
            {
                node.Status = "ceph stablize (30s)";

                NeonHelper.WaitFor(
                    () =>
                    {
                        return GetCephClusterStatus(node).IsHiveFsReady;
                    },
                    timeout: TimeSpan.FromSeconds(120),
                    pollTime: TimeSpan.FromSeconds(2));

                // Wait longer to be really sure the filesystem initialization has completed.

                Thread.Sleep(TimeSpan.FromSeconds(30));
            }
            catch (TimeoutException)
            {
                node.Fault("Timeout waiting for Ceph file system.");
            }
            finally
            {
                node.Status = "ceph stablized";
            }

            // We're going to use the FUSE client to mount the file system at [/mnt/hivefs].

            node.InvokeIdempotentAction("setup/ceph-mount",
                () =>
                {
                    var monNode = hive.Definition.SortedNodes.First(n => n.Labels.CephMON);

                    node.Status = "mount file system";
                    node.SudoCommand($"mkdir -p /mnt/hivefs");

                    // I've seen this fail with an transient error sometimes because no
                    // MDS server is ready.  We'll retry a few times to mitigate this.

                    var retry = new LinearRetryPolicy(typeof(TransientException), maxAttempts: 30, TimeSpan.FromSeconds(1));

                    retry.InvokeAsync(
                        async () =>
                        {
                            var response = node.SudoCommand($"ceph-fuse -m {monNode.PrivateAddress}:6789 /mnt/hivefs", RunOptions.Defaults & ~RunOptions.FaultOnError);

                            if (response.ExitCode != 0)
                            {
                                throw new TransientException(response.ErrorSummary);
                            }

                            if (response.ErrorText.Contains("ceph mount failed"))
                            {
                                // $hack)jeff.lill):
                                //
                                // I've seen situations where mounting failed but the exit code was
                                // still zero.  This appears to be a bug:
                                //
                                //      https://tracker.ceph.com/issues/23665
                                //
                                // I'm going to detect this case and throw a transient exception
                                // and retry.
                                //
                                // The strange thing is that the command also reports:
                                //
                                //      probably no MDS server is up?
                                //
                                // but I explicitly verify that the MDS servers are up and active
                                // above, so I'm not sure why this is failing.

                                throw new TransientException(response.ErrorSummary);
                            }

                            await Task.CompletedTask;

                        }).Wait();
                });

            // $hack(jeff.lill):
            //
            // I couldn't enable the built-in [ceph-fuse@/*.service] to have
            // [/mnt/hivefs] mount on reboot via:
            //
            //      systemctl enable ceph-fuse@/hivefs.service
            //
            // I was seeing an "Invalid argument" error.  I'm going to workaround
            // this by creating and enabling my own service.

            node.InvokeIdempotentAction("setup/ceph-fuse-service",
                () =>
                {
                    node.Status = "ceph fuse service";
                    node.UploadText("/etc/systemd/system/ceph-fuse-hivefs.service",
$@"[Unit]
Description=Ceph FUSE client (for /mnt/hivefs)
After=network-online.target local-fs.target time-sync.target
Wants=network-online.target local-fs.target time-sync.target
Conflicts=umount.target
PartOf=ceph-fuse.target

[Service]
EnvironmentFile=-/etc/default/ceph
Environment=CLUSTER=ceph
ExecStart=/usr/bin/ceph-fuse -f -o nonempty --cluster ${{CLUSTER}} /mnt/hivefs
TasksMax=infinity

{UnitRestartSettings}

[Install]
WantedBy=ceph-fuse.target
WantedBy=docker.service
");
                    node.SudoCommand("chmod 644 /etc/systemd/system/ceph-fuse-hivefs.service");
                    node.SudoCommand($"systemctl enable ceph-fuse-hivefs.service");
                    node.SudoCommand($"systemctl start ceph-fuse-hivefs.service");
                });

            if (node == hive.FirstManager)
            {
                node.InvokeIdempotentAction("setup/hive-fs-init",
                    () =>
                    {
                        // Initialize [/mnt/hivefs]:
                        //
                        //      /mnt/hivefs/READY   - Read-only file whose presence indicates that the file system is mounted
                        //      /mnt/hivefs/docker  - Holds mapped Docker volumes
                        //      /mnt/hivefs/neon    - Reserved for neonHIVE

                        node.Status = "populate hiveFS";
                        node.SudoCommand($"touch /mnt/hivefs/READY && chmod 444 /mnt/hivefs/READY");
                        node.SudoCommand($"mkdir -p /mnt/hivefs/docker && chown root:root /mnt/hivefs/docker && chmod 770 /mnt/hivefs/docker");
                        node.SudoCommand($"mkdir -p /mnt/hivefs/neon && chown root:root /mnt/hivefs/neon && chmod 770 /mnt/hivefs/neon");
                    });
            }

            node.InvokeIdempotentAction("setup/hive-fs-ready",
                () =>
                {
                    // Wait up to 120 seconds for the hive file system to indicate that it's ready
                    // by the presence of the [/mnt/hivefs/READY] file.

                    node.Status = "waiting for hiveFS";

                    NeonHelper.WaitFor(
                        () =>
                        {
                            try
                            {
                                return node.FileExists("/mnt/hivefs/READY");
                            }
                            catch (Exception e)
                            {
                                node.LogException("Waiting for hiveFS", e);
                                return false;
                            }
                        },
                        timeout: TimeSpan.FromSeconds(120),
                        pollTime: TimeSpan.FromSeconds(1));
                });

            // Download and install the [neon-volume-plugin].

            node.InvokeIdempotentAction("setup/neon-volume-plugin",
                () =>
                {
                    node.Status = "neon-volume-plugin: install";

                    var installCommand = new CommandBundle("./install.sh");

                    installCommand.AddFile("install.sh",
$@"# Download and install the plugin.

curl {Program.CurlOptions} {hive.Definition.HiveFS.VolumePluginPackage} -o /tmp/neon-volume-plugin-deb 1>&2
dpkg --install /tmp/neon-volume-plugin-deb
rm /tmp/neon-volume-plugin-deb

# Enable and start the plugin service.

systemctl daemon-reload
systemctl enable neon-volume-plugin
systemctl start neon-volume-plugin
",
                        isExecutable: true);

                    node.SudoCommand(installCommand);
                });

            node.InvokeIdempotentAction("setup/neon-volume",
                () =>
                {
                    // $hack(jeff.lill):
                    //
                    // We need to create a volume using this plugin on every hive node
                    // so that the plugin will be reported to hive managers so they
                    // will be able to schedule tasks using the plugin on these nodes.
                    //
                    // This appears to be a legacy plugin issue.  I suspect that this
                    // won't be necessary for managed plugins.
                    //
                    //      https://github.com/jefflill/NeonForge/issues/226

                    node.Status = "neon-volume-plugin: register";
                    node.SudoCommand("docker volume create --driver=neon neon-do-not-remove");
                });
        }

        /// <summary> 
        /// Configures the Vault load balancer service: <b>neon-proxy-vault</b>.
        /// </summary>
        private void ConfigureVaultProxy()
        {
            // Create the comma separated list of Vault manager endpoints formatted as:
            //
            //      NODE:IP:PORT

            var sbEndpoints = new StringBuilder();

            foreach (var manager in hive.Definition.SortedManagers)
            {
                sbEndpoints.AppendWithSeparator($"{manager.Name}:{manager.Name}.{hive.Definition.Hostnames.Vault}:{NetworkPorts.Vault}", ",");
            }

            hive.FirstManager.InvokeIdempotentAction("setup/proxy-vault",
                () =>
                {
                    // Docker mesh routing seemed unstable on versions [17.03.0-ce]
                    // thru [17.12.1-ce] so we're going to provide an option to work
                    // around this by running the PUBLIC, PRIVATE and VAULT proxies 
                    // on all nodes and  publishing the ports to the host (not the mesh).
                    //
                    //      https://github.com/jefflill/NeonForge/issues/104
                    //
                    // Note that this mode feature is documented (somewhat poorly) here:
                    //
                    //      https://docs.docker.com/engine/swarm/services/#publish-ports

                    var options = new List<string>();

                    if (hive.Definition.Docker.GetAvoidIngressNetwork(hive.Definition))
                    {
                        options.Add("--publish");
                        options.Add($"mode=host,published={HiveHostPorts.ProxyVault},target={NetworkPorts.Vault}");
                    }
                    else
                    {
                        options.Add("--constraint");
                        options.Add($"node.role==manager");

                        options.Add("--publish");
                        options.Add($"{HiveHostPorts.ProxyVault}:{NetworkPorts.Vault}");
                    }

                    // Deploy [neon-proxy-vault].

                    ServiceHelper.StartService(hive, "neon-proxy-vault", hive.Definition.Image.ProxyVault,
                        new CommandBundle(
                            "docker service create",
                            "--name", "neon-proxy-vault",
                            "--detach=false",
                            "--mode", "global",
                            "--endpoint-mode", "vip",
                            "--network", HiveConst.PrivateNetwork,
                            options,
                            "--mount", "type=bind,source=/etc/neon/host-env,destination=/etc/neon/host-env,readonly=true",
                            "--mount", "type=bind,src=/usr/local/share/ca-certificates,dst=/mnt/host/ca-certificates,readonly=true",
                            "--env", $"VAULT_ENDPOINTS={sbEndpoints}",
                            "--env", $"LOG_LEVEL=INFO",
                            "--restart-delay", hive.Definition.Docker.RestartDelay,
                            ServiceHelper.ImagePlaceholderArg));
                });


            // We also need to deploy [neon-proxy-vault] to any pet nodes as Docker containers
            // to forward any Vault related traffic to the primary Vault instance running on onez
            // of the managers because pets aren't part of the Swarm.

            var vaultTasks = new List<Task>();

            foreach (var pet in hive.Pets)
            {
                var task = Task.Run(
                    () =>
                    {
                        var steps = new ConfigStepList();

                        ServiceHelper.AddContainerStartSteps(hive, steps, pet, "neon-proxy-vault", hive.Definition.Image.ProxyVault,
                            new CommandBundle(
                                "docker run",
                                "--name", "neon-proxy-vault",
                                "--detach",
                                "--publish", $"{HiveHostPorts.ProxyVault}:{NetworkPorts.Vault}",
                                "--mount", "type=bind,source=/etc/neon/host-env,destination=/etc/neon/host-env,readonly=true",
                                "--mount", "type=bind,src=/usr/local/share/ca-certificates,dst=/mnt/host/ca-certificates,readonly=true",
                                "--env", $"VAULT_ENDPOINTS={sbEndpoints}",
                                "--env", $"LOG_LEVEL=INFO",
                                "--restart", "always",
                                ServiceHelper.ImagePlaceholderArg));

                        hive.Configure(steps);
                    });

                vaultTasks.Add(task);
            }

            NeonHelper.WaitAllAsync(vaultTasks).Wait();
        }

        /// <summary>
        /// Ensures that Vault is unsealed on all manager nodes. 
        /// </summary>
        private void VaultUnseal()
        {
            // Wait for the Vault instance on each manager node to become ready 
            // and then unseal them.

            var firstManager = hive.FirstManager;
            var timer        = new Stopwatch();
            var timeout      = TimeSpan.FromMinutes(5);

            // We're going to use a raw JsonClient here that is configured
            // to accept self-signed certificates.  We can't use [NeonClusterHelper.Vault]
            // here because the hive isn't fully initialized yet.

            var httpHandler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip,
            };

            using (httpHandler)
            {
                foreach (var manager in hive.Managers)
                {
                    using (var vaultJsonClient = new JsonClient(httpHandler, disposeHandler: false))
                    {
                        vaultJsonClient.BaseAddress = new Uri($"https://{manager.Name}.{hive.Definition.Hostnames.Vault}:{NetworkPorts.Vault}/");

                        // Wait for Vault to start and be able to respond to requests.

                        timer.Restart();

                        manager.Status = "vault: start";

                        while (true)
                        {
                            if (timer.Elapsed > timeout)
                            {
                                manager.Fault($"[Vault] is not ready after waiting [{timeout}].");
                                return;
                            }

                            try
                            {
                                var jsonResponse = vaultJsonClient.GetUnsafeAsync("/v1/sys/seal-status").Result;

                                if (jsonResponse.IsSuccess)
                                {
                                    break;
                                }
                            }
                            catch (Exception e)
                            {
                                manager.LogException(e);
                                Thread.Sleep(TimeSpan.FromSeconds(1));
                            }
                        }

                        // Unseal the Vault instance.

                        manager.Status = "vault: unseal";

                        manager.SudoCommand($"vault-direct operator unseal -reset", RunOptions.None);    // This clears any previous uncompleted unseal attempts

                        for (int i = 0; i < hiveLogin.VaultCredentials.KeyThreshold; i++)
                        {
                            manager.SudoCommand($"vault-direct operator unseal", hive.SecureRunOptions | RunOptions.FaultOnError, hiveLogin.VaultCredentials.UnsealKeys[i]);
                        }

                        // Wait for Vault to indicate that it's unsealed and is
                        // ready to accept commands.

                        timer.Reset();

                        manager.Status = "vault: wait for unseal";

                        while (true)
                        {
                            var response = manager.SudoCommand("vault-direct status", RunOptions.LogOutput);

                            firstManager.Log($"*** VAULT STATUS: {response.ExitCode}/{response.AllText}");

                            if (response.ExitCode == 0)
                            {
                                break;
                            }

                            Thread.Sleep(TimeSpan.FromSeconds(5));
                        }

                        manager.Status = string.Empty;
                    }

                    // $todo(jeff.lill):
                    //
                    // Vault doesn't actually seem to be ready right away, even after
                    // verifing that all of the Vault instances are unsealed.  I believe
                    // this is because [neon-proxy-vault] has not yet realized that the
                    // Vault instances have transitioned to being healthy.
                    //
                    // We're going to ping Vault status via the proxy/REST API until
                    // it answers as ready.

                    using (var vaultJsonClient = new JsonClient(httpHandler, disposeHandler: false))
                    {
                        vaultJsonClient.BaseAddress = new Uri($"https://{manager.Name}.{hive.Definition.Hostnames.Vault}:{NetworkPorts.Vault}/");

                        manager.Status = "vault: stablize";

                        timer.Reset();

                        while (true)
                        {
                            if (timer.Elapsed >= timeout)
                            {
                                firstManager.Fault($"[Vault] cluster is not responding as ready after waiting [{timeout}].");
                                return;
                            }

                            try
                            {
                                var jsonResponse = vaultJsonClient.GetAsync("/v1/sys/seal-status").Result;

                                firstManager.Log($"*** VAULT STATUS-CODE: {jsonResponse.HttpResponse.StatusCode}/{jsonResponse.HttpResponse.ReasonPhrase}");

                                if (jsonResponse.IsSuccess)
                                {
                                    break;
                                }
                            }
                            catch (Exception e)
                            {
                                manager.LogException(e);
                                Thread.Sleep(TimeSpan.FromSeconds(1));
                            }
                        }

                        manager.Status = string.Empty;
                    }
                }
            }

            // Be really sure that vault is ready on all managers.

            hive.Vault.WaitUntilReady();
        }

        /// <summary>
        /// Configures the hive's HashiCorp Vault.
        /// </summary>
        private void ConfigureVault()
        {
            var firstManager = hive.FirstManager;

            try
            {
                // Initialize the Vault cluster using the first manager if it
                // hasn't already been initialized.

                if (hiveLogin.VaultCredentials == null)
                {
                    firstManager.Status = "vault: init";

                    var response = firstManager.SudoCommand(
                        "vault-direct operator init",
                        hive.SecureRunOptions | RunOptions.FaultOnError,
                        $"-key-shares={hive.Definition.Vault.KeyCount}",
                        $"-key-threshold={hive.Definition.Vault.KeyThreshold}");

                    if (response.ExitCode != 0)
                    {
                        firstManager.Fault($"[vault operator init] exit code [{response.ExitCode}]");
                        return;
                    }

                    var rawVaultCredentials = response.OutputText;

                    hiveLogin.VaultCredentials = VaultCredentials.FromInit(rawVaultCredentials, hive.Definition.Vault.KeyThreshold);

                    // Persist the Vault credentials.

                    hiveLogin.Save();
                }

                // Unseal Vault.

                VaultUnseal();

                // Configure the audit backend so that it sends events to syslog.

                firstManager.InvokeIdempotentAction("setup/vault-audit",
                    () =>
                    {
                        // $todo(jeff.lill):
                        //
                        // This command fails with later Vault versions for some
                        // reason.  We're not doing anything with the audit stream
                        // at this point anyway so I'm going to comment this out.
                        // This issue is tracked here:
                        //
                        //      https://github.com/jefflill/NeonForge/issues/37

                        //firstManager.Status = "vault: audit enable";
                        //hive.VaultCommand("vault audit enable syslog tag=\"vault\" facility=\"AUTH\"");
                    });

                // Mount a [generic] backend dedicated to neonHIVE related secrets.

                firstManager.InvokeIdempotentAction("setup/vault-enable-neon-secret",
                    () =>
                    {
                        firstManager.Status = "vault: enable neon-secret backend";
                        hive.Vault.Command("vault secrets enable", "-path=neon-secret", "generic");
                    });

                // Mount the [transit] backend and create the hive key.

                firstManager.InvokeIdempotentAction("setup/vault-enable-transit",
                    () =>
                    {
                        firstManager.Status = "vault: transit backend";
                        hive.Vault.Command("vault secrets enable transit");
                        hive.Vault.Command($"vault write -f transit/keys/{HiveConst.VaultTransitKey}");
                    });

                // Mount the [approle] backend.

                firstManager.InvokeIdempotentAction("setup/vault-enable-approle",
                    () =>
                    {
                        firstManager.Status = "vault: approle backend";
                        hive.Vault.Command("vault auth enable approle");
                    });

                // Initialize the standard policies.

                firstManager.InvokeIdempotentAction("setup/vault-policies",
                    () =>
                    {
                        firstManager.Status = "vault: policies";

                        var writeCapabilities = VaultCapabilies.Create | VaultCapabilies.Read | VaultCapabilies.Update | VaultCapabilies.Delete | VaultCapabilies.List;
                        var readCapabilities  = VaultCapabilies.Read | VaultCapabilies.List;

                        hive.Vault.SetPolicy(new VaultPolicy("neon-reader", "neon-secret/*", readCapabilities));
                        hive.Vault.SetPolicy(new VaultPolicy("neon-writer", "neon-secret/*", writeCapabilities));
                        hive.Vault.SetPolicy(new VaultPolicy("neon-cert-reader", "neon-secret/cert/*", readCapabilities));
                        hive.Vault.SetPolicy(new VaultPolicy("neon-cert-writer", "neon-secret/cert/*", writeCapabilities));
                        hive.Vault.SetPolicy(new VaultPolicy("neon-hosting-reader", "neon-secret/hosting/*", readCapabilities));
                        hive.Vault.SetPolicy(new VaultPolicy("neon-hosting-writer", "neon-secret/hosting/*", writeCapabilities));
                        hive.Vault.SetPolicy(new VaultPolicy("neon-service-reader", "neon-secret/service/*", readCapabilities));
                        hive.Vault.SetPolicy(new VaultPolicy("neon-service-writer", "neon-secret/service/*", writeCapabilities));
                        hive.Vault.SetPolicy(new VaultPolicy("neon-global-reader", "neon-secret/global/*", readCapabilities));
                        hive.Vault.SetPolicy(new VaultPolicy("neon-global-writer", "neon-secret/global/*", writeCapabilities));
                    });

                // Initialize the [neon-proxy-*] related service roles.  Each of these services 
                // need read access to the TLS certificates and [neon-proxy-manager] also needs
                // read access to the hosting options.

                firstManager.InvokeIdempotentAction("setup/vault-roles",
                    () =>
                    {
                        firstManager.Status = "vault: roles";

                        hive.Vault.SetAppRole("neon-proxy-manager", "neon-cert-reader", "neon-hosting-reader");
                        hive.Vault.SetAppRole("neon-proxy-public", "neon-cert-reader");
                        hive.Vault.SetAppRole("neon-proxy-private", "neon-cert-reader");
                    });

                // Store the hive hosting options in the Vault so services that need to perform
                // hosting level operations will have the credentials and other information to 
                // modify the environment.  For example in cloud environments, the [neon-proxy-manager]
                // service needs to be able to update the worker traffic manager rules so they match
                // the current PUBLIC routes.

                firstManager.InvokeIdempotentAction("setup/vault-hostingoptions",
                    () =>
                    {
                        firstManager.Status = "vault: hosting options";

                        using (var vault = HiveHelper.OpenVault(HiveCredentials.FromVaultToken(hiveLogin.VaultCredentials.RootToken)))
                        {
                            vault.WriteJsonAsync("neon-secret/hosting/options", hive.Definition.Hosting).Wait();

                            // Store the zipped OpenVPN certificate authority files in the hive Vault.

                            var vpnCaCredentials = hiveLogin.VpnCredentials;

                            if (vpnCaCredentials != null)
                            {
                                var vpnCaFiles = VpnCaFiles.LoadZip(vpnCaCredentials.CaZip, vpnCaCredentials.CaZipKey);

                                vpnCaFiles.Clean();
                                vault.WriteBytesAsync("neon-secret/vpn/ca.zip.encrypted", vpnCaFiles.ToZipBytes(vpnCaCredentials.CaZipKey)).Wait();
                            }
                        }
                    });
            }
            finally
            {
                hive.FirstManager.Status = string.Empty;
            }
        }

        /// <summary>
        /// Configures Consul values.
        /// </summary>
        private void ConfigureConsul()
        {
            var firstManager = hive.FirstManager;

            firstManager.InvokeIdempotentAction("setup/consul-initialize",
                () =>
                {
                    firstManager.Status = "consul initialize";

                    // Persist the hive definition (without important secrets) to
                    // Consul so it will be available services like [neon-proxy-manager]
                    // immediately (before [neon-hive-manager] spins up).

                    var loginClone = hiveLogin.Clone();

                    loginClone.ClearRootSecrets();
                    loginClone.Definition.Hosting.ClearSecrets();

                    HiveHelper.PutDefinitionAsync(loginClone.Definition, savePets: true).Wait();

                    firstManager.Status = "saving hive globals";

                    hive.Globals.Set(HiveGlobals.CreateDateUtc, DateTime.UtcNow.ToString(NeonHelper.DateFormatTZ, CultureInfo.InvariantCulture));
                    hive.Globals.Set(HiveGlobals.NeonCli, Program.MinimumVersion);
                    hive.Globals.Set(HiveGlobals.SetupPending, true);
                    hive.Globals.Set(HiveGlobals.UserAllowUnitTesting, hive.Definition.AllowUnitTesting);
                    hive.Globals.Set(HiveGlobals.UserDisableAutoUnseal, false);
                    hive.Globals.Set(HiveGlobals.UserLogRetentionDays, hive.Definition.Log.RetentionDays);
                    hive.Globals.Set(HiveGlobals.Uuid, Guid.NewGuid().ToString("D").ToLowerInvariant());
                    hive.Globals.Set(HiveGlobals.Version, Program.Version);
                });

            
            // Write the Consul globals that hold the HiveMQ settings for the
            // [app], [neon], and [sysadmin] accounts. 

            firstManager.InvokeIdempotentAction("setup/hivemq-settings",
                () =>
                {
                    firstManager.Status = "consul: hivemq settings";

                    // $note(jeff.lill):
                    //
                    // We're going to assign up to 10 swarm nodes as the hosts for both
                    // the AMQP and Admin traffic, favoring non-manager nodes if possible.
                    // This works because the services are behind a traffic manager rule
                    // so all we're using these hosts for is to have the Docker ingress
                    // network forward traffic to the traffic manager which will forward
                    // it on to a healthy RabbitMQ node.
                    //
                    // This should work pretty well and this will tolerate some changes
                    // to the hive host nodes, as long as at least one of the listed hive
                    // hosts is still active.

                    var hosts    = new List<string>();
                    var maxHosts = 10;

                    foreach (var node in hive.Definition.SortedWorkers)
                    {
                        hosts.Add($"{node.Name}.{hive.Definition.Hostnames.HiveMQ}");

                        if (hosts.Count >= maxHosts)
                        {
                            break;
                        }
                    }

                    if (hosts.Count < maxHosts)
                    {
                        foreach (var node in hive.Definition.SortedManagers)
                        {
                            hosts.Add($"{node.Name}.{hive.Definition.Hostnames.HiveMQ}");

                            if (hosts.Count >= maxHosts)
                            {
                                break;
                            }
                        }
                    }

                    var hiveMQSettings = new HiveMQSettings()
                    {
                        AmqpHosts   = hosts,
                        AmqpPort    = HiveHostPorts.ProxyPrivateHiveMQAMQP,
                        AdminHosts  = hosts,
                        AdminPort   = HiveHostPorts.ProxyPrivateHiveMQAdmin,
                        TlsEnabled  = false,
                        Username    = HiveConst.HiveMQSysadminUser,
                        Password    = hive.Definition.HiveMQ.SysadminPassword,
                        VirtualHost = "/"
                    };

                    firstManager.InvokeIdempotentAction("setup/neon-hivemq-settings-sysadmin",
                        () => hive.Globals.Set(HiveGlobals.HiveMQSettingSysadmin, hiveMQSettings));

                    hiveMQSettings.Username    = HiveConst.HiveMQNeonUser;
                    hiveMQSettings.Password    = hive.Definition.HiveMQ.NeonPassword;
                    hiveMQSettings.VirtualHost = HiveConst.HiveMQNeonVHost;

                    firstManager.InvokeIdempotentAction("setup/neon-hivemq-settings-neon",
                        () => hive.Globals.Set(HiveGlobals.HiveMQSettingsNeon, hiveMQSettings));

                    hiveMQSettings.Username    = HiveConst.HiveMQAppUser;
                    hiveMQSettings.Password    = hive.Definition.HiveMQ.AppPassword;
                    hiveMQSettings.VirtualHost = HiveConst.HiveMQAppVHost;

                    firstManager.InvokeIdempotentAction("setup/neon-hivemq-settings-app",
                        () => hive.Globals.Set(HiveGlobals.HiveMQSettingsApp, hiveMQSettings));

                    // Persist the HiveMQ bootstrap settings to Consul.  These settings have no credentials
                    // and reference the HiveMQ nodes directly (not via a traffic manager rule).

                    hive.HiveMQ.SaveBootstrapSettings();
                });
        }

        /// <summary>
        /// Configures any hive Docker secrets.
        /// </summary>
        public void ConfigureSecrets()
        {
            var firstManager = hive.FirstManager;

            // Create the [neon-ssh-credentials] Docker secret.

            firstManager.InvokeIdempotentAction("setup/neon-ssh-credentials",
                () =>
                {
                    firstManager.Status = "secret: SSH credentials";
                    hive.Docker.Secret.Set("neon-ssh-credentials", $"{hiveLogin.SshUsername}/{hiveLogin.SshPassword}");
                });

            // Create the [neon-ceph-dashboard-credentials] Docker secret.

            firstManager.InvokeIdempotentAction("setup/neon-ceph-dashboard-credentials",
                () =>
                {
                    firstManager.Status = "secret: Ceph dashboard credentials";
                    hiveLogin.CephDashboardUsername = HiveConst.DefaultUsername;
                    // hiveLogin.CephDashboardPassword = NeonHelper.GetRandomPassword(20);  $todo(jeff.lill): UNCOMMENT THIS????
                    hiveLogin.CephDashboardPassword = HiveConst.DefaultPassword;
                    hive.Docker.Secret.Set("neon-ceph-dashboard-credentials", $"{hiveLogin.CephDashboardUsername}/{hiveLogin.CephDashboardPassword}");
                });
        }

        /// <summary>
        /// Generates the SSH key to be used for authenticating SSH client connections.
        /// </summary>
        /// <param name="manager">A hive manager node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void GenerateClientSshKey(SshProxy<NodeDefinition> manager, TimeSpan stepDelay)
        {
            // Here's some information explaining what how I'm doing this:
            //
            //      https://help.ubuntu.com/community/SSH/OpenSSH/Configuring
            //      https://help.ubuntu.com/community/SSH/OpenSSH/Keys

            if (!sshTlsAuth)
            {
                return;
            }

            if (hiveLogin.SshClientKey != null)
            {
                return; // Key has already been created.
            }

            Thread.Sleep(stepDelay);

            hiveLogin.SshClientKey = new SshClientKey();

            // $hack(jeff.lill): 
            //
            // We're going to generate a 2048 bit key pair on one of the
            // manager nodes and then download and then delete it.  This
            // means that the private key will be persisted to disk (tmpfs)
            // for a moment but I'm going to worry about that too much
            // since we'll be rebooting the manager later on during setup.
            //
            // Technically, I could have installed OpenSSL or something
            // on Windows or figured out the .NET Crypto libraries but
            // but OpenSSL didn't support generating the PUB format
            // SSH expects for the client public key.

            const string keyGenScript =
@"
# Generate a 2048-bit key without a passphrase (the -N """" option).

ssh-keygen -t rsa -b 2048 -N """" -C ""neonhive"" -f /run/ssh-key

# Relax permissions so we can download the key parts.

chmod 666 /run/ssh-key*
";
            var bundle = new CommandBundle("./keygen.sh");

            bundle.AddFile("keygen.sh", keyGenScript, isExecutable: true);

            manager.SudoCommand(bundle);

            using (var stream = new MemoryStream())
            {
                manager.Download("/run/ssh-key.pub", stream);

                hiveLogin.SshClientKey.PublicPUB = Encoding.UTF8.GetString(stream.ToArray());
            }

            using (var stream = new MemoryStream())
            {
                manager.Download("/run/ssh-key", stream);

                hiveLogin.SshClientKey.PrivatePEM = Encoding.UTF8.GetString(stream.ToArray());
            }

            manager.SudoCommand("rm /run/ssh-key*");

            // We're going to use WinSCP to convert the OpenSSH PEM formatted key
            // to the PPK format PuTTY/WinSCP require.  Note that this won't work
            // when the tool is running in a Docker Linux container.  We're going
            // to handle the conversion in the outer shim as a post run action.

            if (NeonHelper.IsWindows)
            {
                var pemKeyPath = Path.Combine(Program.HiveTempFolder, Guid.NewGuid().ToString("D"));
                var ppkKeyPath = Path.Combine(Program.HiveTempFolder, Guid.NewGuid().ToString("D"));

                try
                {
                    File.WriteAllText(pemKeyPath, hiveLogin.SshClientKey.PrivatePEM);

                    ExecuteResult result;

                    try
                    {
                        result = NeonHelper.ExecuteCapture("winscp.com", $@"/keygen ""{pemKeyPath}"" /comment=""{hive.Definition.Name} Key"" /output=""{ppkKeyPath}""");
                    }
                    catch (Win32Exception)
                    {
                        return; // Tolerate when WinSCP isn't installed.
                    }

                    if (result.ExitCode != 0)
                    {
                        Console.WriteLine(result.OutputText);
                        Console.Error.WriteLine(result.ErrorText);
                        Program.Exit(result.ExitCode);
                    }

                    hiveLogin.SshClientKey.PrivatePPK = File.ReadAllText(ppkKeyPath);

                    // Persist the SSH client key.

                    hiveLogin.Save();
                }
                finally
                {
                    if (File.Exists(pemKeyPath))
                    {
                        File.Delete(pemKeyPath);
                    }

                    if (File.Exists(ppkKeyPath))
                    {
                        File.Delete(ppkKeyPath);
                    }
                }
            }
        }

        /// <summary>
        /// Changes the admin account's password on a node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void SetStrongPassword(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            node.InvokeIdempotentAction("setup/strong-password",
                () =>
                {
                    Thread.Sleep(stepDelay);

                    node.Status = "strong password";

                    var script =
$@"
echo '{hiveLogin.SshUsername}:{hiveLogin.SshPassword}' | chpasswd
";
                    var bundle = new CommandBundle("./set-strong-password.sh");

                    bundle.AddFile("set-strong-password.sh", script, isExecutable: true);

                    var response = node.SudoCommand(bundle);

                    if (response.ExitCode != 0)
                    {
                        Console.Error.WriteLine($"*** ERROR: Unable to set a strong password [exitcode={response.ExitCode}].");
                        Program.Exit(response.ExitCode);
                    }

                    node.UpdateCredentials(SshCredentials.FromUserPassword(hiveLogin.Username, hiveLogin.SshPassword));
                });
        }

        /// <summary>
        /// Generates the private key that will be used to secure SSH on the hive servers.
        /// </summary>
        private void ConfigureSshCerts()
        {
            hive.FirstManager.InvokeIdempotentAction("setup/ssh-server-key",
                () =>
                {
                    hive.FirstManager.Status = "generate server SSH key";

                    var configScript =
@"
# Generate the SSH server key and fingerprint.

mkdir -p /dev/shm/ssh

# For idempotentcy, ensure that the key file doesn't already exist to
# avoid having the [ssh-keygen] command prompt and wait for permission
# to overwrite it.

if [ -f /dev/shm/ssh/ssh_host_rsa_key ] ; then
    rm /dev/shm/ssh/ssh_host_rsa_key
fi

ssh-keygen -f /dev/shm/ssh/ssh_host_rsa_key -N '' -t rsa

# Extract the host's SSL RSA key fingerprint to a temporary file
# so [neon-cli] can download it.

ssh-keygen -l -E md5 -f /dev/shm/ssh/ssh_host_rsa_key > /dev/shm/ssh/ssh.fingerprint

# The files need to have user permissions so we can download them.

chmod 777 /dev/shm/ssh/
chmod 666 /dev/shm/ssh/ssh_host_rsa_key
chmod 666 /dev/shm/ssh/ssh_host_rsa_key.pub
chmod 666 /dev/shm/ssh/ssh.fingerprint
";
                    var bundle = new CommandBundle("./config.sh");

                    bundle.AddFile("config.sh", configScript, isExecutable: true);
                    hive.FirstManager.SudoCommand(bundle);

                    hive.FirstManager.Status = "download server SSH key";

                    hiveLogin.SshHiveHostPrivateKey     = hive.FirstManager.DownloadText("/dev/shm/ssh/ssh_host_rsa_key");
                    hiveLogin.SshHiveHostPublicKey      = hive.FirstManager.DownloadText("/dev/shm/ssh/ssh_host_rsa_key.pub");
                    hiveLogin.SshHiveHostKeyFingerprint = hive.FirstManager.DownloadText("/dev/shm/ssh/ssh.fingerprint");

                    // Delete the SSH key files for security.

                    hive.FirstManager.SudoCommand("rm -r /dev/shm/ssh");

                    // Persist the server SSH key and fingerprint.

                    hiveLogin.Save();
                });
        }

        /// <summary>
        /// Configures SSH on a node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void ConfigureSsh(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            // Configure the SSH credentials on all hive nodes.

            node.InvokeIdempotentAction("setup/ssh",
                () =>
                {
                    Thread.Sleep(stepDelay);

                    CommandBundle bundle;

                    // Here's some information explaining what how I'm doing this:
                    //
                    //      https://help.ubuntu.com/community/SSH/OpenSSH/Configuring
                    //      https://help.ubuntu.com/community/SSH/OpenSSH/Keys

                    if (sshTlsAuth)
                    {
                        node.Status = "client SSH key";

                        // Enable the public key by appending it to [$HOME/.ssh/authorized_keys],
                        // creating the file if necessary.  Note that we're allowing only a single
                        // authorized key.

                        var addKeyScript =
$@"
chmod go-w ~/
mkdir -p $HOME/.ssh
chmod 700 $HOME/.ssh
touch $HOME/.ssh/authorized_keys
cat ssh-key.pub > $HOME/.ssh/authorized_keys
chmod 600 $HOME/.ssh/authorized_keys
";
                        bundle = new CommandBundle("./addkeys.sh");

                        bundle.AddFile("addkeys.sh", addKeyScript, isExecutable: true);
                        bundle.AddFile("ssh-key.pub", hiveLogin.SshClientKey.PublicPUB);

                        // NOTE: I'm explictly not running the bundle as [sudo] because the OpenSSH
                        //       server is very picky about the permissions on the user's [$HOME]
                        //       and [$HOME/.ssl] folder and contents.  This took me a couple 
                        //       hours to figure out.

                        node.RunCommand(bundle);
                    }

                    // These steps are required for both password and public key authentication.

                    // Upload the server key and edit the SSHD config to disable all host keys 
                    // except for RSA.

                    var configScript =
@"
# Copy the server key.

cp ssh_host_rsa_key /etc/ssh/ssh_host_rsa_key

# Disable all host keys except for RSA.

sed -i 's!^\HostKey /etc/ssh/ssh_host_dsa_key$!#HostKey /etc/ssh/ssh_host_dsa_key!g' /etc/ssh/sshd_config
sed -i 's!^\HostKey /etc/ssh/ssh_host_ecdsa_key$!#HostKey /etc/ssh/ssh_host_ecdsa_key!g' /etc/ssh/sshd_config
sed -i 's!^\HostKey /etc/ssh/ssh_host_ed25519_key$!#HostKey /etc/ssh/ssh_host_ed25519_key!g' /etc/ssh/sshd_config

# Restart SSHD to pick up the changes.

systemctl restart sshd
";
                    bundle = new CommandBundle("./config.sh");

                    bundle.AddFile("config.sh", configScript, isExecutable: true);
                    bundle.AddFile("ssh_host_rsa_key", hiveLogin.SshHiveHostPrivateKey);
                    node.SudoCommand(bundle);
                });
        }

        /// <summary>
        /// Configures the built-in hive dashboards.
        /// </summary>
        private void ConfigureDashboards()
        {
            var firstManager = hive.FirstManager;

            firstManager.InvokeIdempotentAction("setup/dashboards",
                () =>
                {
                    // Configure the [Ceph] dashboard.
                    //
                    // Note that the old [luminous] dashboard is HTTP and is hardcoded to listen
                    // on port 7000 whereas the [mimic] and later releases are HTTPS and listen
                    // on [HiveHostPorts.ProxyPrivateHttpCephDashboard].
                    //
                    // Note that only the Ceph dashboard running on the lead MGR can actually
                    // render properly so non-leader MGR dashboards will redirect incoming 
                    // requests to the leader.  We're going to mitigate this by setting up
                    // a traffic manager rule that only considers 2XX responses as valid so
                    // that we won't direct traffic to the non-lead MGRs that return 302
                    // redirects.
                    //
                    //      https://github.com/jefflill/NeonForge/issues/222

                    if (hive.Definition.HiveFS.Enabled && hive.Definition.Dashboard.HiveFS)
                    {
                        firstManager.Status = "hivefs dashboard";

                        if (hive.Definition.HiveFS.Release == "luminous")
                        {
                            var hiveFSDashboard = new HiveDashboard()
                            {
                                Name        = "hivefs",
                                Title       = "Hive File System",
                                Folder      = HiveConst.DashboardSystemFolder,
                                Url         = $"http://reachable-manager:{HiveHostPorts.ProxyPrivateHttpCephDashboard}",
                                Description = "Ceph distributed file system"
                            };

                            hive.Dashboard.Set(hiveFSDashboard);

                            var rule = new TrafficHttpRule()
                            {
                                Name     = "neon-hivefs-dashboard",
                                System   = true,
                                Resolver = null
                            };

                            // The [luminous] dashboard is not secured by TLS, so we can perform
                            // normal HTTP health checks.  Note that only the active lead Ceph MGR
                            // node's dashboard actually works.  The non-leader nodes will return
                            // a 307 (temporary redirect) status code.
                            //
                            // We're going to consider only servers that return 2xx status codes
                            // as healthy so we'll always direct traffic to the lead MGR.

                            rule.CheckMode   = TrafficCheckMode.Http;
                            rule.CheckTls    = false;
                            rule.CheckExpect = @"rstatus ^2\d\d";

                            // Initialize the frontends and backends.

                            rule.Frontends.Add(
                                new TrafficHttpFrontend()
                                {
                                    ProxyPort = HiveHostPorts.ProxyPrivateHttpCephDashboard
                                });

                            foreach (var monNode in hive.Nodes.Where(n => n.Metadata.Labels.CephMON))
                            {
                                rule.Backends.Add(
                                    new TrafficHttpBackend()
                                    {
                                        Server = monNode.Metadata.PrivateAddress.ToString(),
                                        Port   = 7000,  // The [luminous] dashboard is hardcoded to port 7000
                                    });
                            }

                            hive.PrivateTraffic.SetRule(rule);
                        }
                        else
                        {
                            // [mimic] and later releases is a bit more complicated because
                            // the dashboard is required to serve HTTPS.  So the dashboard 
                            // manages the certificate/key and which means that we need to
                            // deploy a pass-thru TCP rule.
                            //
                            // Note that only the active lead Ceph MGR node's dashboard actually
                            // works.  The non-leader nodes will return a 307 (temporary redirect) 
                            // status code.  The trick here is that we need to enable an HTTPS 
                            // based health check.
                            //
                            // We're going to consider only servers that return 2xx status codes
                            // as healthy so we'll always direct traffic to the lead MGR.

                            var hiveFSDashboard = new HiveDashboard()
                            {
                                Name        = "hivefs",
                                Title       = "Hive File System",
                                Folder      = HiveConst.DashboardSystemFolder,
                                Url         = $"https://reachable-manager:{HiveHostPorts.ProxyPrivateHttpCephDashboard}",
                                Description = "Ceph distributed file system"
                            };

                            hive.Dashboard.Set(hiveFSDashboard);

                            var rule = new TrafficTcpRule()
                            {
                                Name     = "neon-hivefs-dashboard",
                                System   = true,
                                Resolver = null
                            };

                            // The [luminous] dashboard is not secured by TLS, so we can perform
                            // normal HTTP health checks.  Note that only the active lead Ceph MGR
                            // node's dashboard actually works.  The non-leader nodes will return
                            // a 307 (temporary redirect) status code.
                            //
                            // We're going to consider only servers that return 2xx status codes
                            // as healthy so we'll always direct traffic to the lead MGR.

                            rule.CheckMode   = TrafficCheckMode.Http;
                            rule.CheckTls    = true;
                            rule.CheckExpect = @"rstatus ^2\d\d";

                            // Initialize the frontends and backends.

                            rule.Frontends.Add(
                                new TrafficTcpFrontend()
                                {
                                    ProxyPort = HiveHostPorts.ProxyPrivateHttpCephDashboard
                                });

                            rule.Backends.Add(
                                new TrafficTcpBackend()
                                {
                                    Group      = HiveHostGroups.CephMON,
                                    GroupLimit = 5,
                                    Port       = HiveHostPorts.CephDashboard
                                });

                            hive.PrivateTraffic.SetRule(rule);
                        }

                        firstManager.Status = string.Empty;
                    }

                    // Configure the Consul dashboard.

                    if (hive.Definition.Dashboard.Consul)
                    {
                        firstManager.Status = "consul dashboard";

                        var consulDashboard = new HiveDashboard()
                        {
                            Name        = "consul",
                            Title       = "Consul",
                            Folder      = HiveConst.DashboardSystemFolder,
                            Url         = $"{hive.Definition.Consul.Scheme}://reachable-manager:{NetworkPorts.Consul}/ui",
                            Description = "Hive Consul key/value store"
                        };

                        hive.Dashboard.Set(consulDashboard);
                        firstManager.Status = string.Empty;
                    }

                    // Configure the Kibana dashboard.

                    if (hive.Definition.Log.Enabled && hive.Definition.Dashboard.Kibana)
                    {
                        firstManager.Status = "kibana dashboard";

                        var kibanaDashboard = new HiveDashboard()
                        {
                            Name        = "kibana",
                            Title       = "Kibana",
                            Folder      = HiveConst.DashboardSystemFolder,
                            Url         = $"http://reachable-manager:{HiveHostPorts.ProxyPrivateKibanaDashboard}",
                            Description = "Kibana hive monitoring dashboard"
                        };

                        hive.Dashboard.Set(kibanaDashboard);
                        firstManager.Status = string.Empty;
                    }

                    // Configure the Vault dashboard.

                    if (hive.Definition.Dashboard.Vault)
                    {
                        firstManager.Status = "vault dashboard";

                        var vaultDashboard = new HiveDashboard()
                        {
                            Name        = "vault",
                            Title       = "Vault",
                            Folder      = HiveConst.DashboardSystemFolder,
                            Url         = $"https://reachable-manager:{hive.Definition.Vault.Port}/ui",
                            Description = "Hive Vault secure storage"
                        };

                        hive.Dashboard.Set(vaultDashboard);
                        firstManager.Status = string.Empty;
                    }

                    // Configure the HiveMQ dashboard.

                    if (hive.Definition.Dashboard.HiveMQ)
                    {
                        firstManager.Status = "hivemq dashboard";

                        var rabbitDashboard = new HiveDashboard()
                        {
                            Name        = "hivemq",
                            Title       = "Hive Messaging System",
                            Folder      = HiveConst.DashboardSystemFolder,
                            Url         = $"http://reachable-manager:{HiveHostPorts.ProxyPrivateHiveMQAdmin}",
                            //Url         = $"https://reachable-manager:{HiveHostPorts.ProxyPrivateHiveMQManagement}",
                            Description = "RabbitMQ based messaging system"
                        };

                        hive.Dashboard.Set(rabbitDashboard);
                    }

                    firstManager.Status = string.Empty;
                });
        }
    }
}
