//-----------------------------------------------------------------------------
// FILE:	    KubernetesFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
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
using Neon.Xunit;
using System.IO;

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
    /// that you'll have your unit test inherit from 
    /// </para>
    /// <para>
    /// <b>To connect to an existing cluster</b>, you'll need to call one of the <see cref="Connect(K8SConfiguration, string, string)"/>,
    /// <see cref="Connect(KubernetesClientConfiguration)"/>, or <see cref="Connect(string, string, string)"/>
    /// methods to connect to an existing cluster within the constructor.
    /// </para>
    /// <para>
    /// <b>To deploy a temporary neonKUBE cluster</b>, you'll need to call one of
    /// the <see cref="Deploy(ClusterDefinition)"/>, <see cref="Deploy(FileInfo)"/>, or
    /// <see cref="Deploy(string)"/> methods within the constructor to provision
    /// and setup the cluster using the specified cluster definition.  The <see cref="ClusterDefinition"/>
    /// property will be set in this case.
    /// </para>
    /// <para>
    /// The <b>Connect()</b> and <b>Deploy()</b> methods return <see cref="TestFixtureStatus.Started"/>
    /// the first time one of these methods have been called on a fixture instance or after a
    /// <see cref="Reset"/> call or <see cref="TestFixtureStatus.AlreadyRunning"/> when the
    /// fixture is already managing a cluster.
    /// </para>
    /// </remarks>
    public class KubernetesFixture : TestFixture
    {
        private bool        deployed = false;

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
        /// Connects the Kubernetes cluster specified in the default kubeconfig.  You can explicitly specify
        /// the configuration file location via <paramref name="kubeconfigPath"/> and override the current 
        /// context and API server endpoint using the remaining optional parameters.
        /// </summary>
        /// <param name="kubeconfigPath">Optionally specifies a specific kubeconfig file.</param>
        /// <param name="currentContext">Optionally overrides the current context.</param>
        /// <param name="masterUrl">Optionally overrides the URI for the API server endpoint.</param>
        /// <returns>
        /// <see cref="TestFixtureStatus.Started"/> the first time one of the <b>Connect()</b> methods have been called 
        /// on a fixture instance or after a <see cref="Reset"/> call or <see cref="TestFixtureStatus.AlreadyRunning"/>
        /// when the fixture is already managing a cluster.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown a <b>Deploy()</b> method has already been called on the fixture.  This fixture
        /// does not support mixing <b>Connect()</b> and <b>Deploy()</b> calls.
        /// </exception>
        public TestFixtureStatus Connect(string kubeconfigPath = null, string currentContext = null, string masterUrl = null)
        {
            if (Client != null)
            {
                if (deployed)
                {
                    throw new InvalidOperationException("[Deploy()] has already been called on this fixture.");
                }

                return TestFixtureStatus.AlreadyRunning;
            }

            Client = new Kubernetes(KubernetesClientConfiguration.BuildConfigFromConfigFile(kubeconfigPath, currentContext, masterUrl));

            return TestFixtureStatus.Started;
        }

        /// <summary>
        /// Connects the Kubernetes cluster specified by <see cref="KubernetesClientConfiguration"/>.
        /// </summary>
        /// <param name="kubeconfig">Specifies the <see cref="KubernetesClientConfiguration"/>.</param>
        /// <returns>
        /// <see cref="TestFixtureStatus.Started"/> the first time one of the <b>Connect()</b> methods have been called 
        /// on a fixture instance or after a <see cref="Reset"/> call or <see cref="TestFixtureStatus.AlreadyRunning"/>
        /// when the fixture is already managing a cluster.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown a <b>Deploy()</b> method has already been called on the fixture.  This fixture
        /// does not support mixing <b>Connect()</b> and <b>Deploy()</b> calls.
        /// </exception>
        public TestFixtureStatus Connect(KubernetesClientConfiguration kubeconfig)
        {
            Covenant.Requires<ArgumentNullException>(kubeconfig != null, nameof(kubeconfig));

            if (Client != null)
            {
                if (deployed)
                {
                    throw new InvalidOperationException("[Deploy()] has already been called on this fixture.");
                }

                return TestFixtureStatus.AlreadyRunning;
            }

            Client = new Kubernetes(kubeconfig);

            return TestFixtureStatus.Started;
        }

        /// <summary>
        /// Connects the Kubernetes cluster specified by <see cref="K8SConfiguration"/>.  You can override the current  
        /// context and API server endpoint using the remaining optional parameters.
        /// </summary>
        /// <param name="k8sConfig">The configuration.</param>
        /// <param name="currentContext">Optionally overrides the current context.</param>
        /// <param name="masterUrl">Optionally overrides the URI for the API server endpoint.</param>
        /// <returns>
        /// <see cref="TestFixtureStatus.Started"/> the first time one of the <b>Connect()</b> methods have been called 
        /// on a fixture instance or after a <see cref="Reset"/> call or <see cref="TestFixtureStatus.AlreadyRunning"/>
        /// when the fixture is already managing a cluster.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown a <b>Deploy()</b> method has already been called on the fixture.  This fixture
        /// does not support mixing <b>Connect()</b> and <b>Deploy()</b> calls.
        /// </exception>
        public TestFixtureStatus Connect(K8SConfiguration k8sConfig, string currentContext = null, string masterUrl = null)
        {
            Covenant.Requires<ArgumentNullException>(k8sConfig != null, nameof(k8sConfig));

            if (Client != null)
            {
                if (deployed)
                {
                    throw new InvalidOperationException("[Deploy()] has already been called on this fixture.");
                }

                return TestFixtureStatus.AlreadyRunning;
            }

            Client = new Kubernetes(KubernetesClientConfiguration.BuildConfigFromConfigObject(k8sConfig, currentContext, masterUrl));

            return TestFixtureStatus.Started;
        }

        /// <summary>
        /// Deploys a new cluster as specified by the cluster definition model passed.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition model.</param>
        /// <returns>The connected <see cref="Kubernetes"/> client.  This will also be available from <see cref="Client"/>.</returns>
        /// <returns>
        /// <see cref="TestFixtureStatus.Started"/> the first time one of the <b>Delpoy()</b> methods have been called 
        /// on a fixture instance or after a <see cref="Reset"/> call or <see cref="TestFixtureStatus.AlreadyRunning"/>
        /// when the fixture is already managing a cluster.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown a <b>Connect()</b> method has already been called on the fixture.  This fixture
        /// does not support mixing <b>Connect()</b> and <b>Deploy()</b> calls.
        /// </exception>
        public TestFixtureStatus Deploy(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            if (Client != null)
            {
                if (!deployed)
                {
                    throw new InvalidOperationException("[Connect()] has already been called on this fixture.");
                }

                return TestFixtureStatus.AlreadyRunning;
            }

            throw new NotImplementedException();

            return TestFixtureStatus.Started;
        }

        /// <summary>
        /// Deploys a new cluster as specified by the cluster definition YAML definition.
        /// </summary>
        /// <param name="clusterDefinitionYaml">The cluster definition YAML.</param>
        /// <returns>The connected <see cref="Kubernetes"/> client.  This will also be available from <see cref="Client"/>.</returns>
        /// <returns>
        /// <see cref="TestFixtureStatus.Started"/> the first time one of the <b>Delpoy()</b> methods have been called 
        /// on a fixture instance or after a <see cref="Reset"/> call or <see cref="TestFixtureStatus.AlreadyRunning"/>
        /// when the fixture is already managing a cluster.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown a <b>Connect()</b> method has already been called on the fixture.  This fixture
        /// does not support mixing <b>Connect()</b> and <b>Deploy()</b> calls.
        /// </exception>
        public TestFixtureStatus Deploy(string clusterDefinitionYaml)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinitionYaml != null, nameof(clusterDefinitionYaml));

            if (Client != null)
            {
                if (!deployed)
                {
                    throw new InvalidOperationException("[Connect()] has already been called on this fixture.");
                }

                return TestFixtureStatus.AlreadyRunning;
            }

            throw new NotImplementedException();

            return TestFixtureStatus.Started;
        }

        /// <summary>
        /// Deploys a new cluster as specified by a cluster definition YAML file.
        /// </summary>
        /// <param name="clusterDefinitionPath">Path to the cluster definition YAML file.</param>
        /// <returns>The connected <see cref="Kubernetes"/> client.  This will also be available from <see cref="Client"/>.</returns>
        /// <returns>
        /// <see cref="TestFixtureStatus.Started"/> the first time one of the <b>Delpoy()</b> methods have been called 
        /// on a fixture instance or after a <see cref="Reset"/> call or <see cref="TestFixtureStatus.AlreadyRunning"/>
        /// when the fixture is already managing a cluster.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown a <b>Connect()</b> method has already been called on the fixture.  This fixture
        /// does not support mixing <b>Connect()</b> and <b>Deploy()</b> calls.
        /// </exception>
        public TestFixtureStatus Deploy(FileInfo clusterDefinitionPath)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinitionPath != null, nameof(clusterDefinitionPath));

            if (Client != null)
            {
                if (!deployed)
                {
                    throw new InvalidOperationException("[Connect()] has already been called on this fixture.");
                }

                return TestFixtureStatus.AlreadyRunning;
            }

            throw new NotImplementedException();

            return TestFixtureStatus.Started;
        }

        /// <summary>
        /// Returns the standard <see cref="Kubernetes"/> client instance that can be used to
        /// manage the attached cluster.  This property is set when a cluster is connected or
        /// deployed.
        /// </summary>
        public Kubernetes Client { get; private set; }

        /// <summary>
        /// Returns the cluster definition for cluster deployed by this fixture or <c>null</c>
        /// when the fixture just connected to the cluster.
        /// </summary>
        public ClusterDefinition ClusterDefinition { get; private set; }

        /// <inheritdoc/>
        public override void Reset()
        {
            if (deployed)
            {
                // 1. Stop and remove the cluster
                // 2. Remove the temporary cluster folder
            }

            deployed          = false;
            Client            = null;
            ClusterDefinition = null;

            base.Reset();
        }
    }
}
