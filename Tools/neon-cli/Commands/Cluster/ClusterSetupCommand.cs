//-----------------------------------------------------------------------------
// FILE:	    ClusterSetupCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
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

using Neon.Cluster;
using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>cluster setup</b> command.
    /// </summary>
    public class ClusterSetupCommand : CommandBase
    {
        private const string usage = @"
Configures a neonCLUSTER as described in the cluster definition file.

USAGE: 

    neon cluster setup [OPTIONS] root@CLUSTER

ARGUMENTS:

    CLUSTER     - The cluster name.

OPTIONS:

    --unredacted        - Runs Vault related commands without redacting logs.
                          This is useful for debugging cluster setup issues.
                          Do not use for production clusters.
";
        private string              clusterLoginPath;
        private ClusterLogin        clusterLogin;
        private ClusterProxy        cluster;
        private string              managerNodeNames     = string.Empty;
        private string              managerNodeAddresses = string.Empty;
        private int                 managerCount         = 0;
        private bool                sshTlsAuth;

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "cluster", "setup" }; }
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
            if (Program.ClusterLogin != null)
            {
                Console.Error.WriteLine("*** ERROR: You are logged into a cluster.  You need to logout before setting up another.");
                Program.Exit(1);
            }

            if (commandLine.Arguments.Length < 1)
            {
                Console.Error.WriteLine("*** ERROR: [root@CLUSTER] argument is required.");
                Program.Exit(1);
            }

            var login = NeonClusterHelper.SplitLogin(commandLine.Arguments[0]);

            if (!login.IsOK)
            {
                Console.WriteLine($"*** ERROR: Invalid username/cluster [{commandLine.Arguments[0]}].  Expected something like: USER@CLUSTER");
                Program.Exit(1);
            }

            var username    = login.Username;
            var clusterName = login.ClusterName;

            clusterLoginPath = Program.GetClusterLoginPath(username, clusterName);

            if (!File.Exists(clusterLoginPath))
            {
                Console.Error.WriteLine($"*** ERROR: Be sure to prepare the cluster first using [neon cluster prepare...].  File [{clusterLoginPath}] not found.");
                Program.Exit(1);
            }

            clusterLogin      = NeonHelper.JsonDeserialize<ClusterLogin>(File.ReadAllText(clusterLoginPath));
            clusterLogin.Path = clusterLoginPath;

            if (!clusterLogin.SetupPending)
            {
                Console.Error.WriteLine($"*** ERROR: Cluster [{clusterLogin.ClusterName}] has already been setup.");
            }

            cluster = new ClusterProxy(clusterLogin, Program.CreateNodeProxy<NodeDefinition>, RunOptions.LogOutput | RunOptions.FaultOnError);

            if (cluster.Definition.Vault.DebugSetup)
            {
                // Note that we log a warning when this is true for each node in [ConfifureCommon()].

                cluster.VaultRunOptions = RunOptions.FaultOnError;
            }

            // We need to ensure that any necessary VPN connection is opened if we're
            // not provisioning on-premise or not running in the tool container.

            if (clusterLogin.Definition.Vpn.Enabled && 
                clusterLogin.Definition.Hosting.IsCloudProvider && 
                !NeonClusterHelper.InToolContainer)
            {
                NeonClusterHelper.VpnOpen(clusterLogin,
                    onStatus: message => Console.WriteLine($"*** {message}"),
                    onError: message => Console.Error.WriteLine($"*** ERROR {message}"));
            }

            // Generate a string with the IP addresses of the management nodes separated
            // by spaces.  We'll need this when we initialize the management nodes.
            //
            // We're also going to select the management address that we'll use to for
            // joining regular nodes to the cluster.  We'll use the first management
            // node when sorting in ascending order by name for this.

            foreach (var managerNodeDefinition in cluster.Definition.SortedManagers)
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
                cluster.VaultRunOptions = RunOptions.None;
            }

            // Perform the setup operations.

            var controller = 
                new SetupController<NodeDefinition>(new string[] { "cluster", "setup", $"[{cluster.Name}]" }, cluster.Nodes)
                {
                    ShowStatus  = !Program.Quiet,
                    MaxParallel = Program.MaxParallel
                };

            controller.AddWaitUntilOnlineStep("connect");

            switch (cluster.Definition.HostNode.SshAuth)
            {
                case AuthMethods.Password:

                    sshTlsAuth = false;
                    break;

                case AuthMethods.Tls:

                    sshTlsAuth = true;
                    break;

                default:

                    throw new NotSupportedException($"Unsupported SSH authentication method [{cluster.Definition.HostNode.SshAuth}].");
            }

            if (sshTlsAuth)
            {
                controller.AddStep("ssh client cert", n => GenerateClientSshKey(n), n => n == cluster.FirstManager);
            }

            controller.AddStep("verify OS", n => CommonSteps.VerifyOS(n));
            controller.AddGlobalStep("create certs", () => CreateCertificates());

            // We're going to configure the managers separately from the workers
            // because we need to be careful about when we reboot the managers
            // since this will also take down the VPN.  We're also going to 
            // reboot all of the managers together after common manager 
            // configuration is complete for the same reason.

            controller.AddStep("manager initialize", n => ConfigureCommon(n), n => n.Metadata.IsManager);

            controller.AddStep("manager restart", 
                n =>
                {
                    n.InvokeIdempotentAction("setup-common-restart", () => RebootAndWait(n));
                },
                n => n.Metadata.IsManager,
                noParallelLimit: true);

            controller.AddStep("manager config", 
                n => ConfigureManager(n),
                n => n.Metadata.IsManager,
                stepDelaySeconds: cluster.Definition.StepDelaySeconds);

            // Configure the workers and pets.

            if (cluster.Workers.Count() > 0 || cluster.Pets.Count() > 0)
            {
                var workerPetStepLabel = "worker/pet config";

                if (cluster.Workers.Count() == 0)
                {
                    workerPetStepLabel = "pet config";
                }
                else if (cluster.Pets.Count() == 0)
                {
                    workerPetStepLabel = "worker config";
                }

                controller.AddStep(workerPetStepLabel,
                    n =>
                    {
                        ConfigureCommon(n);
                        n.InvokeIdempotentAction("setup-common-restart", () => RebootAndWait(n));
                        ConfigureNonManager(n);
                    },
                    n => n.Metadata.IsWorker || n.Metadata.IsPet,
                    stepDelaySeconds: cluster.Definition.StepDelaySeconds);
            }

            // Create the Swarm.

            controller.AddStep("swarm create", n => CreateSwarm(n), n => n == cluster.FirstManager);
            controller.AddStep("swarm join", n => JoinSwarm(n), n => n != cluster.FirstManager && !n.Metadata.IsPet);

            // Continue with the configuration unless we're just installing bare Docker.

            if (!cluster.Definition.BareDocker)
            {
                if (cluster.Definition.Ceph.Enabled)
                {
                    controller.AddStep("ceph packages", 
                        n => CephPackages(n),
                        stepDelaySeconds: cluster.Definition.StepDelaySeconds);

                    controller.AddGlobalStep("ceph settings", () => CephSettings());
                    controller.AddStep("ceph bootstrap", n => CephBootstrap(n), n => n.Metadata.Labels.CephMON);
                    controller.AddStep("ceph cluster", n => CephCluster(n), noParallelLimit: true);
                }

                controller.AddStep("networks", n => CreateClusterNetworks(n), n => n == cluster.FirstManager);
                controller.AddStep("node labels", n => AddNodeLabels(n), n => n == cluster.FirstManager);

                if (cluster.Definition.Docker.RegistryCache)
                {
                    var registryCacheConfigurator = new RegistryCache(cluster, clusterLoginPath);

                    controller.AddStep("registry cache", n => registryCacheConfigurator.Configure(n));
                }

                if (cluster.Definition.Docker.RegistryCache && cluster.Definition.NodeDefinitions.Count > 1)
                {
                    // The cluster deploys a local registry cache and we have multiple
                    // nodes, so we're going to pull images on the first manager first 
                    // so they get loaded into the cluster's register's registry cache 
                    // and then pull for all of the other nodes in a subsequent step
                    // (so we don't slam the Internet connection and the package mirrors.

                    controller.AddStep("pull images to cache", n => PullImages(n, pullAll: true), n => n == cluster.FirstManager);
                    controller.AddStep("pull images to nodes", n => PullImages(n), n => n != cluster.FirstManager);
                }
                else
                {
                    // Just pull the images to all nodes in parallel if there's 
                    // no registry cache deployed.

                    controller.AddStep("pull images", n => PullImages(n));
                }

                controller.AddGlobalStep("cluster key/value",
                    () =>
                    {
                        NeonClusterHelper.OpenCluster(cluster);

                        VaultProxy();
                        VaultInitialize();
                        ConsulInitialize();
                    });

                var clusterServices = new ClusterServices(cluster);

                controller.AddGlobalStep("cluster services", () => clusterServices.Configure(cluster.FirstManager));

                if (cluster.Pets.Count() > 0)
                {
                    controller.AddStep("cluster containers", n => clusterServices.DeployContainers(n));
                }

                if (cluster.Definition.Log.Enabled)
                {
                    var logServices = new LogServices(cluster);

                    controller.AddGlobalStep("log services", () => logServices.Configure(cluster.FirstManager));
                    controller.AddStep("log containers", n => logServices.DeployContainers(n));
                }

                controller.AddStep("check managers", n => ClusterDiagnostics.CheckManager(n, cluster.Definition), n => n.Metadata.IsManager);
                controller.AddStep("check workers/pets", n => ClusterDiagnostics.CheckWorkersOrPet(n, cluster.Definition), n => n.Metadata.IsWorker || n.Metadata.IsPet);

                if (cluster.Definition.Log.Enabled)
                {
                    controller.AddGlobalStep("check logging", () => ClusterDiagnostics.CheckLogServices(cluster));
                }
            }

            // Change the root account's password to something very strong.  
            // This step should be very close to the last one so it will still be
            // possible to log into nodes with the old password to diagnose
            // setup issues.
            //
            // Note that the if statement verifies that we haven't already generated
            // the strong password in a previous setup run that failed.  This prevents
            // us from generating a new password, perhaps resulting in cluster nodes
            // having different passwords.

            if (!clusterLogin.HasStrongSshPassword)
            {
                if (cluster.Definition.HostNode.PasswordLength > 0)
                {
                    clusterLogin.SshPassword          = NeonHelper.GetRandomPassword(cluster.Definition.HostNode.PasswordLength);
                    clusterLogin.HasStrongSshPassword = true;
                }
                else
                {
                    clusterLogin.SshPassword = Program.MachinePassword;
                }

                clusterLogin.Save();
            }

            if (cluster.Definition.HostNode.PasswordLength > 0)
            {
                controller.AddStep("strong password",
                    n =>
                    {
                        SetStrongPassword(n);
                    });
            }

            controller.AddGlobalStep("ssh certs", () => ConfigureSshCerts());

            // This needs to be run last because it will likely disable
            // SSH username/password authentication which may block
            // connection attempts.
            //
            // It's also good to do this last so it'll be possible to 
            // manually login with the original credentials to diagnose
            // setup issues.

            controller.AddStep("ssh secured", n => ConfigureSsh(n));

            // Start setup.

            if (!controller.Run())
            {
                Console.Error.WriteLine("*** ERROR: One or more configuration steps failed.");
                Program.Exit(1);
            }

            // Update the cluster login file.

            clusterLogin.SetupPending = false;
            clusterLogin.IsRoot       = true;
            clusterLogin.Username     = NeonClusterConst.RootUser;
            clusterLogin.Definition   = cluster.Definition;
            clusterLogin.SshUsername  = Program.MachineUsername;

            if (cluster.Definition.Vpn.Enabled)
            {
                // We don't need to save the certificate authority files any more
                // because they've been stored in the cluster Vault.

                clusterLogin.VpnCredentials.CaZip = null;
            }

            clusterLogin.Save();

            Console.WriteLine($"*** Logging into [{NeonClusterConst.RootUser}@{cluster.Definition.Name}].");

            // Note that we're going to login via the VPN for cloud environments
            // but not for local hosting since the operator had to be on-premise
            // to have just completed cluster setup.
            var currentLogin =
                new CurrentClusterLogin()
                {
                    Login  = $"{NeonClusterConst.RootUser}@{cluster.Definition.Name}",
                    ViaVpn = clusterLogin.Definition.Hosting.Environment != HostingEnvironments.Machine
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
                // We'll run the command in [--no-tool-container] mode for this option.

                return new DockerShimInfo(isShimmed: false);
            }

            if (Program.ClusterLogin != null)
            {
                Console.Error.WriteLine("*** ERROR: You are logged into a cluster.  You need to logout before setting up another.");
                Program.Exit(1);
            }

            // The argument should be the user's cluster login.

            if (commandLine.Arguments.Length < 1)
            {
                Console.Error.WriteLine("*** ERROR: [root@CLUSTER] argument is required.");
                Program.Exit(1);
            }

            var login = NeonClusterHelper.SplitLogin(commandLine.Arguments[0]);

            if (!login.IsOK)
            {
                Console.WriteLine($"*** ERROR: Invalid username/cluster [{commandLine.Arguments[0]}].  Expected something like: USER@CLUSTER");
                Program.Exit(1);
            }

            var username    = login.Username;
            var clusterName = login.ClusterName;

            var clusterLoginPath = Program.GetClusterLoginPath(username, clusterName);

            if (!File.Exists(clusterLoginPath))
            {
                Console.Error.WriteLine($"*** ERROR: Be sure to prepare the cluster first using [neon cluster prepare...].  File [{clusterLoginPath}] not found.");
                Program.Exit(1);
            }

            clusterLogin      = NeonHelper.JsonDeserialize<ClusterLogin>(File.ReadAllText(clusterLoginPath));
            clusterLogin.Path = clusterLoginPath;

            clusterLogin.Definition.Validate();

            if (!clusterLogin.SetupPending)
            {
                Console.Error.WriteLine($"*** ERROR: Cluster [{clusterName}] has already been setup.");
            }

            // Cloud deployments had their VPN configured when they were prepared
            // so we need to connect the VPN now so we can setup the nodes.  
            //
            // On-premise clusters are always setup via local network connections
            // so we will be connecting the VPN. 

            if (clusterLogin.Definition.Vpn.Enabled &&
                clusterLogin.Definition.Hosting.IsCloudProvider)
            {
                NeonClusterHelper.VpnOpen(clusterLogin,
                    onStatus: message => Console.WriteLine($"*** {message}"),
                    onError: message => Console.WriteLine($"*** ERROR: {message}"));
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

                    var pemKeyPath = Path.Combine(Program.ClusterTempFolder, Guid.NewGuid().ToString("D"));
                    var ppkKeyPath = Path.Combine(Program.ClusterTempFolder, Guid.NewGuid().ToString("D"));

                    try
                    {
                        // Reload the login to pick up changes from the shimmed command.

                        clusterLogin      = NeonClusterHelper.LoadClusterLogin(NeonClusterConst.RootUser, clusterLogin.Definition.Name);
                        clusterLogin.Path = clusterLoginPath;

                        // Update the the PuTTY/WinSCP key.

                        File.WriteAllText(pemKeyPath, clusterLogin.SshClientKey.PrivatePEM);

                        ExecuteResult result;

                        try
                        {
                            result = NeonHelper.ExecuteCaptureStreams("winscp.com", $@"/keygen ""{pemKeyPath}"" /comment=""{clusterLogin.Definition.Name} Key"" /output=""{ppkKeyPath}""");
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

                        clusterLogin.SshClientKey.PrivatePPK = File.ReadAllText(ppkKeyPath);

                        clusterLogin.Save();
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

            return new DockerShimInfo(isShimmed: true, ensureConnection: false);
        }

        /// <summary>
        /// Generates the cluster certificates.
        /// </summary>
        private void CreateCertificates()
        {
            const int bitCount  = 2048;
            const int validDays = 365000;    // About 1,000 years.

            if (clusterLogin.VaultCertificate == null)
            {
                clusterLogin.VaultCertificate = TlsCertificate.CreateSelfSigned(NeonHosts.Vault, bitCount, validDays);
            }

            // $todo(jeff.lill): Generate the Consul cert.

            // Persist the certificates into the cluster login.

            clusterLogin.Save();
        }

        /// <summary>
        /// Performs common node configuration.
        /// </summary>
        /// <param name="node">The target cluster node.</param>
        private void ConfigureCommon(SshProxy<NodeDefinition> node)
        {
            //-----------------------------------------------------------------
            // NOTE: 
            //
            // We're going to perform the following steps outside of the
            // idempotent check to make it easier to debug and modify 
            // scripts and tools when cluster setup has been partially
            // completed.  These steps are implicitly idempotent and
            // complete pretty quickly.

            if (cluster.Definition.Vault.DebugSetup)
            {
                node.Log($"SECURITY WARNING: Cluster definition [{nameof(ClusterDefinition.Vault)}.{nameof(VaultOptions.DebugSetup)}=true] which disables redaction of potentially sensitive VAULT information.  This should never be enabled for production clusters.");
            }

            // Configure the node's environment variables.

            CommonSteps.ConfigureEnvironmentVariables(node, cluster.Definition);

            // Upload the setup and configuration files.

            node.InitializeNeonFolders();
            node.UploadConfigFiles(cluster.Definition);
            node.UploadTools(cluster.Definition);

            //-----------------------------------------------------------------
            // Ensure the following steps are executed only once.

            node.InvokeIdempotentAction("setup-common",
                () =>
                {
                    // Ensure that the node has been prepared for setup.

                    var waitedForPackageManager = CommonSteps.PrepareNode(node, cluster.Definition);

                    if (!waitedForPackageManager)
                    {
                        // Make sure that the APT package manager is ready if
                        // [CommonSteps.PrepareNode()] hasn't already done that.

                        CommonSteps.WaitForPackageManager(node);
                    }

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

                    // Perform basic node setup including changing the host name.

                    UploadHostsFile(node);
                    UploadHostEnvFile(node);

                    node.Status = "run: setup-node.sh";
                    node.SudoCommand("setup-node.sh");

                    // Create and configure the internal cluster self-signed certificates.

                    node.Status = "install certs";

                    // Install the Vault certificate.

                    if (!node.Metadata.IsPet)
                    {
                        node.UploadText($"/usr/local/share/ca-certificates/{NeonHosts.Vault}.crt", clusterLogin.VaultCertificate.Cert);
                        node.SudoCommand("mkdir -p /etc/vault");
                        node.UploadText($"/etc/vault/vault.crt", clusterLogin.VaultCertificate.Cert);
                        node.UploadText($"/etc/vault/vault.key", clusterLogin.VaultCertificate.Key);
                        node.SudoCommand("chmod 600 /etc/vault/*");

                        // $todo(jeff.lill): Install the Consul certificate once we support Consul TLS.

                        node.SudoCommand("update-ca-certificates");
                    }

                    // Tune Linux for SSDs, if enabled.

                    node.Status = "run: setup-ssd.sh";
                    node.SudoCommand("setup-ssd.sh");
                });
        }

        /// <summary>
        /// Reboots the nodes and waits until the package manager is ready.
        /// </summary>
        /// <param name="node">The cluster node.</param>
        private void RebootAndWait(SshProxy<NodeDefinition> node)
        {
            node.Status = "rebooting...";
            node.Reboot(wait: true);

            CommonSteps.WaitForPackageManager(node);
        }

        /// <summary>
        /// Generates and uploads the <b>/etc/hosts</b> file for a node.
        /// </summary>
        /// <param name="node">The target node.</param>
        private void UploadHostsFile(SshProxy<NodeDefinition> node)
        {
            var sbHosts = new StringBuilder();

            sbHosts.Append(
$@"
127.0.0.1	    localhost
127.0.1.1	    {node.Name}
::1             localhost ip6-localhost ip6-loopback
ff02::1         ip6-allnodes
ff02::2         ip6-allrouters
");
            node.UploadText("/etc/hosts", sbHosts.ToString(), 4, Encoding.UTF8);
        }

        /// <summary>
        /// Generates and uploads the <b>/etc/neoncluster/env-host</b> file for a node.
        /// </summary>
        /// <param name="node">The target node.</param>
        private void UploadHostEnvFile(SshProxy<NodeDefinition> node)
        {
            var sbEnvHost       = new StringBuilder();
            var vaultDirectLine = string.Empty;

            if (node.Metadata.IsManager)
            {
                vaultDirectLine = $"export VAULT_DIRECT_ADDR={cluster.Definition.Vault.GetDirectUri(node.Name)}";
            }

            if (!node.Metadata.IsPet)
            {
                // Upload the full [/etc/neoncluster/env-host] file for Docker Swarm nodes.

                sbEnvHost.AppendLine(
$@"#------------------------------------------------------------------------------
# FILE:         /etc/neoncluster/env-host
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# This script can be mounted into containers that required extended knowledge
# about the cluster and host node.  This will be mounted to [/etc/neoncluster/env-host]
# such that the container entrypoint script can execute it.

# Define the cluster and Docker host related environment variables.

export NEON_CLUSTER={cluster.Definition.Name}
export NEON_DATACENTER={cluster.Definition.Datacenter}
export NEON_ENVIRONMENT={cluster.Definition.Environment}
export NEON_HOSTING={cluster.Definition.Hosting.Environment.ToString().ToLowerInvariant()}
export NEON_NODE_NAME={node.Name}
export NEON_NODE_ROLE={node.Metadata.Role}
export NEON_NODE_IP={node.Metadata.PrivateAddress}
export NEON_NODE_SSD={node.Metadata.Labels.StorageSSD.ToString().ToLowerInvariant()}
export NEON_APT_PROXY={NeonClusterHelper.GetPackageProxyReferences(cluster.Definition)}

export VAULT_ADDR={cluster.Definition.Vault.Uri}
{vaultDirectLine}
export CONSUL_HTTP_ADDR={NeonHosts.Consul}:{cluster.Definition.Consul.Port}
export CONSUL_HTTP_FULLADDR=http://{NeonHosts.Consul}:{cluster.Definition.Consul.Port}
");
            }
            else
            {
                // Upload a more limited [/etc/neoncluster/env-host] file for external nodes.

                sbEnvHost.AppendLine(
$@"#------------------------------------------------------------------------------
# FILE:         /etc/neoncluster/env-host
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# This script can be mounted into containers that required extended knowledge
# about the cluster and host node.  This will be mounted to [/etc/neoncluster/env-host]
# such that the container entrypoint script can execute it.

# Define the cluster and Docker host related environment variables.

export NEON_CLUSTER={cluster.Definition.Name}
export NEON_DATACENTER={cluster.Definition.Datacenter}
export NEON_ENVIRONMENT={cluster.Definition.Environment}
export NEON_HOSTING={cluster.Definition.Hosting.Environment.ToString().ToLowerInvariant()}
export NEON_NODE_NAME={node.Name}
export NEON_NODE_ROLE={node.Metadata.Role}
export NEON_NODE_IP={node.Metadata.PrivateAddress}
export NEON_NODE_SSD={node.Metadata.Labels.StorageSSD.ToString().ToLowerInvariant()}
export NEON_APT_PROXY={NeonClusterHelper.GetPackageProxyReferences(cluster.Definition)}
");
            }

            node.UploadText($"{NodeHostFolders.Config}/env-host", sbEnvHost.ToString(), 4, Encoding.UTF8);
        }

        /// <summary>
        /// Generates the Consul configuration file for a cluster node.
        /// </summary>
        /// <param name="node">The target cluster node.</param>
        /// <returns>The configuration file text.</returns>
        private string GetConsulConfig(SshProxy<NodeDefinition> node)
        {
            var consulTlsDisabled = true;   // $todo(jeff.lill): Remove this once we support Consul TLS.
            var consulDef         = node.Cluster.Definition.Consul;
            var consulConf        = new JObject();

            consulConf.Add("log_level", "info");
            consulConf.Add("datacenter", cluster.Definition.Datacenter);
            consulConf.Add("node_name", node.Name);
            consulConf.Add("data_dir", "/mnt-data/consul");
            consulConf.Add("advertise_addr", node.Metadata.PrivateAddress.ToString());
            consulConf.Add("client_addr", "0.0.0.0");

            var ports = new JObject();

            ports.Add("http", consulTlsDisabled ? 8500 : -1);
            ports.Add("https", consulTlsDisabled ? -1 : 8500);
            ports.Add("dns", 8600);     // This is the default Consul DNS port.

            consulConf.Add("ports", ports);

            if (!consulTlsDisabled)
            {
                consulConf.Add("cert_file", "/etc/consul.d/consul.crt");
                consulConf.Add("key_file", "/etc/consul.d/consul.key");
            }

            consulConf.Add("ui", true);
            consulConf.Add("leave_on_terminate", false);
            consulConf.Add("skip_leave_on_interrupt", true);
            consulConf.Add("disable_remote_exec", true);
            consulConf.Add("domain", "cluster");

            var recursors = new JArray();

            foreach (var nameserver in cluster.Definition.Network.Nameservers)
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
                consulConf.Add("bootstrap_expect", cluster.Definition.Managers.Count());

                var performance = new JObject();

                performance.Add("raft_multiplier", 1);

                consulConf.Add("performance", performance);
            }
            else
            {
                var managerAddresses = new JArray();

                foreach (var manager in cluster.Managers)
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

            settings.Add("log-level", "info");
            settings.Add("log-driver", "fluentd");
            settings.Add("experimental", cluster.Definition.Docker.Experimental);

            var logOptions = new JObject();

            logOptions.Add("tag", "");
            logOptions.Add("fluentd-async-connect", "true");

            // The default Docker behavior is to attempt to connect to Fluentd
            // when Docker starts a maximum of 10 times at 1 second intervals
            // before permanantly giving up.  I've see this unfortunate behavior
            // happen after a cluster reboot.  The solution is to set the number
            // retries to a very large number (1B * 1s > 11K years).

            logOptions.Add("fluentd-max-retries", "1000000000");

            // Be default, the Fluentd log driver buffers container stdout to
            // RAM up to the container's limit.  This could cause RAM to seriously
            // bloat if the log pipeline stalls.  We're going to have Docker limit
            // containers to 5MB RAM buffer before logging to disk.

            logOptions.Add("fluentd-buffer-limit", $"{5 * NeonHelper.Mega}");

            settings.Add("log-opts", logOptions);

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

            // Specify any registry caches followed by the authoritative
            // external registry.

            var registries = new JArray();

            if (!cluster.Definition.BareDocker && cluster.Definition.Docker.RegistryCache)
            {
                foreach (var manager in cluster.Definition.SortedManagers)
                {
                    registries.Add($"https://{manager.Name}.{NeonHosts.RegistryCache}:{NeonHostPorts.DockerRegistryCache}");
                }
            }

            registries.Add(cluster.Definition.Docker.Registry);

            settings.Add("registry-mirrors", registries);

            return NeonHelper.JsonSerialize(settings, Formatting.Indented);
        }

        /// <summary>
        /// Configures the required VPN return routes for on-premise clusters 
        /// that enable VPN.
        /// </summary>
        /// <param name="node">The target cluster node.</param>
        private void ConfigureVpnPoolRoutes(SshProxy<NodeDefinition> node)
        {
            if (!cluster.Definition.Vpn.Enabled)
            {
                // Note that cloud deployments handle VPN return routing via routing tables setup
                // during cluster preparation.

                return;
            }

            node.Status = "vpn return routes";

            // Since cluster VPN traffic is coming throught the manager nodes and
            // isn't coming through the cluster router directly, we need to setup
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

            foreach (var manager in cluster.Managers.Where(m => m != node))
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
        /// <param name="node">The target cluster node.</param>
        private void ConfigureManager(SshProxy<NodeDefinition> node)
        {
            node.InvokeIdempotentAction("setup-manager",
                () =>
                {
                    // Configure the APT package proxy on the managers
                    // and configure the proxy selector for all nodes.

                    node.Status = "run: setup-apt-proxy.sh";
                    node.SudoCommand("setup-apt-proxy.sh");

                    // Upgrade Linux packages if requested.  We're doing this after
                    // deploying the APT package proxy so it'll be faster.

                    switch (cluster.Definition.HostNode.Upgrade)
                    {
                        case OsUpgrade.Partial:

                            node.Status = "package upgrade (partial)";

                            node.SudoCommand("apt-get upgrade -yq --allow-unauthenticated");
                            break;

                        case OsUpgrade.Full:

                            node.Status = "package upgrade (full)";

                            node.SudoCommand("apt-get dist-upgrade -yq --allow-unauthenticated");
                            break;
                    }

                    // Setup NTP.

                    node.Status = "run: setup-ntp.sh";
                    node.SudoCommand("setup-ntp.sh");

                    // Configure the VPN return routes.

                    ConfigureVpnPoolRoutes(node);

                    // Setup the Consul server and join it to the cluster.

                    node.Status = "upload: consul.json";
                    node.SudoCommand("mkdir -p /etc/consul.d");
                    node.SudoCommand("chmod 770 /etc/consul.d");
                    node.UploadText("/etc/consul.d/consul.json", GetConsulConfig(node));

                    node.Status = "run: setup-consul-server.sh";
                    node.SudoCommand("setup-consul-server.sh", cluster.Definition.Consul.EncryptionKey);

                    if (!cluster.Definition.BareDocker)
                    {
                        // Bootstrap Consul cluster discovery.

                        node.InvokeIdempotentAction("setup-consul-bootstrap",
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
                    node.SudoCommand("setup-docker.sh");

                    if (!string.IsNullOrEmpty(cluster.Definition.Docker.RegistryUsername))
                    {
                        // We need to log into the registry and/or cache.

                        node.Status = "docker login";

                        var loginCommand = new CommandBundle("./docker-login.sh");

                        loginCommand.AddFile("docker-login.sh",
$@"docker login \
-u ""{cluster.Definition.Docker.RegistryUsername}"" \
-p ""{cluster.Definition.Docker.RegistryPassword}"" \
{cluster.Definition.Docker.Registry}",
                            isExecutable: true);

                        node.SudoCommand(loginCommand);
                    }

                    // Clean up any cached APT files.

                    node.Status = "clean up";
                    node.SudoCommand("apt-get clean -yq");
                    node.SudoCommand("rm -rf /var/lib/apt/lists");
                });
        }

        /// <summary>
        /// Creates the initial swarm on the bootstrap manager node passed and 
        /// captures the manager and worker swarm tokens required to join additional
        /// nodes to the cluster.
        /// </summary>
        /// <param name="bootstrapManager">The target bootstrap manager server.</param>
        private void CreateSwarm(SshProxy<NodeDefinition> bootstrapManager)
        {
            if (clusterLogin.SwarmManagerToken != null && clusterLogin.SwarmWorkerToken != null)
            {
                return; // Swarm has already been created.
            }

            bootstrapManager.Status = "create swarm";
            bootstrapManager.DockerCommand(RunOptions.FaultOnError, $"docker swarm init --advertise-addr {bootstrapManager.Metadata.PrivateAddress}:{cluster.Definition.Docker.SwarmPort}");

            var response = bootstrapManager.DockerCommand(RunOptions.FaultOnError, $"docker swarm join-token manager");

            clusterLogin.SwarmManagerToken = ExtractSwarmToken(response.OutputText);

            response = bootstrapManager.DockerCommand(RunOptions.FaultOnError, $"docker swarm join-token worker");

            clusterLogin.SwarmWorkerToken = ExtractSwarmToken(response.OutputText);

            // Persist the swarm tokens into the cluster login.

            clusterLogin.Save();
        }

        /// <summary>
        /// Extracts the Swarm token from a <b>docker swarm join-token [manager|worker]</b> 
        /// command.  The token returned can be used when adding additional nodes to the cluster.
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
                throw new NeonClusterException(errorMsg);
            }

            if (startPos == -1)
            {
                throw new NeonClusterException(errorMsg);
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
                throw new NeonClusterException($"Cannot extract swarm token from:\r\n\r\n{commandResponse}");
            }

            return commandResponse.Substring(startPos, endPos - startPos).Trim();
        }

        /// <summary>
        /// Configures non-manager nodes like workers or individuals.
        /// </summary>
        /// <param name="node">The target cluster node.</param>
        private void ConfigureNonManager(SshProxy<NodeDefinition> node)
        {
            node.InvokeIdempotentAction($"setup-{node.Metadata.Role}",
                () =>
                {
                    // Configure the APT package proxy on the managers
                    // and configure the proxy selector for all nodes.

                    node.Status = "run: setup-apt-proxy.sh";
                    node.SudoCommand("setup-apt-proxy.sh");

                    // Upgrade Linux packages if requested.  We're doing this after
                    // deploying the APT package proxy so it'll be faster.

                    switch (cluster.Definition.HostNode.Upgrade)
                    {
                        case OsUpgrade.Partial:

                            node.Status = "package upgrade (partial)";

                            node.SudoCommand("apt-get upgrade -yq --allow-unauthenticated");
                            break;

                        case OsUpgrade.Full:

                            node.Status = "package upgrade (full)";

                            node.SudoCommand("apt-get dist-upgrade -yq --allow-unauthenticated");
                            break;
                    }

                    // Setup NTP.

                    node.Status = "run: setup-ntp.sh";
                    node.SudoCommand("setup-ntp.sh");

                    // Configure the VPN return routes.

                    ConfigureVpnPoolRoutes(node);

                    if (!cluster.Definition.BareDocker)
                    {
                        // Setup the Consul proxy and join it to the cluster.

                        node.Status = "upload: consul.json";
                        node.SudoCommand("mkdir -p /etc/consul.d");
                        node.SudoCommand("chmod 770 /etc/consul.d");
                        node.UploadText("/etc/consul.d/consul.json", GetConsulConfig(node));

                        node.Status = "run: setup-consul-proxy.sh";
                        node.SudoCommand("setup-consul-proxy.sh", cluster.Definition.Consul.EncryptionKey);

                        // Join this node's Consul agent with the master(s).

                        node.InvokeIdempotentAction("setup-consul-join",
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
                    node.SudoCommand("setup-docker.sh");

                    if (!string.IsNullOrEmpty(cluster.Definition.Docker.RegistryUsername))
                    {
                        // We need to log into the registry and/or cache.

                        node.Status = "docker login";

                        var loginCommand = new CommandBundle("./docker-login.sh");

                        loginCommand.AddFile("docker-login.sh",
$@"docker login \
-u ""{cluster.Definition.Docker.RegistryUsername}"" \
-p ""{cluster.Definition.Docker.RegistryPassword}"" \
{cluster.Definition.Docker.Registry}", 
                            isExecutable: true);

                        node.SudoCommand(loginCommand);
                    }

                    if (!cluster.Definition.BareDocker)
                    {
                        // Configure Vault client.

                        node.Status = "run: setup-vault-client.sh";
                        node.SudoCommand("setup-vault-client.sh");
                    }

                    // Clean up any cached APT files.

                    node.Status = "clean up";
                    node.SudoCommand("apt-get clean -yq");
                    node.SudoCommand("rm -rf /var/lib/apt/lists");
                });
        }

        /// <summary>
        /// Creates the standard cluster overlay networks.
        /// </summary>
        /// <param name="manager">The manager node.</param>
        private void CreateClusterNetworks(SshProxy<NodeDefinition> manager)
        {
            // $todo(jeff.lill):
            //
            // Enable the network encryption when Docker networking seems
            // to stablize.  Here's the tracking issue:
            //
            //      https://github.com/jefflill/NeonForge/issues/102

            manager.InvokeIdempotentAction("setup-docker-networks",
                () =>
                {
                    manager.DockerCommand(
                        "docker network create",
                            "--driver", "overlay",
                            "--opt", "encrypt",
                            "--subnet", cluster.Definition.Network.PublicSubnet,
                            cluster.Definition.Network.PublicAttachable ? "--attachable" : null,
                            NeonClusterConst.PublicNetwork);

                    manager.DockerCommand(
                        "docker network create",
                            "--driver", "overlay",
                            "--opt", "encrypt",
                            "--subnet", cluster.Definition.Network.PrivateSubnet,
                            cluster.Definition.Network.PrivateAttachable ? "--attachable" : null,
                            NeonClusterConst.PrivateNetwork);
                });
        }

        /// <summary>
        /// Adds the node labels.
        /// </summary>
        /// <param name="manager">The manager node.</param>
        private void AddNodeLabels(SshProxy<NodeDefinition> manager)
        {
            manager.InvokeIdempotentAction("setup-node-labels",
                () =>
                {
                    foreach (var node in cluster.Nodes.Where(n => n.Metadata.InSwarm))
                    {
                        var labelDefinitions = new List<string>();

                        labelDefinitions.Add($"{NodeLabels.LabelDatacenter}={cluster.Definition.Datacenter.ToLowerInvariant()}");
                        labelDefinitions.Add($"{NodeLabels.LabelEnvironment}={cluster.Definition.Environment.ToString().ToLowerInvariant()}");

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

                        foreach (var labelDefinition in labelDefinitions)
                        {
                            // We occasionaly see "out of order" errors from labeling operations.
                            // These seem to be transient, so we're going to retry a few times
                            // before actually giving up.

                            var retry = new LinearRetryPolicy(e => e is NeonClusterException, retryInterval: TimeSpan.FromSeconds(2));

                            retry.InvokeAsync(
                                async () =>
                                {
                                    var response = manager.DockerCommand(RunOptions.Defaults & ~RunOptions.FaultOnError, "docker node update --label-add", labelDefinition, node.Name);

                                    if (response.ExitCode != 0)
                                    {
                                        throw new NeonClusterException(response.ErrorSummary);
                                    }

                                    await Task.CompletedTask;

                                }).Wait();
                        }

                        node.Status = "labeling";
                    }
                });
        }

        /// <summary>
        /// Pulls common images to the node.
        /// </summary>
        /// <param name="node">The target cluster node.</param>
        /// <param name="pullAll">
        /// Optionally specifies that all cluster images should be pulled to the
        /// node regardless of the node properties.  This is used to pull images
        /// into the cache.
        /// </param>
        private void PullImages(SshProxy<NodeDefinition> node, bool pullAll = false)
        {
            node.InvokeIdempotentAction("setup-pull-images",
                () =>
                {
                    var images = new List<string>()
                    {
                        Program.ResolveDockerImage("neoncluster/ubuntu-16.04"),
                        Program.ResolveDockerImage("neoncluster/ubuntu-16.04-dotnet"),
                        Program.ResolveDockerImage(cluster.Definition.ProxyImage),
                        Program.ResolveDockerImage(cluster.Definition.ProxyVaultImage)
                    };

                    if (node.Metadata.IsManager)
                    {
                        images.Add(Program.ResolveDockerImage(cluster.Definition.ClusterManagerImage));
                        images.Add(Program.ResolveDockerImage(cluster.Definition.ProxyManagerImage));
                        images.Add(Program.ResolveDockerImage(cluster.Definition.DnsImage));
                        images.Add(Program.ResolveDockerImage(cluster.Definition.DnsMonImage));
                    }

                    if (cluster.Definition.Log.Enabled)
                    {
                        // All nodes pull these images:

                        images.Add(Program.ResolveDockerImage(cluster.Definition.Log.HostImage));
                        images.Add(Program.ResolveDockerImage(cluster.Definition.Log.MetricbeatImage));

                        // [neon-log-collector] only runs on managers.

                        if (pullAll || node.Metadata.IsManager)
                        {
                            images.Add(Program.ResolveDockerImage(cluster.Definition.Log.CollectorImage));
                        }

                        // [elasticsearch] only runs on designated nodes.

                        if (pullAll || node.Metadata.Labels.LogEsData)
                        {
                            images.Add(Program.ResolveDockerImage(cluster.Definition.Log.EsImage));
                        }
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
        /// <param name="node">The target cluster node.</param>
        private void JoinSwarm(SshProxy<NodeDefinition> node)
        {
            if (node == cluster.FirstManager)
            {
                // This node is implictly joined to the cluster.

                node.Status = "joined";
                return;
            }

            node.InvokeIdempotentAction("setup-swarm-join",
                () =>
                {
                    node.Status = "joining";

                    if (node.Metadata.IsManager)
                    {
                        node.DockerCommand(RunOptions.Redact, $"docker swarm join --token {clusterLogin.SwarmManagerToken} {cluster.FirstManager.Metadata.PrivateAddress}:2377");
                    }
                    else
                    {
                        // Must be a worker node.

                        node.DockerCommand(RunOptions.Redact, $"docker swarm join --token {clusterLogin.SwarmWorkerToken} {cluster.FirstManager.Metadata.PrivateAddress}:2377");
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
@"# These settings configure the service to restart on failure after
# waiting 30 seconds for up to a 365 days (effectively forever).
# [StartLimitInterval] is set to the number of minutes in a year
# and [StartLimitBurst] is set to the number of 30 second intervals
# in [StartLimitInterval].

Restart=on-failure
RestartSec=30
StartLimitInterval=525600min
StartLimitBurst=1051200";
            }
        }

        /// <summary>
        /// Installs the required Ceph Storage Cluster packages on the node without
        /// configuring them.
        /// </summary>
        /// <param name="node">The target cluster node.</param>
        private void CephPackages (SshProxy<NodeDefinition> node)
        {
            if (!cluster.Definition.Ceph.Enabled)
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
            // $todo(jeff.lill): We're currently ignoring the Ceph version number.

            node.InvokeIdempotentAction("setup-ceph-packages",
                () =>
                {
                    node.Status = "ceph package install";

                    // Extract the Ceph release and version from the configuration.
                    // Note that the version is optional and is currently ignored.

                    var parts       = cluster.Definition.Ceph.Version.Split('/');
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
                    node.SudoCommand($"apt-get update");
                    node.SudoCommand($"apt-get install -yq ceph");

                    // We also need need support for extended file system attributes
                    // so we can set the maximum size in bytes and/or maximum number
                    // files in a directory via [setfattr] and [getfattr].

                    node.SudoCommand($"apt-get install -yq attr");

                    //---------------------------------------------------------
                    // The default Ceph service systemd unit files don't try very
                    // hard to restart after failures so we're going to overwrite 
                    // them with our own versions.

                    string unitPath;

                    // Ceph-MDS

                    unitPath = "/lib/systemd/system/ceph-mds@.service";

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

                    unitPath = "/lib/systemd/system/ceph-mgr@.service";

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

                    unitPath = "/lib/systemd/system/ceph-mgrs@.service";

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

                    unitPath = "/lib/systemd/system/ceph-mon@.service";

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

                    unitPath = "/lib/systemd/system/ceph-osd@.service";

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

            // Download and install the [neon-volume-plugin].

            node.InvokeIdempotentAction("setup-neon-volume-plugin",
                () =>
                {
                    node.Status = "neon-volume-plugin install";

                    var installCommand = new CommandBundle("./install.sh");

                    installCommand.AddFile("install.sh",
$@"# Download and install the plugin.

curl -4fsSLv --retry 10 --retry-delay 30 {cluster.Definition.Ceph.VolumePluginPackage} -o /tmp/neon-volume-plugin-deb 1>&2
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
        }

        /// <summary>
        /// Generates the Ceph related configuration settings as a global step.  This
        /// assumes that <see cref="CephPackages"/> has already been completed.
        /// </summary>
        private void CephSettings()
        {
            if (!cluster.Definition.Ceph.Enabled)
            {
                return;
            }

            if (clusterLogin.Ceph != null)
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

            var manager          = cluster.FirstManager;
            var cephConfig       = new CephConfig();
            var tmpPath          = "/tmp/ceph";
            var monKeyringPath   = LinuxPath.Combine(tmpPath, "mon.keyring");
            var adminKeyringPath = LinuxPath.Combine(tmpPath, "admin.keyring");
            var osdKeyringPath   = LinuxPath.Combine(tmpPath, "osd.keyring");
            var monMapPath       = LinuxPath.Combine(tmpPath, "monmap");
            var runOptions       = RunOptions.Defaults | RunOptions.FaultOnError;

            clusterLogin.Ceph = cephConfig;

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

            foreach (var monNode in cluster.Nodes.Where(n => n.Metadata.Labels.CephMON))
            {
                sbAddOptions.AppendWithSeparator($"--add {monNode.Name} {monNode.PrivateAddress}");
            }

            manager.SudoCommand($"monmaptool --create {sbAddOptions} --fsid {cephConfig.Fsid} {monMapPath}", runOptions);
            manager.SudoCommand($"chmod 666 {monMapPath}", runOptions);

            cephConfig.MonitorMap = manager.DownloadBytes(monMapPath);

            // Make sure we've deleted any temporary files.

            manager.SudoCommand($"rm -r {tmpPath}", runOptions);

            // Persist the cluster login so we'll have the keyrings and monmap
            // in case we have to restart setup.

            clusterLogin.Save();
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

            foreach (var monitorNode in cluster.Definition.SortedNodes.Where(n => n.Labels.CephMON))
            {
                sbHostNames.AppendWithSeparator(monitorNode.Name, ", ");
                sbHostAddresses.AppendWithSeparator(monitorNode.PrivateAddress, ", ");
            }

            var clusterSubnet =
                HostingManager.IsCloudEnvironment(cluster.Definition.Hosting.Environment)
                    ? cluster.Definition.Network.CloudSubnet
                    : cluster.Definition.Network.PremiseSubnet;

            node.SudoCommand("mkdir -p /etc/ceph");

            var mdsConf = string.Empty;

            if (node.Metadata.Labels.CephMDS)
            {
                mdsConf =
$@"
[mds.{node.Name}]
host = {node.Name}
mds cache memory limit = {(int)(node.Metadata.GetCephMDSCacheSize(cluster.Definition) * CephOptions.CacheSizeFudge)}
mds_standby_replay = true
";
            }

            node.UploadText("/etc/ceph/ceph.conf",
$@"[global]
fsid = {clusterLogin.Ceph.Fsid}
mon initial members = {sbHostNames}
mon host = {sbHostAddresses}
mon health preluminous compat warning = false
public network = {clusterSubnet}
auth cluster required = cephx
auth service required = cephx
auth client required = cephx
osd journal size = {ClusterDefinition.ValidateSize(cluster.Definition.Ceph.OSDJournalSize, cluster.Definition.Ceph.GetType(), nameof(cluster.Definition.Ceph.OSDJournalSize)) / NeonHelper.Mega}
osd pool default size = {cluster.Definition.Ceph.OSDReplicaCount}
osd pool default min size = {cluster.Definition.Ceph.OSDReplicaCountMin}
osd pool default pg num = {cluster.Definition.Ceph.OSDPlacementGroups}
osd pool default pgp num = {cluster.Definition.Ceph.OSDPlacementGroups}
osd crush chooseleaf type = 1
bluestore_cache_size = {(int)(node.Metadata.GetCephOSDCacheSize(cluster.Definition) * CephOptions.CacheSizeFudge) / NeonHelper.Mega}
{mdsConf}
");
            if (!configOnly)
            {
                var cephUser         = cluster.Definition.Ceph.Username;
                var adminKeyringPath = "/etc/ceph/ceph.client.admin.keyring";

                node.UploadText(adminKeyringPath, clusterLogin.Ceph.AdminKeyring);
                node.SudoCommand($"chown {cephUser}:{cephUser} {adminKeyringPath}");
                node.SudoCommand($"chown 640 {adminKeyringPath}");
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
            var result = node.SudoCommand($"ceph auth get-or-create mds.{node.Name} mds \"allow \" osd \"allow *\" mon \"allow rwx\"");

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
        /// Bootstraps the Ceph monitor/manager nodes.
        /// </summary>
        /// <param name="node">The target node.</param>
        private void CephBootstrap(SshProxy<NodeDefinition> node)
        {
            node.InvokeIdempotentAction("setup-ceph-bootstrap",
                () =>
                {
                    var cephUser = cluster.Definition.Ceph.Username;
                    var tempPath = "/tmp/ceph";

                    // Create a temporary folder.

                    node.SudoCommand($"mkdir -p {tempPath}");

                    // Upload the Ceph config file here because we'll need it below.

                    UploadCephConf(node, configOnly: true);

                    // Create the monitor data directory and load the monitor map and keyring.

                    node.Status = "ceph-mon config";

                    var monFolder = $"/var/lib/ceph/mon/{clusterLogin.Ceph.Name}-{node.Name}";
                    
                    node.SudoCommand($"mkdir -p {monFolder}");
                    node.SudoCommand($"chown {cephUser}:{cephUser} {monFolder}");
                    node.SudoCommand($"chmod 770 {monFolder}");

                    var monitorMapPath     = LinuxPath.Combine(tempPath, "monmap");
                    var monitorKeyringPath = LinuxPath.Combine(tempPath, "ceph.mon.keyring");

                    node.UploadBytes(monitorMapPath, clusterLogin.Ceph.MonitorMap);
                    node.UploadText(monitorKeyringPath, clusterLogin.Ceph.MonitorKeyring);
                    node.SudoCommandAsUser(cephUser, $"ceph-mon --mkfs -i {node.Name} --monmap {monitorMapPath} --keyring {monitorKeyringPath}");

                    // Upload the client admin keyring.

                    var adminKeyringPath = "/etc/ceph/ceph.client.admin.keyring";

                    node.UploadText(adminKeyringPath, clusterLogin.Ceph.AdminKeyring);
                    node.SudoCommand($"chown {cephUser}:{cephUser} {adminKeyringPath}");
                    node.SudoCommand($"chown 640 {adminKeyringPath}");

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
                    node.SudoCommand($"chown 640 {mgrKeyringPath}");
                    node.Status = "ceph-mgr start";
                    node.SudoCommand($"systemctl enable ceph-mgr@{node.Name}");
                    node.SudoCommand($"systemctl start ceph-mgr@{node.Name}");

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
            /// Indicates that the neonCLUSTER includes a Caph cluster.
            /// </summary>
            public bool IsEnabled { get; set; }

            /// <summary>
            /// Indicates that the cluster is healthy.
            /// </summary>
            public bool IsHealthy { get; set; }

            /// <summary>
            /// The number of Monitor servers configured for the cluster.
            /// </summary>
            public int MONCount { get; set; }

            /// <summary>
            /// The number of OSD servers configured for the cluster.
            /// </summary>
            public int OSDCount { get; set; }

            /// <summary>
            /// The current number of active OSD servers.
            /// </summary>
            public int OSDActiveCount { get; set; }

            /// <summary>
            /// The number of MDS servers configured for the cluster.
            /// </summary>
            public int MDSCount { get; set; }

            /// <summary>
            /// The number of active MDS servers configured for the cluster.
            /// </summary>
            public int MDSActiveCount { get; set; }

            /// <summary>
            /// Indicates that the <b>cfs</b> file system is ready.
            /// </summary>
            public bool IsCfsReady { get; set; }
        }

        /// <summary>
        /// Queries a node for the current Ceph cluster status.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <returns>The <cref cref="CephClusterStatus"/>.</returns>
        private CephClusterStatus GetCephClusterStatus(SshProxy<NodeDefinition> node)
        {
            var status = new CephClusterStatus();

            if (!cluster.Definition.Ceph.Enabled)
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
            // Detect if the [cfs] file system is ready.  There's probably a
            // way to detect this from the JSON status above but I need to
            // move on to other things.  This is fragile because it assumes
            // that there's only one file system deployed.

            result = node.SudoCommand("ceph mds stat");

            if (result.ExitCode == 0)
            {
                status.IsCfsReady = result.OutputText.StartsWith("cfs-") &&
                                    result.OutputText.Contains("up:active");
            }

            return status;
        }

        /// <summary>
        /// Configures the Ceph cluster by configuring and starting the OSD and MDS 
        /// services and then creating and mounting a CephFS file system.
        /// </summary>
        /// <param name="node">The target node.</param>
        private void CephCluster(SshProxy<NodeDefinition> node)
        {
            node.InvokeIdempotentAction("setup-ceph-osd",
                () =>
                {
                    node.Status = "ceph-osd config";

                    var cephUser = cluster.Definition.Ceph.Username;

                    // All nodes need the config file.

                    UploadCephConf(node);

                    // Configure OSD if enabled for this node.

                    if (node.Metadata.Labels.CephOSD)
                    {
                        node.Status = "ceph-osd start";
                        node.UploadText("/var/lib/ceph/bootstrap-osd/ceph.keyring", clusterLogin.Ceph.OSDKeyring);
                        node.SudoCommand($"ceph-volume lvm create --bluestore --data {node.Metadata.Labels.CephOSDDevice}");
                        node.Status = string.Empty;
                    }
                });

            // Wait for the cluster OSD services to come up.  We're going to have
            // all nodes wait but only the first manager will report status to
            // the UX.

            var osdCount = cluster.Definition.Nodes.Count(n => n.Labels.CephOSD);

            if (node == cluster.FirstManager)
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

                        if (node == cluster.FirstManager)
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
                node.InvokeIdempotentAction("setup-ceph-mds",
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

            // Wait for the cluster MDS services to come up.  We're going to have
            // all nodes wait but only the first manager will report status to
            // the UX.

            var mdsCount = cluster.Definition.Nodes.Count(n => n.Labels.CephMDS);

            if (node == cluster.FirstManager)
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

                        if (node == cluster.FirstManager)
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

            // We're going to have the first manager create the [cfs_data] and [cfs_metadata] storage 
            // pools and then the [cfs] filesystem on top of those pools.  Then we'll have the first
            // manager wait for the filesystem to be created.

            if (node == cluster.FirstManager)
            {
                node.InvokeIdempotentAction("setup-ceph-fs",
                    () =>
                    {
                        node.Status = "create file system";
                        node.SudoCommand($"ceph osd pool create cfs_data {cluster.Definition.Ceph.OSDPlacementGroups}");
                        node.SudoCommand($"ceph osd pool create cfs_metadata {cluster.Definition.Ceph.OSDPlacementGroups}");
                        node.SudoCommand($"ceph fs new cfs cfs_metadata cfs_data");
                    });
            }

            // Wait for the file system to start.

            try
            {
                NeonHelper.WaitFor(
                    () =>
                    {
                        return GetCephClusterStatus(node).IsCfsReady;
                    },
                    timeout: TimeSpan.FromSeconds(120),
                    pollTime: TimeSpan.FromSeconds(2));
            }
            catch (TimeoutException)
            {
                node.Fault("Timeout waiting for Ceph file system.");
            }

            // We're going to use the FUSE client to mount the file system at [/cfs].

            node.InvokeIdempotentAction("setup-ceph-mount",
                () =>
                {
                    var monNode = cluster.Definition.SortedNodes.First(n => n.Labels.CephMON);

                    node.Status = "mount file system";
                    node.SudoCommand($"mkdir -p /cfs");
                    node.SudoCommand($"ceph-fuse -m {monNode.PrivateAddress}:6789 /cfs");
                });

            // $hack(jeff.lill):
            //
            // I couldn't enable the built-in [ceph-fuse@/*.service] to have
            // [/cfs] mount on reboot via:
            //
            //      systemctl enable ceph-fuse@/cfs.service
            //
            // I was seeing an "Invalid argument" error.  I'm going to workaround
            // this by creating and enabling my own service.

            node.InvokeIdempotentAction("setup-ceph-fuse-service",
                () =>
                {
                    node.Status = "create fuse service";
                    node.UploadText("/lib/systemd/system/ceph-fuse-cfs.service",
$@"[Unit]
Description=Ceph FUSE client (for /cfs)
After=network-online.target local-fs.target time-sync.target
Wants=network-online.target local-fs.target time-sync.target
Conflicts=umount.target
PartOf=ceph-fuse.target

[Service]
EnvironmentFile=-/etc/default/ceph
Environment=CLUSTER=ceph
ExecStart=/usr/bin/ceph-fuse -f --cluster ${{CLUSTER}} /cfs
TasksMax=infinity

{UnitRestartSettings}

[Install]
WantedBy=ceph-fuse.target
WantedBy=docker.service
");
                    node.SudoCommand("chmod 644 /lib/systemd/system/ceph-fuse-cfs.service");
                    node.SudoCommand($"systemctl enable ceph-fuse-cfs.service");
                });

            if (node == cluster.FirstManager)
            {
                node.InvokeIdempotentAction("setup-ceph-fs-init",
                    () =>
                    {
                        // Initialize [/cfs]:
                        //
                        //      /cfs/READY  - Read-only file whose presence indicates that the file system is mounted
                        //      /cfs/docker - Holds mapped Docker volumes
                        //      /cfs/neon   - Reserved for neonCLUSTER

                        node.Status = "populate file system";
                        node.SudoCommand($"touch /cfs/READY && chmod 444 /cfs/READY");
                        node.SudoCommand($"mkdir -p /cfs/docker && chown root:root /cfs/docker && chmod 770 /cfs/docker");
                        node.SudoCommand($"mkdir -p /cfs/neon && chown root:root /cfs/neon && chmod 770 /cfs/neon");
                    });
            }
        }

        /// <summary> 
        /// Configures the Vault load balancer service: <b>neon-proxy-vault</b>.
        /// </summary>
        private void VaultProxy()
        {
            // Create the comma separated list of Vault manager endpoints formatted as:
            //
            //      NODE:IP:PORT

            var sbEndpoints = new StringBuilder();

            foreach (var manager in cluster.Definition.SortedManagers)
            {
                sbEndpoints.AppendWithSeparator($"{manager.Name}:{manager.PrivateAddress}:{NetworkPorts.Vault}", ",");
            }

            cluster.FirstManager.InvokeIdempotentAction("setup-proxy-vault",
                () =>
                {
                    // Docker mesh routing seemed unstable on versions 17.03.0-ce
                    // thru 17.06.0-ce so we're going to provide an option to work
                    // around this by running the PUBLIC, PRIVATE and VAULT proxies 
                    // on all nodes and  publishing the ports to the host (not the mesh).
                    //
                    //      https://github.com/jefflill/NeonForge/issues/104
                    //
                    // Note that this mode feature is documented (somewhat poorly) here:
                    //
                    //      https://docs.docker.com/engine/swarm/services/#publish-ports

                    var options = new List<string>();

                    if (cluster.Definition.Docker.AvoidIngressNetwork)
                    {
                        options.Add("--publish");
                        options.Add($"mode=host,published={NeonHostPorts.ProxyVault},target={NetworkPorts.Vault}");
                    }
                    else
                    {
                        options.Add("--constraint");
                        options.Add($"node.role==manager");

                        options.Add("--publish");
                        options.Add($"{NeonHostPorts.ProxyVault}:{NetworkPorts.Vault}");
                    }

                    // Deploy [neon-proxy-vault].

                    var steps   = new ConfigStepList();
                    var command = CommandStep.CreateIdempotentDocker(cluster.FirstManager.Name, "setup-neon-proxy-vault",
                    "docker service create",
                        "--name", "neon-proxy-vault",
                        "--detach=false",
                        "--mode", "global",
                        "--endpoint-mode", "vip",
                        "--network", NeonClusterConst.PrivateNetwork,
                        options,
                        "--mount", "type=bind,source=/etc/neoncluster/env-host,destination=/etc/neoncluster/env-host,readonly=true",
                        "--env", $"VAULT_ENDPOINTS={sbEndpoints}",
                        "--env", $"LOG_LEVEL=INFO",
                        "--restart-delay", cluster.Definition.Docker.RestartDelay,
                        Program.ResolveDockerImage(cluster.Definition.ProxyVaultImage));

                    steps.Add(command);

                    //---------------------------------------------------------
                    // $hack(jeff.lill): 
                    //
                    // Fragile: Give Vault a chance to start.  It would be better to have the subsequent
                    // steps that depend on Vault to detect when ser service is not ready and retry.

                    steps.Add(CommandStep.CreateSudo(cluster.FirstManager.Name, "sleep 15"));

                    //---------------------------------------------------------

                    steps.Add(cluster.GetFileUploadSteps(cluster.Managers, LinuxPath.Combine(NodeHostFolders.Scripts, "neon-proxy-vault.sh"), command.ToBash()));

                    cluster.Configure(steps);
                });


            // We also need to deploy [neon-proxy-vault] to any pet nodes as Docker containers
            // to forward any Vault related traffic to the primary Vault instance running on onez
            // of the managers because pets aren't part of the Swarm.

            foreach (var pet in cluster.Pets)
            {
                pet.InvokeIdempotentAction("setup-proxy-vault",
                    () =>
                    {
                        var steps   = new ConfigStepList();
                        var command = CommandStep.CreateIdempotentDocker(pet.Name, "setup-neon-proxy-vault",
                        "docker run",
                            "--name", "neon-proxy-vault",
                            "--detach",
                            "--publish", $"{NeonHostPorts.ProxyVault}:{NetworkPorts.Vault}",
                            "--mount", "type=bind,source=/etc/neoncluster/env-host,destination=/etc/neoncluster/env-host,readonly=true",
                            "--env", $"VAULT_ENDPOINTS={sbEndpoints}",
                            "--env", $"LOG_LEVEL=INFO",
                            "--restart", "always",
                            Program.ResolveDockerImage(cluster.Definition.ProxyVaultImage));

                        steps.Add(command);
                        steps.Add(cluster.GetFileUploadSteps(new[] { pet }, LinuxPath.Combine(NodeHostFolders.Scripts, "neon-proxy-vault.sh"), command.ToBash()));

                        cluster.Configure(steps);
                    });
            }
        }

        /// <summary>
        /// Ensures that Vault is unsealed on all manager nodes. 
        /// </summary>
        private void VaultUnseal()
        {
            // Wait for the Vault instance on each manager node to become ready 
            // and then unseal them.

            foreach (var manager in cluster.Managers)
            {
                // Wait up to two minutes for Vault to initialize.

                var timer   = new Stopwatch();
                var timeout = TimeSpan.FromMinutes(5);

                while (true)
                {
                    if (timer.Elapsed > timeout)
                    {
                        manager.Fault($"[Vault] did not become ready after [{timeout}].");
                        return;
                    }

                    var response = manager.SudoCommand("vault-direct status", RunOptions.LogOutput);

                    if (response.ExitCode == 2 /* sealed */ &&
                        response.OutputText.Contains("High-Availability Enabled: true"))
                    {
                        break;
                    }

                    Thread.Sleep(TimeSpan.FromSeconds(5));
                }

                // Unseal the Vault instance.

                manager.Status = "vault: unseal";

                manager.SudoCommand($"vault-direct unseal -reset");     // This clears any previous unseal attempts

                for (int i = 0; i < clusterLogin.VaultCredentials.KeyThreshold; i++)
                {
                    manager.SudoCommand($"vault-direct unseal", cluster.VaultRunOptions, clusterLogin.VaultCredentials.UnsealKeys[i]);
                }

                // $hack(jeff.lill): Fragile: Wait for Vault to unseal and be ready to accept commands.

                Thread.Sleep(TimeSpan.FromSeconds(15));
            }
        }

        /// <summary>
        /// Initializes the cluster's HashiCorp Vault.
        /// </summary>
        private void VaultInitialize()
        {
            var firstManager = cluster.FirstManager;

            firstManager.InvokeIdempotentAction("setup-vault-initialize",
                () =>
                {
                    if (clusterLogin.VaultCredentials != null)
                    {
                        return; // Vault is already initialized.
                    }

                    try
                    {
                        // Initialize the Vault cluster using the first manager.

                        firstManager.Status = "vault: init";

                        var response = firstManager.SudoCommand(
                            "vault-direct init",
                            RunOptions.LogOnErrorOnly | cluster.VaultRunOptions,
                            $"-key-shares={cluster.Definition.Vault.KeyCount}",
                            $"-key-threshold={cluster.Definition.Vault.KeyThreshold}");

                        if (response.ExitCode > 0)
                        {
                            firstManager.Fault($"[vault init] exit code [{response.ExitCode}]");
                            return;
                        }

                        var rawVaultCredentials = response.OutputText;

                        clusterLogin.VaultCredentials = VaultCredentials.FromInit(rawVaultCredentials, cluster.Definition.Vault.KeyThreshold);

                        // Persist the Vault credentials.

                        clusterLogin.Save();

                        // Unseal Vault.

                        VaultUnseal();

                        // Configure the audit backend so that it sends events to syslog.

                        firstManager.Status = "vault: audit enable";
                        cluster.VaultCommand("vault audit-enable syslog tag=\"vault\" facility=\"AUTH\"");

                        // Mount a [generic] backend dedicated to neonCLUSTER related secrets.

                        firstManager.Status = "vault: mount neon-secret backend";
                        cluster.VaultCommand("vault mount", "-path=neon-secret", "-description=Reserved for neonCLUSTER secrets", "generic");

                        // Mount the [transit] backend and create the cluster key.

                        firstManager.Status = "vault: transit backend";
                        cluster.VaultCommand("vault mount transit");
                        cluster.VaultCommand($"vault write -f transit/keys/{NeonClusterConst.VaultTransitKey}");

                        // Mount the [approle] backend.

                        firstManager.Status = "vault: approle backend";
                        cluster.VaultCommand("vault auth-enable approle");

                        // Initialize the standard policies.

                        firstManager.Status = "vault: policies";

                        var writeCapabilities = VaultCapabilies.Create | VaultCapabilies.Read | VaultCapabilies.Update | VaultCapabilies.Delete | VaultCapabilies.List;
                        var readCapabilities  = VaultCapabilies.Read | VaultCapabilies.List;

                        cluster.CreateVaultPolicy(new VaultPolicy("neon-reader", "neon-secret/*", readCapabilities));
                        cluster.CreateVaultPolicy(new VaultPolicy("neon-writer", "neon-secret/*", writeCapabilities));
                        cluster.CreateVaultPolicy(new VaultPolicy("neon-cert-reader", "neon-secret/cert/*", readCapabilities));
                        cluster.CreateVaultPolicy(new VaultPolicy("neon-cert-writer", "neon-secret/cert/*", writeCapabilities));
                        cluster.CreateVaultPolicy(new VaultPolicy("neon-hosting-reader", "neon-secret/hosting/*", readCapabilities));
                        cluster.CreateVaultPolicy(new VaultPolicy("neon-hosting-writer", "neon-secret/hosting/*", writeCapabilities));
                        cluster.CreateVaultPolicy(new VaultPolicy("neon-service-reader", "neon-secret/service/*", readCapabilities));
                        cluster.CreateVaultPolicy(new VaultPolicy("neon-service-writer", "neon-secret/service/*", writeCapabilities));
                        cluster.CreateVaultPolicy(new VaultPolicy("neon-global-reader", "neon-secret/global/*", readCapabilities));
                        cluster.CreateVaultPolicy(new VaultPolicy("neon-global-writer", "neon-secret/global/*", writeCapabilities));

                        // Initialize the [neon-proxy-*] related service roles.  Each of these services 
                        // need read access to the TLS certificates and [neon-proxy-manager] also needs
                        // read access to the hosting options.

                        firstManager.Status = "vault: roles";

                        cluster.CreateVaultAppRole("neon-proxy-manager", "neon-cert-reader", "neon-hosting-reader");
                        cluster.CreateVaultAppRole("neon-proxy-public", "neon-cert-reader");
                        cluster.CreateVaultAppRole("neon-proxy-private", "neon-cert-reader");

                        using (var vault = NeonClusterHelper.OpenVault(ClusterCredentials.FromVaultToken(clusterLogin.VaultCredentials.RootToken)))
                        {
                            // Store the the cluster hosting options in the Vault so services that need to
                            // perform hosting level operations will have the credentials and other information
                            // to modify the environment.  For example in cloud environments, the [neon-proxy-manager]
                            // service needs to be able to update the worker load balancer rules so they match
                            // the current PUBLIC routes.

                            vault.WriteJsonAsync("neon-secret/hosting/options", cluster.Definition.Hosting).Wait();

                            // Store the zipped OpenVPN certificate authority files in the cluster Vault.

                            var vpnCaCredentials = clusterLogin.VpnCredentials;

                            if (vpnCaCredentials != null)
                            {
                                var vpnCaFiles = VpnCaFiles.LoadZip(vpnCaCredentials.CaZip, vpnCaCredentials.CaZipKey);

                                vpnCaFiles.Clean();
                                vault.WriteBytesAsync("neon-secret/vpn/ca.zip.encrypted", vpnCaFiles.ToZipBytes(vpnCaCredentials.CaZipKey)).Wait();
                            }
                        }
                    }
                    finally
                    {
                        cluster.FirstManager.Status = string.Empty;
                    }
                });
        }

        /// <summary>
        /// Initializes Consul values.
        /// </summary>
        private void ConsulInitialize()
        {
            var firstManager = cluster.FirstManager;

            firstManager.InvokeIdempotentAction("setup-consul-initialize",
                () =>
                {
                    firstManager.Status = "consul initialize";

                    // Persist the cluster definition (without the hosting options)
                    // to Consul so it will be available services like [neon-proxy-manager]
                    // immediately (before [neon-cluster-manager] spins up).

                    var loginClone = clusterLogin.Clone();

                    loginClone.ClearRootSecrets();
                    loginClone.Definition.Hosting = null;

                    NeonClusterHelper.PutDefinitionAsync(loginClone.Definition, savePets: true).Wait();
                });
        }

        /// <summary>
        /// Generates the SSH key to be used for authenticating SSH client connections.
        /// </summary>
        /// <param name="manager">A cluster manager node.</param>
        private void GenerateClientSshKey(SshProxy<NodeDefinition> manager)
        {
            // Here's some information explaining what how I'm doing this:
            //
            //      https://help.ubuntu.com/community/SSH/OpenSSH/Configuring
            //      https://help.ubuntu.com/community/SSH/OpenSSH/Keys

            if (!sshTlsAuth)
            {
                return;
            }

            if (clusterLogin.SshClientKey != null)
            {
                return; // Key has already been created.
            }

            clusterLogin.SshClientKey = new SshClientKey();

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

ssh-keygen -t rsa -b 2048 -N """" -C ""neoncluster"" -f /run/ssh-key

# Relax permissions so we can download the key parts.

chmod 666 /run/ssh-key*
";
            var bundle = new CommandBundle("./keygen.sh");

            bundle.AddFile("keygen.sh", keyGenScript, isExecutable: true);

            manager.SudoCommand(bundle);

            using (var stream = new MemoryStream())
            {
                manager.Download("/run/ssh-key.pub", stream);

                clusterLogin.SshClientKey.PublicPUB = Encoding.UTF8.GetString(stream.ToArray());
            }

            using (var stream = new MemoryStream())
            {
                manager.Download("/run/ssh-key", stream);

                clusterLogin.SshClientKey.PrivatePEM = Encoding.UTF8.GetString(stream.ToArray());
            }

            manager.SudoCommand("rm /run/ssh-key*");

            // We're going to use WinSCP to convert the OpenSSH PEM formatted key
            // to the PPK format PuTTY/WinSCP require.  Note that this won't work
            // when the tool is running in a Docker Linux container.  We're going
            // to handle the conversion in the outer shim as a post run action.

            if (NeonHelper.IsWindows)
            {
                var pemKeyPath = Path.Combine(Program.ClusterTempFolder, Guid.NewGuid().ToString("D"));
                var ppkKeyPath = Path.Combine(Program.ClusterTempFolder, Guid.NewGuid().ToString("D"));

                try
                {
                    File.WriteAllText(pemKeyPath, clusterLogin.SshClientKey.PrivatePEM);

                    ExecuteResult result;

                    try
                    {
                        result = NeonHelper.ExecuteCaptureStreams("winscp.com", $@"/keygen ""{pemKeyPath}"" /comment=""{cluster.Definition.Name} Key"" /output=""{ppkKeyPath}""");
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

                    clusterLogin.SshClientKey.PrivatePPK = File.ReadAllText(ppkKeyPath);

                    // Persist the SSH client key.

                    clusterLogin.Save();
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
        private void SetStrongPassword(SshProxy<NodeDefinition> node)
        {
            node.InvokeIdempotentAction("setup-strong-password",
                () =>
                {
                    node.Status = "set strong password";

                    var script =
$@"
echo '{Program.MachineUsername}:{clusterLogin.SshPassword}' | chpasswd
";
                    var bundle = new CommandBundle("./set-strong-password.sh");

                    bundle.AddFile("set-strong-password.sh", script, isExecutable: true);

                    var response = node.SudoCommand(bundle);

                    if (response.ExitCode != 0)
                    {
                        Console.WriteLine($"*** ERROR: Unable to set a strong password [exitcode={response.ExitCode}].");
                        Program.Exit(response.ExitCode);
                    }

                    node.UpdateCredentials(SshCredentials.FromUserPassword(Program.MachineUsername, clusterLogin.SshPassword));
                });
        }

        /// <summary>
        /// Generates the private key that will be used to secure SSH on the cluster servers.
        /// </summary>
        private void ConfigureSshCerts()
        {
            cluster.FirstManager.InvokeIdempotentAction("setup-ssh-server-key",
                () =>
                {
                    cluster.FirstManager.Status = "generate server SSH key";

                    var configScript =
@"
# Generate the SSH server key and fingerprint.

mkdir -p /dev/shm/ssh

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
                    cluster.FirstManager.SudoCommand(bundle);

                    cluster.FirstManager.Status = "download server SSH key";

                    clusterLogin.SshClusterHostPrivateKey     = cluster.FirstManager.DownloadText("/dev/shm/ssh/ssh_host_rsa_key");
                    clusterLogin.SshClusterHostPublicKey      = cluster.FirstManager.DownloadText("/dev/shm/ssh/ssh_host_rsa_key.pub");
                    clusterLogin.SshClusterHostKeyFingerprint = cluster.FirstManager.DownloadText("/dev/shm/ssh/ssh.fingerprint");

                    // Delete the SSH key files for security.

                    cluster.FirstManager.SudoCommand("rm -r /dev/shm/ssh");

                    // Persist the server SSH key and fingerprint.

                    clusterLogin.Save();
                });
        }

        /// <summary>
        /// Configures SSH on a node.
        /// </summary>
        /// <param name="node">The target node.</param>
        private void ConfigureSsh(SshProxy<NodeDefinition> node)
        {
            node.InvokeIdempotentAction("setup-ssh",
                () =>
                {
                    CommandBundle bundle;

                    // Here's some information explaining what how I'm doing this:
                    //
                    //      https://help.ubuntu.com/community/SSH/OpenSSH/Configuring
                    //      https://help.ubuntu.com/community/SSH/OpenSSH/Keys

                    if (sshTlsAuth)
                    {
                        node.Status = "set client SSH key";

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
                        bundle.AddFile("ssh-key.pub", clusterLogin.SshClientKey.PublicPUB);

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
                    bundle.AddFile("ssh_host_rsa_key", clusterLogin.SshClusterHostPrivateKey);
                    node.SudoCommand(bundle);
                });
        }
    }
}
