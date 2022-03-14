//-----------------------------------------------------------------------------
// FILE:	    KubernetesFixture.cs
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
using Neon.Retry;
using Neon.Net;
using Neon.SSH;
using Neon.Xunit;
using Neon.Tasks;

namespace Neon.Kube.Xunit
{
    /// <summary>
    /// Fixture for testing against an existing Kubernetes cluster as well as 
    /// a neonKUBE cluster deployed by the fixture.
    /// </summary>
    /// <remarks>
    /// <note>
    /// <para>
    /// <b>IMPORTANT:</b> The base Neon <see cref="TestFixture"/> implementation <b>DOES NOT</b>
    /// support parallel test execution because fixtures may impact global machine state
    /// like starting a Docker container, modifying the local DNS <b>hosts</b> file, or 
    /// configuring a test database.
    /// </para>
    /// <para>
    /// You should explicitly disable parallel execution in all test assemblies that
    /// rely on test fixtures by adding a C# file called <c>AssemblyInfo.cs</c> with:
    /// </para>
    /// <code language="csharp">
    /// [assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]
    /// </code>
    /// <para>
    /// and then define your test classes like:
    /// </para>
    /// <code language="csharp">
    /// public class MyTests
    /// {
    ///     [Collection(TestCollection.NonParallel)]
    ///     [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    ///     [Fact]
    ///     public void Test()
    ///     {
    ///     }
    /// }
    /// </code>
    /// </note>
    /// <para>
    /// This fixture can be used to run tests against any existing Kubernetes cluster
    /// as well as a new neonKUBE cluster deployed by the fixture.  The idea here is
    /// that you'll have your unit test class inherit from <see cref="IClassFixture{TFixture}"/>,
    /// passing <see cref="KubernetesFixture"/> as the type parameter and then implementing
    /// a test class constructor that has a <see cref="KubernetesFixture"/> parameter that
    /// will receive an instance of the the fixture.
    /// </para>
    /// <para>
    /// <b>To connect to an existing cluster</b>, you'll need to call one of the <see cref="ConnectAsync(K8SConfiguration, string, string)"/>,
    /// <see cref="ConnectAsync(KubernetesClientConfiguration)"/>, or <see cref="ConnectAsync(string, string, string)"/>
    /// methods to connect to an existing cluster within the constructor.
    /// </para>
    /// <para>
    /// <b>To deploy a temporary neonKUBE cluster</b>, you'll need to call one of
    /// the <see cref="DeployAsync(ClusterDefinition, string, bool, bool, int, string)"/>, <see cref="DeployAsync(FileInfo, string, bool, bool, int, string)"/>,
    /// or <see cref="DeployAsync(string, string, bool, bool, int, string)"/> methods within the constructor to provision
    /// and setup the cluster using the specified cluster definition.  The <see cref="ClusterDefinition"/>
    /// property will be set in this case.
    /// </para>
    /// <para>
    /// The <b>ConnectAsync()</b> and <b>DeployAsync()</b> methods return <see cref="TestFixtureStatus.Started"/>
    /// the first time one of these methods have been called on a fixture instance or after a
    /// <see cref="Reset"/> call or <see cref="TestFixtureStatus.AlreadyRunning"/> when the
    /// fixture is already managing a cluster.
    /// </para>
    /// <note>
    /// Any existing neonKUBE cluster will be removed by the <b>DeployAsync()</b> methods and any neonKUBE clusters 
    /// created by <b>DeployAsync()</b> methods will be automatically removed when <see cref="Reset"/> is called 
    /// or when xUnit finishes running the tests in your class.
    /// </note>
    /// <para><b>CLUSTER DEPLOYMENT CONFLICTS</b></para>
    /// <para>
    /// One thing you'll need to worry about is the possibility that a cluster created by one of the <b>DeployAsync()</b> 
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
    /// <para>
    /// The idea here is prevent cluster and/or VM naming conflicts for test clusters deployed in parallel
    /// by different runners or developers on their own machines.
    /// </para>
    /// </remarks>
    public class KubernetesFixture : TestFixture
    {
        private bool    deployed              = false;
        private bool    unredacted            = false;
        bool            removeOrphansByPrefix = false;

        /// <summary>
        /// Constructor.
        /// </summary>
        public KubernetesFixture()
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
        /// Returns a standard <see cref="Kubernetes"/> client instance that can be used to
        /// manage the attached cluster.  This property is set when a cluster is connected or
        /// deployed.
        /// </summary>
        public Kubernetes Client { get; private set; }

        /// <summary>
        /// Returns the cluster definition for cluster deployed by this fixture via one of the
        /// <b>DeployAsync()</b> methods or <c>null</c> when the fixture was connected to the cluster
        /// via one of the <b>ConnectAsync()</b> methods.
        /// </summary>
        public ClusterDefinition ClusterDefinition { get; private set; }

        /// <summary>
        /// <para>
        /// Connects the Kubernetes cluster specified in the default kubeconfig.  You can explicitly specify
        /// the configuration file location via <paramref name="kubeconfigPath"/> and override the current 
        /// context and API server endpoint using the remaining optional parameters.
        /// </para>
        /// <note>
        /// Unlike the <b>DeployAsync()</b> methods, the <b>ConnectAsync()</b> methods make no attempt to reset the
        /// Kubernetes cluster to any initial state.  You'll need to do that yourself by performing cluster
        /// operations via the <see cref="Client"/>
        /// </note>
        /// </summary>
        /// <param name="kubeconfigPath">Optionally specifies a specific kubeconfig file.</param>
        /// <param name="currentContext">Optionally overrides the current context.</param>
        /// <param name="masterUrl">Optionally overrides the URI for the API server endpoint.</param>
        /// <returns>
        /// <see cref="TestFixtureStatus.Started"/> the first time one of the <b>ConnectAsync()</b> methods have been called 
        /// on a fixture instance or after a <see cref="Reset"/> call or <see cref="TestFixtureStatus.AlreadyRunning"/>
        /// when the fixture is already managing a cluster.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a <b>DeployAsync()</b> method has already been called on the fixture.  This fixture
        /// does not support mixing <b>ConnectAsync()</b> and <b>DeployAsync()</b> calls.
        /// </exception>
        public async Task<TestFixtureStatus> ConnectAsync(string kubeconfigPath = null, string currentContext = null, string masterUrl = null)
        {
            await SyncContext.Clear;

            if (Client != null)
            {
                if (deployed)
                {
                    throw new InvalidOperationException("[DeployAsync()] has already been called on this fixture.");
                }

                return await Task.FromResult(TestFixtureStatus.AlreadyRunning);
            }

            Client = new KubernetesClient(KubernetesClientConfiguration.BuildConfigFromConfigFile(kubeconfigPath, currentContext, masterUrl));

            return await Task.FromResult(TestFixtureStatus.Started);
        }

        /// <summary>
        /// <para>
        /// Connects the Kubernetes cluster specified by <see cref="KubernetesClientConfiguration"/>.
        /// </para>
        /// <note>
        /// Unlike the <b>DeployAsync()</b> methods, the <b>ConnectAsync()</b> methods make no attempt to reset the
        /// Kubernetes cluster to any initial state.  You'll need to do that yourself by performing cluster
        /// operations via the <see cref="Client"/>
        /// </note>
        /// </summary>
        /// <param name="kubeconfig">Specifies the <see cref="KubernetesClientConfiguration"/>.</param>
        /// <returns>
        /// <see cref="TestFixtureStatus.Started"/> the first time one of the <b>ConnectAsync()</b> methods have been called 
        /// on a fixture instance or after a <see cref="Reset"/> call or <see cref="TestFixtureStatus.AlreadyRunning"/>
        /// when the fixture is already managing a cluster.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a <b>DeployAsync()</b> method has already been called on the fixture.  This fixture
        /// does not support mixing <b>ConnectAsync()</b> and <b>DeployAsync()</b> calls.
        /// </exception>
        public async Task<TestFixtureStatus> ConnectAsync(KubernetesClientConfiguration kubeconfig)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(kubeconfig != null, nameof(kubeconfig));

            if (Client != null)
            {
                if (deployed)
                {
                    throw new InvalidOperationException("[DeployAsync()] has already been called on this fixture.");
                }

                return await Task.FromResult(TestFixtureStatus.AlreadyRunning);
            }

            Client = new KubernetesClient(kubeconfig);

            return await Task.FromResult(TestFixtureStatus.Started);
        }

        /// <summary>
        /// <para>
        /// Connects the Kubernetes cluster specified by <see cref="K8SConfiguration"/>.  You can override the current  
        /// context and API server endpoint using the remaining optional parameters.
        /// </para>
        /// <note>
        /// Unlike the <b>DeployAsync()</b> methods, the <b>ConnectAsync()</b> methods make no attempt to reset the
        /// Kubernetes cluster to any initial state.  You'll need to do that yourself by performing cluster
        /// operations via the <see cref="Client"/>
        /// </note>
        /// </summary>
        /// <param name="k8sConfig">The configuration.</param>
        /// <param name="currentContext">Optionally overrides the current context.</param>
        /// <param name="masterUrl">Optionally overrides the URI for the API server endpoint.</param>
        /// <returns>
        /// <see cref="TestFixtureStatus.Started"/> the first time one of the <b>ConnectAsync()</b> methods have been called 
        /// on a fixture instance or after a <see cref="Reset"/> call or <see cref="TestFixtureStatus.AlreadyRunning"/>
        /// when the fixture is already managing a cluster.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a <b>DeployAsync()</b> method has already been called on the fixture.  This fixture
        /// does not support mixing <b>ConnectAsync()</b> and <b>DeployAsync()</b> calls.
        /// </exception>
        public async Task<TestFixtureStatus> ConnectAsync(K8SConfiguration k8sConfig, string currentContext = null, string masterUrl = null)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(k8sConfig != null, nameof(k8sConfig));

            if (Client != null)
            {
                if (deployed)
                {
                    throw new InvalidOperationException("[DeployAsync()] has already been called on this fixture.");
                }

                return await Task.FromResult(TestFixtureStatus.AlreadyRunning);
            }

            Client = new KubernetesClient(KubernetesClientConfiguration.BuildConfigFromConfigObject(k8sConfig, currentContext, masterUrl));

            return await Task.FromResult(TestFixtureStatus.Started);
        }

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
        /// <returns>The connected <see cref="Kubernetes"/> client.  This will also be available from <see cref="Client"/>.</returns>
        /// <returns>
        /// <see cref="TestFixtureStatus.Started"/> the first time one of the <b>DeployAsync()</b> methods have been called 
        /// on a fixture instance or after a <see cref="Reset"/> call or <see cref="TestFixtureStatus.AlreadyRunning"/>
        /// when the fixture is already managing a cluster.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a <b>ConnectAsync()</b> method has already been called on the fixture.  This fixture
        /// does not support mixing <b>ConnectAsync()</b> and <b>DeployAsync()</b> calls.
        /// </exception>
        /// <remarks>
        /// <note>
        /// <para>
        /// <b>IMPORTANT:</b> Only one <see cref="KubernetesFixture"/> can be run at a time on
        /// any one computer.  This is due to the fact that cluster state like the kubeconfig,
        /// neonKUBE logins, logs and other files will be written to <b>$(USERPROFILE)/.neonkube/automation/(fixture)/*.*</b>
        /// so multiple fixture instances will be confused when trying to manage these same files.
        /// </para>
        /// <para>
        /// This means that not only will running <see cref="KubernetesFixture"/> based tests in parallel
        /// within the same instance of Visual Studio fail, but but running these tests in different
        /// Visual Studio instances will also fail.
        /// </para>
        /// </note>
        /// <para>
        /// The <paramref name="removeOrphansByPrefix"/> parameter is typically enabled when running unit tests
        /// via the <b>KubernetesFixture</b> to ensure that clusters and VMs orphaned by previous interrupted
        /// test runs are removed in addition to removing the cluster specified by the cluster definition.
        /// </para>
        /// </remarks>
        public async Task<TestFixtureStatus> DeployAsync(
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

            try
            {
                this.removeOrphansByPrefix = removeOrphansByPrefix;
                this.unredacted            = unredacted;

                if (this.Client != null)
                {
                    if (!this.deployed)
                    {
                        throw new InvalidOperationException("[ConnectAsync() has already been called on this fixture.");
                    }

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
        /// <returns>The connected <see cref="Kubernetes"/> client.  This will also be available from <see cref="Client"/>.</returns>
        /// <returns>
        /// <see cref="TestFixtureStatus.Started"/> the first time one of the <b>DeployAsync()</b> methods have been called 
        /// on a fixture instance or after a <see cref="Reset"/> call or <see cref="TestFixtureStatus.AlreadyRunning"/>
        /// when the fixture is already managing a cluster.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a <b>ConnectAsync()</b> method has already been called on the fixture.  This
        /// fixture does not support mixing <b>ConnectAsync()</b> and <b>DeployAsync()</b> calls.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <paramref name="removeOrphansByPrefix"/> parameter is typically enabled when running unit tests
        /// via the <b>KubernetesFixture</b> to ensure that clusters and VMs orphaned by previous interrupted
        /// test runs are removed in addition to removing the cluster specified by the cluster definition.
        /// </para>
        /// </remarks>
        public async Task<TestFixtureStatus> DeployAsync(
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

            return await DeployAsync(
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
        /// <returns>The connected <see cref="Kubernetes"/> client.  This will also be available from <see cref="Client"/>.</returns>
        /// <returns>
        /// <see cref="TestFixtureStatus.Started"/> the first time one of the <b>DeployAsync()</b> methods have been called 
        /// on a fixture instance or after a <see cref="Reset"/> call or <see cref="TestFixtureStatus.AlreadyRunning"/>
        /// when the fixture is already managing a cluster.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a <b>ConnectAsync()</b> method has already been called on the fixture.  This fixture
        /// does not support mixing <b>ConnectAsync()</b> and <b>DeployAsync()</b> calls.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <paramref name="removeOrphansByPrefix"/> parameter is typically enabled when running unit tests
        /// via the <b>KubernetesFixture</b> to ensure that clusters and VMs orphaned by previous interrupted
        /// test runs are removed in addition to removing the cluster specified by the cluster definition.
        /// </para>
        /// </remarks>
        public async Task<TestFixtureStatus> DeployAsync(
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

            return await DeployAsync(
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
            if (deployed)
            {
                RemoveCluster(ClusterDefinition);
            }

            deployed          = false;
            Client            = null;
            ClusterDefinition = null;

            base.Reset();
        }

        /// <summary>
        /// Removes any cluster associated with a cluster defintion.
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
