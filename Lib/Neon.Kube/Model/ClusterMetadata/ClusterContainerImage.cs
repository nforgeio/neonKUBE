//-----------------------------------------------------------------------------
// FILE:	    ClusterContainerImage.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
//
// The contents of this repository are for private use by neonFORGE, LLC. and may not be
// divulged or used for any purpose by other organizations or individuals without a
// formal written and signed agreement with neonFORGE, LLC.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Kube;
using Neon.IO;
using Neon.Net;
using Neon.SSH;

namespace Neon.Kube
{
    /// <summary>
    /// Holds information about a container image deployed as part of cluster setup.
    /// </summary>
    public class ClusterContainerImage
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ClusterContainerImage()
        {
        }

        /// <summary>
        /// <para>
        /// Specifies the reference to the container image within one of the neonFORGE
        /// container registeries.
        /// </para>
        /// <note>
        /// Source references have their tags set to the neonKUBE cluster version.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "SourceRef", Required = Required.Always)]
        [YamlMember(Alias = "sourceRef", ApplyNamingConventions = false)]
        public string SourceRef { get; set; }

        /// <summary>
        /// Specifies the reference to the container image including the <b>image digest</b>
        /// within one of the neonFORGE container registeries.
        /// </summary>
        [JsonProperty(PropertyName = "SourceDigestRef", Required = Required.Always)]
        [YamlMember(Alias = "sourceDigestRef", ApplyNamingConventions = false)]
        public string SourceDigestRef { get; set; }

        /// <summary>
        /// <para>
        /// Specifies the internal cluster reference to the container image as deployed
        /// within the cluster.  This is the reference used for persisting the container
        /// to the local registry as well as executing the container on cluster nodes
        /// via CRI-O.
        /// </para>
        /// <note>
        /// Internal references need to use the original tags because some related operators
        /// require that.  <b>neon-cluster-operator</b> uses this these references to download
        /// container images from <see cref="SourceRef"/> and then persist them to the local cluster
        /// registry as <see cref="InternalRef"/>.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "InternalRef", Required = Required.Always)]
        [YamlMember(Alias = "internalRef", ApplyNamingConventions = false)]
        public string InternalRef { get; set; }

        /// <summary>
        /// Specifies the reference to the container image including the <b>image digest</b>
        /// within as deployed within the cluster.  This is the reference used for persisting 
        /// the container to the local registry as well as executing the container on cluster
        /// nodes via CRI-O.
        /// </summary>
        [JsonProperty(PropertyName = "InternalDigestRef", Required = Required.Always)]
        [YamlMember(Alias = "internalDigestRef", ApplyNamingConventions = false)]
        public string InternalDigestRef { get; set; }
    }
}
