//-----------------------------------------------------------------------------
// FILE:	    HiveFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using Consul;
using EasyNetQ;
using EasyNetQ.Management.Client;
using Neon.Common;
using Neon.Cryptography;
using Neon.Hive;
using Neon.HiveMQ;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

// $todo(jeff.lill):
//
// I need to think about resetting Docker registry login state.  We're
// not currently doing this.

namespace Neon.Xunit.Hive
{
    /// <summary>
    /// An Xunit test fixture used to run unit tests on a neonHIVE.
    /// </summary>
    /// <remarks>
    /// <note>
    /// <para>
    /// <b>IMPORTANT:</b> The Neon <see cref="TestFixture"/> implementation <b>DOES NOT</b>
    /// support parallel test execution because fixtures may impact global machine state
    /// like starting a Couchbase Docker container, modifying the local DNS <b>hosts</b>
    /// file or managing a Docker Swarm or neonHIVE.
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
    /// provisioned neonHIVE.  This is useful for performing integration tests
    /// within a fully functional environment.  This fixture is similar to <see cref="DockerFixture"/>
    /// and in fact, inherits some functionality from that, but <see cref="DockerFixture"/>
    /// hosts tests against a local single node Docker Swarm rather than a full
    /// neonHIVE.
    /// </para>
    /// <para>
    /// neonHIVEs do not allow the <see cref="HiveFixture"/> to perform unit
    /// tests by default, as a safety measure.  You can enable this before hive
    /// deployment by setting <see cref="HiveDefinition.AllowUnitTesting"/><c>=true</c>
    /// or by manually invoking this command for an existing hive:
    /// </para>
    /// <code>
    /// neon hive set allow-unit-testing=yes
    /// </code>
    /// <para>
    /// This fixture is pretty easy to use.  Simply have your test class inherit
    /// from <see cref="IClassFixture{HiveFixture}"/> and add a public constructor
    /// that accepts a <see cref="HiveFixture"/> as the only argument.  Then
    /// you can call it's <see cref="LoginAndInitialize(string, Action)"/> method within
    /// the constructor passing the hive login name as well as an <see cref="Action"/>.
    /// You may also use the fixture to initialize hive services, networks, secrets,
    /// load balancers, etc. within your custom action.
    /// </para>
    /// <note>
    /// Do not call the base <see cref="TestFixture.Initialize(Action)"/> method
    /// is not supported by this fixture and will throw an exception.
    /// </note>
    /// <para>
    /// The specified hive login file must be already present on the current
    /// machine for the current user.  The <see cref="LoginAndInitialize(string, Action)"/> method will
    /// logout from the current hive (if any) and then login to the one specified.
    /// </para>
    /// <note>
    /// You can also specify a <c>null</c> or empty login name.  In this case,
    /// the fixture will attempt to retrieve the login name from the <b>NEON_TEST_HIVE</b>
    /// environment variable.  This is very handy because it allows developers to
    /// specify different target test hives without having to bake this into the 
    /// unit tests themselves.
    /// </note>
    /// <para>
    /// This fixture provides several methods and properties for managing the 
    /// hive state.  These may be called within the test class constructor's 
    /// action method, within the test constructor but outside of the action, 
    /// or within the test class methods:
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>Misc</b></term>
    ///     <description>
    ///     <see cref="ClearConsul()"/>
    ///     <see cref="ClearHiveMQ()"/>
    ///     <see cref="ClearVault()"/>
    ///     <see cref="Hive"/><br/>
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
    ///     <see cref="RemoveCertificate(string)"/><br/>
    ///     <see cref="SetSelfSignedCertificate(string, string, Wildcard)"/>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>Configs</b></term>
    ///     <description>
    ///     <see cref="DockerFixture.ClearConfigs(bool)"/><br/>
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
    ///     <see cref="SetDnsEntry(DnsEntry, bool)"/>
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
    ///     <term><b>Traffic Director Rules</b></term>
    ///     <description>
    ///     <see cref="ClearTrafficManagers(bool)"/><br/>
    ///     <see cref="ListTrafficManagers(string, bool)"/><br/>
    ///     <see cref="PutTrafficManagerRule(string, TrafficRule, bool)"/><br/>
    ///     <see cref="RemoveTrafficManagerRule(string, string, bool)"/><br/>
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
    /// <see cref="HiveFixture"/> derives from <see cref="TestFixtureSet"/> so you can
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
    ///     The basic idea here is to have your test class initialize the hive
    ///     once within the test class constructor inside of the initialize action
    ///     with common state and services that all of the tests can access.
    ///     </para>
    ///     <para>
    ///     This will be quite a bit faster than reconfiguring the hive at the
    ///     beginning of every test and can work well for many situations.
    ///     </para>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>initialize every test</b></term>
    ///     <description>
    ///     For scenarios where the hive must be cleared before every test,
    ///     you can use the <see cref="Reset()"/> method to reset its
    ///     state within each test method, populate the hive as necessary,
    ///     and then perform your tests.
    ///     </description>
    /// </item>
    /// </list>
    /// </remarks>
    /// <threadsafety instance="true"/>
    public class HiveFixture : DockerFixture
    {
        /// <summary>
        /// Used to track how many fixture instances for the current test run
        /// remain so we can determine when to reset the hive.
        /// </summary>
        private static int RefCount = 0;

        //---------------------------------------------------------------------
        // Instance members

        private object                                      syncLock   = new object();
        private HiveProxy                                   hive;
        private bool                                        resetOnInitialize;
        private bool                                        disableChecks;
        private Dictionary<string, List<NodeDefinition>>    nodeGroups;

        /// <summary>
        /// Constructs the fixture.
        /// </summary>
        public HiveFixture()
            : base(reset: false)
        {
            if (RefCount++ == 0)
            {
                // We need to wait until after we've connected to the
                // hive before calling [Reset()].

                resetOnInitialize = true;
            }
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~HiveFixture()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!base.IsDisposed)
                {
                    if (--RefCount <= 0)
                    {
                        Reset();
                    }

                    Covenant.Assert(RefCount >= 0, "Reference count underflow.");
                }

                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// <b>DO NOT USE:</b> This method is not supported by <see cref="HiveFixture"/>.
        /// Use <see cref="LoginAndInitialize(string, Action)"/> instead.
        /// </summary>
        /// <param name="action">The optional custom initialization action.</param>
        /// <exception cref="NotSupportedException">Thrown always.</exception>
        public new void Initialize(Action action = null)
        {
            throw new NotSupportedException($"[{nameof(HiveFixture)}.Initialize(Action)] is not supported.  Use {nameof(HiveFixture)}.Initialize(string, Action)] instead.");
        }

        /// <summary>
        /// Initializes the fixture if it hasn't already been intialized by
        /// connecting to a hive and invoking the optional initialization
        /// <see cref="Action"/>.
        /// </summary>
        /// <param name="login">
        /// Optionally specifies a hive login like <b>USER@HIVE</b> or you can pass
        /// <c>null</c> to connect to the hive specified by the <b>NEON_TEST_HIVE</b>
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

            // We need to connect the hive before calling the base initialization
            // method.  We're going to use [neon-cli] to log out of the current
            // hive and log into the new one.

            if (string.IsNullOrEmpty(login))
            {
                login = Environment.GetEnvironmentVariable("NEON_TEST_HIVE");
            }

            if (string.IsNullOrEmpty(login))
            {
                throw new ArgumentException($"[{nameof(login)}] or the NEON_TEST_HIVE environment variable must specify the target hive login.");
            }

            var loginInfo = HiveHelper.SplitLogin(login);

            if (!loginInfo.IsOK)
            {
                throw new ArgumentException($"Invalid username/hive [{login}].  Expected something like: USER@HIVE");
            }

            var loginPath = HiveHelper.GetLoginPath(loginInfo.Username, loginInfo.HiveName);

            if (!File.Exists(loginPath))
            {
                throw new ArgumentException($"Cluster login [{login}] does not exist on the current machine and user account.");
            }

            // Use [neon-cli] to login the local machine and user account to the hive.
            // We're going temporarily set [disableChecks=true] so [NeonExecute()] won't
            // barf because we're not connected to the hive yet.

            try
            {
                disableChecks = true;

                var result = NeonExecute("login", login);

                if (result.ExitCode != 0)
                {
                    throw new HiveException($"[neon login {login}] command failed: {result.AllText}");
                }
            }
            finally
            {
                disableChecks = false;
            }

            // Open a proxy to the hive.

            hive = HiveHelper.OpenHiveRemote(loginPath: loginPath);

            // Ensure that the target hive allows unit testing.

            if (!hive.Globals.TryGetBool(HiveGlobals.UserAllowUnitTesting, out var allowUnitTesting))
            {
                allowUnitTesting = false;
            }

            if (!allowUnitTesting)
            {
                throw new NotSupportedException($"The [{hive.Name}] hive does not support unit testing.  Use the [neon hive set allow-unit-testing=true] command to enable this.");
            }

            // Enable Docker secret retrieval so we'll be able to obtain
            // secrets like the HiveMQ settings.

            HiveHelper.EnableSecretRetrival();

            // We needed to defer the [Reset()] call until after the hive
            // was connected.

            if (resetOnInitialize)
            {
                Reset();
            }

            // Initialize the hosts fixture.

            Hosts = new HostsFixture();

            // Initialize the base class.

            base.Initialize(action);

            return true;
        }

        /// <summary>
        /// Ensures that hive is connected.
        /// </summary>
        private void CheckCluster()
        {
            if (disableChecks)
            {
                return;
            }

            if (hive == null)
            {
                throw new InvalidOperationException("Cluster is not connected.");
            }

            var currentHiveLogin = CurrentHiveLogin.Load();

            if (currentHiveLogin == null)
            {
                throw new InvalidOperationException("Somebody logged out from under the test hive while tests were running.");
            }

            var loginInfo = HiveHelper.SplitLogin(currentHiveLogin.Login);

            if (!loginInfo.HiveName.Equals(hive.HiveLogin.HiveName, StringComparison.InvariantCultureIgnoreCase) ||
                !loginInfo.Username.Equals(hive.HiveLogin.Username, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new InvalidOperationException($"Somebody logged into [{currentHiveLogin.Login}] while tests were running.");
            }
        }

        /// <summary>
        /// Returns the <see cref="global::Neon.Hive.HiveProxy"/> for the test hive
        /// that can be used to get information about the hive as well as to invoke commands
        /// on individual hive nodes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The object returned by this property has several properties that may be useful for
        /// unit tests:
        /// </para>
        /// <para>
        /// <see cref="global::Neon.Hive.HiveProxy.HiveLogin"/> returns the desearialized
        /// hive login information, including host node and HashiCorp Vault credentials. 
        /// <see cref="global::Neon.Hive.HiveProxy.Definition"/> returns the hive
        /// definition.  You can also access the <see cref="global::Neon.Hive.HiveProxy.Nodes"/>
        /// collection to obtain <see cref="SshProxy{NodeDefinition}"/> proxies that can be used
        /// to submit SSH commands to hive nodes.
        /// </para>
        /// <note>
        /// <b>IMPORTANT:</b> The <see cref="SshProxy{NodeDefinition}"/> class <b>is not thread-safe</b>,
        /// so you'll need to take care to run only one command at a time on each node.
        /// </note>
        /// </remarks>
        public HiveProxy Hive
        {
            get
            {
                base.CheckDisposed();
                this.CheckCluster();

                return hive;
            }
        }

        /// <summary>
        /// Returns a dictionary with the hive node groups. 
        /// </summary>
        public Dictionary<string, List<NodeDefinition>> NodeGroups
        {
            get
            {
                base.CheckDisposed();
                this.CheckCluster();

                if (nodeGroups != null)
                {
                    nodeGroups = hive.Definition.GetHostGroups(excludeAllGroup: true);
                }

                return nodeGroups;
            }
        }

        /// <summary>
        /// Returns a <see cref="HostsFixture"/> that can be used to manage local
        /// DNS host mappings.  Note that this fixture is reset by <see cref="Reset"/>.
        /// </summary>
        public HostsFixture Hosts { get; private set; }

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
        /// Executes an arbitrary <b>docker</b> CLI command on a hive manager, 
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
            neonArgs.Add("--shim");     // We need to shim the command so [neon-cli] won't clear local [hosts] file definitions.
            neonArgs.Add("--");

            foreach (var item in args)
            {
                neonArgs.Add(item);
            }

            return NeonExecutionReport(NeonHelper.ExecuteCapture("neon", neonArgs.ToArray()));
        }

        /// <summary>
        /// Executes an arbitrary <b>docker</b> CLI command on a hive manager, 
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

            var neonArgs = "docker --shim -- " + argString; // We need to shim the command so [neon-cli] won't clear local [hosts] file definitions.

            return DockerExecutionReport(NeonHelper.ExecuteCapture("neon", neonArgs));
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

            // We need to shim the command so [neon-cli] won't clear local [hosts] file definitions.

            var argList = args.ToList();

            argList.Insert(0, "--shim");

            return DockerExecutionReport(NeonHelper.ExecuteCapture("neon", argList.ToArray()));
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

            // We need to shim the command so [neon-cli] won't clear local [hosts] file definitions.

            argString = "--shim " + argString;

            return NeonHelper.ExecuteCapture("neon", argString);
        }

        /// <summary>
        /// Resets the local Docker daemon by clearing all swarm services and state
        /// as well as removing all containers.
        /// </summary>
        /// <remarks>
        /// <note>
        /// As a safety measure, this method ensures that the local Docker instance
        /// <b>IS NOT</b> a member of a multi-node swarm to avoid wiping out production
        /// hives by accident.
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
                    () => ClearTrafficManagers(),
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
                    () => ClearVolumes(),
                    () => ClearHiveMQ()
                });

            // We also need to reset any temporary DNS host records.

            Hosts?.Reset();
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
        /// make sense for a multi-node hive.
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

            throw new InvalidOperationException($"[{nameof(HiveFixture)}] does not currently support container deployment.");
        }

        /// <summary>
        /// <b>DO NOTE USE:</b> This inherited method from <see cref="DockerFixture"/> doesn't
        /// make sense for a multi-node hive.
        /// </summary>
        /// <param name="includeSystem">Optionally include built-in neonHIVE containers whose names start with <b>neon-</b>.</param>
        /// <returns>A list of <see cref="DockerFixture.ContainerInfo"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown always.</exception>
        public new List<ContainerInfo> ListContainers(bool includeSystem = false)
        {
            base.CheckDisposed();
            this.CheckCluster();

            throw new InvalidOperationException($"[{nameof(HiveFixture)}] does not support this method.");
        }

        /// <summary>
        /// <b>DO NOTE USE:</b> This inherited method from <see cref="DockerFixture"/> doesn't
        /// make sense for a multi-node hive.
        /// </summary>
        /// <param name="name">The container name.</param>
        /// <exception cref="InvalidOperationException">Thrown always.</exception>
        public new void RemoveContainer(string name)
        {
            base.CheckDisposed();
            this.CheckCluster();

            throw new InvalidOperationException($"[{nameof(HiveFixture)}] does not support this method.");
        }

        /// <summary>
        /// <b>DO NOTE USE:</b> This inherited method from <see cref="DockerFixture"/> doesn't
        /// make sense for a multi-node hive.
        /// </summary>
        /// <param name="removeSystem">Optionally remove system services as well.</param>
        /// <remarks>
        /// By default, this method will not remove neonHIVE system containers
        /// whose names begin with <b>neon-</b>.  You can remove these too by
        /// passing <paramref name="removeSystem"/><c>=true</c>.
        /// </remarks>
        public new void ClearContainers(bool removeSystem = false)
        {
            throw new InvalidOperationException($"[{nameof(HiveFixture)}] does not support this method.");
        }

        //---------------------------------------------------------------------
        // Hive DNS

        /// <summary>
        /// Removes all local hive DNS entries.
        /// </summary>
        /// <param name="removeSystem">Optionally remove system entries as well.</param>
        /// <remarks>
        /// <para>
        /// By default, this method will not remove neonHIVE system DNS entries.  
        /// You can remove these too by passing <paramref name="removeSystem"/><c>=true</c>.
        /// </para>
        /// <note>
        /// Any system DNS entries with hostnames that start with <b>xunit-</b> 
        /// will also be removed.  These are assumed to be present for system
        /// related unit tests.
        /// </note>
        /// </remarks>
        public void ClearDnsEntries(bool removeSystem = false)
        {
            var actions = new List<Action>();

            foreach (var entry in hive.Dns.List(includeSystem: true))
            {
                if (removeSystem || !entry.IsSystem || entry.Hostname.StartsWith("xunit-"))
                {
                    actions.Add(() => hive.Dns.Remove(entry.Hostname));
                }
            }

            if (actions.Count > 0)
            {
                NeonHelper.WaitForParallel(actions);

                // Fetch the current hive time and the last time the DNS was changed
                // and then wait long enough such that at least [ClearDelay] time has
                // elapsed since the DNS was last changed to ensure that any DNS answers
                // cached on hive nodes have a chance to be evicted.

                var hiveTime           = hive.GetTimeUtc();
                var lastDnsChangeTime  = hive.Dns.GetChangeTime();
                var elapsedSinceChange = (hiveTime - lastDnsChangeTime);

                if (elapsedSinceChange < TimeSpan.Zero)
                {
                    // This shouldn't ever happen but we'll wait the full time just to be safe.

                    Thread.Sleep(ClearDelay);
                }
                else if (elapsedSinceChange < ClearDelay)
                {
                    // Delay enough such that there [ClearDelay] has passed since the last change.

                    Thread.Sleep(ClearDelay - elapsedSinceChange);
                }
                else
                {
                    // At least [ClearDelay] time has passed since the last change so we don't
                    // need to delay any longer.
                }
            }
        }

        /// <summary>
        /// Lists the DNS entries persisted to the hive.
        /// </summary>
        /// <param name="includeSystem">
        /// Optionally include the built-in system DNS entries.
        /// </param>
        /// <returns>The list of <see cref="DnsEntry"/> instances.</returns>
        public List<DnsEntry> ListDnsEntries(bool includeSystem = false)
        {
            return hive.Dns.List(includeSystem);
        }

        /// <summary>
        /// Removes the named hive DNS entry if it exists.
        /// </summary>
        /// <param name="name">The DNS entry name.</param>
        public void RemoveDnsEntry(string name)
        {
            hive.Dns.Remove(name);
        }

        /// <summary>
        /// Sets a hive DNS entry.
        /// </summary>
        /// <param name="entry">The entry to be set.</param>
        /// <param name="waitUntilPropagated">
        /// Optionally signals hive nodes to wipe their DNS cache and reload local hosts
        /// so the changes will be proactively  propagated across the hive.  This defaults
        /// to <c>false</c>.
        /// </param>
        public void SetDnsEntry(DnsEntry entry, bool waitUntilPropagated = false)
        {
            hive.Dns.Set(entry, waitUntilPropagated);
        }

        /// <summary>
        /// Poactively signals the hive to wipe any cached DNS entries and reload
        /// any local host definitions.
        /// </summary>
        public void ConvergeDns()
        {
            hive.Dns.Reload(wipe: true);
        }

        //---------------------------------------------------------------------
        // Images

        /// <summary>
        /// Removes all unreferenced images from all hive nodes.  <see cref="Reset"/>
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

            foreach (var node in hive.Nodes)
            {
                actions.Add(
                    () =>
                    {
                        using (var nodeClone = node.Clone())
                        {
                            nodeClone.DockerCommand("docker image prune --all --force", RunOptions.None);
                        }
                    });
            }

            if (actions.Count > 0)
            {
                NeonHelper.WaitForParallel(actions);
                Thread.Sleep(ClearDelay);
            }
        }

        /// <summary>
        /// Pulls a specific image to all hive nodes.
        /// </summary>
        /// <param name="image">The image name.</param>
        public override void PullImage(string image)
        {
            base.CheckDisposed();

            var actions = new List<Action>();

            foreach (var node in hive.Nodes)
            {
                actions.Add(
                    () =>
                    {
                        using (var nodeClone = node.Clone())
                        {
                            nodeClone.DockerCommand($"docker image pull {image}", RunOptions.None);
                        }
                    });
            }

            NeonHelper.WaitForParallel(actions);
        }

        //---------------------------------------------------------------------
        // Volumes

        /// <summary>
        /// Removes all Docker volumes from the hive nodes.
        /// </summary>
        /// <param name="removeSystem">Optionally remove system volumes as well.</param>
        /// <remarks>
        /// By default, this method will not remove neonHIVE system volumes
        /// whose names begin with <b>neon-</b>.  You can remove these too by
        /// passing <paramref name="removeSystem"/><c>=true</c>.
        /// </remarks>
        public new void ClearVolumes(bool removeSystem = false)
        {
            base.CheckDisposed();

            var actions = new List<Action>();

            foreach (var node in hive.Nodes)
            {
                actions.Add(
                    () =>
                    {
                        using (var nodeClone = node.Clone())
                        {
                            var result = nodeClone.DockerCommand(RunOptions.None, "docker", "volume", "ls", "--format", "{{.Name}}");

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
                            // built-in neonHIVE containers that bind-mount to the local
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
                                result = nodeClone.DockerCommand(RunOptions.None, "docker", "volume", "rm", volumes);

                                if (result.ExitCode != 0 && !result.ErrorText.Contains("volume is in use"))
                                {
                                    throw new Exception($"Cannot remove Docker volume on [node={nodeClone.Name}]: {result.AllText}");
                                }
                            }
                        }
                    });
            }

            if (actions.Count > 0)
            {
                NeonHelper.WaitForParallel(actions);
                Thread.Sleep(ClearDelay);
            }
        }

        /// <summary>
        /// Lists the volumes on a named hive node.
        /// </summary>
        /// <param name="nodeName">The target node name.</param>
        /// <returns>List the node volumes by name.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the named node doesn't exist.</exception>
        public List<string> ListVolumes(string nodeName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nodeName));

            var node = hive.GetNode(nodeName);
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
        /// Persists a traffic manager rule object to the hive.
        /// </summary>
        /// <param name="directorName">The traffic manager name (<b>public</b> or <b>private</b>).</param>
        /// <param name="rule">The rule.</param>
        /// <param name="deferUpdate">
        /// <para>
        /// Optionally defers expicitly notifying the <b>neon-proxy-manager</b> of the
        /// change until <see cref="TrafficManager.Update()"/> is called or the <b>neon-proxy-manager</b>
        /// performs the periodic check for changes (which defaults to 60 seconds).  You
        /// may consider passing <paramref name="deferUpdate"/><c>=true</c> when you are
        /// modifying a multiple rules at the same time to avoid making the proxy manager
        /// and proxy instances handle each rule change individually.
        /// </para>
        /// <para>
        /// Instead, you could pass <paramref name="deferUpdate"/><c>=true</c> for all of
        /// the rule changes and then call <see cref="TrafficManager.Update()"/> afterwards.
        /// </para>
        /// </param>
        public void PutTrafficManagerRule(string directorName, TrafficRule rule, bool deferUpdate = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(directorName));
            Covenant.Requires<ArgumentNullException>(rule != null);

            base.CheckDisposed();
            this.CheckCluster();

            var trafficManager = hive.GetTrafficManager(directorName);

            trafficManager.SetRule(rule, deferUpdate: deferUpdate);
        }

        /// <summary>
        /// Persists a traffic manager rule described as JSON or YAML text to the hive.
        /// </summary>
        /// <param name="directorName">The traffic manager name (<b>public</b> or <b>private</b>).</param>
        /// <param name="jsonOrYaml">The route JSON or YAML description.</param>
        /// <param name="deferUpdate">
        /// <para>
        /// Optionally defers expicitly notifying the <b>neon-proxy-manager</b> of the
        /// change until <see cref="TrafficManager.Update()"/> is called or the <b>neon-proxy-manager</b>
        /// performs the periodic check for changes (which defaults to 60 seconds).  You
        /// may consider passing <paramref name="deferUpdate"/><c>=true</c> when you are
        /// modifying a multiple rules at the same time to avoid making the proxy manager
        /// and proxy instances handle each rule change individually.
        /// </para>
        /// <para>
        /// Instead, you could pass <paramref name="deferUpdate"/><c>=true</c> for all of
        /// the rule changes and then call <see cref="TrafficManager.Update()"/> afterwards.
        /// </para>
        /// </param>
        public void PutTrafficManagerRule(string directorName, string jsonOrYaml, bool deferUpdate = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(directorName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(jsonOrYaml));

            base.CheckDisposed();
            this.CheckCluster();

            var directorManager = hive.GetTrafficManager(directorName);
            var rule            = TrafficRule.Parse(jsonOrYaml);

            directorManager.SetRule(rule);
        }

        /// <summary>
        /// Lists traffic manager rules.
        /// </summary>
        /// <param name="directorName">The traffic manager name (<b>public</b> or <b>private</b>).</param>
        /// <param name="includeSystem">Optionally include built-in neonHIVE containers whose names start with <b>neon-</b>.</param>
        /// <returns>The rules for the named traffic manager.</returns>
        public List<TrafficRule> ListTrafficManagers(string directorName, bool includeSystem = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(directorName));

            base.CheckDisposed();
            this.CheckCluster();

            var trafficManager = hive.GetTrafficManager(directorName);
            var rules          = trafficManager.ListRules();

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
        /// Removes a traffic manager rule.
        /// </summary>
        /// <param name="directorName">The traffic manager name (<b>public</b> or <b>private</b>).</param>
        /// <param name="name">The rule name.</param>
        /// <param name="deferUpdate">
        /// <para>
        /// Optionally defers expicitly notifying the <b>neon-proxy-manager</b> of the
        /// change until <see cref="TrafficManager.Update()"/> is called or the <b>neon-proxy-manager</b>
        /// performs the periodic check for changes (which defaults to 60 seconds).  You
        /// may consider passing <paramref name="deferUpdate"/><c>=true</c> when you are
        /// modifying a multiple rules at the same time to avoid making the proxy manager
        /// and proxy instances handle each rule change individually.
        /// </para>
        /// <para>
        /// Instead, you could pass <paramref name="deferUpdate"/><c>=true</c> for all of
        /// the rule changes and then call <see cref="TrafficManager.Update()"/> afterwards.
        /// </para>
        /// </param>
        public void RemoveTrafficManagerRule(string directorName, string name, bool deferUpdate = false)
        {
            Covenant.Requires<ArgumentNullException>((bool)!string.IsNullOrEmpty(directorName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            base.CheckDisposed();
            this.CheckCluster();

            var trafficManager = hive.GetTrafficManager(directorName);

            trafficManager.RemoveRule(name, deferUpdate);
        }

        /// <summary>
        /// Removes all traffic manager rules.
        /// </summary>
        /// <param name="removeSystem">Optionally remove system rules as well.</param>
        /// <remarks>
        /// <note>
        /// This does not currently restart the proxy bridges running on 
        /// hive pet nodes.  This may change in future releases.
        /// </note>
        /// <para>
        /// By default, this method will not remove neonHIVE system rules
        /// whose names begin with <b>neon-</b>.  You can remove these too by
        /// passing <paramref name="removeSystem"/><c>=true</c>.
        /// </para>
        /// </remarks>
        public void ClearTrafficManagers(bool removeSystem = false)
        {
            var deletions = false;

            foreach (var trafficManagerName in new string[] { "public", "private" })
            {
                foreach (var route in ListTrafficManagers(trafficManagerName, removeSystem))
                {
                    RemoveTrafficManagerRule(trafficManagerName, route.Name, deferUpdate: true);
                    deletions = true;
                }
            }

            if (deletions)
            {
                hive.PublicTraffic.Update();
                hive.PrivateTraffic.Update();
            }

            // Purge all cached content.

            hive.PublicTraffic.PurgeAll();
            hive.PrivateTraffic.PurgeAll();
            Thread.Sleep(TimeSpan.FromSeconds(2));  // Give the purge some time to complete.
        }

        //---------------------------------------------------------------------
        // Certificates

        /// <summary>
        /// Persists a certificate to the hive.
        /// </summary>
        /// <param name="name">The certificate name.</param>
        /// <param name="certPem">The PEM encoded certificate and private key.</param>
        public void PutCertificate(string name, string certPem)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(certPem));

            base.CheckDisposed();
            this.CheckCluster();

            hive.Certificate.Set(name, TlsCertificate.Parse(certPem));
        }

        /// <summary>
        /// Creates and persists a self-signed certificate to the hive.
        /// </summary>
        /// <param name="name">The certificate name.</param>
        /// <param name="hostname">
        /// <para>
        /// Specifies the hostname to be protected by the certificate
        /// like <b>test.com</b>.
        /// </para>
        /// </param>
        /// <param name="wildcard">
        /// Optionally create a wildcard by specifying
        /// <see cref="Wildcard.SubdomainsOnly"/> or 
        /// <see cref="Wildcard.RootAndSubdomains"/>.
        /// </param>
        /// <remarks>
        /// This method is handy for verifying SSL functionality without
        /// having to worry about purchasing and/or manually generating
        /// a certificate.  The only real downside is that most HTTP
        /// clients will fail to process requests to endpoints with
        /// self-signed certificates.  You'll need to disable these
        /// checks in your test code.
        /// </remarks>
        public void SetSelfSignedCertificate(string name, string hostname, Wildcard wildcard = Wildcard.None)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(hostname));

            base.CheckDisposed();
            this.CheckCluster();

            hive.Certificate.Set(name, TlsCertificate.CreateSelfSigned(hostname, wildcard: wildcard));
        }

        /// <summary>
        /// Lists the names of the hive certificates.
        /// </summary>
        /// <param name="includeSystem">Optionally include built-in neonHIVE containers whose names start with <b>neon-</b>.</param>
        /// <returns>The certificate names.</returns>
        public List<string> ListCertificates(bool includeSystem = false)
        {
            base.CheckDisposed();
            this.CheckCluster();

            var certificates = hive.Certificate.List();

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

            hive.Certificate.Remove(name);
        }

        /// <summary>
        /// Removes all certificates.
        /// </summary>
        /// <param name="removeSystem">Optionally remove system certificates as well.</param>
        /// <remarks>
        /// By default, this method will not remove neonHIVE system certificates
        /// whose names begin with <b>neon-</b>.  You can remove these too by
        /// passing <paramref name="removeSystem"/><c>=true</c>.
        /// </remarks>
        public void ClearCertificates(bool removeSystem = false)
        {
            var actions = new List<Action>();

            foreach (var certificate in ListCertificates(removeSystem))
            {
                actions.Add(() => RemoveCertificate(certificate));
            }

            if (actions.Count > 0)
            {
                NeonHelper.WaitForParallel(actions);
                Thread.Sleep(ClearDelay);
            }
        }

        //---------------------------------------------------------------------
        // Consul

        /// <summary>
        /// <para>
        /// Returns a client that can be used to access the hive's key/value store.
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
            get { return HiveHelper.Consul; }
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

            foreach (var dashboard in hive.Dashboard.List())
            {
                if (dashboard.Folder == null)
                {
                    continue;
                }

                if (!dashboard.Folder.Equals(HiveConst.DashboardSystemFolder, StringComparison.InvariantCultureIgnoreCase))
                {
                    tasks.Add(Task.Run(() => hive.Dashboard.Remove(dashboard.Name)));
                }
            }

            if (tasks.Count > 0)
            {
                NeonHelper.WaitAllAsync(tasks).Wait();
                Thread.Sleep(ClearDelay);
            }
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
        // Hive nodes

        /// <summary>
        /// Remove containers as well as unreferenced volumes and networks from each hive node.
        /// </summary>
        /// <param name="noContainers">Optionally disable container removal.</param>
        /// <param name="noVolumes">Optionally disable volume removal.</param>
        /// <param name="noNetworks">Optionally disable network removal.</param>
        /// <param name="removeSystem">Optionally include built-in neonHIVE items whose names start with <b>neon-</b>.</param>
        public void ClearNodes(bool noContainers = false, bool noVolumes = false, bool noNetworks = false, bool removeSystem = false)
        {
            var actions = new List<Action>();

            foreach (var node in hive.Nodes)
            {
                actions.Add(
                    () =>
                    {
                        using (var nodeClone = node.Clone()) {

                            // List the containers and build up a list of the container
                            // IDs we're going to remove.

                            if (!noContainers)
                            {
                                var response = nodeClone.SudoCommand("docker ps --format {{.ID}}~{{.Names}}", RunOptions.None);

                                if (response.ExitCode != 0)
                                {
                                    throw new Exception($"Unable to list node [{nodeClone.Name}] containers: {response.AllText}");
                                }

                                var sbDeleteIDs = new StringBuilder();

                                using (var reader = new StringReader(response.OutputText))
                                {
                                    foreach (var line in reader.Lines())
                                    {
                                        var fields = line.Split('~');
                                        var id     = fields[0];
                                        var name   = fields[1];

                                        if (removeSystem || !name.StartsWith("neon-", StringComparison.InvariantCultureIgnoreCase))
                                        {
                                            sbDeleteIDs.AppendWithSeparator(id);
                                        }
                                    }
                                }

                                if (sbDeleteIDs.Length > 0)
                                {
                                    response = nodeClone.SudoCommand($"docker rm --force {sbDeleteIDs}", RunOptions.None);

                                    if (response.ExitCode != 0)
                                    {
                                        throw new Exception($"Unable to remove node [{nodeClone.Name}] containers: {response.AllText}");
                                    }
                                }
                            }

                            // Purge any unreferenced volumes and networks.

                            if (!noVolumes)
                            {
                                var response = nodeClone.SudoCommand($"docker volume prune --force", RunOptions.None);

                                if (response.ExitCode != 0)
                                {
                                    throw new Exception($"Unable to purge node [{nodeClone.Name}] volumes: {response.AllText}");
                                }
                            }

                            if (!noNetworks)
                            {
                                var response = nodeClone.SudoCommand($"docker network prune --force", RunOptions.None);

                                if (response.ExitCode != 0)
                                {
                                    throw new Exception($"Unable to purge node [{nodeClone.Name}] networks: {response.AllText}");
                                }
                            }
                        }
                    });
            }

            if (actions.Count > 0)
            {
                NeonHelper.WaitForParallel(actions);
                Thread.Sleep(ClearDelay);
            }
        }

        //---------------------------------------------------------------------
        // HiveMQ

        /// <summary>
        /// Returns HiveMQ cluster settings for the <b>sysadmin</b> user.
        /// </summary>
        public HiveMQSettings GetHiveMQSettings()
        {
            if (Hive.Globals.TryGetObject<HiveMQSettings>(HiveGlobals.HiveMQSettingSysadmin, out var settings))
            {
                return settings;
            }
            else
            {
                throw new KeyNotFoundException($"Unable to find or parse [{hive.Globals.GetKey(HiveGlobals.HiveMQSettingSysadmin)}] in Consul.");
            }
        }

        /// <summary>
        /// Returns a lower-level <see cref="IConnection"/> that can be used to
        /// perform messaging operations on HiveMQ.  This connects as the 
        /// <b>sysadmin</b> user.
        /// </summary>
        public IConnection ConnectHiveMQConnection()
        {
            return GetHiveMQSettings().ConnectRabbitMQ();
        }

        /// <summary>
        /// Returns a high-level <see cref="IBus"/> instance that can be used to 
        /// perform messaging operations on the HiveMQ.  This connects as the
        /// <b>sysadmin</b> user.
        /// </summary>
        public IBus ConnectHiveMQBus()
        {
            return GetHiveMQSettings().ConnectEasyNetQ();
        }

        /// <summary>
        /// Returns a <see cref="ManagementClient"/> you can use the manage
        /// the HiveMQ.  This connects as the <b>sysadmin</b> user.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The object returned is thread-safe and most applications should
        /// establish a single connection and then share that for all operations.  
        /// Creating and disposing connections for each operation will be inefficient.
        /// </para>
        /// <note>
        /// The instance returned should be disposed when you are done with it.
        /// </note>
        /// </remarks>
        public ManagementClient ConnectHiveMQManager()
        {
            var hiveMQSettings = GetHiveMQSettings();

            if (hiveMQSettings == null)
            {
                throw new HiveException("Unable to obtain the [neon-hivemq-sysadmin] Docker secret.  This needs to be mounted into the current application.");
            }

            return hiveMQSettings.ConnectManager();
        }

        /// <summary>
        /// Resets the HiveMQ state.
        /// </summary>
        private void ClearHiveMQ()
        {
            // We're going to assume that unit tests do not use or modify
            // the [neon] vhost or user and have not modified the [sysadmin] 
            // credentials or permissions.
            //
            // So here's what we need to do to reset the state:
            //
            //      1. Remove any vhosts besides [/] and [neon].  This will
            //         remove any related queues and other state.
            //      2. Remove any users besides [sysadmin] and [neon].  This
            //         will remove [app] too.
            //      3. Recreate the [app] user.
            //      4. Recreate the [app] vhost.
            //      5. Set the [app] user permissions for the [app] vhost.

            using (var mqManager = ConnectHiveMQManager())
            {
                // Remove vhosts other than [/] and [neon].

                foreach (var vhost in mqManager.GetVHostsAsync().Result)
                {
                    if (vhost.Name != "/" && vhost.Name != HiveConst.HiveMQNeonVHost)
                    {
                        mqManager.DeleteVirtualHostAsync(vhost).Wait();
                    }
                }

                // Remove users other than [sysadmin] and [neon].

                foreach (var user in mqManager.GetUsersAsync().Result)
                {
                    if (user.Name != HiveConst.HiveMQSysadminUser && user.Name != HiveConst.HiveMQNeonUser)
                    {
                        mqManager.DeleteUserAsync(user).Wait();
                    }
                }

                // Recreate the [app] vhost.

                var appVHost = mqManager.CreateVirtualHostAsync(HiveConst.HiveMQAppVHost).Result;

                // Recreate the [app] user and set its permissions for the [app] vhost.

                var appUser = mqManager.CreateUserAsync(new EasyNetQ.Management.Client.Model.UserInfo(HiveConst.HiveMQAppUser, hive.Definition.HiveMQ.AppPassword)).Result;

                mqManager.CreatePermissionAsync(new EasyNetQ.Management.Client.Model.PermissionInfo(appUser, appVHost)).Wait();
            }
        }
    }
}
