//-----------------------------------------------------------------------------
// FILE:	    ClusterSetupCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

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

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Kube;
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
Configures a neonHIVE as described in the cluster definition file.

USAGE: 

    neon cluster setup [OPTIONS] root@CLUSTER-NAME  

OPTIONS:

    --unredacted        - Runs Vault and other commands with potential
                          secrets without redacting logs.  This is useful 
                          for debugging cluster setup  issues.  Do not
                          use for production hives.
";
        private const string logBeginMarker  = "# CLUSTER-BEGIN-SETUP ############################################################";
        private const string logEndMarker    = "# CLUSTER-END-SETUP-SUCCESS ######################################################";
        private const string logFailedMarker = "# CLUSTER-END-SETUP-FAILED #######################################################";

        private KubeConfig              kubeConfig;
        private KubeConfigContext       kubeContext;
        private KubeContextExtension    kubeContextExtension;
        private ClusterProxy            cluster;
        private KubeSetupInfo           kubeSetupInfo;

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
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            if (commandLine.Arguments.Length < 1)
            {
                Console.Error.WriteLine("*** ERROR: [root@CLUSTER-NAME] argument is required.");
                Program.Exit(1);
            }

            var contextName = KubeConfigName.Parse(commandLine.Arguments[0]);

            kubeContextExtension = KubeHelper.GetContextExtension(contextName);

            if (kubeContextExtension == null)
            {
                Console.Error.WriteLine($"*** ERROR: Be sure to prepare the cluster first via [neon cluster prepare...].");
                Program.Exit(1);
            }
            else if (!kubeContextExtension.SetupPending)
            {
                Console.Error.WriteLine($"*** ERROR: Cluster [{contextName.Cluster}] has already been setup.");
            }

            kubeConfig                       = KubeConfig.Load();
            kubeContext                      = new KubeConfigContext(contextName);
            kubeContext.Properties.Extension = kubeContextExtension;

            if (kubeConfig.GetCluster(contextName.Cluster) != null)
            {
                Console.Error.WriteLine($"*** ERROR: You already have a deployed cluster named [{contextName.Cluster}].");
                Program.Exit(1);
            }

            KubeHelper.SetKubeContext(kubeContext);

            // Note that cluster setup appends to existing log files.

            cluster = new ClusterProxy(kubeContext, Program.CreateNodeProxy<NodeDefinition>, appendLog: true, defaultRunOptions: RunOptions.LogOutput | RunOptions.FaultOnError);

            // Configure global options.

            if (commandLine.HasOption("--unredacted"))
            {
                cluster.SecureRunOptions = RunOptions.None;
            }

            // Perform the setup operations.

            var controller =
                new SetupController<NodeDefinition>(new string[] { "cluster", "setup", $"[{cluster.Name}]" }, cluster.Nodes)
                {
                    ShowStatus = !Program.Quiet,
                    MaxParallel = Program.MaxParallel
                };

            controller.AddGlobalStep("setup details",
                () =>
                {
                    using (var client = new HeadendClient())
                    {
                        kubeSetupInfo = client.GetSetupInfoAsync(cluster.Definition).Result;
                    }
                });

            controller.AddGlobalStep("download binaries", () => DownloadBinaries());

            controller.AddWaitUntilOnlineStep("connect");

            controller.AddStep("ssh client cert",
                (node, stepDelay) =>
                {
                    GenerateClientSshKey(node, stepDelay);
                },
                node => node == cluster.FirstMaster);

            controller.AddStep("verify OS",
                (node, stepDelay) =>
                {
                    Thread.Sleep(stepDelay);
                    CommonSteps.VerifyOS(node);
                });

            // Write the operation begin marker to all cluster node logs.

            cluster.LogLine(logBeginMarker);

            // Perform common configuration for all cluster nodes.

            controller.AddStep("setup nodes",
                (node, stepDelay) =>
                {
                    SetupCommon(node, stepDelay);
                    node.InvokeIdempotentAction("setup/common-restart", () => RebootAndWait(node));
                    SetupNode(node);
                },
                node => true,
                stepStaggerSeconds: cluster.Definition.Setup.StepStaggerSeconds);

            // Create the Swarm.

            controller.AddStep("cluster create",
                (node, stepDelay) =>
                {
                    InitializeCluster(node, stepDelay);
                },
                node => node == cluster.FirstMaster);

            controller.AddStep("cluster join",
                (node, stepDelay) =>
                {
                    JoinCluster(node, stepDelay);
                },
                node => node != cluster.FirstMaster);

            // Continue with the configuration.

            controller.AddStep("check masters",
                (node, stepDelay) =>
                {
                    Thread.Sleep(stepDelay);
                    HiveDiagnostics.CheckMaster(node, cluster.Definition);
                },
                node => node.Metadata.IsMaster);

            controller.AddStep("check workers",
                (node, stepDelay) =>
                {
                    Thread.Sleep(stepDelay);
                    HiveDiagnostics.CheckWorker(node, cluster.Definition);
                },
                node => node.Metadata.IsWorker);

            // $todo(jeff.lill):
            //
            // Note that this step isn't entirely idempotent.  The problem happens
            // when the password change fails on one or more of the nodes and succeeds
            // on others.  This will result in SSH connection failures for the nodes
            // that had their passwords changes.
            //
            // One solution would be to store credentials in the node definitions
            // rather than using common credentials across all nodes.
            //
            //      https://github.com/jefflill/NeonForge/issues/397

            kubeContextExtension.SshStrongPassword = NeonHelper.GetRandomPassword(cluster.Definition.NodeOptions.PasswordLength);
            kubeContextExtension.Save();

            controller.AddStep("strong password",
                (node, stepDelay) =>
                {
                    SetStrongPassword(node, TimeSpan.Zero);
                });

            controller.AddGlobalStep("passwords set",
                () =>
                {
                    // This hidden step sets the SSH provisioning password to NULL to 
                    // indicate that the final password has been set for all of the nodes.

                    kubeContextExtension.HasStrongSshPassword = true;
                    kubeContextExtension.Save();
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

            // Start setup.

            if (!controller.Run())
            {
                // Write the operation end/failed to all cluster node logs.

                cluster.LogLine(logFailedMarker);

                Console.Error.WriteLine("*** ERROR: One or more configuration steps failed.");
                Program.Exit(1);
            }

            // Persist the new strong password and indicate that setup is complete.

            kubeContextExtension.SshPassword     = kubeContextExtension.SshStrongPassword;
            kubeContextExtension.SshStrongPassword = null;
            kubeContextExtension.SetupPending    = false;

            kubeContextExtension.Save();

            // Write the operation end marker to all cluster node logs.

            cluster.LogLine(logEndMarker);

            // Update the kubecluster config.

            Console.WriteLine($"*** Connecting to [{kubeContext.Name}].");
            kubeConfig.SetContext(kubeContext.Name);
            Console.WriteLine();
        }

        /// <summary>
        /// Downloads any required binaries to the cache if they're not already present.
        /// </summary>
        private async void DownloadBinaries()
        {
            var handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            var firstMaster = cluster.FirstMaster;

            using (var client = new HttpClient(handler, disposeHandler: true))
            {
                var hostPlatform = KubeHelper.HostPlatform;
                var kubeCtlPath  = KubeHelper.GetComponentCachePath(hostPlatform, "kubectl", cluster.Definition.Kubernetes.Version);

                if (!File.Exists(kubeCtlPath))
                {
                    firstMaster.Status = "download: kubectl";

                    string kubeCtlUri;

                    switch (hostPlatform)
                    {
                        case KubeHostPlatform.Linux:

                            kubeCtlUri = kubeSetupInfo.LinuxKubeCtlUri;
                            break;

                        case KubeHostPlatform.Osx:

                            kubeCtlUri = kubeSetupInfo.OsxKubeCtlUri;
                            break;

                        case KubeHostPlatform.Windows:

                            kubeCtlUri = kubeSetupInfo.WindowsKubeCtlUri;
                            break;

                        default:

                            throw new NotSupportedException($"Unsupported workstation platform [{hostPlatform}]");
                    }

                    using (var response = await client.GetStreamAsync(kubeCtlUri))
                    {
                        using (var file = new FileStream(kubeCtlPath, FileMode.Create, FileAccess.ReadWrite))
                        {
                            await response.CopyToAsync(file);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Basic configuration that will happen every time if DEBUG setup
        /// mode is ENABLED or else will be invoked idempotently (if that's 
        /// a word).
        /// </summary>
        /// <param name="node">The target node.</param>
        private void ConfigureBasic(SshProxy<NodeDefinition> node)
        {
            // Configure the node's environment variables.

            CommonSteps.ConfigureEnvironmentVariables(node, cluster.Definition);

            // Upload the setup and configuration files.

            node.CreateHostFolders();
            node.UploadConfigFiles(cluster.Definition, kubeSetupInfo);
            node.UploadResources(cluster.Definition, kubeSetupInfo);
        }

        /// <summary>
        /// Performs common node configuration.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void SetupCommon(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            //-----------------------------------------------------------------
            // NOTE: 
            //
            // We're going to perform the following steps outside of the
            // idempotent check to make it easier to debug and modify 
            // scripts and tools when cluster setup has been partially
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

                    CommonSteps.PrepareNode(node, cluster.Definition, kubeSetupInfo);

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

                    node.Status = "setup: package-proxy";
                    node.SudoCommand("setup-package-proxy.sh");

                    // Perform basic node setup including changing the hostname.

                    UploadHostsFile(node);

                    node.Status = "setup: node";
                    node.SudoCommand("setup-node.sh");

                    // Tune Linux for SSDs, if enabled.

                    node.Status = "tune: disks";
                    node.SudoCommand("setup-ssd.sh");
                });
        }

        /// <summary>
        /// Performs basic node configuration.
        /// </summary>
        /// <param name="node">The target node.</param>
        private void SetupNode(SshProxy<NodeDefinition> node)
        {
            node.InvokeIdempotentAction($"setup/{node.Metadata.Role}",
                () =>
                {
                    // Configure the APT package proxy on the masters
                    // and configure the proxy selector for all nodes.

                    node.Status = "setup: package proxy";
                    node.SudoCommand("setup-package-proxy.sh");

                    // Upgrade Linux packages if requested.  We're doing this after
                    // deploying the APT package proxy so it'll be faster.

                    switch (cluster.Definition.NodeOptions.Upgrade)
                    {
                        case OsUpgrade.Partial:

                            node.Status = "upgrade: partial";

                            node.SudoCommand("safe-apt-get upgrade -yq");
                            break;

                        case OsUpgrade.Full:

                            node.Status = "upgrade: full";

                            node.SudoCommand("safe-apt-get dist-upgrade -yq");
                            break;
                    }

                    // Check to see whether the upgrade requires a reboot and
                    // do that now if necessary.

                    if (node.FileExists("/var/run/reboot-required"))
                    {
                        node.Status = "restarting...";
                        node.Reboot();
                    }

                    // Setup NTP.

                    node.Status = "setup: NTP";
                    node.SudoCommand("setup-ntp.sh");

                    // Setup Docker.

                    node.Status = "setup: docker";

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
        /// Reboots the cluster nodes.
        /// </summary>
        /// <param name="node">The cluster node.</param>
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
            var separator = new string(' ', Math.Max(16 - nodeAddress.Length, 1));

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
        /// Creates the initial cluster on the bootstrap master node passed and 
        /// captures the master and worker cluster tokens required to join 
        /// additional nodes to the cluster.
        /// </summary>
        /// <param name="bootstrapMaster">The target bootstrap manager server.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void InitializeCluster(SshProxy<NodeDefinition> bootstrapMaster, TimeSpan stepDelay)
        {
            if (kubeContextExtension.ClusterJoinToken != null)
            {
                return; // Cluster has already been initialized.
            }

            Thread.Sleep(stepDelay);

            bootstrapMaster.Status = "initialize cluster";

            // $todo(jeff.lill): Implement this.
#if TODO
            bootstrapManager.DockerCommand(RunOptions.FaultOnError, $"docker swarm init --advertise-addr {bootstrapManager.Metadata.PrivateAddress}:{cluster.Definition.Docker.SwarmPort}");

            var response = bootstrapManager.DockerCommand(RunOptions.FaultOnError, $"docker swarm join-token manager");

            hiveLogin.SwarmManagerToken = ExtractSwarmToken(response.OutputText);

            response = bootstrapManager.DockerCommand(RunOptions.FaultOnError, $"docker swarm join-token worker");

            hiveLogin.SwarmWorkerToken = ExtractSwarmToken(response.OutputText);
#endif

            // Persist the swarm tokens into the cluster login.

            kubeContextExtension.Save();
        }

        /// <summary>
        /// Extracts the cluster join token from a <b>docker swarm join-token [manager|worker]</b> 
        /// command.  The token returned can be used when adding additional nodes to the cluster.
        /// </summary>
        /// <param name="commandResponse">The command response string.</param>
        /// <returns>The join token.</returns>
        private string ExtractClusterToken(string commandResponse)
        {
            // $todo(jeff.lill): Implement this.

            const string tokenOpt = "--token ";

            var startPos = commandResponse.IndexOf(tokenOpt);
            var errorMsg = $"Cannot extract swarm token from:\r\n\r\n{commandResponse}";

            if (startPos == -1)
            {
                throw new KubeException(errorMsg);
            }

            if (startPos == -1)
            {
                throw new KubeException(errorMsg);
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
                throw new KubeException($"Cannot extract swarm token from:\r\n\r\n{commandResponse}");
            }

            return commandResponse.Substring(startPos, endPos - startPos).Trim();
        }

        /// <summary>
        /// Adds the node labels.
        /// </summary>
        /// <param name="master">A master node.</param>
        private void AddNodeLabels(SshProxy<NodeDefinition> master)
        {
            master.InvokeIdempotentAction("setup/node-labels",
                () =>
                {
                    master.Status = "labeling";

                    foreach (var node in cluster.Nodes)
                    {
                        var labelDefinitions = new List<string>();

                        labelDefinitions.Add($"{NodeLabels.LabelDatacenter}={cluster.Definition.Datacenter.ToLowerInvariant()}");
                        labelDefinitions.Add($"{NodeLabels.LabelEnvironment}={cluster.Definition.Environment.ToString().ToLowerInvariant()}");

                        // $todo(jeff.lill): Implement this.
                    }
                });
        }

        /// <summary>
        /// Adds the node to the cluster.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void JoinCluster(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            if (node == cluster.FirstMaster)
            {
                // This node is implictly joined to the cluster.

                node.Status = "joined";
                return;
            }

            node.InvokeIdempotentAction("setup/swarm-join",
                () =>
                {
                    Thread.Sleep(stepDelay);

                    node.Status = "joining";

                    // $todo(jeff.lill): IMplement this.
                });

            node.Status = "joined";
        }

        /// <summary>
        /// Generates the SSH key to be used for authenticating SSH client connections.
        /// </summary>
        /// <param name="master">A cluster manager node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void GenerateClientSshKey(SshProxy<NodeDefinition> master, TimeSpan stepDelay)
        {
            // Here's some information explaining what how I'm doing this:
            //
            //      https://help.ubuntu.com/community/SSH/OpenSSH/Configuring
            //      https://help.ubuntu.com/community/SSH/OpenSSH/Keys

            if (kubeContextExtension.SshClientKey != null)
            {
                return; // Key has already been created.
            }

            Thread.Sleep(stepDelay);

            kubeContextExtension.SshClientKey = new SshClientKey();

            // $hack(jeff.lill): 
            //
            // We're going to generate a 2048 bit key pair on one of the
            // master nodes and then download and then delete it.  This
            // means that the private key will be persisted to disk (tmpfs)
            // for a moment but I'm going to worry about that too much
            // since we'll be rebooting the master later on during setup.
            //
            // Technically, I could have installed OpenSSL or something
            // on Windows or figured out the .NET Crypto libraries but
            // but OpenSSL didn't support generating the PUB format
            // SSH expects for the client public key.

            const string keyGenScript =
@"
# Generate a 2048-bit key without a passphrase (the -N option).

rm -f /run/ssh-key*
ssh-keygen -t rsa -b 2048 -N """" -C ""neonhive"" -f /run/ssh-key

# Relax permissions so we can download the key parts.

chmod 666 /run/ssh-key*
";
            var bundle = new CommandBundle("./keygen.sh");

            bundle.AddFile("keygen.sh", keyGenScript, isExecutable: true);

            master.SudoCommand(bundle);

            using (var stream = new MemoryStream())
            {
                master.Download("/run/ssh-key.pub", stream);

                kubeContextExtension.SshClientKey.PublicPUB = NeonHelper.ToLinuxLineEndings(Encoding.UTF8.GetString(stream.ToArray()));
            }

            using (var stream = new MemoryStream())
            {
                master.Download("/run/ssh-key", stream);

                kubeContextExtension.SshClientKey.PrivatePEM = NeonHelper.ToLinuxLineEndings(Encoding.UTF8.GetString(stream.ToArray()));
            }

            master.SudoCommand("rm /run/ssh-key*");

            // We're going to use WinSCP to convert the OpenSSH PEM formatted key
            // to the PPK format PuTTY/WinSCP require.  Note that this won't work
            // when the tool is running in a Docker Linux container.  We're going
            // to handle the conversion in the outer shim as a post run action.

            if (NeonHelper.IsWindows)
            {
                var pemKeyPath = Path.Combine(KubeHelper.TempFolder, Guid.NewGuid().ToString("D"));
                var ppkKeyPath = Path.Combine(KubeHelper.TempFolder, Guid.NewGuid().ToString("D"));

                try
                {
                    File.WriteAllText(pemKeyPath, kubeContextExtension.SshClientKey.PrivatePEM);

                    ExecuteResult result;

                    try
                    {
                        result = NeonHelper.ExecuteCapture("winscp.com", $@"/keygen ""{pemKeyPath}"" /comment=""{cluster.Definition.Name} Key"" /output=""{ppkKeyPath}""");
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

                    kubeContextExtension.SshClientKey.PrivatePPK = NeonHelper.ToLinuxLineEndings(File.ReadAllText(ppkKeyPath));

                    // Persist the SSH client key.

                    kubeContextExtension.Save();
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
echo '{kubeContextExtension.SshUsername}:{kubeContextExtension.SshStrongPassword}' | chpasswd
";
                    var bundle = new CommandBundle("./set-strong-password.sh");

                    bundle.AddFile("set-strong-password.sh", script, isExecutable: true);

                    var response = node.SudoCommand(bundle);

                    if (response.ExitCode != 0)
                    {
                        Console.Error.WriteLine($"*** ERROR: Unable to set a strong password [exitcode={response.ExitCode}].");
                        Program.Exit(response.ExitCode);
                    }

                    node.UpdateCredentials(SshCredentials.FromUserPassword(kubeContextExtension.SshUsername, kubeContextExtension.SshStrongPassword));
                });
        }

        /// <summary>
        /// Generates the private key that will be used to secure SSH on the cluster nodes.
        /// </summary>
        private void ConfigureSshCerts()
        {
            cluster.FirstMaster.InvokeIdempotentAction("setup/ssh-server-key",
                () =>
                {
                    cluster.FirstMaster.Status = "generate: server SSH key";

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
                    cluster.FirstMaster.SudoCommand(bundle);

                    cluster.FirstMaster.Status = "download: server SSH key";

                    kubeContextExtension.SshNodePrivateKey  = cluster.FirstMaster.DownloadText("/dev/shm/ssh/ssh_host_rsa_key");
                    kubeContextExtension.SshNodePublicKey   = cluster.FirstMaster.DownloadText("/dev/shm/ssh/ssh_host_rsa_key.pub");
                    kubeContextExtension.SshNodeFingerprint = cluster.FirstMaster.DownloadText("/dev/shm/ssh/ssh.fingerprint");

                    // Delete the SSH key files for security.

                    cluster.FirstMaster.SudoCommand("rm -r /dev/shm/ssh");

                    // Persist the server SSH key and fingerprint.

                    kubeContextExtension.Save();
                });
        }

        /// <summary>
        /// Configures SSH on a node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void ConfigureSsh(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            // Configure the SSH credentials on all cluster nodes.

            node.InvokeIdempotentAction("setup/ssh",
                () =>
                {
                    Thread.Sleep(stepDelay);

                    CommandBundle bundle;

                    // Here's some information explaining what how I'm doing this:
                    //
                    //      https://help.ubuntu.com/community/SSH/OpenSSH/Configuring
                    //      https://help.ubuntu.com/community/SSH/OpenSSH/Keys

                    node.Status = "setup: client SSH key";

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
                    bundle.AddFile("ssh-key.pub", kubeContextExtension.SshClientKey.PublicPUB);

                    // NOTE: I'm explictly not running the bundle as [sudo] because the OpenSSH
                    //       server is very picky about the permissions on the user's [$HOME]
                    //       and [$HOME/.ssl] folder and contents.  This took me a couple 
                    //       hours to figure out.

                    node.RunCommand(bundle);

                    // These steps are required for both password and public key authentication.

                    // Upload the server key and edit the [sshd] config to disable all host keys 
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
                    bundle.AddFile("ssh_host_rsa_key", kubeContextExtension.SshNodePrivateKey);
                    node.SudoCommand(bundle);
                });
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(shimability: DockerShimability.None);
        }
    }
}