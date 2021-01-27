//-----------------------------------------------------------------------------
// FILE:	    ClusterPrepareCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.ComponentModel;
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
using Neon.SSH;

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

    --unredacted                - Runs commands with potential secrets without 
                                  redacting logs.  This is useful for debugging 
                                  cluster setup issues.  Do not use for production
                                  clusters.

    --remove-templates          - Removes any cached local virtual machine 
                                  templates without actually setting up a 
                                  cluster.  You can use this to ensure that 
                                  cluster will be created from the most recent
                                  template.

Server Requirements:
--------------------

    * Supported version of Linux (server)
    * Known [sysadmin] sudoer user
    * OpenSSH installed
";
        private const string    logBeginMarker  = "# CLUSTER-BEGIN-PREPARE ##########################################################";
        private const string    logEndMarker    = "# CLUSTER-END-PREPARE-SUCCESS ####################################################";
        private const string    logFailedMarker = "# CLUSTER-END-PREPARE-FAILED #####################################################";

        private ClusterProxy    cluster;
        private HostingManager  hostingManager;
        private string          clusterDefPath;
        private string          packageCaches;

        /// <inheritdoc/>
        public override string[] Words => new string[] { "cluster", "prepare" };

        /// <inheritdoc/>
        public override string[] ExtendedOptions => new string[] { "--package-cache", "--unredacted", "--remove-templates" };

        /// <inheritdoc/>
        public override bool NeedsSshCredentials(CommandLine commandLine) => !commandLine.HasOption("--remove-templates");

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }
        
        /// <inheritdoc/>
        public override async Task RunAsync(CommandLine commandLine)
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

                foreach (var fileName in Directory.GetFiles(KubeHelper.NodeImageFolder, "*.*", SearchOption.TopDirectoryOnly))
                {
                    File.Delete(fileName);
                }

                Program.Exit(0);
            }

            // Implement the command.

            if (KubeHelper.CurrentContext != null)
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

            ClusterDefinition.ValidateFile(clusterDefPath, strict: true);

            var clusterDefinition = ClusterDefinition.FromFile(clusterDefPath, strict: true);

            // NOTE: Cluster prepare starts new log files.

            cluster = new ClusterProxy(clusterDefinition, Program.CreateNodeProxy<NodeDefinition>, appendToLog: false, defaultRunOptions: RunOptions.LogOutput | RunOptions.FaultOnError);

            if (KubeHelper.Config.GetContext(cluster.Definition.Name) != null)
            {
                Console.Error.WriteLine($"*** ERROR: A login named [{cluster.Definition.Name}] already exists.");
                Program.Exit(1);
            }

            // Configure global options.

            if (commandLine.HasOption("--unredacted"))
            {
                cluster.SecureRunOptions = RunOptions.None;
            }

            var failed = false;

            try
            {
                await KubeHelper.Desktop.StartOperationAsync($"Preparing [{cluster.Name}]");

                //-----------------------------------------------------------------
                // Try to ensure that no servers are already deployed on the IP addresses defined
                // for cluster nodes because provisoning over an existing cluster will likely
                // corrupt the existing cluster and also probably prevent the new cluster from
                // provisioning correctly.
                //
                // Note that we're not going to perform this check for the [BareMetal] hosting 
                // environment because we're expecting the bare machines to be already running 
                // with the assigned addresses and we're also not going to do this for cloud
                // environments because we're assuming that the cluster will run in its own
                // private network so there'll be no possibility of conflicts.
                //
                // We also won't do this for cloud deployments because those nodes will be
                // running in an isolated private network.

                if (cluster.Definition.Hosting.Environment != HostingEnvironment.BareMetal && 
                    !cluster.Definition.Hosting.IsCloudProvider)
                {
                    Console.WriteLine();
                    Console.WriteLine(" Scanning for IP address conflicts...");
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
                            using (var pinger = new Pinger())
                            {
                                // We're going to try pinging up to [pingAttempts] times for each node
                                // just in case the network is sketchy and we're losing reply packets.

                                for (int i = 0; i < pingAttempts; i++)
                                {
                                    var reply = pinger.SendPingAsync(node.Address, (int)pingTimeout.TotalMilliseconds).Result;

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

                        foreach (var node in pingConflicts.OrderBy(n => NetHelper.AddressToUint(NetHelper.ParseIPv4Address(n.Address))))
                        {
                            Console.Error.WriteLine($"{node.Address, 16}:    {node.Name}");
                        }

                        Program.Exit(1);
                    }
                }

                //-----------------------------------------------------------------
                // Perform basic environment provisioning.  This creates basic cluster components
                // such as virtual machines, networks, load balancers, public IP addresses, security
                // groups, etc. as required for the hosting environment.

                hostingManager = new HostingManagerFactory(() => HostingLoader.Initialize()).GetManager(cluster, Program.LogPath);

                if (hostingManager == null)
                {
                    Console.Error.WriteLine($"*** ERROR: No hosting manager for the [{cluster.Definition.Hosting.Environment}] environment could be located.");
                    Program.Exit(1);
                }

                hostingManager.ShowStatus  = !Program.Quiet;
                hostingManager.MaxParallel = Program.MaxParallel;
                hostingManager.WaitSeconds = Program.WaitSeconds;

                if (hostingManager.RequiresAdminPrivileges)
                {
                    Program.VerifyAdminPrivileges($"Provisioning to [{cluster.Definition.Hosting.Environment}] requires elevated administrator privileges.");
                }

                // Load the cluster login information if it exists and if it indicates that
                // setup is still pending, we'll use that information (especially the generated
                // secure SSH password).
                //
                // Otherwise, we'll write (or overwrite) the context file with a fresh context.

                var clusterLoginPath = KubeHelper.GetClusterLoginPath((KubeContextName)$"{KubeConst.RootUser}@{clusterDefinition.Name}");
                var clusterLogin     = ClusterLogin.Load(clusterLoginPath);

                if (clusterLogin == null || !clusterLogin.SetupDetails.SetupPending)
                {
                    clusterLogin = new ClusterLogin(clusterLoginPath)
                    {
                        ClusterDefinition = clusterDefinition,
                        SshUsername       = KubeConst.SysAdminUser,
                        SetupDetails      = new KubeSetupDetails() { SetupPending = true }
                    };

                    clusterLogin.Save();
                }

                // We're going to generate a secure random password and we're going to append
                // an extra 4-character string to ensure that the password meets Azure (and probably
                // other cloud) minimum requirements:
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
                // We're going to use the cloud API to configure this secure password
                // when creating the VMs.  For on-premise hypervisor environments such
                // as Hyper-V and XenServer, we're going use the [neon-init]
                // service to mount a virtual DVD that will change the password before
                // configuring the network on first boot.
                //
                // For bare metal, we're going to leave the password along and just use
                // whatever the user specified when the nodes were built out.

                var orgSshPassword = Program.MachinePassword;

                if (hostingManager.GenerateSecurePassword && string.IsNullOrEmpty(clusterLogin.SshPassword))
                {
                    clusterLogin.SshPassword = NeonHelper.GetCryptoRandomPassword(clusterDefinition.Security.PasswordLength);

                    // Append a string that guarantees that the generated password meets
                    // cloud minimum requirements.

                    clusterLogin.SshPassword += ".Aa0";
                    clusterLogin.Save();
                }

                // We're also going to generate the server's SSH key here and pass that to the hosting
                // manager's provisioner.  We need to do this up front because some hosting environments
                // like AWS don't allow SSH password authentication by default, so we'll need the SSH key
                // to initialize the nodes after they've been provisioned for those environments.

                if (clusterLogin.SshKey == null)
                {
                    // Generate a 2048 bit SSH key pair.

                    clusterLogin.SshKey = KubeHelper.GenerateSshKey(cluster.Name, KubeConst.SysAdminUser);

                    // We're going to use WinSCP (if it's installed) to convert the OpenSSH PEM formatted key
                    // to the PPK format PuTTY/WinSCP requires.

                    if (NeonHelper.IsWindows)
                    {
                        var pemKeyPath = Path.Combine(KubeHelper.TempFolder, Guid.NewGuid().ToString("d"));
                        var ppkKeyPath = Path.Combine(KubeHelper.TempFolder, Guid.NewGuid().ToString("d"));

                        try
                        {
                            File.WriteAllText(pemKeyPath, clusterLogin.SshKey.PrivateOpenSSH);

                            ExecuteResponse result;

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

                            clusterLogin.SshKey.PrivatePPK = NeonHelper.ToLinuxLineEndings(File.ReadAllText(ppkKeyPath));

                            // Persist the SSH key.

                            clusterLogin.Save();
                        }
                        finally
                        {
                            NeonHelper.DeleteFile(pemKeyPath);
                            NeonHelper.DeleteFile(ppkKeyPath);
                        }
                    }
                }

                if (!hostingManager.ProvisionAsync(clusterLogin, clusterLogin.SshPassword, orgSshPassword).Result)
                {
                    Program.Exit(1);
                }

                // Ensure that the nodes have valid IP addresses.

                cluster.Definition.ValidatePrivateNodeAddresses();

                var ipAddressToServer = new Dictionary<IPAddress, NodeSshProxy<NodeDefinition>>();

                foreach (var node in cluster.Nodes.OrderBy(n => n.Name))
                {
                    NodeSshProxy<NodeDefinition> duplicateServer;

                    if (node.Address == IPAddress.Any)
                    {
                        throw new ArgumentException($"Node [{node.Name}] has not been assigned an IP address.");
                    }

                    if (ipAddressToServer.TryGetValue(node.Address, out duplicateServer))
                    {
                        throw new ArgumentException($"Nodes [{duplicateServer.Name}] and [{node.Name}] have the same IP address [{node.Metadata.Address}].");
                    }

                    ipAddressToServer.Add(node.Address, node);
                }

                // We're going to use the masters to be package caches unless the user
                // specifies something else.

                packageCaches = commandLine.GetOption("--package-cache");     // This overrides the cluster definition, if specified.

                if (!string.IsNullOrEmpty(packageCaches))
                {
                    cluster.Definition.PackageProxy = packageCaches;
                }

                if (string.IsNullOrEmpty(cluster.Definition.PackageProxy))
                {
                    var sbProxies = new StringBuilder();

                    foreach (var master in cluster.Masters)
                    {
                        sbProxies.AppendWithSeparator($"{master.Address}:{NetworkPorts.AppCacherNg}");
                    }

                    cluster.Definition.PackageProxy = sbProxies.ToString();
                }

                //-----------------------------------------------------------------
                // Prepare the cluster.

                // Write the operation begin marker to all cluster node logs.

                cluster.LogLine(logBeginMarker);

                var operation = $"Preparing [{cluster.Definition.Name}] cluster nodes";

                var setupController = 
                    new SetupController<NodeDefinition>(operation, cluster.Nodes)
                    {
                        ShowStatus  = !Program.Quiet,
                        MaxParallel = Program.MaxParallel,
                        ShowElapsed = true
                    };

                // Configure the setup controller state.

                setupController.Add(KubeSetup.ClusterProxyProperty, cluster);
                setupController.Add(KubeSetup.HostingManagerProperty, hostingManager);

                // Configure the setup steps.

                setupController.AddWaitUntilOnlineStep(timeout: TimeSpan.FromMinutes(15));
                setupController.AddNodeStep("node OS verify", (state, node) => node.VerifyNodeOS(setupController));
                setupController.AddNodeStep("node credentials",
                    (state, node) =>
                    {
                        node.ConfigureSshKey(setupController, clusterLogin);
                    });
                setupController.AddNodeStep("node prepare",
                    (state, node) =>
                    {
                        node.PrepareNode(setupController, shutdown: false);
                    });
            
                // Some hosting managers may have to some additional work after the node has
                // been otherwise prepared.

                hostingManager.AddPostPrepareSteps(setupController);

                // Start cluster preparation.

                if (!setupController.Run())
                {
                    // Write the operation end/failed marker to all cluster node logs.

                    cluster.LogLine(logFailedMarker);

                    Console.Error.WriteLine("*** ERROR: One or more configuration steps failed.");
                    Program.Exit(1);
                }

                // Write the operation end marker to all cluster node logs.

                cluster.LogLine(logEndMarker);
            }
            catch
            {
                failed = true;
                throw;
            }
            finally
            {
                hostingManager?.Dispose();

                if (!failed)
                {
                    await KubeHelper.Desktop.EndOperationAsync($"Cluster [{cluster.Name}] has been prepared and is ready for setup.");
                }
                else
                {
                    await KubeHelper.Desktop.EndOperationAsync($"Cluster [{cluster.Name}] prepare has failed.", failed: true);
                }
            }
        }
    }
}
