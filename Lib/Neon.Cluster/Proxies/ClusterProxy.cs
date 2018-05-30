//-----------------------------------------------------------------------------
// FILE:	    ClusterProxy.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Consul;

using Neon.Common;
using Neon.Docker;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace Neon.Cluster
{
    /// <summary>
    /// Remotely manages a neonCLUSTER.
    /// </summary>
    public class ClusterProxy : IDisposable
    {
        //---------------------------------------------------------------------
        // Local types

        /// <summary>
        /// Enumerates how <see cref="GetHealthyManager(HealthyManagerMode)"/> should
        /// behave when no there are no healthy cluster managers.
        /// </summary>
        public enum HealthyManagerMode
        {
            /// <summary>
            /// Throw an exception when no managers are healthy.
            /// </summary>
            Throw,

            /// <summary>
            /// Return the first manager when no managers are healthy.
            /// </summary>
            ReturnFirst,

            /// <summary>
            /// Return <c>null</c> when no managers are healthy.
            /// </summary>
            ReturnNull
        }

        //---------------------------------------------------------------------
        // Implementation

        private object                                                      syncRoot = new object();
        private VaultClient                                                 vaultClient;
        private ConsulClient                                                consulClient;
        private RunOptions                                                  defaultRunOptions;
        private Func<string, string, IPAddress, SshProxy<NodeDefinition>>   nodeProxyCreator;

        /// <summary>
        /// Constructs a cluster proxy from a cluster login.
        /// </summary>
        /// <param name="clusterLogin">The cluster login information.</param>
        /// <param name="nodeProxyCreator">
        /// The optional application supplied function that creates a node proxy
        /// given the node name, public address or FQDN, private address, and
        /// the node definition.
        /// </param>
        /// <param name="defaultRunOptions">
        /// Optionally specifies the <see cref="RunOptions"/> to be assigned to the 
        /// <see cref="SshProxy{TMetadata}.DefaultRunOptions"/> property for the
        /// nodes managed by the cluster proxy.  This defaults to <see cref="RunOptions.None"/>.
        /// </param>
        /// <remarks>
        /// The <paramref name="nodeProxyCreator"/> function will be called for each node in
        /// the cluster definition giving the application the chance to create the management
        /// proxy using the node's SSH credentials and also to specify logging.  A default
        /// creator that doesn't initialize SSH credentials and logging is used if a <c>null</c>
        /// argument is passed.
        /// </remarks>
        public ClusterProxy(ClusterLogin clusterLogin, Func<string, string, IPAddress, SshProxy<NodeDefinition>> nodeProxyCreator = null, RunOptions defaultRunOptions = RunOptions.None)
            : this(clusterLogin.Definition, nodeProxyCreator, defaultRunOptions)
        {
            this.ClusterLogin = clusterLogin;
        }

        /// <summary>
        /// Constructs a cluster proxy from a cluster definition.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <param name="nodeProxyCreator">
        /// The application supplied function that creates a management proxy
        /// given the node name, public address or FQDN, private address, and
        /// the node definition.
        /// </param>
        /// <param name="defaultRunOptions">
        /// Optionally specifies the <see cref="RunOptions"/> to be assigned to the 
        /// <see cref="SshProxy{TMetadata}.DefaultRunOptions"/> property for the
        /// nodes managed by the cluster proxy.  This defaults to <see cref="RunOptions.None"/>.
        /// </param>
        /// <remarks>
        /// The <paramref name="nodeProxyCreator"/> function will be called for each node in
        /// the cluster definition giving the application the chance to create the management
        /// proxy using the node's SSH credentials and also to specify logging.  A default
        /// creator that doesn't initialize SSH credentials and logging is used if a <c>null</c>
        /// argument is passed.
        /// </remarks>
        public ClusterProxy(ClusterDefinition clusterDefinition, Func<string, string, IPAddress, SshProxy<NodeDefinition>> nodeProxyCreator = null, RunOptions defaultRunOptions = RunOptions.None)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null);

            if (nodeProxyCreator == null)
            {
                nodeProxyCreator =
                    (name, publicAddress, privateAddress) =>
                    {
                        var login = NeonClusterHelper.ClusterLogin;

                        if (login != null)
                        {
                            return new SshProxy<NodeDefinition>(name, publicAddress, privateAddress, login.GetSshCredentials());
                        }
                        else
                        {
                            // Note that the proxy returned won't actually work because we're not 
                            // passing valid SSH credentials.  This is useful for situations where
                            // we need a cluster proxy for global things (like managing a hosting
                            // environment) where we won't need access to specific cluster nodes.

                            return new SshProxy<NodeDefinition>(name, publicAddress, privateAddress, SshCredentials.FromUserPassword("null", ""));
                        }
                    };
            }

            this.Definition          = clusterDefinition;
            this.ClusterLogin        = new ClusterLogin();
            this.defaultRunOptions   = defaultRunOptions;
            this.nodeProxyCreator    = nodeProxyCreator;
            this.DockerSecret        = new DockerSecretsManager(this);
            this.Certificate         = new CertificateManager(this);
            this.DnsHosts            = new DnsHostsManager(this);
            this.PublicLoadBalancer  = new LoadBalanceManager(this, "public");
            this.PrivateLoadBalancer = new LoadBalanceManager(this, "private");
            this.Registry            = new RegistryManager(this);

            CreateNodes();
        }

        /// <summary>
        /// Releases all resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected virtual void Dispose(bool disposing)
        {
            lock (syncRoot)
            {
                if (vaultClient != null)
                {
                    vaultClient.Dispose();
                    vaultClient = null;
                }

                if (consulClient != null)
                {
                    consulClient.Dispose();
                    consulClient = null;
                }
            }
        }

        /// <summary>
        /// Returns the cluster name.
        /// </summary>
        public string Name
        {
            get { return Definition.Name; }
        }

        /// <summary>
        /// The associated <see cref="IHostingManager"/> or <c>null</c>.
        /// </summary>
        public IHostingManager HostingManager { get; set; }

        /// <summary>
        /// Returns the cluster login information.
        /// </summary>
        public ClusterLogin ClusterLogin { get; set; }

        /// <summary>
        /// Indicates that any <see cref="SshProxy{TMetadata}"/> instances belonging
        /// to this cluster proxy should use public address/DNS names for SSH connections
        /// rather than their private cluster address.  This defaults to <c>false</c>
        /// and must be modified before establising a node connection to have any effect.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When this is <c>false</c>, connections will be established using node
        /// private addresses.  This implies that the current client has direct
        /// access to the cluster LAN via a direct connection or a VPN.
        /// </para>
        /// <para>
        /// Setting this to <c>true</c> is usually limited to cluster setup scenarios
        /// before the VPN is configured.  Exactly which public addresses and ports will
        /// be used when this is <c>true</c> is determined by the <see cref="HostingManager"/> 
        /// implementation for the current environment.
        /// </para>
        /// </remarks>
        public bool UseNodePublicAddress { get; set; } = false;

        /// <summary>
        /// Returns the cluster definition.
        /// </summary>
        public ClusterDefinition Definition { get; private set; }

        /// <summary>
        /// Returns the read-only list of cluster node proxies.
        /// </summary>
        public IReadOnlyList<SshProxy<NodeDefinition>> Nodes { get; private set; }

        /// <summary>
        /// Returns the first cluster manager node as sorted by name.
        /// </summary>
        public SshProxy<NodeDefinition> FirstManager { get; private set; }

        /// <summary>
        /// Manages cluster Docker secrets.
        /// </summary>
        public DockerSecretsManager DockerSecret { get; private set; }

        /// <summary>
        /// Manages cluster TLS certificates.
        /// </summary>
        public CertificateManager Certificate { get; private set; }

        /// <summary>
        /// Manages the local cluster DNS.
        /// </summary>
        public DnsHostsManager DnsHosts { get; private set; }

        /// <summary>
        /// Manages the cluster's public load balancer.
        /// </summary>
        public LoadBalanceManager PublicLoadBalancer { get; private set; }

        /// <summary>
        /// Manages the cluster's private load balancer.
        /// </summary>
        public LoadBalanceManager PrivateLoadBalancer { get; private set; }

        /// <summary>
        /// Manages the cluster's Docker registry credentials and local registry.
        /// </summary>
        public RegistryManager Registry { get; private set; }

        /// <summary>
        /// Returns the named load balancer manager.
        /// </summary>
        /// <param name="name">The load balancer name (one of <b>public</b> or <b>private</b>).</param>
        public LoadBalanceManager GetLoadBalancerManager(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            switch (name.ToLowerInvariant())
            {
                case "public":

                    return PublicLoadBalancer;

                case "private":

                    return PrivateLoadBalancer;

                default:

                    throw new ArgumentException($"[{name}] is not a valid proxy name.  Specify [public] or [private].");
            }
        }

        /// <summary>
        /// Specifies the <see cref="RunOptions"/> to use when executing commands that 
        /// include secrets.  This defaults to <see cref="RunOptions.Redact"/> for best 
        /// security but may be changed to just <see cref="RunOptions.None"/> when debugging
        /// cluster setup.
        /// </summary>
        public RunOptions SecureRunOptions { get; set; } = RunOptions.Redact | RunOptions.FaultOnError;

        /// <summary>
        /// Enumerates the cluster manager node proxies sorted in ascending order by name.
        /// </summary>
        public IEnumerable<SshProxy<NodeDefinition>> Managers
        {
            get { return Nodes.Where(n => n.Metadata.IsManager).OrderBy(n => n.Name); }
        }

        /// <summary>
        /// Enumerates the cluster worker node proxies sorted in ascending order by name.
        /// </summary>
        public IEnumerable<SshProxy<NodeDefinition>> Workers
        {
            get { return Nodes.Where(n => n.Metadata.IsWorker).OrderBy(n => n.Name); }
        }

        /// <summary>
        /// Enumerates the cluster pet node proxies sorted in ascending order by name.
        /// </summary>
        public IEnumerable<SshProxy<NodeDefinition>> Pets
        {
            get { return Nodes.Where(n => n.Metadata.IsPet).OrderBy(n => n.Name); }
        }

        /// <summary>
        /// Initializes or reinitializes the <see cref="Nodes"/> list.  This is called during
        /// construction and also in rare situations where the node proxies need to be 
        /// recreated (e.g. after configuring node static IP addresses).
        /// </summary>
        public void CreateNodes()
        {
            var nodes = new List<SshProxy<NodeDefinition>>();

            foreach (var nodeDefinition in Definition.SortedNodes)
            {
                var node = nodeProxyCreator(nodeDefinition.Name, nodeDefinition.PublicAddress, IPAddress.Parse(nodeDefinition.PrivateAddress ?? "0.0.0.0"));

                node.Cluster           = this;
                node.DefaultRunOptions = defaultRunOptions;
                node.Metadata          = nodeDefinition;
                nodes.Add(node);
            }

            this.Nodes        = nodes;
            this.FirstManager = Nodes.Where(n => n.Metadata.IsManager).OrderBy(n => n.Name).First();
        }

        /// <summary>
        /// Returns the <see cref="SshProxy{TMetadata}"/> instance for a named node.
        /// </summary>
        /// <param name="nodeName">The node name.</param>
        /// <returns>The node proxy instance.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the name node is not present in the cluster.</exception>
        public SshProxy<NodeDefinition> GetNode(string nodeName)
        {
            var node = Nodes.SingleOrDefault(n => string.Compare(n.Name, nodeName, StringComparison.OrdinalIgnoreCase) == 0);

            if (node == null)
            {
                throw new KeyNotFoundException($"The node [{nodeName}] is not present in the cluster.");
            }

            return node;
        }

        /// <summary>
        /// Looks for the <see cref="SshProxy{TMetadata}"/> instance for a named node.
        /// </summary>
        /// <param name="nodeName">The node name.</param>
        /// <returns>The node proxy instance or <c>null</c> if the named node does not exist.</returns>
        public SshProxy<NodeDefinition> FindNode(string nodeName)
        {
            return Nodes.SingleOrDefault(n => string.Compare(n.Name, nodeName, StringComparison.OrdinalIgnoreCase) == 0);
        }

        /// <summary>
        /// Returns a manager node that appears to be healthy.
        /// </summary>
        /// <param name="failureMode">Specifies what should happen when there are no healthy managers.</param>
        /// <returns>The healthy manager node.</returns>
        /// <exception cref="NeonClusterException">
        /// Thrown if no healthy managers are present and
        /// <paramref name="failureMode"/>=<see cref="HealthyManagerMode.Throw"/>.
        /// </exception>
        public SshProxy<NodeDefinition> GetHealthyManager(HealthyManagerMode failureMode = HealthyManagerMode.ReturnFirst)
        {
            // Try sending up to three pings to each manager node in parallel
            // to get a list of the health ones.  Then we'll return the first
            // healthy manager from the list (as sorted by name).
            //
            // This will consistently return the first manager node by name
            // if it's health, otherwise it will fail over to the next, etc.

            const int tryCount = 3;

            var healthyManagers = new List<SshProxy<NodeDefinition>>();
            var pingOptions     = new PingOptions(ttl: 32, dontFragment: true);
            var pingTimeout     = TimeSpan.FromSeconds(1);

            for (int i = 0; i < tryCount; i++)
            {
                Parallel.ForEach(Nodes.Where(n => n.Metadata.IsManager),
                    manager =>
                    {
                        using (var ping = new Ping())
                        {
                            var reply = ping.Send(manager.PrivateAddress, (int)pingTimeout.TotalMilliseconds);

                            if (reply.Status == IPStatus.Success)
                            {
                                lock (healthyManagers)
                                {
                                    healthyManagers.Add(manager);
                                }
                            }
                        }
                    });

                if (healthyManagers.Count > 0)
                {
                    return healthyManagers.OrderBy(n => n.Name).First();
                }
            }

            switch (failureMode)
            {
                case HealthyManagerMode.ReturnFirst:

                    return FirstManager;

                case HealthyManagerMode.ReturnNull:

                    return null;

                case HealthyManagerMode.Throw:

                    throw new NeonClusterException("Could not locate a healthy cluster manager node.");

                default:

                    throw new NotImplementedException($"Unexpected failure [mode={failureMode}].");
            }
        }

        /// <summary>
        /// Performs cluster configuration steps.
        /// </summary>
        /// <param name="steps">The configuration steps.</param>
        public void Configure(ConfigStepList steps)
        {
            Covenant.Requires<ArgumentNullException>(steps != null);

            foreach (var step in steps)
            {
                step.Run(this);
            }
        }

        /// <summary>
        /// Returns steps that upload a text file to a set of node proxies.
        /// </summary>
        /// <param name="nodes">The node proxies to receive the upload.</param>
        /// <param name="path">The target path on the Linux node.</param>
        /// <param name="text">The input text.</param>
        /// <param name="tabStop">Optionally expands TABs into spaces when non-zero.</param>
        /// <param name="outputEncoding">Optionally specifies the output text encoding (defaults to UTF-8).</param>
        /// <returns>The steps.</returns>
        public IEnumerable<ConfigStep> GetFileUploadSteps(IEnumerable<SshProxy<NodeDefinition>> nodes, string path, string text, int tabStop = 0, Encoding outputEncoding = null)
        {
            var steps = new ConfigStepList();

            foreach (var node in nodes)
            {
                steps.Add(UploadStep.Text(node.Name, path, text, tabStop, outputEncoding));
            }

            return steps;
        }

        /// <summary>
        /// Returns a Consul client.
        /// </summary>
        /// <returns>The <see cref="ConsulClient"/>.</returns>
        public ConsulClient Consul
        {
            get
            {
                lock (syncRoot)
                {
                    if (consulClient != null)
                    {
                        return consulClient;
                    }

                    consulClient = NeonClusterHelper.OpenConsul();
                }

                return consulClient;
            }
        }

        /// <summary>
        /// Returns a Vault client using the root token.
        /// </summary>
        /// <returns>The <see cref="VaultClient"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="ClusterLogin"/> has not yet been initialized with the Vault root token.</exception>
        public VaultClient Vault
        {
            get
            {
                if (!ClusterLogin.HasVaultRootCredentials)
                {
                    throw new InvalidOperationException($"[{nameof(ClusterProxy)}.{nameof(ClusterLogin)}] has not yet been initialized with the Vault root token.");
                }

                lock (syncRoot)
                {
                    if (vaultClient != null)
                    {
                        return vaultClient;
                    }

                    vaultClient = VaultClient.OpenWithToken(new Uri(Definition.Vault.GetDirectUri(GetHealthyManager().Name)), ClusterLogin.VaultCredentials.RootToken);
                }

                return vaultClient;
            }
        }

        /// <summary>
        /// Ensure that we have the Vault token.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the root token is not available.</exception>
        private void VerifyVaultToken()
        {
            if (!ClusterLogin.HasVaultRootCredentials)
            {
                throw new InvalidOperationException($"[{nameof(ClusterProxy)}.{nameof(ClusterLogin)}] has not yet been initialized with the Vault root token.");
            }
        }

        /// <summary>
        /// Wait for all Vault instances to report being unsealed and then
        /// to be able to perform an operation (e.g. writing a secret).
        /// </summary>
        public void VaultWaitUntilReady()
        {
            var readyManagers = new HashSet<string>();
            var timeout       = TimeSpan.FromSeconds(120);
            var timer         = new Stopwatch();

            // Wait for all of the managers to report being unsealed.

            timer.Start();

            foreach (var manager in Managers)
            {
                manager.Status = "vault: verify unsealed";
            }

            while (readyManagers.Count < Managers.Count())
            {
                if (timer.Elapsed >= timeout)
                {
                    var sbNotReadyManagers = new StringBuilder();

                    foreach (var manager in Managers.Where(m => !readyManagers.Contains(m.Name)))
                    {
                        sbNotReadyManagers.AppendWithSeparator(manager.Name, ", ");
                    }

                    throw new NeonClusterException($"Vault not unsealed after waiting [{timeout}] on: {sbNotReadyManagers}");
                }

                foreach (var manager in Managers.Where(m => !readyManagers.Contains(m.Name)))
                {
                    var response = manager.SudoCommand("vault-direct status");

                    if (response.ExitCode == 0)
                    {
                        readyManagers.Add(manager.Name);
                        manager.Status = "vault: unsealed";
                    }
                }

                Thread.Sleep(TimeSpan.FromSeconds(2));
            }

            // Now, verify that all managers are really ready by verifying that
            // we can write a secret to each of them.  We'll keep retrying for
            // a while when this fails.

            readyManagers.Clear();
            timer.Restart();

            foreach (var manager in Managers)
            {
                manager.Status = "vault: check ready";
            }

            while (readyManagers.Count < Managers.Count())
            {
                if (timer.Elapsed >= timeout)
                {
                    var sbNotReadyManagers = new StringBuilder();

                    foreach (var manager in Managers.Where(m => !readyManagers.Contains(m.Name)))
                    {
                        sbNotReadyManagers.AppendWithSeparator(manager.Name, ", ");
                    }

                    throw new NeonClusterException($"Vault not ready after waiting [{timeout}] on: {sbNotReadyManagers}");
                }

                foreach (var manager in Managers.Where(m => !readyManagers.Contains(m.Name)))
                {
                    var response = VaultCommand(manager, $"vault-direct write secret {manager.Name}-ready=true");

                    if (response.ExitCode == 0)
                    {
                        readyManagers.Add(manager.Name);
                        manager.Status = "vault: ready";
                    }
                }

                Thread.Sleep(TimeSpan.FromSeconds(2));
            }

            // Looks like all Vault instances are ready, so remove the secrets we added.

            foreach (var manager in Managers.Where(m => !readyManagers.Contains(m.Name)))
            {
                VaultCommandNoFault(manager, $"vault-direct delete secret {manager.Name}-ready");
            }

            foreach (var manager in Managers)
            {
                manager.Status = string.Empty;
            }
        }

        /// <summary>
        /// Executes a command on a specific cluster manager node using the root Vault token.
        /// </summary>
        /// <param name="manager">The target manager.</param>
        /// <param name="command">The command (including the <b>vault</b>).</param>
        /// <param name="args">The optional arguments.</param>
        /// <returns>The command response.</returns>
        /// <remarks>
        /// <note>
        /// This method faults and throws an exception if the command returns
        /// a non-zero exit code.
        /// </note>
        /// </remarks>
        public CommandResponse VaultCommand(SshProxy<NodeDefinition> manager, string command, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(manager != null);
            Covenant.Requires<ArgumentNullException>(command != null);

            VerifyVaultToken();

            var scriptBundle = new CommandBundle(command, args);
            var bundle = new CommandBundle("./vault-command.sh");

            bundle.AddFile("vault-command.sh",
$@"#!/bin/bash
export VAULT_TOKEN={ClusterLogin.VaultCredentials.RootToken}
{scriptBundle}
",
                isExecutable: true);

            var response = manager.SudoCommand(bundle, SecureRunOptions | RunOptions.FaultOnError);

            response.BashCommand = bundle.ToBash();

            return response;
        }

        /// <summary>
        /// Executes a command on a healthy cluster manager node using the root Vault token.
        /// </summary>
        /// <param name="command">The command (including the <b>vault</b>).</param>
        /// <param name="args">The optional arguments.</param>
        /// <returns>The command response.</returns>
        /// <remarks>
        /// <note>
        /// This method faults and throws an exception if the command returns
        /// a non-zero exit code.
        /// </note>
        /// </remarks>
        public CommandResponse VaultCommand(string command, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(command != null);

            return VaultCommand(GetHealthyManager(), command, args);
        }

        /// <summary>
        /// Executes a command on a specific cluster manager node using the root Vault token.
        /// </summary>
        /// <param name="manager">The target manager.</param>
        /// <param name="command">The command (including the <b>vault</b>).</param>
        /// <param name="args">The optional arguments.</param>
        /// <returns>The command response.</returns>
        /// <remarks>
        /// <note>
        /// This method does not fault or throw an exception if the command returns
        /// a non-zero exit code.
        /// </note>
        /// </remarks>
        public CommandResponse VaultCommandNoFault(SshProxy<NodeDefinition> manager, string command, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(manager != null);
            Covenant.Requires<ArgumentNullException>(command != null);

            VerifyVaultToken();

            var scriptBundle = new CommandBundle(command, args);
            var bundle = new CommandBundle("./vault-command.sh");

            bundle.AddFile("vault-command.sh",
$@"#!/bin/bash
export VAULT_TOKEN={ClusterLogin.VaultCredentials.RootToken}
{scriptBundle}
",
                isExecutable: true);

            var response = manager.SudoCommand(bundle, SecureRunOptions);

            response.BashCommand = bundle.ToBash();

            return response;
        }

        /// <summary>
        /// Executes a command on a healthy cluster manager node using the root Vault token.
        /// </summary>
        /// <param name="command">The command (including the <b>vault</b>).</param>
        /// <param name="args">The optional arguments.</param>
        /// <returns>The command response.</returns>
        /// <remarks>
        /// <note>
        /// This method does not fault or throw an exception if the command returns
        /// a non-zero exit code.
        /// </note>
        /// </remarks>
        public CommandResponse VaultCommandNoFault(string command, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(command != null);

            return VaultCommandNoFault(GetHealthyManager(), command, args);
        }

        /// <summary>
        /// Creates a Vault access control policy.
        /// </summary>
        /// <param name="policy">The policy.</param>
        /// <returns>The command response.</returns>
        public CommandResponse CreateVaultPolicy(VaultPolicy policy)
        {
            Covenant.Requires<ArgumentNullException>(policy != null);

            VerifyVaultToken();

            var bundle = new CommandBundle("./create-vault-policy.sh");

            bundle.AddFile("create-vault-policy.sh",
$@"#!/bin/bash
export VAULT_TOKEN={ClusterLogin.VaultCredentials.RootToken}
vault policy-write {policy.Name} policy.hcl
",
                isExecutable: true);

            bundle.AddFile("policy.hcl", policy);

            var response = GetHealthyManager().SudoCommand(bundle, SecureRunOptions | RunOptions.FaultOnError);

            response.BashCommand = bundle.ToBash();

            return response;
        }

        /// <summary>
        /// Removes a Vault access control policy.
        /// </summary>
        /// <param name="policyName">The policy name.</param>
        /// <returns>The command response.</returns>
        public CommandResponse RemoveVaultPolicy(string policyName)
        {
            Covenant.Requires<ArgumentException>(ClusterDefinition.IsValidName(policyName));

            return VaultCommand($"vault policy-delete {policyName}");
        }

        /// <summary>
        /// Creates a Vault AppRole.
        /// </summary>
        /// <param name="roleName">The role name.</param>
        /// <param name="policies">The policy names or HCL details.</param>
        /// <returns>The command response.</returns>
        public CommandResponse CreateVaultAppRole(string roleName, params string[] policies)
        {
            Covenant.Requires<ArgumentNullException>(roleName != null);
            Covenant.Requires<ArgumentNullException>(policies != null);

            var sbPolicies = new StringBuilder();

            if (sbPolicies != null)
            {
                foreach (var policy in policies)
                {
                    if (string.IsNullOrEmpty(policy))
                    {
                        throw new ArgumentNullException("Null or empty policy.");
                    }

                    sbPolicies.AppendWithSeparator(policy, ",");
                }
            }

            // Note that we have to escape any embedded double quotes in the policies
            // because they may include HCL rather than being just policy names.

            return VaultCommand($"vault write auth/approle/role/{roleName} \"policies={sbPolicies.Replace("\"", "\"\"")}\"");
        }

        /// <summary>
        /// Removes a Vault AppRole.
        /// </summary>
        /// <param name="roleName">The role name.</param>
        /// <returns>The command response.</returns>
        public CommandResponse RemoveVaultAppRole(string roleName)
        {
            Covenant.Requires<ArgumentException>(ClusterDefinition.IsValidName(roleName));

            return VaultCommand($"vault delete auth/approle/role/{roleName}");
        }

        /// <summary>
        /// Attempts to retrieve a named cluster setting as a <c>string</c>.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="output">Returns as the setting value.</param>
        /// <returns><c>true</c> if the setting exists and was returned.</returns>
        /// <remarks>
        /// <note>
        /// Well known cluster setting names are defined in <see cref="NeonClusterSettings"/>.
        /// </note>
        /// </remarks>
        public bool TryGetSettingString(string name, out string output)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(ClusterDefinition.IsValidName(name));

            output = null;

            var key   = $"neon/cluster/settings/{name}";
            var value = Consul.KV.GetStringOrDefault(key).Result;

            if (value == null)
            {
                return false;
            }

            output = value;

            return true;
        }

        /// <summary>
        /// Attempts to retrieve a named cluster setting as a <c>bool</c>.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="output">Returns as the setting value.</param>
        /// <returns><c>true</c> if the setting exists and was returned.</returns>
        /// <exception cref="FormatException">Thrown if the setting value could not be parsed.</exception>
        /// <remarks>
        /// <note>
        /// Well known cluster setting names are defined in <see cref="NeonClusterSettings"/>.
        /// </note>
        /// </remarks>
        public bool TryGetSettingBool(string name, out bool output)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(ClusterDefinition.IsValidName(name));

            output = default(bool);

            var key   = $"neon/cluster/settings/{name}";
            var value = Consul.KV.GetStringOrDefault(key).Result;

            if (value == null)
            {
                return false;
            }

            output = NeonHelper.ParseBool(value);

            return true;
        }

        /// <summary>
        /// Attempts to retrieve a named cluster setting as an <c>int</c>.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="output">Returns as the setting value.</param>
        /// <returns><c>true</c> if the setting exists and was returned.</returns>
        /// <exception cref="FormatException">Thrown if the setting value could not be parsed.</exception>
        /// <remarks>
        /// <note>
        /// Well known cluster setting names are defined in <see cref="NeonClusterSettings"/>.
        /// </note>
        /// </remarks>
        public bool TryGetSettingInt(string name, out int output)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(ClusterDefinition.IsValidName(name));

            output = default(int);

            var key   = $"neon/cluster/settings/{name}";
            var value = Consul.KV.GetStringOrDefault(key).Result;

            if (value == null)
            {
                return false;
            }

            output = int.Parse(value);

            return true;
        }

        /// <summary>
        /// Attempts to retrieve a named cluster setting as a <c>long</c>.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="output">Returns as the setting value.</param>
        /// <returns><c>true</c> if the setting exists and was returned.</returns>
        /// <exception cref="FormatException">Thrown if the setting value could not be parsed.</exception>
        /// <remarks>
        /// <note>
        /// Well known cluster setting names are defined in <see cref="NeonClusterSettings"/>.
        /// </note>
        /// </remarks>
        public bool TryGetSettingLong(string name, out long output)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(ClusterDefinition.IsValidName(name));

            output = default(int);

            var key   = $"neon/cluster/settings/{name}";
            var value = Consul.KV.GetStringOrDefault(key).Result;

            if (value == null)
            {
                return false;
            }

            output = long.Parse(value);

            return true;
        }

        /// <summary>
        /// Attempts to retrieve a named cluster setting as a <c>double</c>.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="output">Returns as the setting value.</param>
        /// <returns><c>true</c> if the setting exists and was returned.</returns>
        /// <exception cref="FormatException">Thrown if the setting value could not be parsed.</exception>
        /// <remarks>
        /// <note>
        /// Well known cluster setting names are defined in <see cref="NeonClusterSettings"/>.
        /// </note>
        /// </remarks>
        public bool TryGetSettingDouble(string name, out double output)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(ClusterDefinition.IsValidName(name));

            output = default(double);

            var key   = $"neon/cluster/settings/{name}";
            var value = Consul.KV.GetStringOrDefault(key).Result;

            if (value == null)
            {
                return false;
            }

            output = double.Parse(value);

            return true;
        }

        /// <summary>
        /// Attempts to retrieve a named cluster setting as a <see cref="TimeSpan"/>.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="output">Returns as the setting value.</param>
        /// <returns><c>true</c> if the setting exists and was returned.</returns>
        /// <exception cref="FormatException">Thrown if the setting value could not be parsed.</exception>
        /// <remarks>
        /// <note>
        /// Well known cluster setting names are defined in <see cref="NeonClusterSettings"/>.
        /// </note>
        /// </remarks>
        public bool TryGetSettingTimeSpan(string name, out TimeSpan output)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(ClusterDefinition.IsValidName(name));

            output = default(TimeSpan);

            var key   = $"neon/cluster/settings/{name}";
            var value = Consul.KV.GetStringOrDefault(key).Result;

            if (value == null)
            {
                return false;
            }

            output = TimeSpan.Parse(value);

            return true;
        }

        /// <summary>
        /// Sets or removes a named <c>string</c> cluster setting.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="value">The setting value or <c>null</c> to remove the setting if it exists.</param>
        /// <remarks>
        /// <note>
        /// Well known cluster setting names are defined in <see cref="NeonClusterSettings"/>.
        /// </note>
        /// </remarks>
        public async void SetSetting(string name, string value)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(ClusterDefinition.IsValidName(name));

            var key = $"neon/cluster/settings/{name}";

            if (value == null)
            {
                await Consul.KV.Delete(key);
            }
            else
            {
                await Consul.KV.PutString(key, value);
            }
        }

        /// <summary>
        /// Sets or removes a named <c>bool</c> cluster setting.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="value">The setting value or <c>null</c> to remove the setting if it exists.</param>
        /// <remarks>
        /// <note>
        /// Well known cluster setting names are defined in <see cref="NeonClusterSettings"/>.
        /// </note>
        /// </remarks>
        public async void SetSetting(string name, bool? value)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(ClusterDefinition.IsValidName(name));

            var key = $"neon/cluster/settings/{name}";

            if (value == null)
            {
                await Consul.KV.Delete(key);
            }
            else
            {
                await Consul.KV.PutString(key, value.Value ? "true" : "false");
            }
        }

        /// <summary>
        /// Sets or removes a named <c>int</c> cluster setting.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="value">The setting value or <c>null</c> to remove the setting if it exists.</param>
        /// <remarks>
        /// <note>
        /// Well known cluster setting names are defined in <see cref="NeonClusterSettings"/>.
        /// </note>
        /// </remarks>
        public async void SetSetting(string name, int? value)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(ClusterDefinition.IsValidName(name));

            var key = $"neon/cluster/{name}";

            if (value == null)
            {
                await Consul.KV.Delete(key);
            }
            else
            {
                await Consul.KV.PutString(key, value.Value.ToString());
            }
        }

        /// <summary>
        /// Sets or removes a named <c>long</c> cluster setting.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="value">The setting value or <c>null</c> to remove the setting if it exists.</param>
        /// <remarks>
        /// <note>
        /// Well known cluster setting names are defined in <see cref="NeonClusterSettings"/>.
        /// </note>
        /// </remarks>
        public async void SetSetting(string name, long? value)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(ClusterDefinition.IsValidName(name));

            var key = $"neon/cluster/{name}";

            if (value == null)
            {
                await Consul.KV.Delete(key);
            }
            else
            {
                await Consul.KV.PutString(key, value.Value.ToString());
            }
        }

        /// <summary>
        /// Sets or removes a named <c>double</c> cluster setting.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="value">The setting value or <c>null</c> to remove the setting if it exists.</param>
        /// <remarks>
        /// <note>
        /// Well known cluster setting names are defined in <see cref="NeonClusterSettings"/>.
        /// </note>
        /// </remarks>
        public async void SetSetting(string name, double? value)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(ClusterDefinition.IsValidName(name));

            var key = $"neon/cluster/{name}";

            if (value == null)
            {
                await Consul.KV.Delete(key);
            }
            else
            {
                await Consul.KV.PutString(key, value.Value.ToString());
            }
        }

        /// <summary>
        /// Sets or removes a named <see cref="TimeSpan"/> cluster setting.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="value">The setting value or <c>null</c> to remove the setting if it exists.</param>
        /// <remarks>
        /// <note>
        /// Well known cluster setting names are defined in <see cref="NeonClusterSettings"/>.
        /// </note>
        /// </remarks>
        public async void SetSetting(string name, TimeSpan? value)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(ClusterDefinition.IsValidName(name));

            var key = $"neon/cluster/{name}";

            if (value == null)
            {
                await Consul.KV.Delete(key);
            }
            else
            {
                await Consul.KV.PutString(key, value.Value.ToString());
            }
        }

        /// <summary>
        /// Inspects a service, returning details about its current state.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <param name="strict">Optionally specify strict JSON parsing.</param>
        /// <returns>The <see cref="ServiceDetails"/> or <c>null</c> if the service doesn't exist.</returns>
        public ServiceDetails InspectService(string name, bool strict = false)
        {
            var response = GetHealthyManager().DockerCommand(RunOptions.None, "docker", "service", "inspect", name);

            if (response.ExitCode != 0)
            {
                if (response.AllText.Contains("Status: Error: no such service:"))
                {
                    return null;
                }

                throw new Exception($"Cannot inspect service [{name}]: {response.AllText}");
            }

            // The inspection response is actually an array with a single
            // service details element, so we'll need to extract that element
            // and then parse it.

            var jArray      = JArray.Parse(response.OutputText);
            var jsonDetails = jArray[0].ToString(Formatting.Indented);
            var details     = NeonHelper.JsonDeserialize<ServiceDetails>(jsonDetails, strict);

            details.Normalize();

            return details;
        }

        /// <summary>
        /// Indicates that the cluster certificates and or load balancer rules may have been changed.
        /// This has the effect of signalling <b>neon-proxy-manager</b> to to regenerate the proxy 
        /// definitions and update all of the load balancers when changes are detected.
        /// </summary>
        public void SignalLoadBalancerUpdate()
        {
            Consul.KV.PutString("neon/service/neon-proxy-manager/conf/reload", Guid.NewGuid().ToString("D")).Wait();
        }
    }
}
