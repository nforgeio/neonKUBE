//-----------------------------------------------------------------------------
// FILE:	    ClusterSetupCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

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
using Neon.Time;

namespace NeonTool
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

    --unclassified  - Runs Vault commands without redacting logs.  This
                      is useful for debugging cluster setup issues.  Do
                      not use for production clusters.
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
            get { return new string[] { "--unclassified" }; }
        }

        /// <inheritdoc/>
        public override bool NeedsCommandCredentials
        {
            get { return true; }
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
                Console.WriteLine($"*** ERROR: Invalid username and cluster [{commandLine.Arguments[0]}].  Expected something like: USER@CLUSTER");
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

            if (!clusterLogin.PartialSetup)
            {
                Console.Error.WriteLine($"*** ERROR: Cluster [{cluster.Definition.Name}] has already been setup.");
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
                clusterLogin.Definition.Hosting.Environment != HostingEnvironments.Machine && 
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

            if (commandLine.HasOption("--unclassified"))
            {
                cluster.VaultRunOptions = RunOptions.None;
            }

            // Perform the setup operations.

            var controller = 
                new SetupController(new string[] { "cluster", "setup", $"[{cluster.Name}]" }, cluster.Nodes)
                {
                    ShowStatus  = !Program.Quiet,
                    MaxParallel = Program.MaxParallel
                };

            controller.AddWaitUntilOnlineStep("connect");

            switch (cluster.Definition.HostAuth.SshAuth)
            {
                case AuthMethods.Password:

                    sshTlsAuth = false;
                    break;

                case AuthMethods.Tls:

                    sshTlsAuth = true;
                    break;

                default:

                    throw new NotSupportedException($"Unsupported SSH authentication method [{cluster.Definition.HostAuth.SshAuth}].");
            }

            if (sshTlsAuth)
            {
                controller.AddStep("ssh client cert", n => GenerateClientSshKey(n), n => n.Metadata.IsManager);
            }

            controller.AddStep("verify OS", n => CommonSteps.VerifyOS(n));
            controller.AddGlobalStep("create certs", () => CreateCertificates());

            // We're going to configure the managers separately from the workers
            // because we need to be careful about when we reboot the managers
            // since this will also take down the VPN.  We're also going to 
            // reboot all of the managers together after common configuration
            // for the same reason.

            controller.AddStep("manager initialize", n => ConfigureCommon(n), n => n.Metadata.IsManager);

            controller.AddStepNoParallelLimit("manager restart", 
                n =>
                {
                    n.InvokeIdempotentAction("setup-common-restart", () => RebootAndWait(n));
                },
                n => n.Metadata.IsManager);

            controller.AddStep("manager config", n => ConfigureManager(n), n => n.Metadata.IsManager);

            // Configure the workers.

            controller.AddStep("worker config", 
                n =>
                {
                    ConfigureCommon(n);
                    n.InvokeIdempotentAction("setup-common-restart", () => RebootAndWait(n));
                    ConfigureWorker(n);
                },
                n => n.Metadata.IsWorker);

            controller.AddStep("swarm create", n => CreateSwarm(n), n => n == cluster.FirstManager);
            controller.AddStep("swarm join", n => JoinSwarm(n), n => n != cluster.FirstManager);

            if (!cluster.Definition.BareDocker)
            {
                controller.AddStep("networks", n => CreateClusterNetworks(n), n => n == cluster.FirstManager);
                controller.AddStep("node labels", n => AddNodeLabels(n), n => n == cluster.FirstManager);

                if (cluster.Definition.Docker.RegistryCache)
                {
                    var registryCacheConfigurator = new RegistryCache(cluster, clusterLoginPath);

                    controller.AddStep("registry cache", n => registryCacheConfigurator.Configure(n));
                }

                var managerPulledEvent = new ManualResetEvent(false);

                controller.AddStep("pull images",
                    n =>
                    {
                        if (cluster.Definition.Docker.RegistryCache)
                        {
                            // If the cluster deploys a local registry cache then
                            // pull images on the first manager first so they get
                            // loaded into the cluster's register's registry cache 
                            // and then pull for all of the other nodes.
                            //
                            // I'm going to use a bit of thread synchronization so
                            // this will appear to be a single step to the operator.

                            if (n == cluster.FirstManager)
                                {
                                    PullImages(n);
                                    managerPulledEvent.Set();
                                }
                                else
                                {
                                    managerPulledEvent.WaitOne();
                                    PullImages(n);
                                }
                            }
                            else
                            {
                            // Simply pull in parallel if there's no local registry cache.

                            PullImages(n);
                            }
                        });

                controller.AddGlobalStep("cluster key/value",
                    () =>
                    {
                        NeonClusterHelper.OpenCluster(cluster);

                        VaultProxy();
                        VaultInitialize();
                        ConsulInitialize();
                    });

                controller.AddGlobalStep("cluster services", () => new ClusterServices(cluster).Configure(cluster.FirstManager));

                if (cluster.Definition.Log.Enabled)
                {
                    controller.AddGlobalStep("log services", () => new LogServices(cluster).Configure(cluster.FirstManager));

                    // $todo(jeff.lill): 
                    //
                    // It's a bit weird that I'm not doing these steps in [LogServices.Configure()] 
                    // along with the other cluster configuration.

                    controller.AddStep("metricbeat", n => DeployMetricbeat(n));
                    controller.AddGlobalStep("metricbeat dashboards", () => InstallMetricbeatDashboards(cluster));
                }

                controller.AddStep("check managers", n => ClusterDiagnostics.CheckClusterManager(n, cluster.Definition), n => n.Metadata.IsManager);
                controller.AddStep("check workers", n => ClusterDiagnostics.CheckClusterWorker(n, cluster.Definition), n => n.Metadata.IsWorker);

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
                if (cluster.Definition.HostAuth.PasswordLength > 0)
                {
                    clusterLogin.SshPassword          = NeonHelper.GetRandomPassword(cluster.Definition.HostAuth.PasswordLength);
                    clusterLogin.HasStrongSshPassword = true;
                }
                else
                {
                    clusterLogin.SshPassword = Program.Password;
                }

                clusterLogin.Save();
            }

            if (cluster.Definition.HostAuth.PasswordLength > 0)
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

            clusterLogin.PartialSetup = false;
            clusterLogin.IsRoot       = true;
            clusterLogin.Username     = NeonClusterConst.RootUser;
            clusterLogin.Definition   = cluster.Definition;
            clusterLogin.SshUsername  = Program.Username;

            if (cluster.Definition.Vpn.Enabled)
            {
                // We don't need to save the certificate authority files any more
                // because they've been stored in the cluster Vault.

                clusterLogin.VpnCredentials.CaZip = null;
            }

            clusterLogin.Save();

            Console.WriteLine($"*** Logging into [{NeonClusterConst.RootUser}@{cluster.Definition.Name}].");

            // Note that we're going to login via the VPN for cloud environments
            // but not for local hosting since the operator had to be on premise
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
        public override ShimInfo Shim(DockerShim shim)
        {
            var commandLine = shim.CommandLine.Shift(Words.Length);

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
                Console.WriteLine($"*** ERROR: Invalid username and cluster [{commandLine.Arguments[0]}].  Expected something like: USER@CLUSTER");
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

            if (!clusterLogin.PartialSetup)
            {
                Console.Error.WriteLine($"*** ERROR: Cluster [{clusterName}] has already been setup.");
            }

            // Cloud deployments had their VPN configured when they were prepared
            // so we need to connect the VPN now so we can setup the nodes.  
            //
            // On-premise clusters are always setup via local network connections
            // so we will be connecting the VPN. 

            if (clusterLogin.Definition.Vpn.Enabled && clusterLogin.Definition.Hosting.Environment != HostingEnvironments.Machine)
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

            return new ShimInfo(isShimmed: true, ensureConnection: false);
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
        private void ConfigureCommon(NodeProxy<NodeDefinition> node)
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

                    // Configure the APT proxy server early, if there is one.

                    if (!string.IsNullOrEmpty(cluster.Definition.PackageCache))
                    {
                        node.Status = "run: setup-apt-proxy.sh";
                        node.SudoCommand("setup-apt-proxy.sh");
                    }

                    // Perform basic node setup including changing the host name.

                    UploadHostsFile(node);
                    UploadHostEnvFile(node);

                    node.Status = "run: setup-node.sh";
                    node.SudoCommand("setup-node.sh");

                    // Create and configure the internal cluster self-signed certificates.

                    node.Status = "install certs";

                    // Install the Vault certificate.

                    node.UploadText($"/usr/local/share/ca-certificates/{NeonHosts.Vault}.crt", clusterLogin.VaultCertificate.Cert);
                    node.SudoCommand("mkdir -p /etc/vault");
                    node.UploadText($"/etc/vault/vault.crt", clusterLogin.VaultCertificate.Cert);
                    node.UploadText($"/etc/vault/vault.key", clusterLogin.VaultCertificate.Key);
                    node.SudoCommand("chmod 600 /etc/vault/*");

                    // $todo(jeff.lill): Install the Consul certificate.

                    node.SudoCommand("update-ca-certificates");

                    // Tune Linux for SSDs if enabled.

                    node.Status = "run: setup-ssd.sh";
                    node.SudoCommand("setup-ssd.sh");
                });
        }

        /// <summary>
        /// Reboots the nodes and waits until the package manager is ready.
        /// </summary>
        /// <param name="node">The cluster node.</param>
        private void RebootAndWait(NodeProxy<NodeDefinition> node)
        {
            node.Status = "rebooting...";
            node.Reboot(wait: true);

            CommonSteps.WaitForPackageManager(node);
        }

        /// <summary>
        /// Generates and uploads the <b>/etc/hosts</b> file for a node.
        /// </summary>
        /// <param name="node">The target node.</param>
        private void UploadHostsFile(NodeProxy<NodeDefinition> node)
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
        private void UploadHostEnvFile(NodeProxy<NodeDefinition> node)
        {
            var sbEnvHost       = new StringBuilder();
            var vaultDirectLine = string.Empty;

            if (node.Metadata.IsManager)
            {
                vaultDirectLine = $"export VAULT_DIRECT_ADDR={cluster.Definition.Vault.GetDirectUri(node.Name)}";
            }

            sbEnvHost.AppendLine(
$@"#------------------------------------------------------------------------------
# FILE:         /etc/neoncluster/env-host
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
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
export NEON_APT_CACHE={cluster.Definition.PackageCache ?? string.Empty}

export VAULT_ADDR={cluster.Definition.Vault.Uri}
{vaultDirectLine}
export CONSUL_HTTP_ADDR={NeonHosts.Consul}:{cluster.Definition.Consul.Port}
export CONSUL_HTTP_FULLADDR=http://{NeonHosts.Consul}:{cluster.Definition.Consul.Port}
");

            node.UploadText($"{NodeHostFolders.Config}/env-host", sbEnvHost.ToString(), 4, Encoding.UTF8);
        }

        /// <summary>
        /// Generates the Consul configuration file for a cluster node.
        /// </summary>
        /// <param name="node">The target cluster node.</param>
        /// <returns>The configuration file text.</returns>
        private string GetConsulConfig(NodeProxy<NodeDefinition> node)
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
        private string GetDockerConfig(NodeProxy<NodeDefinition> node)
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

            logOptions.Add("fluentd-buffer-limit", $"{5 * 1024 * 1024}");

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
                    registries.Add($"https://{manager.Name}.{NeonHosts.RegistryCache}:{NeonHostPorts.RegistryCache}");
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
        private void ConfigureVpnReturnRoutes(NodeProxy<NodeDefinition> node)
        {
            if (cluster.Definition.Hosting.Environment != HostingEnvironments.Machine)
            {
                // Cloud handle VPN return routing via routing tables setup
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

            var routeCommands = new List<string>();

            foreach (var manager in cluster.Managers.Where(m => m != node))
            {
                routeCommands.Add($"ip route add {manager.Metadata.VpnReturnSubnet} via {manager.PrivateAddress} dev eth0 || true");
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
        /// Complete a manager node configuration.
        /// </summary>
        /// <param name="node">The target cluster node.</param>
        private void ConfigureManager(NodeProxy<NodeDefinition> node)
        {
            node.InvokeIdempotentAction("setup-manager",
                () =>
                {
                    // Setup NTP.

                    node.Status = "run: setup-ntp.sh";
                    node.SudoCommand("setup-ntp.sh");

                    // Configure the VPN return routes.

                    ConfigureVpnReturnRoutes(node);

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
                    node.SudoCommand("chmod 660 /etc/docker/daemon.json");
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
        private void CreateSwarm(NodeProxy<NodeDefinition> bootstrapManager)
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
        /// Configures a worker node.
        /// </summary>
        /// <param name="node">The target cluster node.</param>
        private void ConfigureWorker(NodeProxy<NodeDefinition> node)
        {
            node.InvokeIdempotentAction("setup-worker",
                () =>
                {
                    // Setup NTP.

                    node.Status = "run: setup-ntp.sh";
                    node.SudoCommand("setup-ntp.sh");

                    // Configure the VPN return routes.

                    ConfigureVpnReturnRoutes(node);

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
        private void CreateClusterNetworks(NodeProxy<NodeDefinition> manager)
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
        private void AddNodeLabels(NodeProxy<NodeDefinition> manager)
        {
            manager.InvokeIdempotentAction("setup-node-labels",
                () =>
                {
                    foreach (var node in cluster.Nodes)
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
                            manager.DockerCommand("docker node update --label-add", labelDefinition, node.Name);
                        }

                        node.Status = "labeling";
                    }
                });
        }

        /// <summary>
        /// Pulls common images to the node.
        /// </summary>
        /// <param name="node">The target cluster node.</param>
        private void PullImages(NodeProxy<NodeDefinition> node)
        {
            node.InvokeIdempotentAction("setup-pull-images",
                () =>
                {
                    var images = new List<string>()
                    {
                        "neoncluster/alpine",
                        "neoncluster/ubuntu-16.04",
                        "neoncluster/neon-proxy-vault"
                    };

                    if (cluster.Definition.Log.Enabled)
                    {
                        images.Add(cluster.Definition.Log.HostImage);
                        images.Add(cluster.Definition.Log.CollectorImage);
                        images.Add(cluster.Definition.Log.EsImage);
                        images.Add(cluster.Definition.Log.MetricbeatImage);
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
        private void JoinSwarm(NodeProxy<NodeDefinition> node)
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
                        node.DockerCommand(RunOptions.Classified, $"docker swarm join --token {clusterLogin.SwarmManagerToken} {cluster.FirstManager.Metadata.PrivateAddress}:2377");
                    }
                    else
                    {
                        // Must be a worker node.

                        node.DockerCommand(RunOptions.Classified, $"docker swarm join --token {clusterLogin.SwarmWorkerToken} {cluster.FirstManager.Metadata.PrivateAddress}:2377");
                    }
                });

            node.Status = "joined";
        }

        /// <summary>
        /// Configures the Vault load balancer service: <b>neon-proxy-vault</b>.
        /// </summary>
        private void VaultProxy()
        {
            cluster.FirstManager.InvokeIdempotentAction("setup-vault-proxy",
                () =>
                {
                    // Create the comma separated list of Vault manager endpoints formatted as:
                    //
                    //      NODE:IP:PORT

                    var sbEndpoints = new StringBuilder();

                    foreach (var manager in cluster.Definition.SortedManagers)
                    {
                        sbEndpoints.AppendWithSeparator($"{manager.Name}:{manager.PrivateAddress}:{NetworkPorts.Vault}", ",");
                    }

                    // $todo(jeff.lill):
                    //
                    // Docker mesh routing seems unstable right now on versions 17.03.0-ce
                    // thru 17.06.0-ce so we're going to temporarily work around this by
                    // running the PUBLIC, PRIVATE and VAULT proxies on all nodes and 
                    // publishing the ports to the host (not the mesh).
                    //
                    //      https://github.com/jefflill/NeonForge/issues/104
                    //
                    // Note that this mode feature is documented (somewhat poorly) here:
                    //
                    //      https://docs.docker.com/engine/swarm/services/#publish-ports

                    // Deploy [neon-proxy-vault] on all manager nodes.

                    var steps   = new ConfigStepList();
                    var command = CommandStep.CreateIdempotentDocker(cluster.FirstManager.Name, "setup-neon-proxy-vault",
                        "docker service create",
                            "--name", "neon-proxy-vault",
                            "--mode", "global",
                            "--endpoint-mode", "vip",
                            "--network", NeonClusterConst.PrivateNetwork,
#if !MESH_NETWORK_WORKS
                            "--publish", $"mode=host,published={NeonHostPorts.ProxyVault},target={NetworkPorts.Vault}",
#else
                            "--constraint", $"node.role==manager",
                            "--publish", $"{NeonHostPorts.ProxyVault}:{NetworkPorts.Vault}",
#endif
                            "--mount", "type=bind,source=/etc/neoncluster/env-host,destination=/etc/neoncluster/env-host,readonly=true",
                            "--env", $"VAULT_ENDPOINTS={sbEndpoints}",
                            "--restart-delay", cluster.Definition.Docker.RestartDelay,
                            "neoncluster/neon-proxy-vault");

                    steps.Add(command);
                    steps.Add(CommandStep.CreateSudo(cluster.FirstManager.Name, "sleep 15"));  // $hack(jeff.lill): Fragile: Give Vault proxy a chance to start.
                    steps.Add(cluster.GetFileUploadSteps(cluster.Managers, LinuxPath.Combine(NodeHostFolders.Scripts, "neon-proxy-vault.sh"), command.ToBash()));

                    cluster.Configure(steps);
                });
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

                    NeonClusterHelper.PutClusterDefinitionAsync(loginClone.Definition).Wait();
                });
        }

        /// <summary>
        /// Deploys <b>Elastic Metricbeat</b> to the node.
        /// </summary>
        /// <param name="node">The target cluster node.</param>
        private void DeployMetricbeat(NodeProxy<NodeDefinition> node)
        {
            node.InvokeIdempotentAction("setup-metricbeat",
                () =>
                {
                    node.Status = "deploying metricbeat";

                    node.DockerCommand(
                        "docker run",
                            "--name", "neon-log-metricbeat",
                            "--detach",
                            "--net", "host",
                            "--restart", "always",
                            "--volume", "/etc/neoncluster/env-host:/etc/neoncluster/env-host:ro",
                            "--volume", "/proc:/hostfs/proc:ro",
                            "--volume", "/:/hostfs:ro",
                            "--log-driver", "json-file",
                            "neoncluster/metricbeat");
                });
        }

        /// <summary>
        /// Installs the <b>Elastic Metricbeat</b> dashboards to the log Elasticsearch cluster.
        /// </summary>
        /// <param name="cluster">The cluster proxy.</param>
        private void InstallMetricbeatDashboards(ClusterProxy cluster)
        {
            cluster.FirstManager.InvokeIdempotentAction("setup-metricbeat-dashboards",
                () =>
                {
                    // Note that we're going to add the Metricbeat dashboards to Elasticsearch
                    // even when the Kibana dashboard isn't enabled because it doesn't cost
                    // much and to make it easier for operators that wish to install Kibana
                    // themselves.

                    cluster.FirstManager.Status = "metricbeat dashboards";

                    cluster.FirstManager.DockerCommand(
                        "docker run --rm",
                            "--name", "neon-log-metricbeat-dash",
                            "--volume", "/etc/neoncluster/env-host:/etc/neoncluster/env-host:ro",
                            "neoncluster/metricbeat", "import-dashboards");
                });
        }

        /// <summary>
        /// Generates the SSH key to be used for authenticating SSH client connections.
        /// </summary>
        /// <param name="manager">A cluster manager node.</param>
        private void GenerateClientSshKey(NodeProxy<NodeDefinition> manager)
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
            // for a moment but I'm going to worry about this too much.
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
        private void SetStrongPassword(NodeProxy<NodeDefinition> node)
        {
            node.InvokeIdempotentAction("setup-strong-password",
                () =>
                {
                    node.Status = "set strong password";

                    var script =
$@"
echo '{Program.Username}:{clusterLogin.SshPassword}' | chpasswd
";
                    var bundle = new CommandBundle("./set-strong-password.sh");

                    bundle.AddFile("set-strong-password.sh", script, isExecutable: true);

                    var response = node.SudoCommand(bundle);

                    if (response.ExitCode != 0)
                    {
                        Console.WriteLine($"*** ERROR: Unable to set a strong password [exitcode={response.ExitCode}].");
                        Program.Exit(response.ExitCode);
                    }

                    node.UpdateCredentials(SshCredentials.FromUserPassword(Program.Username, clusterLogin.SshPassword));
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
chmod 666 /dev/shm/ssh/ssh.fingerprint
";
                    var bundle = new CommandBundle("./config.sh");

                    bundle.AddFile("config.sh", configScript, isExecutable: true);
                    cluster.FirstManager.SudoCommand(bundle);

                    cluster.FirstManager.Status = "download server SSH key";

                    clusterLogin.SshServerKey            = cluster.FirstManager.DownloadText("/dev/shm/ssh/ssh_host_rsa_key");
                    clusterLogin.SshServerKeyFingerprint = cluster.FirstManager.DownloadText("/dev/shm/ssh/ssh.fingerprint");

                    cluster.FirstManager.SudoCommand("rm -r /dev/shm/ssh");

                    // Persist the server SSH key and fingerprint.

                    clusterLogin.Save();
                });
        }

        /// <summary>
        /// Configures SSH on a node.
        /// </summary>
        /// <param name="node">The target node.</param>
        private void ConfigureSsh(NodeProxy<NodeDefinition> node)
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
                    bundle.AddFile("ssh_host_rsa_key", clusterLogin.SshServerKey);
                    node.SudoCommand(bundle);
                });
        }
    }
}
