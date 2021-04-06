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
    /// Fixture for testing against Kubernetes clusters including optionally
    /// deploying a neonKUBE cluster.
    /// </summary>
    /// <remarks>
    /// <para>
    /// </para>
    /// </remarks>
    public class KubernetesFixture : TestFixture
    {
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
        /// <returns>The connected <see cref="Kubernetes"/> client.  This will also be available from <see cref="Client"/>.</returns>
        public Kubernetes Connect(string kubeconfigPath = null, string currentContext = null, string masterUrl = null)
        {
            return Client = new Kubernetes(KubernetesClientConfiguration.BuildConfigFromConfigFile(kubeconfigPath, currentContext, masterUrl));
        }

        /// <summary>
        /// Connects the Kubernetes cluster specified by <see cref="KubernetesClientConfiguration"/>.  You 
        /// override the current context and API server endpoint using the remaining optional parameters.
        /// </summary>
        /// <param name="kubeconfig">Specifies the <see cref="KubernetesClientConfiguration"/>.</param>
        /// <returns>The connected <see cref="Kubernetes"/> client.  This will also be available from <see cref="Client"/>.</returns>
        public Kubernetes Connect(KubernetesClientConfiguration kubeconfig)
        {
            Covenant.Requires<ArgumentNullException>(kubeconfig != null, nameof(kubeconfig));

            return Client = new Kubernetes(kubeconfig);
        }

        /// <summary>
        /// Connects the Kubernetes cluster specified by <see cref="K8SConfiguration"/>.  You  override the current  
        /// context and API server endpoint using the remaining optional parameters.
        /// </summary>
        /// <param name="k8sConfig">The configuration.</param>
        /// <param name="currentContext">Optionally overrides the current context.</param>
        /// <param name="masterUrl">Optionally overrides the URI for the API server endpoint.</param>
        /// <returns>The connected <see cref="Kubernetes"/> client.  This will also be available from <see cref="Client"/>.</returns>
        public Kubernetes Connect(K8SConfiguration k8sConfig, string currentContext = null, string masterUrl = null)
        {
            Covenant.Requires<ArgumentNullException>(k8sConfig != null, nameof(k8sConfig));

            return Client = new Kubernetes(KubernetesClientConfiguration.BuildConfigFromConfigObject(k8sConfig, currentContext, masterUrl));
        }

        /// <summary>
        /// Deploys a new cluster as specified by the cluster definition model passed.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition model.</param>
        /// <returns>The connected <see cref="Kubernetes"/> client.  This will also be available from <see cref="Client"/>.</returns>
        public Kubernetes Deploy(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            throw new NotImplementedException();
        }

        /// <summary>
        /// Deploys a new cluster as specified by the cluster definition YAML definition.
        /// </summary>
        /// <param name="clusterDefinitionYaml">The cluster definition YAML.</param>
        /// <returns>The connected <see cref="Kubernetes"/> client.  This will also be available from <see cref="Client"/>.</returns>
        public Kubernetes Deploy(string clusterDefinitionYaml)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinitionYaml != null, nameof(clusterDefinitionYaml));

            throw new NotImplementedException();
        }

        /// <summary>
        /// Deploys a new cluster as specified by a cluster definition YAML file.
        /// </summary>
        /// <param name="clusterDefinitionPath">Path to the cluster definition YAML file.</param>
        /// <returns>The connected <see cref="Kubernetes"/> client.  This will also be available from <see cref="Client"/>.</returns>
        public Kubernetes Deploy(FileInfo clusterDefinitionPath)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinitionPath != null, nameof(clusterDefinitionPath));

            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns the <see cref="Kubernetes"/> client instance that can be used to
        /// control the attached cluster.
        /// </summary>
        public Kubernetes Client { get; private set; }

        /// <inheritdoc/>
        public override void Reset()
        {
            base.Reset();
        }
    }
}
