//-----------------------------------------------------------------------------
// FILE:	    ClusterProxy.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;

using Neon.Common;
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
        private object                                                      syncRoot = new object();
        private VaultClient                                                 vaultClient;
        private ConsulClient                                                consulClient;
        private RunOptions                                                  defaultRunOptions;
        private Func<string, string, IPAddress, SshProxy<NodeDefinition>>  nodeProxyCreator;

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
                        // Note that the proxy returned won't actually work because we're not 
                        // passing valid SSH credentials.  This us useful for situations where
                        // we need a cluster proxy for global things (like managing a hosting
                        // environment) where we won't need access to specific cluster nodes.

                        return new SshProxy<NodeDefinition>(name, publicAddress, privateAddress, SshCredentials.FromUserPassword("null", ""));
                    };
            }

            this.Definition        = clusterDefinition;
            this.ClusterLogin      = new ClusterLogin();
            this.defaultRunOptions = defaultRunOptions;
            this.nodeProxyCreator  = nodeProxyCreator;
            this.DockerSecret      = new DockerSecretsManager(this);
            this.Certificate       = new CertiticateManager(this);
            this.PublicProxy       = new ProxyManager(this, "public");
            this.PrivateProxy      = new ProxyManager(this, "private");

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
        /// Returns the first cluster manager node that will be used for global
        /// cluster setup.
        /// </summary>
        public SshProxy<NodeDefinition> FirstManager { get; private set; }

        /// <summary>
        /// Returns the object to be used to manage cluster Docker secrets.
        /// </summary>
        public DockerSecretsManager DockerSecret { get; private set; }

        /// <summary>
        /// Returns the object to be used to manage cluster TLS certificates.
        /// </summary>
        public CertiticateManager Certificate { get; private set; }

        /// <summary>
        /// Manages the cluster's public proxy.
        /// </summary>
        public ProxyManager PublicProxy { get; private set; }

        /// <summary>
        /// Manages the cluster's private proxy.
        /// </summary>
        public ProxyManager PrivateProxy { get; private set; }

        /// <summary>
        /// Specifies the <see cref="RunOptions"/> to use when executing cluster Vault
        /// commands.  This defaults to <see cref="RunOptions.Redact"/> and
        /// <see cref="RunOptions.FaultOnError"/> for best security but may be changed
        /// to just <see cref="RunOptions.FaultOnError"/> when debugging
        /// cluster setup.
        /// </summary>
        public RunOptions VaultRunOptions { get; set; } = RunOptions.Redact | RunOptions.FaultOnError;

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
        /// <exception cref="InvalidOperationException">Thrown if <see cref="ClusterLogin"/> has not yet been intialized with the Vault root token.</exception>
        public VaultClient Vault
        {
            get
            {
                if (ClusterLogin.VaultCredentials == null || string.IsNullOrEmpty(ClusterLogin.VaultCredentials.RootToken))
                {
                    throw new InvalidOperationException($"[{nameof(ClusterProxy)}.{nameof(ClusterLogin)}] has not yet been intialized with the Vault root token.");
                }

                lock (syncRoot)
                {
                    if (vaultClient != null)
                    {
                        return vaultClient;
                    }

                    vaultClient = VaultClient.OpenWithToken(new Uri(Definition.Vault.GetDirectUri(FirstManager.Name)), ClusterLogin.VaultCredentials.RootToken);
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
            if (ClusterLogin.VaultCredentials == null || string.IsNullOrEmpty(ClusterLogin.VaultCredentials.RootToken))
            {
                throw new InvalidOperationException($"[{nameof(ClusterProxy)}.{nameof(ClusterLogin)}] has not yet been intialized with the Vault root token.");
            }
        }

        /// <summary>
        /// Executes a command on a cluster manager node using the root Vault token.
        /// </summary>
        /// <param name="command">The command (including the <b>vault</b>).</param>
        /// <param name="args">The optional arguments.</param>
        /// <returns>The command response.</returns>
        public CommandResponse VaultCommand(string command, params object[] args)
        {
            VerifyVaultToken();

            var scriptBundle = new CommandBundle(command, args);
            var bundle       = new CommandBundle("./vault-command.sh");

            bundle.AddFile("vault-command.sh",
$@"#!/bin/bash
export VAULT_TOKEN={ClusterLogin.VaultCredentials.RootToken}
{scriptBundle}
",
                isExecutable: true);

            var response = FirstManager.SudoCommand(bundle, VaultRunOptions);

            response.BashCommand = bundle.ToBash();

            return response;
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

            var response = FirstManager.SudoCommand(bundle, VaultRunOptions);

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
    }
}
