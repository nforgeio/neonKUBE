//-----------------------------------------------------------------------------
// FILE:	    KubeContextExtension.cs
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
    /// Holds extended cluster information such as the cluster definition and
    /// node SSH credentials.  These records are persisted as files to the 
    /// <b>$HOME/.neonkube/contexts</b> folder in YAML files named like
    /// <b><i>NAME</i>.context.yaml</b>, where <i>NAME</i> identifies the 
    /// associated Kubernetes context.
    /// </summary>
    public class KubeContextExtension
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeContextExtension()
        {
        }

        /// <summary>
        /// The credentials required to perform SSH/SCP operations 
        /// on the cluster nodes.
        /// </summary>
        [JsonProperty(PropertyName = "sshCredentials", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "sshCredentials", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public SshCredentials SshCredentials { get; set; }

        /// <summary>
        /// The cluster definition.
        /// </summary>
        [JsonProperty(PropertyName = "clusterDefinition", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "clusterDefinition", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public ClusterDefinition ClusterDefinition { get; set; }

        /// <summary>
        /// Indicates whether provisioning is complete but setup is still
        /// pending for this cluster
        /// </summary>
        [JsonProperty(PropertyName = "SetupPending", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "SetupPending", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool SetupPending { get; set; } = false;
    }
}
