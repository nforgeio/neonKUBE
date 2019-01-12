//-----------------------------------------------------------------------------
// FILE:	    KubeLogin.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Cryptography;

namespace Neon.Kube
{
    /// <summary>
    /// Holds the information required to log into or manage a Kubernetes cluster.
    /// </summary>
    public class KubeLogin
    {
        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="contextName">The Kubernetes context name.</param>
        /// <param name="apiVersion">The cluster API server protocol version.</param>
        /// <param name="cluster">The information required to connect to the cluster.</param>
        /// <param name="sshCredentials">The credentials required to perform SSH/SCP operations on the cluster nodes.</param>
        /// <param name="definition">The cluster definition.</param>
        internal KubeLogin(
            string              contextName = null,
            string              apiVersion = null,
            KubeConfigCluster   cluster = null,
            SshCredentials      sshCredentials = null,
            KubeDefinition      definition = null
        )
        {
            this.ContextName    = contextName;
            this.ApiVersion     = apiVersion;
            this.Cluster        = cluster;
            this.SshCredentials = sshCredentials;
            this.Definition     = definition;
        }

        /// <summary>
        /// Returns the Kubernetes context name.
        /// </summary>
        public string ContextName { get; private set; }

        /// <summary>
        /// Returns the cluster API server protocol version.
        /// </summary>
        public string ApiVersion { get; private set; }

        /// <summary>
        /// Returns the information required to connect to the cluster. 
        /// </summary>
        public KubeConfigCluster Cluster { get; private set; }

        /// <summary>
        /// Returns the credentials required to perform SSH/SCP operations 
        /// on the cluster nodes.
        /// </summary>
        public SshCredentials SshCredentials { get; private set; }

        /// <summary>
        /// Returns the cluster definition.
        /// </summary>
        public KubeDefinition Definition { get; private set; }

        /// <summary>
        /// Returns the <see cref="SshCredentials"/> for the hive that can be used
        /// by <see cref="SshProxy{TMetadata}"/> and the <b>SSH.NET</b> Nuget package.
        /// </summary>
        /// <returns>The credentials.</returns>
        public SshCredentials GetSshCredentials()
        {
            return SshCredentials ?? SshCredentials.None;
        }
    }
}
