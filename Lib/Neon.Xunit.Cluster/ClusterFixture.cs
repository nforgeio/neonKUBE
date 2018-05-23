//-----------------------------------------------------------------------------
// FILE:	    ClusterFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using YamlDotNet.RepresentationModel;
using Xunit;

using Neon.Cluster;
using Neon.Common;
using Neon.Cryptography;
using Neon.Xunit;

// $todo(jeff.lill):
//
// I need to think about resetting Docker registry login state.  We're
// not currently doing this.

namespace Neon.Xunit.Cluster
{
    /// <summary>
    /// An Xunit test fixture used to run unit tests on a neonCLUSTER.
    /// </summary>
    /// <remarks>
    /// <note>
    /// <para>
    /// <b>IMPORTANT:</b> The Neon <see cref="TestFixture"/> implementation <b>DOES NOT</b>
    /// support parallel test execution because fixtures may impact global machine state
    /// like starting a Couchbase Docker container, modifying the local DNS <b>hosts</b>
    /// file or managing a Docker Swarm or neonCLUSTER.
    /// </para>
    /// <para>
    /// You should explicitly disable parallel execution in all test assemblies that
    /// rely on test fixtures by adding a C# file called <c>AssemblyInfo.cs</c> with:
    /// </para>
    /// <code language="csharp">
    /// [assembly: CollectionBehavior(DisableTestParallelization = true)]
    /// </code>
    /// </note>
    /// <para>
    /// This Xunit test fixture can be used to run unit tests running on a fullly 
    /// provisioned neonCLUSTER.  This is useful for performing integration tests
    /// within a fully functional environment.  This fixture is similar to <see cref="DockerFixture"/>
    /// and in fact, inherits some functionality from that, but <see cref="DockerFixture"/>
    /// hosts tests against a local single node Docker Swarm rather than a full
    /// neonCLUSTER.
    /// </para>
    /// <para>
    /// neonCLUSTERs do not allow the <see cref="ClusterFixture"/> to perform unit
    /// tests by default, as a safety measure.  You can enable this before cluster
    /// deployment by setting <see cref="ClusterDefinition.AllowUnitTesting"/><c>=true</c>
    /// or by manually invoking this command for an existing cluster:
    /// </para>
    /// <code>
    /// neon cluster set allow-unit-testing=yes
    /// </code>
    /// <para>
    /// This fixture is pretty easy to use.  Simply have your test class inherit
    /// from <see cref="IClassFixture{ClusterFixture}"/> and add a public constructor
    /// that accepts a <see cref="ClusterFixture"/> as the only argument.  Then
    /// you can call it's <see cref="LoginAndInitialize(string, Action)"/> method within
    /// the constructor passing the cluster login name as well as an <see cref="Action"/>.
    /// You may also use the fixture to initialize cluster services, networks, secrets,
    /// load balancers, etc. within your custom action.
    /// </para>
    /// <note>
    /// Do not call the base <see cref="TestFixture.Initialize(Action)"/> method
    /// is not supported by this fixture and will throw an exception.
    /// </note>
    /// <para>
    /// The specified cluster login file must be already present on the current
    /// machine for the current user.  The <see cref="LoginAndInitialize(string, Action)"/> method will
    /// logout from the current cluster (if any) and then login to the one specified.
    /// </para>
    /// <note>
    /// You can also specify a <c>null</c> or empty login name.  In this case,
    /// the fixture will attempt to retrieve the login name from the <b>NEON_TEST_CLUSTER</b>
    /// environment variable.  This is very handy because it allows developers to
    /// specify different target test clusters without having to bake this into the 
    /// unit tests themselves.
    /// </note>
    /// <para>
    /// This fixture provides several methods and properties for managing the 
    /// cluster state.  These may be called within the test class constructor's 
    /// action method, within the test constructor but outside of the action, 
    /// or within the test class methods:
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>Misc</b></term>
    ///     <description>
    ///     <see cref="ClearConsul()"/>
    ///     <see cref="ClearVault()"/>
    ///     <see cref="Cluster"/><br/>
    ///     <see cref="DockerExecute(string)"/><br/>
    ///     <see cref="DockerExecute(object[])"/>
    ///     <see cref="NeonExecute(string)"/><br/>
    ///     <see cref="NeonExecute(object[])"/><br/>
    ///     <see cref="Reset"/>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>Certificates</b></term>
    ///     <description>
    ///     <see cref="ClearCertificates(bool)"/><br/>
    ///     <see cref="ListCertificates(bool)"/><br/>
    ///     <see cref="PutCertificate(string, string)"/><br/>
    ///     <see cref="PutSelfSignedCertificate(string, string)"/><br/>
    ///     <see cref="RemoveCertificate(string)"/><br/>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>Configs</b></term>
    ///     <description>
    ///     <see cref="DockerFixture.ClearSecrets(bool)"/><br/>
    ///     <see cref="DockerFixture.CreateConfig(string, byte[], string[])"/><br/>
    ///     <see cref="DockerFixture.CreateConfig(string, string, string[])"/><br/>
    ///     <see cref="DockerFixture.ListConfigs(bool)"/><br/>
    ///     <see cref="DockerFixture.RemoveConfig(string)"/>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>Containers</b></term>
    ///     <description>
    ///     <para>
    ///     <b>Container functionality is not currently implemented by the fixture.</b>
    ///     </para>
    ///     <para>
    ///     <see cref="DockerFixture.ClearContainers(bool)"/><br/>
    ///     <see cref="DockerFixture.ListContainers(bool)"/><br/>
    ///     <see cref="DockerFixture.RemoveContainer(string)"/><br/>
    ///     <see cref="DockerFixture.RunContainer(string, string, string[], string[], string[])"/><br/>
    ///     </para>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>DNS</b></term>
    ///     <description>
    ///     <see cref="ClearDnsEntries(bool)"/><br/>
    ///     <see cref="ConvergeDns()"/>
    ///     <see cref="ListDnsEntries(bool)"/><br/>
    ///     <see cref="RemoveDnsEntry(string)"/><br/>
    ///     <see cref="SetDnsEntry(DnsEntry)"/><br/>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>Images</b></term>
    ///     <description>
    ///     <see cref="ClearImages()"/><br/>
    ///     <see cref="PullImage(string)"/><br/>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>Networks</b></term>
    ///     <description>
    ///     <see cref="DockerFixture.ClearNetworks(bool)"/><br/>
    ///     <see cref="DockerFixture.CreateNetwork(string, string[])"/><br/>
    ///     <see cref="DockerFixture.ListNetworks(bool)"/><br/>
    ///     <see cref="DockerFixture.RemoveNetwork(string)"/>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>Load Balancer Rules</b></term>
    ///     <description>
    ///     <see cref="ClearLoadBalancers(bool)"/><br/>
    ///     <see cref="ListLoadBalancerRules(string, bool)"/><br/>
    ///     <see cref="PutLoadBalancerRule(string, LoadBalancerRule)"/><br/>
    ///     <see cref="RemoveLoadBalancerRule(string, string)"/><br/>
    ///     <see cref="RestartLoadBalancers()"/><br/>
    ///     <see cref="RestartPrivateLoadbalancers()"/><br/>
    ///     <see cref="RestartPublicLoadBalancers()"/>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>Secrets</b></term>
    ///     <description>
    ///     <see cref="DockerFixture.ClearSecrets(bool)"/><br/>
    ///     <see cref="DockerFixture.CreateSecret(string, byte[], string[])"/><br/>
    ///     <see cref="DockerFixture.CreateSecret(string, string, string[])"/><br/>
    ///     <see cref="DockerFixture.ListSecrets(bool)"/><br/>
    ///     <see cref="DockerFixture.RemoveSecret(string)"/>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>Services</b></term>
    ///     <description>
    ///     <see cref="DockerFixture.ClearServices(bool)"/>
    ///     <see cref="DockerFixture.CreateService(string, string, string[], string[], string[])"/><br/>
    ///     <see cref="DockerFixture.ListServices(bool)"/><br/>
    ///     <see cref="DockerFixture.InspectService(string, bool)"/><br/>
    ///     <see cref="DockerFixture.RemoveService(string)"/><br/>
    ///     <see cref="DockerFixture.RestartService(string)"/><br/>
    ///     <see cref="DockerFixture.RollbackService(String)"/><br/>
    ///     <see cref="DockerFixture.UpdateService(string, string[])"/>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>Stacks</b></term>
    ///     <description>
    ///     <see cref="DockerFixture.ClearStacks(bool)"/><br/>
    ///     <see cref="DockerFixture.DeployStack(string, string, string[], TimeSpan, TimeSpan)"/><br/>
    ///     <see cref="DockerFixture.ListStacks(bool)"/><br/>
    ///     <see cref="DockerFixture.RemoveStack(string)"/>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>Volumes</b></term>
    ///     <description>
    ///     <see cref="ClearVolumes(bool)"/><br/>
    ///     <see cref="ListVolumes(string)"/>
    ///     </description>
    /// </item>
    /// </list>
    /// <note>
    /// <see cref="ClusterFixture"/> derives from <see cref="TestFixtureSet"/> so you can
    /// use <see cref="TestFixtureSet.AddFixture{TFixture}(string, TFixture, Action{TFixture})"/> 
    /// to add additional fixtures within your custom initialization action for advanced scenarios.
    /// </note>
    /// <para>
    /// There are two basic patterns for using this fixture.
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>initialize once</b></term>
    ///     <description>
    ///     <para>
    ///     The basic idea here is to have your test class initialize the cluster
    ///     once within the test class constructor inside of the initialize action
    ///     with common state and services that all of the tests can access.
    ///     </para>
    ///     <para>
    ///     This will be quite a bit faster than reconfiguring the cluster at the
    ///     beginning of every test and can work well for many situations.
    ///     </para>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>initialize every test</b></term>
    ///     <description>
    ///     For scenarios where the cluster must be cleared before every test,
    ///     you can use the <see cref="Reset()"/> method to reset its
    ///     state within each test method, populate the cluster as necessary,
    ///     and then perform your tests.
    ///     </description>
    /// </item>
    /// </list>
    /// </remarks>
    /// <threadsafety instance="true"/>
    public class ClusterFixture : DockerFixture
    {
        /// <summary>
        /// Used to track how many fixture instances for the current test run
        /// remain so we can determine when to reset the cluster.
        /// </summary>
        private static int RefCount = 0;

        //---------------------------------------------------------------------
        // Instance members

        private ClusterProxy                                cluster;
        private bool                                        resetOnInitialize;
        private bool                                        disableChecks;
        private Dictionary<string, List<NodeDefinition>>    nodeGroups;

        /// <summary>
        /// Constructs the fixture.
        /// </summary>
        public ClusterFixture()
            : base(reset: false)
        {
            if (RefCount++ == 0)
            {
                // We need to wait until after we've connected to the
                // cluster before calling [Reset()].

                resetOnInitialize = true;
            }
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~ClusterFixture()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected override void Dispose(bool disposing)
        {
            if (!base.IsDisposed)
            {
                if (--RefCount <= 0)
                {
                    Reset();
                }

                Covenant.Assert(RefCount >= 0, "Reference count underflow.");
            }
        }

        /// <summary>
        /// <b>DO NOT USE:</b> This method is not supported by <see cref="ClusterFixture"/>.
        /// Use <see cref="LoginAndInitialize(string, Action)"/> instead.
        /// </summary>
        /// <param name="action">The optional custom initialization action.</param>
        /// <exception cref="NotSupportedException">Thrown always.</exception>
        public new void Initialize(Action action = null)
        {
            throw new NotSupportedException($"[{nameof(ClusterFixture)}.Initialize(Action)] is not supported.  Use {nameof(ClusterFixture)}.Initialize(string, Action)] instead.");
        }

        /// <summary>
        /// Initializes the fixture if it hasn't already been intialized
        /// by connecting the specified including invoking the optional
        /// <see cref="Action"/>.
        /// </summary>
        /// <param name="login">
        /// Optionally specifies a cluster login like <b>USER@CLUSTER</b> or you can pass
        /// <c>null</c> to connect to the cluster specified by the <b>NEON_TEST_CLUSTER</b>
        /// environment variable.
        /// </param>
        /// <param name="action">The optional initialization action.</param>
        /// <returns>
        /// <c>true</c> if the fixture wasn't previously initialized and
        /// this method call initialized it or <c>false</c> if the fixture
        /// was already initialized.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown if this is called from within the <see cref="Action"/>.</exception>
        public bool LoginAndInitialize(string login = null, Action action = null)
        {
            CheckDisposed();

            if (InAction)
            {
                throw new InvalidOperationException($"[{nameof(Initialize)}()] cannot be called recursively from within the fixture initialization action.");
            }

            if (IsInitialized)
            {
                return false;
            }

            // We need to connect the cluster before calling the base initialization
            // method.  We're going to use [neon-cli] to log out of the current
            // cluster and log into the new one.

            if (string.IsNullOrEmpty(login))
            {
                login = Environment.GetEnvironmentVariable("NEON_TEST_CLUSTER");
            }

            if (string.IsNullOrEmpty(login))
            {
                throw new ArgumentException($"[{nameof(login)}] or the NEON_TEST_CLUSTER environment variable must specify the target cluster login.");
            }

            var loginInfo = NeonClusterHelper.SplitLogin(login);

            if (!loginInfo.IsOK)
            {
                throw new ArgumentException($"Invalid username/cluster [{login}].  Expected something like: USER@CLUSTER");
            }

            var loginPath = NeonClusterHelper.GetLoginPath(loginInfo.Username, loginInfo.ClusterName);

            if (!File.Exists(loginPath))
            {
                throw new ArgumentException($"Cluster login [{login}] does not exist on the current machine and user account.");
            }

            // Use [neon-cli] to login the local machine and user account to the cluster.
            // We're going temporarily set [disableChecks=true] so [NeonExecute()] won't
            // barf because we're not connected to the cluster yet.

            try
            {
                disableChecks = true;

                var result = NeonExecute("login", login);

                if (result.ExitCode != 0)
                {
                    throw new NeonClusterException($"[neon login {login}] command failed: {result.AllText}");
                }
            }
            finally
            {
                disableChecks = false;
            }

            // Open a proxy to the cluster.

            cluster = NeonClusterHelper.OpenRemoteCluster(loginPath: loginPath);

            // We needed to defer the [Reset()] call until after the cluster
            // was connected.

            if (resetOnInitialize)
            {
                Reset();
            }

            // Initialize the inherited class.

            base.Initialize(action);

            return true;
        }

        /// <summary>
        /// Ensures that cluster is connected.
        /// </summary>
        private void CheckCluster()
        {
            if (disableChecks)
            {
                return;
            }

            if (cluster == null)
            {
                throw new InvalidOperationException("Cluster is not connected.");
            }

            var currentClusterLogin = CurrentClusterLogin.Load();

            if (currentClusterLogin == null)
            {
                throw new InvalidOperationException("Somebody logged out from under the test cluster while tests were running.");
            }

            var loginInfo = NeonClusterHelper.SplitLogin(currentClusterLogin.Login);

            if (!loginInfo.ClusterName.Equals(cluster.ClusterLogin.ClusterName, StringComparison.InvariantCultureIgnoreCase) ||
                !loginInfo.Username.Equals(cluster.ClusterLogin.Username, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new InvalidOperationException($"Somebody logged into [{currentClusterLogin.Login}] while tests were running.");
            }
        }

        /// <summary>
        /// Returns the <see cref="global::Neon.Cluster.ClusterProxy"/> for the test cluster
        /// that can be used to get information about the cluster as well as to invoke commands
        /// on individual cluster nodes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The object returned by this property has several properties that may be useful for
        /// unit tests:
        /// </para>
        /// <para>
        /// <see cref="global::Neon.Cluster.ClusterProxy.ClusterLogin"/> returns the desearialized
        /// cluster login information, including host node and HashiCorp Vault credentials. 
        /// <see cref="global::Neon.Cluster.ClusterProxy.Definition"/> returns the cluster
        /// definition.  You can also access the <see cref="global::Neon.Cluster.ClusterProxy.Nodes"/>
        /// collection to obtain <see cref="SshProxy{NodeDefinition}"/> proxies that can be used
        /// to submit SSH commands to cluster nodes.
        /// </para>
        /// <note>
        /// <b>IMPORTANT:</b> The <see cref="SshProxy{NodeDefinition}"/> class <b>is not thread-safe</b>,
        /// so you'll need to take care to run only one command at a time on each node.
        /// </note>
        /// </remarks>
        public ClusterProxy Cluster
        {
            get
            {
                base.CheckDisposed();
                this.CheckCluster();

                return cluster;
            }
        }

        /// <summary>
        /// Returns a dictionary with the cluster node groups. 
        /// </summary>
        public Dictionary<string, List<NodeDefinition>> NodeGroups
        {
            get
            {
                base.CheckDisposed();
                this.CheckCluster();

                if (nodeGroups != null)
                {
                    nodeGroups = cluster.Definition.GetNodeGroups(excludeAllGroup: true);
                }

                return nodeGroups;
            }
        }

        /// <summary>
        /// Handles error reporting for executed Docker commands.
        /// </summary>
        /// <param name="response">The command response.</param>
        /// <returns>The <paramref name="response"/> value.</returns>
        private ExecuteResult DockerExecutionReport(ExecuteResult response)
        {
            if (response.ExitCode != 0)
            {
                Console.Error.WriteLine($"[docker exitcode={response.ExitCode}]: {response.AllText}");
            }

            return response;
        }

        /// <summary>
        /// Executes an arbitrary <b>docker</b> CLI command on a cluster manager, 
        /// passing unformatted arguments and returns the results.
        /// </summary>
        /// <param name="args">The <b>docker</b> command arguments.</param>
        /// <returns>The <see cref="ExecuteResult"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method formats any arguments passed so they will be suitable 
        /// for passing on the command line by quoting and escaping them
        /// as necessary.
        /// </para>
        /// </remarks>
        public override ExecuteResult DockerExecute(params object[] args)
        {
            base.CheckDisposed();
            this.CheckCluster();

            var neonArgs = new List<object>();

            neonArgs.Add("docker");
            neonArgs.Add("--");

            foreach (var item in args)
            {
                neonArgs.Add(item);
            }

            return NeonExecutionReport(NeonHelper.ExecuteCaptureStreams("neon", neonArgs.ToArray()));
        }

        /// <summary>
        /// Executes an arbitrary <b>docker</b> CLI command on a cluster manager, 
        /// passing  a pre-formatted argument string and returns the results.
        /// </summary>
        /// <param name="argString">The <b>docker</b> command arguments.</param>
        /// <returns>The <see cref="ExecuteResult"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method assumes that the single string argument passed is already
        /// formatted as required to pass on the command line.
        /// </para>
        /// </remarks>
        public override ExecuteResult DockerExecute(string argString)
        {
            base.CheckDisposed();
            this.CheckCluster();

            var neonArgs = "docker -- " + argString;

            return DockerExecutionReport(NeonHelper.ExecuteCaptureStreams("neon", neonArgs));
        }

        /// <summary>
        /// Handles error reporting for executed Neon commands.
        /// </summary>
        /// <param name="response">The command response.</param>
        /// <returns>The <paramref name="response"/> value.</returns>
        private ExecuteResult NeonExecutionReport(ExecuteResult response)
        {
            if (response.ExitCode != 0)
            {
                Console.Error.WriteLine($"[neon exitcode={response.ExitCode}]: {response.AllText}");
            }

            return response;
        }

        /// <summary>
        /// Executes an arbitrary <b>neon</b> CLI command passing unformatted
        /// arguments and returns the results.
        /// </summary>
        /// <param name="args">The <b>neon</b> command arguments.</param>
        /// <returns>The <see cref="ExecuteResult"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method formats any arguments passed so they will be suitable 
        /// for passing on the command line by quoting and escaping them
        /// as necessary.
        /// </para>
        /// </remarks>
        public virtual ExecuteResult NeonExecute(params object[] args)
        {
            base.CheckDisposed();
            this.CheckCluster();

            return DockerExecutionReport(NeonHelper.ExecuteCaptureStreams("neon", args));
        }

        /// <summary>
        /// Executes an arbitrary <b>neon</b> CLI command passing a pre-formatted 
        /// argument string and returns the results.
        /// </summary>
        /// <param name="argString">The <b>neon</b> command arguments.</param>
        /// <returns>The <see cref="ExecuteResult"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method assumes that the single string argument passed is already
        /// formatted as required to pass on the command line.
        /// </para>
        /// </remarks>
        public virtual ExecuteResult NeonExecute(string argString)
        {
            base.CheckDisposed();
            this.CheckCluster();

            return NeonHelper.ExecuteCaptureStreams("neon", argString);
        }

        /// <summary>
        /// Resets the local Docker daemon by clearing all swarm services and state
        /// as well as removing all containers.
        /// </summary>
        /// <remarks>
        /// <note>
        /// As a safety measure, this method ensures that the local Docker instance
        /// <b>IS NOT</b> a member of a multi-node swarm to avoid wiping out production
        /// clusters by accident.
        /// </note>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed. </exception>
        public override void Reset()
        {
            base.CheckDisposed();
            this.CheckCluster();

            // $todo(jeff.lill):
            //
            // I'm not going to worry about removing any containers just yet.
            // Presumably, we'll want to leave any [neon-*] related containers
            // running by default and remove all other non-task (service or stack)
            // containers on all nodes.  One thing to think about is whether
            // this should apply to pet nodes as well.

            // Reset the basic Swarm state in parallel.

            NeonHelper.WaitForParallel(
                new Action[] {
                    () =>
                    {
                        // We're doing these together because stacks,
                        // services, and containers are all related.

                        ClearStacks();
                        ClearServices();
                     // () => ClearContainers()     // Not implemented yet
                    },
                    () => ClearLoadBalancers(),
                });

            // We're clearing these after the services and stacks so
            // we won't see any reference conflicts.  We can do these
            // in parallel too.

            NeonHelper.WaitForParallel(
                new Action[] {
                    () => ClearCertificates(),
                    () => ClearConsul(),
                    () => ClearConfigs(),
                    () => ClearDnsEntries(),
                    () => ClearNetworks(),
                    () => ClearNodes(),
                    () => ClearVault(),
                    () => ClearSecrets(),
                    () => ClearVolumes()
                });
        }

        //---------------------------------------------------------------------
        // Containers

        // $todo(jeff.lill):
        //
        // Rethink these methods.  Perhaps we can implement new ones that include
        // a node name parameter or something.  It would also be nice if Reset()
        // could actually ensure that all containers are removed from a previous
        // test run.

        /// <summary>
        /// <b>DO NOTE USE:</b> This inherited method from <see cref="DockerFixture"/> doesn't
        /// make sense for a multi-node cluster.
        /// </summary>
        /// <param name="name">The container name.</param>
        /// <param name="image">Specifies the container image.</param>
        /// <param name="dockerArgs">Optional arguments to be passed to the <b>docker service create ...</b> command.</param>
        /// <param name="containerArgs">Optional arguments to be passed to the service.</param>
        /// <param name="env">Optional environment variables to be passed to the container, formatted as <b>NAME=VALUE</b> or just <b>NAME</b>.</param>
        /// <exception cref="InvalidOperationException">Thrown always.</exception>
        public new void RunContainer(string name, string image, string[] dockerArgs = null, string[] containerArgs = null, string[] env = null)
        {
            base.CheckDisposed();
            this.CheckCluster();

            throw new InvalidOperationException($"[{nameof(ClusterFixture)}] does not currently support container deployment.");
        }

        /// <summary>
        /// <b>DO NOTE USE:</b> This inherited method from <see cref="DockerFixture"/> doesn't
        /// make sense for a multi-node cluster.
        /// </summary>
        /// <param name="includeSystem">Optionally include built-in neonCLUSTER containers whose names start with <b>neon-</b>.</param>
        /// <returns>A list of <see cref="DockerFixture.ContainerInfo"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown always.</exception>
        public new List<ContainerInfo> ListContainers(bool includeSystem = false)
        {
            base.CheckDisposed();
            this.CheckCluster();

            throw new InvalidOperationException($"[{nameof(ClusterFixture)}] does not support this method.");
        }

        /// <summary>
        /// <b>DO NOTE USE:</b> This inherited method from <see cref="DockerFixture"/> doesn't
        /// make sense for a multi-node cluster.
        /// </summary>
        /// <param name="name">The container name.</param>
        /// <exception cref="InvalidOperationException">Thrown always.</exception>
        public new void RemoveContainer(string name)
        {
            base.CheckDisposed();
            this.CheckCluster();

            throw new InvalidOperationException($"[{nameof(ClusterFixture)}] does not support this method.");
        }

        /// <summary>
        /// <b>DO NOTE USE:</b> This inherited method from <see cref="DockerFixture"/> doesn't
        /// make sense for a multi-node cluster.
        /// </summary>
        /// <param name="removeSystem">Optionally remove system services as well.</param>
        /// <remarks>
        /// By default, this method will not remove neonCLUSTER system containers
        /// whose names begin with <b>neon-</b>.  You can remove these too by
        /// passing <paramref name="removeSystem"/><c>=true</c>.
        /// </remarks>
        public new void ClearContainers(bool removeSystem = false)
        {
            throw new InvalidOperationException($"[{nameof(ClusterFixture)}] does not support this method.");
        }

        //---------------------------------------------------------------------
        // DNS

        /// <summary>
        /// Removes all local cluster DNS entries.
        /// </summary>
        /// <param name="removeSystem">Optionally remove system entries as well.</param>
        /// <remarks>
        /// By default, this method will not remove neonCLUSTER system en tries
        /// whose names begin with <b>(neon)-</b>.  You can remove these too by
        /// passing <paramref name="removeSystem"/><c>=true</c>.
        /// </remarks>
        public void ClearDnsEntries(bool removeSystem = false)
        {
            var actions = new List<Action>();

            foreach (var entry in cluster.LocalDns.List(includeSystem: removeSystem))
            {
                actions.Add(() => cluster.LocalDns.Remove(entry.Hostname));
            }

            NeonHelper.WaitForParallel(actions);
        }

        /// <summary>
        /// Lists the DNS entries persisted to the cluster.
        /// </summary>
        /// <param name="includeSystem">
        /// Optionally include the built-in system images whose names are 
        /// prefixed by <b>(neon)-</b>.
        /// </param>
        /// <returns>The list of <see cref="DnsEntry"/> instances.</returns>
        public List<DnsEntry> ListDnsEntries(bool includeSystem = false)
        {
            return cluster.LocalDns.List(includeSystem);
        }

        /// <summary>
        /// Removes the named cluster DNS entry if it exists.
        /// </summary>
        /// <param name="name">
        /// The DNS entry name.  Note that system entry names are 
        /// prefixed by <b>(neon)-</b>.
        /// </param>
        public void RemoveDnsEntry(string name)
        {
            cluster.LocalDns.Remove(name);
        }

        /// <summary>
        /// Sets a cluster DNS entry.
        /// </summary>
        /// <param name="entry">The entry to be set.</param>
        public void SetDnsEntry(DnsEntry entry)
        {
            cluster.LocalDns.Set(entry);
        }

        /// <summary>
        /// Waits for the cluster DNS state and the DNS entries to converge.
        /// </summary>
        /// <remarks>
        /// <para>
        /// It may take some time for changes made to the cluster DNS to be
        /// reflected in the DNS names actually resolved on the cluster nodes.
        /// Here's an outline of the process:
        /// </para>
        /// <list type="number">
        /// <item>
        /// The [neon-dns-mon] service checks the cluster DNS entries for 
        /// changes every 5 seconds, performs any required endpoint health 
        /// checks and then writes the actual DNS host to address mappings
        /// to Consul.
        /// </item>
        /// <item>
        /// The [neon-dns] service running on each cluster manager, checks
        /// the Consul host/address mappings generated by [neon-dns-mon]
        /// for changes every 5 seconds.  When changes is detected, 
        /// [neon-dns] creates a local file on the managers signalling
        /// the change.
        /// </item>
        /// <item>
        /// The [neon-dns-loader] systemd service running locally on each manager
        /// monitors for the signal file created by [neon-dns] when the 
        /// DNS host/address mappings have changed once a second, and signals
        /// the PowerDNS instance on the manager to reload the entries.
        /// </item>
        /// <item>
        /// All cluster nodes are configured to use the managers as their
        /// upstream name server so any DNS name resolutions will ultimately
        /// be forwarded to a manager, once and locally cached resolutions
        /// will have expired.  Cluster DNS entries are cached for 30 seconds,
        /// so it may take up to 30 seconds for a PowerDNS update to be
        /// consistent on all cluster nodes.
        /// </item>
        /// </list>
        /// <para>
        /// As you can see, it can take something like:
        /// </para>
        /// <code>
        /// 5 + 5 + 1 + 30 = 46 seconds
        /// </code>
        /// <para>
        /// For a change to the cluster DNS to ultimately be consistent on all
        /// cluster nodes.  This method waits 60 seconds to add about 15seconds
        /// for health checks and other overhead.
        /// </para>
        /// </remarks>
        public void ConvergeDns()
        {
            Thread.Sleep(TimeSpan.FromSeconds(60));
        }

        //---------------------------------------------------------------------
        // Images

        /// <summary>
        /// Removes all unreferenced images from all cluster nodes.  <see cref="Reset"/>
        /// does not do this for performance reasonse but tests may use this method
        /// if necessary.
        /// </summary>
        /// <remarks>
        /// <note>
        /// <para>
        /// Using this may result in very slow test performance, especially since
        /// it will purge a local copy of <b>neon-cli</b> if present.  This means
        /// this and any other test images (like Couchbase) will need to be
        /// downloaded again after every reset.
        /// </para>
        /// <para>
        /// We highly recommend that you use <see cref="PullImage(string)"/> to
        /// ensure that the desired images are up-to-date rather than using
        /// <see cref="ClearImages"/>.
        /// </para>
        /// </note>
        /// </remarks>
        public override void ClearImages()
        {
            base.CheckDisposed();

            var actions = new List<Action>();

            foreach (var node in cluster.Nodes)
            {
                actions.Add(
                    () =>
                    {
                        using (var nodeProxy = node.Clone())
                        {
                            nodeProxy.Connect();
                            nodeProxy.DockerCommand("docker image prune --all --force", RunOptions.None);
                        }
                    });
            }

            NeonHelper.WaitForParallel(actions);
        }

        /// <summary>
        /// Pulls a specific image to all cluster nodes.
        /// </summary>
        /// <param name="image">The image name.</param>
        public override void PullImage(string image)
        {
            base.CheckDisposed();

            var actions = new List<Action>();

            foreach (var node in cluster.Nodes)
            {
                actions.Add(
                    () =>
                    {
                        using (var nodeProxy = node.Clone())
                        {
                            nodeProxy.Connect();
                            nodeProxy.DockerCommand($"docker image pull {image}", RunOptions.None);
                        }
                    });
            }

            NeonHelper.WaitForParallel(actions);
        }

        //---------------------------------------------------------------------
        // Volumes

        /// <summary>
        /// Removes all cluster volumes from the cluster nodes.
        /// </summary>
        /// <param name="removeSystem">Optionally remove system volumes as well.</param>
        /// <remarks>
        /// By default, this method will not remove neonCLUSTER system volumes
        /// whose names begin with <b>neon-</b>.  You can remove these too by
        /// passing <paramref name="removeSystem"/><c>=true</c>.
        /// </remarks>
        public new void ClearVolumes(bool removeSystem = false)
        {
            base.CheckDisposed();

            var actions = new List<Action>();

            foreach (var node in cluster.Nodes)
            {
                actions.Add(
                    () =>
                    {
                        using (var nodeProxy = node.Clone())
                        {
                            nodeProxy.Connect();

                            var result = nodeProxy.DockerCommand(RunOptions.None, "docker", "volume", "ls", "--format", "{{.Name}}");

                            if (result.ExitCode != 0)
                            {
                                throw new Exception($"Cannot list Docker volumes: {result.AllText}");
                            }

                            var volumes = new List<string>();

                            using (var reader = new StringReader(result.OutputText))
                            {
                                foreach (var line in reader.Lines(ignoreBlank: true))
                                {
                                    if (!removeSystem && line.StartsWith("neon-", StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        continue;
                                    }

                                    if (line.Equals("neon-do-not-remove"))
                                    {
                                        // Never remove this volume.

                                        continue;
                                    }

                                    volumes.Add(line.Trim());
                                }
                            }

                            // We're going to remove the volumes one at a time so we can 
                            // ignore the [volume in use] errors.  We'll see these for
                            // built-in neonCLUSTER containers that bind-mount to the local
                            // file system.
                            //
                            // The problem is that these volumes are identified only via
                            // a UUID and there's no quick way to identify what these 
                            // volumes are mounted to, outside of inspecting all of the
                            // node containers.
                            //
                            // I believe that ignoring these errors is probably the expected
                            // behavior anyway.

                            foreach (var volume in volumes)
                            {
                                result = nodeProxy.DockerCommand(RunOptions.None, "docker", "volume", "rm", volumes);

                                if (result.ExitCode != 0 && !result.ErrorText.Contains("volume is in use"))
                                {
                                    throw new Exception($"Cannot remove Docker volume on [node={nodeProxy.Name}]: {result.AllText}");
                                }
                            }
                        }
                    });
            }

            NeonHelper.WaitForParallel(actions);
        }

        /// <summary>
        /// Lists the volumes on a named cluster node.
        /// </summary>
        /// <param name="nodeName">The target node name.</param>
        /// <returns>List the node volumes by name.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the named node doesn't exist.</exception>
        public List<string> ListVolumes(string nodeName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nodeName));

            var node = cluster.GetNode(nodeName);
            var list = new List<string>();

            var response = node.SudoCommand("docker volume ls --format \"{{.Name}}\"", RunOptions.None);

            if (response.ExitCode != 0)
            {
                throw new Exception($"[exitcode={response.ExitCode}] listing volumes on node [{nodeName}]: {response.AllText}.");
            }

            using (var reader = new StringReader(response.OutputText))
            {
                foreach (var line in reader.Lines(ignoreBlank: true))
                {
                    list.Add(line);
                }
            }

            return list;
        }

        //---------------------------------------------------------------------
        // Load balancers/rules

        /// <summary>
        /// Persists a load balancer rule object to the cluster.
        /// </summary>
        /// <param name="loadBalancerName">The load balancer name (<b>public</b> or <b>private</b>).</param>
        /// <param name="rule">The rule.</param>
        public void PutLoadBalancerRule(string loadBalancerName, LoadBalancerRule rule)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(loadBalancerName));
            Covenant.Requires<ArgumentNullException>(rule != null);

            base.CheckDisposed();
            this.CheckCluster();

            var loadBalancer = cluster.GetLoadBalancerManager(loadBalancerName);

            loadBalancer.SetRule(rule);
        }

        /// <summary>
        /// Persists a load balancer rule described as JSON or YAML text to the cluster.
        /// </summary>
        /// <param name="loadBalancer">The load balancer name (<b>public</b> or <b>private</b>).</param>
        /// <param name="jsonOrYaml">The route JSON or YAML description.</param>
        public void PutLoadBalancerRule(string loadBalancer, string jsonOrYaml)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(loadBalancer));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(jsonOrYaml));

            base.CheckDisposed();
            this.CheckCluster();

            var loadbalancer = cluster.GetLoadBalancerManager(loadBalancer);
            var rule         = LoadBalancerRule.Parse(jsonOrYaml);

            loadbalancer.SetRule(rule);
        }

        /// <summary>
        /// Lists load balancer rules.
        /// </summary>
        /// <param name="loadBalancerName">The load balancer name (<b>public</b> or <b>private</b>).</param>
        /// <param name="includeSystem">Optionally include built-in neonCLUSTER containers whose names start with <b>neon-</b>.</param>
        /// <returns>The rules for the named load balancer.</returns>
        public List<LoadBalancerRule> ListLoadBalancerRules(string loadBalancerName, bool includeSystem = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(loadBalancerName));

            base.CheckDisposed();
            this.CheckCluster();

            var loadbalancer = cluster.GetLoadBalancerManager(loadBalancerName);
            var rules        = loadbalancer.ListRules();

            if (includeSystem)
            {
                return rules.ToList();
            }
            else
            {
                return rules.Where(r => !r.Name.StartsWith("neon-", StringComparison.InvariantCultureIgnoreCase)).ToList();
            }
        }

        /// <summary>
        /// Removes a load balancer rule.
        /// </summary>
        /// <param name="loadBalancerName">The load balancer name (<b>public</b> or <b>private</b>).</param>
        /// <param name="name">The rule name.</param>
        public void RemoveLoadBalancerRule(string loadBalancerName, string name)
        {
            Covenant.Requires<ArgumentNullException>((bool)!string.IsNullOrEmpty(loadBalancerName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            base.CheckDisposed();
            this.CheckCluster();

            var loadBalancer = cluster.GetLoadBalancerManager(loadBalancerName);

            loadBalancer.RemoveRule(name);
        }

        /// <summary>
        /// Removes all load balancer rules.
        /// </summary>
        /// <param name="removeSystem">Optionally remove system rules as well.</param>
        /// <remarks>
        /// <note>
        /// This does not currently restart the proxy bridges running on 
        /// cluster pet nodes.  This may change in future releases.
        /// </note>
        /// <para>
        /// By default, this method will not remove neonCLUSTER system rules
        /// whose names begin with <b>neon-</b>.  You can remove these too by
        /// passing <paramref name="removeSystem"/><c>=true</c>.
        /// </para>
        /// </remarks>
        public void ClearLoadBalancers(bool removeSystem = false)
        {
            var deleted = false;

            foreach (var loadBalancer in new string[] { "public", "private" })
            {
                foreach (var route in ListLoadBalancerRules(loadBalancer, removeSystem))
                {
                    RemoveLoadBalancerRule(loadBalancer, route.Name);
                    deleted = true;
                }
            }

            if (deleted)
            {
                RestartLoadBalancers();
            }
        }

        /// <summary>
        /// Restarts cluster load balancers to ensure that they pick up any
        /// load balancer definition changes.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This does not currently restart the proxy bridges running on 
        /// cluster pet nodes.  This may change in future releases.
        /// </note>
        /// </remarks>
        public void RestartLoadBalancers()
        {
            // We'll restart these in parallel for better performance.

            var tasks = NeonHelper.WaitAllAsync(
                Task.Run(() => RestartPublicLoadBalancers()),
                Task.Run(() => RestartPrivateLoadbalancers()));

            tasks.Wait();
        }

        /// <summary>
        /// Restarts the <b>public</b> p[roxies to ensure that they picked up any
        /// load balancer definition changes.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This does not currently restart the proxy bridges running on 
        /// cluster pet nodes.  This may change in future releases.
        /// </note>
        /// </remarks>
        public void RestartPublicLoadBalancers()
        {
            // $todo(jeff.lill):
            //
            // We probably need to restart the proxy bridge containers on all
            // of the pets as well.

            DockerExecute("service", "update", "--force", "--update-parallelism", "0", "neon-proxy-public");
        }

        /// <summary>
        /// Restarts the <b>private</b> load balancers to ensure that they picked up any
        /// load balancer definition changes.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This does not currently restart the proxy bridges running on 
        /// cluster pet nodes.  This may change in future releases.
        /// </note>
        /// </remarks>
        public void RestartPrivateLoadbalancers()
        {
            // $todo(jeff.lill):
            //
            // We probably need to restart the proxy bridge containers on all
            // of the pets as well.

            DockerExecute("service", "update", "--force", "--update-parallelism", "0", "neon-proxy-private");
        }

        //---------------------------------------------------------------------
        // Certificates

        /// <summary>
        /// Persists a certificate to the cluster.
        /// </summary>
        /// <param name="name">The certificate name.</param>
        /// <param name="certPem">The PEM encoded certificate and private key.</param>
        public void PutCertificate(string name, string certPem)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(certPem));

            base.CheckDisposed();
            this.CheckCluster();

            cluster.Certificate.Set(name, TlsCertificate.Parse(certPem));
        }

        /// <summary>
        /// Creates and persists a self-signed certificate to the cluster.
        /// </summary>
        /// <param name="name">The certificate name.</param>
        /// <param name="hostname">
        /// <para>
        /// Specifies the hostname to be protected by the certificate
        /// like <b>test.com</b>.
        /// </para>
        /// <note>
        /// You can specify wildcard certifictes like: <b>*.test.com</b>.
        /// </note>
        /// </param>
        /// <remarks>
        /// This method is handy for verifying SSL functionality without
        /// having to worry about purchasing and/or manually generating
        /// a certificate.  The only real downside is that most HTTP
        /// clients will fail to process requests to endpoints with
        /// self-signed certificates.  You'll need to disable these
        /// checks in your test code.
        /// </remarks>
        public void PutSelfSignedCertificate(string name, string hostname)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(hostname));

            base.CheckDisposed();
            this.CheckCluster();

            cluster.Certificate.Set(name, TlsCertificate.CreateSelfSigned(hostname));
        }

        /// <summary>
        /// Lists the names of the cluster certificates.
        /// </summary>
        /// <param name="includeSystem">Optionally include built-in neonCLUSTER containers whose names start with <b>neon-</b>.</param>
        /// <returns>The certificate names.</returns>
        public List<string> ListCertificates(bool includeSystem = false)
        {
            base.CheckDisposed();
            this.CheckCluster();

            var certificates = cluster.Certificate.List();

            if (includeSystem)
            {
                return certificates.ToList();
            }
            else
            {
                return certificates.Where(name => !name.StartsWith("neon-", StringComparison.InvariantCultureIgnoreCase)).ToList();
            }
        }

        /// <summary>
        /// Removes a certificate.
        /// </summary>
        /// <param name="name">The certificate name.</param>
        public void RemoveCertificate(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            base.CheckDisposed();
            this.CheckCluster();

            cluster.Certificate.Remove(name);
        }

        /// <summary>
        /// Removes all certificates.
        /// </summary>
        /// <param name="removeSystem">Optionally remove system rules as well.</param>
        /// <remarks>
        /// By default, this method will not remove neonCLUSTER system certificates
        /// whose names begin with <b>neon-</b>.  You can remove these too by
        /// passing <paramref name="removeSystem"/><c>=true</c>.
        /// </remarks>
        public void ClearCertificates(bool removeSystem = false)
        {
            foreach (var certificate in ListCertificates(removeSystem))
            {
                RemoveCertificate(certificate);
            }
        }

        //---------------------------------------------------------------------
        // Consul

        /// <summary>
        /// <para>
        /// Returns a client that can be used to access the cluster's key/value store.
        /// </para>
        /// <note>
        /// <para>
        /// You'll need to add the following <c>using</c> statement to your test source
        /// code to gain access to the Neon related Consul extensions.
        /// </para>
        /// <code language="csharp">
        /// using Consul;
        /// </code>
        /// </note>
        /// </summary>
        public ConsulClient Consul
        {
            get { return NeonClusterHelper.Consul; }
        }

        /// <summary>
        /// Removes all non-system related keys from Consul.  System keys are
        /// located under the <b>neon*</b> and <b>vault*</b> prefixes.
        /// </summary>
        public void ClearConsul()
        {
            // We can delete all of the non-system keys in parallel for
            // better performance.

            var tasks = new List<Task>();

            foreach (var key in Consul.KV.ListKeys().Result)
            {
                if (!key.StartsWith("neon", StringComparison.InvariantCultureIgnoreCase) &&
                    !key.StartsWith("vault", StringComparison.InvariantCultureIgnoreCase))
                {
                    tasks.Add(Consul.KV.DeleteTree(key));
                }
            }

            // Clear any non-system dashboards.

            foreach (var dashboard in Consul.KV.ListOrEmpty<ClusterDashboard>(NeonClusterConst.ConsulDashboardsKey).Result)
            {
                if (dashboard.Folder == null)
                {
                    continue;
                }

                if (!dashboard.Folder.Equals(NeonClusterConst.DashboardSystemFolder, StringComparison.InvariantCultureIgnoreCase))
                {
                    tasks.Add(Consul.KV.Delete($"{NeonClusterConst.ConsulDashboardsKey}/{dashboard.Name}"));
                }
            }

            NeonHelper.WaitAllAsync(tasks).Wait();
        }

        //---------------------------------------------------------------------
        // Vault

        /// <summary>
        /// Removes all non-system related data from Vault.  System keys are
        /// located under the <b>neon*</b> and <b>vault*</b> prefixes.
        /// </summary>
        public void ClearVault()
        {
            // $todo(jeff.lill):
            //
            // This isn't implemented yet.  Here's the tracking issue:
            //
            //      https://github.com/jefflill/NeonForge/issues/235
        }

        //---------------------------------------------------------------------
        // Cluster nodes

        /// <summary>
        /// Remove containers as well as unreferenced volumes and networks from each cluster node.
        /// </summary>
        /// <param name="noContainers">Optionally disable container removal.</param>
        /// <param name="noVolumes">Optionally disable volume removal.</param>
        /// <param name="noNetworks">Optionally disable network removal.</param>
        /// <param name="removeSystem">Optionally include built-in neonCLUSTER items whose names start with <b>neon-</b>.</param>
        public void ClearNodes(bool noContainers = false, bool noVolumes = false, bool noNetworks = false, bool removeSystem = false)
        {
            var actions = new List<Action>();

            foreach (var node in cluster.Nodes)
            {
                actions.Add(
                    () =>
                    {
                        using (var nodeProxy = node.Clone()) {

                            nodeProxy.Connect();

                            // List the containers and build up a list of the container
                            // IDs we're going to remove.

                            if (!noContainers)
                            {
                                var response = nodeProxy.SudoCommand("docker ps --format {{.ID}}~{{.Names}}", RunOptions.None);

                                if (response.ExitCode != 0)
                                {
                                    throw new Exception($"Unable to list node [{nodeProxy.Name}] containers: {response.AllText}");
                                }

                                var sbDeleteIDs = new StringBuilder();

                                using (var reader = new StringReader(response.OutputText))
                                {
                                    foreach (var line in reader.Lines())
                                    {
                                        var fields = line.Split('~');
                                        var id = fields[0];
                                        var name = fields[1];

                                        if (removeSystem || !name.StartsWith("neon-", StringComparison.InvariantCultureIgnoreCase))
                                        {
                                            sbDeleteIDs.AppendWithSeparator(id);
                                        }
                                    }
                                }

                                if (sbDeleteIDs.Length > 0)
                                {
                                    response = nodeProxy.SudoCommand($"docker rm --force {sbDeleteIDs}", RunOptions.None);

                                    if (response.ExitCode != 0)
                                    {
                                        throw new Exception($"Unable to remove node [{nodeProxy.Name}] containers: {response.AllText}");
                                    }
                                }
                            }

                            // Purge any unreferenced volumes and networks.

                            if (!noVolumes)
                            {
                                var response = nodeProxy.SudoCommand($"docker volume prune --force", RunOptions.None);

                                if (response.ExitCode != 0)
                                {
                                    throw new Exception($"Unable to purge node [{nodeProxy.Name}] volumes: {response.AllText}");
                                }
                            }

                            if (!noNetworks)
                            {
                                var response = nodeProxy.SudoCommand($"docker network prune --force", RunOptions.None);

                                if (response.ExitCode != 0)
                                {
                                    throw new Exception($"Unable to purge node [{nodeProxy.Name}] networks: {response.AllText}");
                                }
                            }
                        }
                    });
            }

            NeonHelper.WaitForParallel(actions);
        }
    }
}
