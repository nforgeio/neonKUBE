//-----------------------------------------------------------------------------
// FILE:	    ClusterFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using k8s;
using k8s.KubeConfigModels;

using Newtonsoft.Json.Linq;
using Xunit;

using Neon.Common;
using Neon.Data;
using Neon.Deployment;
using Neon.IO;
using Neon.Retry;
using Neon.Net;
using Neon.SSH;
using Neon.Xunit;
using Neon.Tasks;

namespace Neon.Kube.Xunit
{
    /// <summary>
    /// <para>
    /// Fixture for testing against neonKUBE clusters.  This can execute against an existing
    /// cluster or it can manage the lifecycle of a new cluster during test runs.
    /// </para>
    /// <note>
    /// The <c>NEON_CLUSTER_TESTING</c> environment variable must be defined on the current
    /// machine to enable this feature.
    /// </note>
    /// </summary>
    /// <remarks>
    /// <note>
    /// <para>
    /// <b>IMPORTANT:</b> The base Neon <see cref="TestFixture"/> implementation <b>DOES NOT</b>
    /// support parallel test execution.  You need to explicitly disable parallel execution in 
    /// all test assemblies that rely on thesex test fixtures by adding a C# file called 
    /// <c>AssemblyInfo.cs</c> with:
    /// </para>
    /// <code language="csharp">
    /// [assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]
    /// </code>
    /// <para>
    /// and then define your test classes like:
    /// </para>
    /// <code language="csharp">
    /// public class MyTests : IClassFixture&lt;ClusterFixture&gt;
    /// {
    ///     private const string clusterDefinitionYaml =
    /// @"name: test
    /// datacenter: test
    /// environment: test
    /// isLocked: false
    /// timeSources:
    /// - pool.ntp.org
    /// kubernetes:
    ///   allowPodsOnMasters: true
    /// hosting:
    ///   environment: hyperv
    ///   hyperv:
    ///     useInternalSwitch: true
    ///   vm:
    ///     namePrefix: "test"
    ///     cores: 4
    ///     memory: 12 GiB
    ///     osDisk: 40 GiB
    /// network:
    ///   premiseSubnet: 100.64.0.0/24
    ///   gateway: 100.64.0.1
    /// nodes:
    ///   master:
    ///     role: master
    ///     address: 100.64.0.2
    /// ";
    ///     
    ///     private ClusterFixture foxture;
    /// 
    ///     public MyTests(ClusterFixture fixture)
    ///     {
    ///         this.fixture = foxture;    
    /// 
    ///         var status = fixture.StartAsync(clusterDefinitionYaml);
    ///         
    ///         switch (status)
    ///         {
    ///             case TestFixtureStatus.Disabled:
    ///             
    ///                 return;
    ///                 
    ///             case TestFixtureStatus.Started:
    ///             
    ///                 // The fixture ensures that the cluster is reset when
    ///                 // [StartAsync()] is called the first time for a 
    ///                 // fixture instance.
    ///                 
    ///                 break;
    ///                 
    ///             case TestFixtureStatus.AlreadyRunning:
    ///             
    ///                 // Reset the cluster between test method calls.
    /// 
    ///                 fixture.Reset();
    ///                 break;
    ///         }
    ///     }
    ///     
    ///     [Collection(TestCollection.NonParallel)]
    ///     [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    ///     [ClusterFact]
    ///     public void Test()
    ///     {
    ///         // Implement your test here.  Note that [fixture.Cluster] returns a [clusterProxy]
    ///         // that can be used to manage the cluster and [fixture.K8s] returns an
    ///         // [IKubernetes] client connected to the cluster with root privileges.
    ///     }
    /// }
    /// </code>
    /// </note>
    /// <para>
    /// This fixture can be used to run tests against an existing neonKUBE cluster as well
    /// as a new clusters deployed by the fixture.  The idea here is that you'll have
    /// your unit test class inherit from <see cref="IClassFixture{TFixture}"/>, passing
    /// <see cref="ClusterFixture"/> as the type parameter and then implementing a test class
    /// constructor that has a <see cref="ClusterFixture"/> parameter that will receive an 
    /// instance of the fixture and use that to initialize the test cluster using 
    /// <see cref="StartAsync(string, string, bool, bool, int, string)"/> or one it its
    /// overrides.
    /// </para>
    /// <para>
    /// <see cref="StartAsync(string, string, bool, bool, int, string)"/> handles the deployment
    /// of the test cluster when it doesn't already exist as well as the removal of any previous
    /// cluster, depending on the parameters passed.  You'll be calling this in your test class
    /// constructor.
    /// </para>
    /// <para>
    /// The <b>StartAsync()</b> methods accept a cluster definition in various forms and returns
    /// <see cref="TestFixtureStatus.Disabled"/> when cluster unit testing is disabled on the 
    /// current machine, <see cref="TestFixtureStatus.Started"/> the first time one of these methods 
    /// have been called on a fixture instance or <see cref="TestFixtureStatus.AlreadyRunning"/>
    /// when <b>StartedAsync()</b> has already been called on the fixture.
    /// </para>
    /// <para>
    /// Any existing neonKUBE cluster may be removed by <c>StartAsync()</c>, depending on the
    /// parameters passed and the test cluster may also be removed when the fixture is disposed,
    /// also depending on the parameter passed.
    /// </para>
    /// <para>
    /// It's up to you to call <see cref="ClusterFixture.Reset(ClusterResetOptions)"/> within your
    /// test class constructor when you wish to reset the cluster state between test method executions.
    /// Alternatively, you could design your tests such that each method runs in its own namespace
    /// to improve performance while still providing some isolation.
    /// </para>
    /// <para><b>CLUSTER TEST METHOD ATTRIBUTES</b></para>
    /// <para>
    /// Tests that require a neonKUBE cluster will generally be quite slow and will require additional
    /// resources on the machine where the test is executing and potentially external resources including
    /// XenServer hosts, cloud accounts, specific network configuration, etc.  This means that cluster
    /// based unit tests can generally run only on specifically configured enviroments.
    /// </para>
    /// <para>
    /// We provide the <see cref="ClusterFactAttribute"/> and <see cref="ClusterTheoryAttribute"/> attributes
    /// to manage this.  These derive from <see cref="FactAttribute"/> and <see cref="TheoryAttribute"/>
    /// respectively and set the base class <c>Skip</c> property when the <c>NEON_CLUSTER_TESTING</c> environment
    /// variable <b>does not exist</b>.
    /// </para>
    /// <para>
    /// Test methods that require neonKUBE clusters should be tagged with <see cref="ClusterFactAttribute"/> or
    /// <see cref="ClusterTheoryAttribute"/> instead of <see cref="FactAttribute"/> or <see cref="TheoryAttribute"/>.
    /// Then by default, these methods won't be executed unless the user has explicitly enabled this on the test
    /// machine by defining the <c>NEON_CLUSTER_TESTING</c> environment variable.
    /// </para>
    /// <para>
    /// In addition to tagging test methods like this, you'll need to modify your test class constructors to do
    /// nothing when the fixture's <c>StartAsync()</c> methods return <see cref="TestFixtureStatus.Disabled"/>.
    /// You can also use <see cref="TestHelper.IsClusterTestingEnabled"/> determine when cluster testing is
    /// disabled.
    /// </para>
    /// <para><b>CLUSTER LOCKS</b></para>
    /// <para>
    /// neonKUBE clusters are generally locked by default after being deployed.  This helps prevent the accidential
    /// disruption or removal of important or production clusters.  The <see cref="ClusterFixture"/> class will
    /// never perform operations on locked clusters to help avoid breaking things.  You'll need to explicitly
    /// unlock already deployed clusters or modify the cluster definition by setting <see cref="ClusterDefinition.IsLocked"/>
    /// to <c>false</c> when deploying your test clusters.
    /// </para>
    /// <note>
    /// The statement above is not strictly true: existing cluster resources may be removed by VM prefix or
    /// cloud resource group depending on the <c>StartAsync()</c> parameters.  This functionality is provided
    /// so that tests can cleanup after previous interrupted tests that didn't cleanup after itself.
    /// </note>
    /// <para><b>CLUSTER DEPLOYMENT CONFLICTS</b></para>
    /// <para>
    /// One thing you'll need to worry about is the possibility that a cluster created by one of the <b>StartAsync()</b> 
    /// methods may conflict with an existing production or neonDESKTOP built-in cluster.  This fixture helps
    /// somewhat by persisting cluster state such as kubconfigs, logins, logs, etc. for each deployed cluster
    /// within separate directories named like <b>$(USERPROFILE)\.neonkube\automation\CLUSTER-NAME</b>.
    /// This effectively isolates clusters deployed by the fixture from the user's clusters as well as from
    /// each other.
    /// </para>
    /// <para>
    /// <b>IMPORTANT:</b> You'll need to ensure that your cluster name does not conflict with any existing
    /// clusters deployed to the same environment and also that the node IP addresses don't conflict with
    /// existing clusters deployed on shared infrastructure such as local machines, Hyper-V or XenServer
    /// instances.  You don't need to worry about IP address conflicts for cloud environments because nodes
    /// run on private networks there.
    /// </para>
    /// <para>
    /// We recommend that you prefix your cluster name with something identifying the machine deploying
    /// the cluster.  This could be the machine name, user or a combination of the machine and the current
    /// username, like <b>runner0-</b> or <b>jeff-</b>, or <b>runner0-jeff-</b>...
    /// </para>
    /// <note>
    /// neonKUBE maintainers can also use <see cref="IProfileClient"/> combined with the <b>neon-assistant</b>
    /// tool to reference per-user and/or per-machine profile settings including things like cluster name prefixes, 
    /// reserved node IP addresses, etc.  These can be referenced by cluster definitions using special macros like
    /// <c>$&lt;$&lt;$&lt;NAME&gt;&gt;&gt;</c> as described here: <see cref="PreprocessReader"/>.
    /// </note>
    /// <para>
    /// The idea here is prevent cluster and/or VM naming conflicts for test clusters deployed in parallel
    /// by different runners or developers on their own workstations as well as specifying environment specific
    /// settings such as host hypervisors, LAN configuration, and node IP addresses.
    /// </para>
    /// </remarks>
    public class ClusterFixture : TestFixture
    {
        private bool    isDeployed            = false;
        private bool    unredacted            = false;
        bool            removeOrphansByPrefix = false;

        /// <summary>
        /// Constructor.
        /// </summary>
        public ClusterFixture()
        {
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!base.IsDisposed)
                {
                    Reset();
                }

                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Returns a <see cref="ClusterProxy"/> instance that can be used to manage the attached
        /// cluster after it has been started.
        /// </summary>
        public ClusterProxy Cluster { get; private set; }

        /// <summary>
        /// Returns a <see cref="IKubernetes"/> client instance with root privileges that can be 
        /// used to manage the test cluster after it has been started.
        /// </summary>
        public IKubernetes K8s => Cluster?.K8s;

        /// <summary>
        /// Returns the cluster definition for cluster deployed by this fixture via one of the
        /// <b>StartAsync()</b> methods or <c>null</c> when the fixture was connected to the cluster
        /// via one of the <b>ConnectAsync()</b> methods.
        /// </summary>
        public ClusterDefinition ClusterDefinition { get; private set; }

        /// <summary>
        /// <para>
        /// Deploys a new cluster as specified by the cluster definition model passed.
        /// </para>
        /// <note>
        /// This method removes any existing neonKUBE cluster before deploying a fresh one.
        /// </note>
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition model.</param>
        /// <param name="imageUriOrPath">
        /// Optionally specifies the (compressed) node image URI or file path to use when
        /// provisioning the cluster.  This defaults to the published image for the current
        /// release as specified by <see cref="KubeVersions.NeonKube"/>.
        /// </param>
        /// <param name="removeOrphansByPrefix">
        /// Optionally specifies that VMs or clusters with the same resource group prefix or VM name
        /// prefix will be removed as well.  See the remarks for more information.
        /// </param>
        /// <param name="unredacted">
        /// Optionally disables the redaction of potentially sensitive information from cluster
        /// deployment logs.  This defaults to <c>false</c>.
        /// </param>
        /// <param name="maxParallel">
        /// Optionally specifies the maximum number of cluster node operations to be performed
        /// in parallel.  This defaults to <b>500</b> which is effectively infinite.
        /// </param>
        /// <param name="headendUri">
        /// Optionally overrides the default neonKUBE headend URI.
        /// </param>
        /// <returns>
        /// <para>
        /// The <see cref="TestFixtureStatus"/>:
        /// </para>
        /// <list type="table">
        /// <item>
        ///     <term><see cref="TestFixtureStatus.Disabled"/></term>
        ///     <description>
        ///     Returned when cluster unit testing is disabled due to the <c>NEON_CLUSTER_TESTING</c> environment
        ///     variable not being present on the current machine which means that <see cref="TestHelper.IsClusterTestingEnabled"/>
        ///     returns <c>false</c>.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><see cref="TestFixtureStatus.Started"/></term>
        ///     <description>
        ///     Returned when one of the <c>StartAsync()</c> methods is called for the first time for the fixture
        ///     instance, indicating that an existing cluster has been connected or a new cluster has been deployed.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><see cref="TestFixtureStatus.AlreadyRunning"/></term>
        ///     <description>
        ///     Returned when one of the <c>StartAsync()</c> methods has already been called by your test
        ///     class instance.
        ///     </description>
        /// </item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// <note>
        /// <para>
        /// <b>IMPORTANT:</b> Only one <see cref="ClusterFixture"/> can be run at a time on
        /// any one computer.  This is due to the fact that cluster state like the kubeconfig,
        /// neonKUBE logins, logs and other files will be written to <b>$(USERPROFILE)/.neonkube/automation/(fixture)/*.*</b>
        /// so multiple fixture instances will be confused when trying to manage these same files.
        /// </para>
        /// <para>
        /// This means that not only will running <see cref="ClusterFixture"/> based tests in parallel
        /// within the same instance of Visual Studio fail, but but running these tests in different
        /// Visual Studio instances will also fail.
        /// </para>
        /// </note>
        /// <para>
        /// The <paramref name="removeOrphansByPrefix"/> parameter is typically enabled when running unit tests
        /// to ensure that clusters and VMs orphaned by previous interrupted test runs are removed in addition to
        /// removing the cluster specified by the cluster definition.
        /// </para>
        /// </remarks>
        public async Task<TestFixtureStatus> StartAsync(
            ClusterDefinition   clusterDefinition,
            string              imageUriOrPath        = null,
            bool                removeOrphansByPrefix = false, 
            bool                unredacted            = false,
            int                 maxParallel           = 500,
            string              headendUri            = null)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));
            Covenant.Requires<ArgumentException>(maxParallel > 0, nameof(maxParallel));

            if (!TestHelper.IsClusterTestingEnabled)
            {
                return TestFixtureStatus.Disabled;
            }

            try
            {
                this.removeOrphansByPrefix = removeOrphansByPrefix;
                this.unredacted            = unredacted;

                if (this.Cluster != null)
                {
                    return await Task.FromResult(TestFixtureStatus.AlreadyRunning);
                }

                // Set the automation mode, using any previously downloaded node image unless
                // the user specifies a custom image.  We're going to host the fixture state
                // files in this fixed folder:
                //
                //      $(USERPROFILE)\.neonkube\automation\(fixture)\*.*
                //
                // for the time being to ensure that we don't accumulate automation folders over
                // time.  We're prefixing the last path segment with a "$" to avoid possible
                // collisions with cluster names which don't allow "$" characters.

                // $todo(jefflill):
                // 
                // It may make sense to include the cluster name in the folder path at some point in
                // the future but I'm not going to worry about this now.

                KubeHelper.SetAutomationMode(imageUriOrPath == null ? KubeAutomationMode.EnabledWithSharedCache : KubeAutomationMode.Enabled, KubeHelper.AutomationPrefix("fixture"));

                // Figure out whether the user passed an image URI or file path or neither,
                // when we'll select the default published image for the current build.

                var imageUri  = (string)null;
                var imagePath = (string)null;

                if (string.IsNullOrEmpty(imageUriOrPath))
                {
                    imageUriOrPath = KubeDownloads.GetDefaultNodeImageUri(clusterDefinition.Hosting.Environment);
                }

                if (imageUriOrPath.StartsWith("http://", StringComparison.InvariantCultureIgnoreCase) || imageUriOrPath.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase))
                {
                    imageUri = imageUriOrPath;
                }
                else
                {
                    imagePath = imageUriOrPath;
                }

                // Remove any existing cluster that may have been provisioned earlier using
                // this (or a similar) cluster definition.  We shouldn't typically need to
                // do this because the fixture removes the cluster when [Reset()] is called,
                // but it's possible that a test was interrupted leaving the last cluster
                // still running.
                //
                // This will also remove any clusters and/or VMs prefixed by [ClusterDefinition.Test.Prefix]
                // when set.

                RemoveCluster(clusterDefinition);

                // Provision the cluster, writing any logs to the test output.

                try
                {
                    var controller = KubeSetup.CreateClusterPrepareController(
                        clusterDefinition,
                        nodeImageUri:   imageUri,
                        nodeImagePath:  imagePath,
                        maxParallel:    maxParallel,
                        unredacted:     unredacted,
                        headendUri:     headendUri);

                    switch (await controller.RunAsync())
                    {
                        case SetupDisposition.Succeeded:

                            break;

                        case SetupDisposition.Failed:

                            throw new NeonKubeException("Cluster prepare failed.");

                        case SetupDisposition.Cancelled:
                        default:

                            throw new NotImplementedException();
                    }
                }
                finally
                {
                    // $todo(jefflill): handle the logs.
                }

                // Setup the cluster, writing any logs to the test output.

                try
                {
                    var controller = KubeSetup.CreateClusterSetupController(
                        clusterDefinition,
                        maxParallel:    maxParallel,
                        unredacted:     unredacted);

                    switch (await controller.RunAsync())
                    {
                        case SetupDisposition.Succeeded:

                            break;

                        case SetupDisposition.Failed:

                            throw new NeonKubeException("Cluster setup failed.");

                        case SetupDisposition.Cancelled:
                        default:

                            throw new NotImplementedException();
                    }
                }
                finally
                {
                    // $todo(jefflill): handle the logs.
                }
            }
            finally
            {
                if (KubeHelper.AutomationMode != KubeAutomationMode.Disabled)
                {
                    KubeHelper.ResetAutomationMode();
                }
            }

            return await Task.FromResult(TestFixtureStatus.Started);
        }

        /// <summary>
        /// <para>
        /// Deploys a new cluster as specified by the cluster definition YAML definition.
        /// </para>
        /// <note>
        /// This method removes any existing neonKUBE cluster before deploying a fresh one.
        /// </note>
        /// </summary>
        /// <param name="clusterDefinitionYaml">The cluster definition YAML.</param>
        /// <param name="imageUriOrPath">
        /// Optionally specifies the (compressed) node image URI or file path to use when
        /// provisioning the cluster.  This defaults to the published image for the current
        /// release as specified by <see cref="KubeVersions.NeonKube"/>.
        /// </param>
        /// <param name="removeOrphansByPrefix">
        /// Optionally specifies that VMs or clusters with the same resource group prefix or VM name
        /// prefix will be removed as well.  See the remarks for more information.
        /// </param>
        /// <param name="unredacted">
        /// Optionally disables the redaction of potentially sensitive information from cluster
        /// deployment logs.  This defaults to <c>false</c>.
        /// </param>
        /// <param name="maxParallel">
        /// Optionally specifies the maximum number of cluster node operations to be performed
        /// in parallel.  This defaults to <b>500</b> which is effectively infinite.
        /// </param>
        /// <param name="headendUri">
        /// Optionally overrides the default neonKUBE headend URI.
        /// </param>
        /// <returns>
        /// <para>
        /// The <see cref="TestFixtureStatus"/>:
        /// </para>
        /// <list type="table">
        /// <item>
        ///     <term><see cref="TestFixtureStatus.Disabled"/></term>
        ///     <description>
        ///     Returned when cluster unit testing is disabled due to the <c>NEON_CLUSTER_TESTING</c> environment
        ///     variable not being present on the current machine which means that <see cref="TestHelper.IsClusterTestingEnabled"/>
        ///     returns <c>false</c>.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><see cref="TestFixtureStatus.Started"/></term>
        ///     <description>
        ///     Returned when one of the <c>StartAsync()</c> methods is called for the first time for the fixture
        ///     instance, indicating that an existing cluster has been connected or a new cluster has been deployed.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><see cref="TestFixtureStatus.AlreadyRunning"/></term>
        ///     <description>
        ///     Returned when one of the <c>StartAsync()</c> methods has already been called by your test
        ///     class instance.
        ///     </description>
        /// </item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// <para>
        /// The <paramref name="removeOrphansByPrefix"/> parameter is typically enabled when running unit tests
        /// to ensure that clusters and VMs orphaned by previous interrupted test runs are removed in addition to
        /// removing the cluster specified by the cluster definition.
        /// </para>
        /// </remarks>
        public async Task<TestFixtureStatus> StartAsync(
            string  clusterDefinitionYaml, 
            string  imageUriOrPath        = null, 
            bool    removeOrphansByPrefix = false, 
            bool    unredacted            = false,
            int     maxParallel           = 500,
            string  headendUri            = null)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(clusterDefinitionYaml != null, nameof(clusterDefinitionYaml));
            Covenant.Requires<ArgumentException>(maxParallel > 0, nameof(maxParallel));

            return await StartAsync(
                clusterDefinition:      ClusterDefinition.FromYaml(clusterDefinitionYaml),
                imageUriOrPath:         imageUriOrPath, 
                removeOrphansByPrefix:  removeOrphansByPrefix,
                unredacted:             unredacted,
                maxParallel:            maxParallel,
                headendUri:             headendUri);
        }

        /// <summary>
        /// <para>
        /// Deploys a new cluster as specified by a cluster definition YAML file.
        /// </para>
        /// <note>
        /// This method removes any existing neonKUBE cluster before deploying a fresh one.
        /// </note>
        /// </summary>
        /// <param name="clusterDefinitionFile"><see cref="FileInfo"/> for the cluster definition YAML file.</param>
        /// <param name="imageUriOrPath">
        /// Optionally specifies the (compressed) node image URI or file path to use when
        /// provisioning the cluster.  This defaults to the published image for the current
        /// release as specified by <see cref="KubeVersions.NeonKube"/>.
        /// </param>
        /// <param name="removeOrphansByPrefix">
        /// Optionally specifies that VMs or clusters with the same resource group prefix or VM name
        /// prefix will be removed as well.  See the remarks for more information.
        /// </param>
        /// <param name="unredacted">
        /// Optionally disables the redaction of potentially sensitive information from cluster
        /// deployment logs.  This defaults to <c>false</c>.
        /// </param>
        /// <param name="maxParallel">
        /// Optionally specifies the maximum number of cluster node operations to be performed
        /// in parallel.  This defaults to <b>500</b> which is effectively infinite.
        /// </param>
        /// <param name="headendUri">
        /// Optionally overrides the default neonKUBE headend URI.
        /// </param>
        /// <returns>
        /// <para>
        /// The <see cref="TestFixtureStatus"/>:
        /// </para>
        /// <list type="table">
        /// <item>
        ///     <term><see cref="TestFixtureStatus.Disabled"/></term>
        ///     <description>
        ///     Returned when cluster unit testing is disabled due to the <c>NEON_CLUSTER_TESTING</c> environment
        ///     variable not being present on the current machine which means that <see cref="TestHelper.IsClusterTestingEnabled"/>
        ///     returns <c>false</c>.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><see cref="TestFixtureStatus.Started"/></term>
        ///     <description>
        ///     Returned when one of the <c>StartAsync()</c> methods is called for the first time for the fixture
        ///     instance, indicating that an existing cluster has been connected or a new cluster has been deployed.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><see cref="TestFixtureStatus.AlreadyRunning"/></term>
        ///     <description>
        ///     Returned when one of the <c>StartAsync()</c> methods has already been called by your test
        ///     class instance.
        ///     </description>
        /// </item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// <para>
        /// The <paramref name="removeOrphansByPrefix"/> parameter is typically enabled when running unit tests
        /// to ensure that clusters and VMs orphaned by previous interrupted test runs are removed in addition to
        /// removing the cluster specified by the cluster definition.
        /// </para>
        /// </remarks>
        public async Task<TestFixtureStatus> StartAsync(
            FileInfo    clusterDefinitionFile,
            string      imageUriOrPath        = null,
            bool        removeOrphansByPrefix = false,
            bool        unredacted            = false, 
            int         maxParallel           = 500,
            string      headendUri            = null)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(clusterDefinitionFile != null, nameof(clusterDefinitionFile));
            Covenant.Requires<ArgumentException>(maxParallel > 0, nameof(maxParallel));

            return await StartAsync(
                clusterDefinition:      ClusterDefinition.FromFile(clusterDefinitionFile.FullName),
                imageUriOrPath:         imageUriOrPath, 
                removeOrphansByPrefix:  removeOrphansByPrefix,
                unredacted:             unredacted,
                maxParallel:            maxParallel,
                headendUri:             headendUri);
        }

        /// <inheritdoc/>
        public override void Reset()
        {
            if (isDeployed)
            {
                RemoveCluster(ClusterDefinition);
            }

            isDeployed        = false;
            Cluster           = null;
            ClusterDefinition = null;

            base.Reset();
        }

        /// <summary>
        /// Removes any existing cluster associated with a cluster defintion.
        /// </summary>
        /// <param name="clusterDefinition">The target cluster definition.</param>
        private void RemoveCluster(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            // Initialize the cluster proxy.

            var cluster = new ClusterProxy(
                clusterDefinition:      clusterDefinition,
                hostingManagerFactory:  new HostingManagerFactory(() => HostingLoader.Initialize()),
                operation:              ClusterProxy.Operation.LifeCycle,
                nodeProxyCreator:       (nodeName, nodeAddress) =>
                {
                    var logStream      = new FileStream(Path.Combine(KubeHelper.LogFolder, $"{nodeName}.log"), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                    var logWriter      = new StreamWriter(logStream);
                    var sshCredentials = SshCredentials.FromUserPassword(KubeConst.SysAdminUser, KubeConst.SysAdminPassword);

                    return new NodeSshProxy<NodeDefinition>(nodeName, nodeAddress, sshCredentials, logWriter: logWriter);
                });

            if (unredacted)
            {
                cluster.SecureRunOptions = RunOptions.None;
            }

            using (cluster)
            {
                cluster.RemoveAsync(removeOrphansByPrefix: removeOrphansByPrefix).WaitWithoutAggregate();
            }
        }
    }
}
