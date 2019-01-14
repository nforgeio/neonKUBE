//-----------------------------------------------------------------------------
// FILE:	    ClusterPrepareCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Neon.Common;
using Neon.Cryptography;
using Neon.Kube;
using Neon.Net;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>cluster prepare</b> command.
    /// </summary>
    public class ClusterPrepareCommand : CommandBase
    {
        private const string usage = @"
Configures cloud platform virtual machines so that they are prepared 
to host a Kubernetes cluster.

USAGE:

    neon cluster prepare [OPTIONS] CLUSTER-DEF

ARGUMENTS:

    CLUSTER-DEF     - Path to the cluster definition file

OPTIONS:

    --package-cache=CACHE-URI   - Optionally specifies an APT Package cache
                                  server to improve setup performance.

    --unredacted                - Runs Vault and other commands with potential
                                  secrets without redacting logs.  This is useful 
                                  for debugging cluster setup  issues.  
                                  Do not use for production cluster.

    --remove-templates          - Removes any cached local virtual machine 
                                  templates without actually setting up a 
                                  cluster.  You can use this to ensure that 
                                  cluster will be created  from the most recent
                                  template.

Server Requirements:
--------------------

    * Supported version of Linux (server)
    * Known root SSH credentials
    * OpenSSH installed (or another SSH server)
    * [sudo] elevates permissions without a password
";
        private const string    logBeginMarker  = "# CLUSTER-BEGIN-PREPARE ##########################################################";
        private const string    logEndMarker    = "# CLUSTER-END-PREPARE ############################################################";
        private const string    logFailedMarker = "# CLUSTER-END-PREPARE-FAILED #####################################################";

        private ClusterProxy    cluster;
        private HostingManager  hostingManager;
        private string          clusterDefPath;
        private string          packageCacheUri;
        private bool            force;

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "cluster", "prepare" }; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--package-cache", "--unredacted", "--remove-templates" }; }
        }

        /// <inheritdoc/>
        public override bool NeedsSshCredentials(CommandLine commandLine)
        {
            return !commandLine.HasOption("--remove-templates");
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
                Help();
                Program.Exit(0);
            }

            // Special-case handling of the [--remove-templates] option.

            if (commandLine.HasOption("--remove-templates"))
            {
                Console.WriteLine("Removing cached virtual machine templates.");

                foreach (var fileName in Directory.GetFiles(KubeHelper.GetVmTemplatesFolder(), "*.*", SearchOption.TopDirectoryOnly))
                {
                    File.Delete(fileName);
                }

                Program.Exit(0);
            }

            // Implement the command.

            packageCacheUri = commandLine.GetOption("--package-cache");     // This overrides the cluster definition, if specified.

            if (KubeHelper.KubeContext != null)
            {
                Console.Error.WriteLine("*** ERROR: You are logged into a cluster.  You need to logout before preparing another.");
                Program.Exit(1);
            }

            if (commandLine.Arguments.Length == 0)
            {
                Console.Error.WriteLine($"*** ERROR: CLUSTER-DEF expected.");
                Program.Exit(1);
            }

            clusterDefPath = commandLine.Arguments[0];
            force          = commandLine.GetFlag("--force");

            ClusterDefinition.ValidateFile(clusterDefPath, strict: true);

            var clusterDefinition = ClusterDefinition.FromFile(clusterDefPath, strict: true);

            clusterDefinition.Provisioner = $"neon-cli:{Program.Version}";  // Identify this tool/version as the cluster provisioner

            // NOTE:
            //
            // Azure has implemented a more restrictive password policy and our
            // default machine password does not meet the requirements:
            //
            // The supplied password must be between 6-72 characters long and must 
            // satisfy at least 3 of password complexity requirements from the following: 
            //
            //      1. Contains an uppercase character
            //      2. Contains a lowercase character
            //      3. Contains a numeric digit
            //      4. Contains a special character
            //      5. Control characters are not allowed
            //
            // It's also probably not a great idea to use a static password when
            // provisioning VMs in public clouds because it might be possible for
            // somebody to use this fact the SSH into nodes while the cluster is 
            // being setup and before we set the secure password at the end.
            //
            // This is less problematic for non-cloud environments because it's
            // likely that the hosts won't initially be able to receive inbound 
            // Internet traffic and besides, we need to have a known password
            // embedded into the VM templates.
            //
            // We're going to handle this for cloud environments by looking
            // at [Program.MachinePassword].  If this is set to the default
            // machine password then we're going to replace it with a randomlly
            // generated password with a few extra characters to ensure that
            // it meets the target cloud's password requirements.  We'll use
            // a non-default password if the operator specified one.

            if (clusterDefinition.Hosting.IsCloudProvider && Program.MachinePassword == KubeConst.DefaulVmTemplatePassword)
            {
                Program.MachinePassword = NeonHelper.GetRandomPassword(20);

                // Append a string that guarantees that the generated password meets
                // cloud minimum requirements.

                Program.MachinePassword += ".Aa0";
            }

            // NOTE: Cluster prepare starts new log files.

            cluster = new ClusterProxy(clusterDefinition, Program.CreateNodeProxy<NodeDefinition>, appendLog: false, defaultRunOptions: RunOptions.LogOutput | RunOptions.FaultOnError);

            if (KubeHelper.KubeConfig.GetContext(cluster.Definition.Name) != null)
            {
                Console.Error.WriteLine($"*** ERROR: A context named [{cluster.Definition.Name}] already exists.");
                Program.Exit(1);
            }

            // Configure global options.

            if (commandLine.HasOption("--unredacted"))
            {
                cluster.SecureRunOptions = RunOptions.None;
            }

            //-----------------------------------------------------------------
            // Try to ensure that no servers are already deployed on the IP addresses defined
            // for cluster nodes because provisoning over an existing cluster will likely
            // corrupt the existing cluster and also probably prevent the new cluster from
            // provisioning correctly.
            //
            // Note that we're not going to perform this check for the [Machine] hosting 
            // environment because we're expecting the bare machines to be already running 
            // with the assigned addresses and we're also not going to do this for cloud
            // environments because we're assuming that the cluster will run in its own
            // private network so there'll ne no possibility of conflicts.

            if (cluster.Definition.Hosting.Environment != HostingEnvironments.Machine && 
                !cluster.Definition.Hosting.IsCloudProvider)
            {
                Console.WriteLine();
                Console.WriteLine("Scanning for IP address conflicts...");
                Console.WriteLine();

                var pingOptions   = new PingOptions(ttl: 32, dontFragment: true);
                var pingTimeout   = TimeSpan.FromSeconds(2);
                var pingConflicts = new List<NodeDefinition>();
                var pingAttempts  = 2;

                // I'm going to use up to 20 threads at a time here for simplicity
                // rather then doing this as async operations.

                var parallelOptions = new ParallelOptions()
                {
                    MaxDegreeOfParallelism = 20
                };

                Parallel.ForEach(cluster.Definition.NodeDefinitions.Values, parallelOptions,
                    node =>
                    {
                        using (var ping = new Ping())
                        {
                            // We're going to try pinging up to [pingAttempts] times for each node
                            // just in case the network it sketchy and we're losing reply packets.

                            for (int i = 0; i < pingAttempts; i++)
                            {
                                var reply = ping.Send(node.PrivateAddress, (int)pingTimeout.TotalMilliseconds);

                                if (reply.Status == IPStatus.Success)
                                {
                                    lock (pingConflicts)
                                    {
                                        pingConflicts.Add(node);
                                    }

                                    break;
                                }
                            }
                        }
                    });

                if (pingConflicts.Count > 0)
                {
                    Console.Error.WriteLine($"*** ERROR: Cannot provision the cluster because [{pingConflicts.Count}] other");
                    Console.Error.WriteLine($"***        machines conflict with the following cluster nodes:");
                    Console.Error.WriteLine();

                    foreach (var node in pingConflicts.OrderBy(n => NetHelper.AddressToUint(IPAddress.Parse(n.PrivateAddress))))
                    {
                        Console.Error.WriteLine($"{node.PrivateAddress, 16}:    {node.Name}");
                    }

                    Program.Exit(1);
                }
            }

            //-----------------------------------------------------------------
            // Perform basic environment provisioning.  This creates basic cluster components
            // such as virtual machines, networks, load balancers, public IP addresses, security
            // groups,... as required for the environment.

            hostingManager = new HostingManagerFactory(() => HostingLoader.Initialize()).GetMaster(cluster, Program.LogPath);

            if (hostingManager == null)
            {
                Console.Error.WriteLine($"*** ERROR: No hosting manager for the [{cluster.Definition.Hosting.Environment}] hosting environment could be located.");
                Program.Exit(1);
            }

            hostingManager.HostUsername = Program.MachineUsername;
            hostingManager.HostPassword = Program.MachinePassword;
            hostingManager.ShowStatus   = !Program.Quiet;
            hostingManager.MaxParallel  = Program.MaxParallel;
            hostingManager.WaitSeconds  = Program.WaitSeconds;

            if (hostingManager.RequiresAdminPrivileges)
            {
                Program.VerifyAdminPrivileges($"Provisioning to [{cluster.Definition.Hosting.Environment}] requires elevated administrator privileges.");
            }

            if (!hostingManager.Provision(force))
            {
                Program.Exit(1);
            }

            // Get the mounted drive prefix from the hosting manager.

            cluster.Definition.DrivePrefix = hostingManager.DrivePrefix;

            // Ensure that the nodes have valid IP addresses.

            cluster.Definition.ValidatePrivateNodeAddresses();

            var ipAddressToServer = new Dictionary<IPAddress, SshProxy<NodeDefinition>>();

            foreach (var node in cluster.Nodes.OrderBy(n => n.Name))
            {
                SshProxy<NodeDefinition> duplicateServer;

                if (node.PrivateAddress == IPAddress.Any)
                {
                    throw new ArgumentException($"Node [{node.Name}] has not been assigned an IP address.");
                }

                if (ipAddressToServer.TryGetValue(node.PrivateAddress, out duplicateServer))
                {
                    throw new ArgumentException($"Nodes [{duplicateServer.Name}] and [{node.Name}] have the same IP address [{node.Metadata.PrivateAddress}].");
                }

                ipAddressToServer.Add(node.PrivateAddress, node);
            }

            //-----------------------------------------------------------------
            // Perform basic node provisioning including operating system updates & configuration,
            // and configure OpenVPN on the manager nodes so that cluster setup will be
            // able to reach the nodes on all ports.

            // Write the operation begin marker to all hive node logs.

            cluster.LogLine(logBeginMarker);

            var operation = $"Preparing [{cluster.Definition.Name}] nodes";

            var controller = 
                new SetupController<NodeDefinition>(operation, cluster.Nodes)
                {
                    ShowStatus  = !Program.Quiet,
                    MaxParallel = Program.MaxParallel
                };

            if (!string.IsNullOrEmpty(packageCacheUri))
            {
                cluster.Definition.PackageProxy = packageCacheUri;
            }

            // Prepare the nodes.

            controller.AddWaitUntilOnlineStep(timeout: TimeSpan.FromMinutes(15));
            hostingManager.AddPostProvisionSteps(controller);
            controller.AddStep("verify OS",
                (node, stepDelay) =>
                {
                    Thread.Sleep(stepDelay);
                    CommonSteps.VerifyOS(node);
                });

            controller.AddStep("prepare", 
                (node, stepDelay) =>
                {
                    Thread.Sleep(stepDelay);
                    CommonSteps.PrepareNode(node, cluster.Definition, shutdown: false);
                },
                stepStaggerSeconds: cluster.Definition.Setup.StepStaggerSeconds);
            
            if (!controller.Run())
            {
                // Write the operation end/failed marker to all hive node logs.

                cluster.LogLine(logFailedMarker);

                Console.Error.WriteLine("*** ERROR: One or more configuration steps failed.");
                Program.Exit(1);
            }

            // Write the hive login file.

            var hiveLoginPath = Program.GetHiveLoginPath(HiveConst.RootUser, cluster.Definition.Name);
            var hiveLogin     = new HiveLogin()
            {
                Path                 = hiveLoginPath,
                Username             = KubeConst.RootUser,
                Definition           = cluster.Definition,
                SshUsername          = Program.MachineUsername,
                SshPassword          = Program.MachinePassword,
                SshProvisionPassword = Program.MachinePassword,
                SetupPending         = true
            };

            // Write the operation end marker to all hive node logs.

            cluster.LogLine(logEndMarker);
        }

        /// <summary>
        /// Initializes the VPN certificate authority as well as the server and root user's certificates and keys.
        /// </summary>
        private void CreateVpnCredentials()
        {
            // This is a bit tricky: We're going to invoke the [neon vpn ca ...] command to
            // initialize the hive's certificate authority files.  This command must be
            // run in the [neon-cli] container so we need to detect whether we're already
            // running in the tool container and do the right thing.
            //
            // Note that the we can't pass the original hive definition file to the command
            // because the command will shim into a [neon-cli] container and any environment
            // variable references within the definition won't be able to be resolved because
            // the environment variables aren't mapped into the container.
            //
            // The solution is to persist a temporary copy of the loaded hive definition 
            // that has already resolved environment variables to the neonFORGE temp folder
            // and pass that.  The user's neonFORGE folder is encrypted in place so doing this
            // will be as safe as storing hive logins there.

            string tempCaFolder;
            string tempDefPath;

            if (KubeHelper.InToolContainer)
            {
                tempCaFolder = "/shim/ca";
            }
            else
            {
                tempCaFolder = Path.Combine(KubeHelper.TempFolder, Guid.NewGuid().ToString("D"));
            }

            tempDefPath = Path.Combine(KubeHelper.TempFolder, $"{Guid.NewGuid().ToString("D").ToLowerInvariant()}.def.json");

            File.WriteAllText(tempDefPath, NeonHelper.JsonSerialize(cluster.Definition, Formatting.Indented));
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(shimability: DockerShimability.None);
        }
    }
}
