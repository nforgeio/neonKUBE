//-----------------------------------------------------------------------------
// FILE:	    ClusterManifest.cs
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
    /// <para>
    /// Holds information about a deployed cluster including things like the container images
    /// that need to be present in the local Harbor deployment.  This information is associated
    /// with a specific version of neonKUBE and is generated automatically during neonCLOUD
    /// node image builds and is uploaded to S3 as a JSON document.
    /// </para>
    /// <para>
    /// This ends up being embedded into the <b>neon-cluster-operator</b> as a resource via
    /// a build task that uses the <b>neon-build get-cluster-manifest</b> command to download the
    /// file from S3 so it can be included in the project.
    /// </para>
    /// </summary>
    public class ClusterManifest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ClusterManifest()
        {
        }

        /// <summary>
        /// Returns information about the container images deployed to a new neonKUBE cluster.
        /// </summary>
        [JsonProperty(PropertyName = "ContainerImages", Required = Required.Always)]
        [YamlMember(Alias = "containerImages", ApplyNamingConventions = false)]
        public List<ClusterContainerImage> ContainerImages { get; set; } = new List<ClusterContainerImage>();
    }
}
