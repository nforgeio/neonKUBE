//-----------------------------------------------------------------------------
// FILE:	    KubeSetupInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace Neon.Kube
{
    /// <summary>
    /// Holds information required to setup a Kubernetes cluster.
    /// </summary>
    public class KubeSetupInfo
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeSetupInfo()
        {
        }

        /// <summary>
        /// Returns the <b>kubectl</b> binary download URI for Windows.
        /// </summary>
        [JsonProperty(PropertyName = "OsxKubeAdminUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "OsxKubeAdminUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string WindowsKubeCtlUri { get; set; }

        /// <summary>
        /// Returns the <b>kubectl</b> binary download URI for OS/X.
        /// </summary>
        [JsonProperty(PropertyName = "OsxKubeCtlUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "OsxKubeCtlUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string OsxKubeCtlUri { get; set; }

        /// <summary>
        /// Returns the <b>kubeadm</b> binary download URI for Linux.
        /// </summary>
        [JsonProperty(PropertyName = "LinuxKubeAdminUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "LinuxKubeAdminUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string LinuxKubeAdminUri { get; set; }

        /// <summary>
        /// Returns the <b>kubectl</b> binary download URI for Linux.
        /// </summary>
        [JsonProperty(PropertyName = "LinuxKubeCtlUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "LinuxKubeCtlUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string LinuxKubeCtlUri { get; set; }

        /// <summary>
        /// Returns the Docker package for Ubuntu.
        /// </summary>
        [JsonProperty(PropertyName = "UbuntuDockerPackageUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "UbuntuDockerPackageUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string UbuntuDockerPackageUri { get; set; }
    }
}
