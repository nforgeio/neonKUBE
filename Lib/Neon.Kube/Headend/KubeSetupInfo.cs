//-----------------------------------------------------------------------------
// FILE:	    KubeSetupInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
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
        /// Returns the <b>kubeadm</b> binrary download URI.
        /// </summary>
        public string KubeAdminUri { get; set; }

        /// <summary>
        /// Returns the <b>kubectl</b> binrary download URI.
        /// </summary>
        public string KubeCtlUri { get; set; }

        /// <summary>
        /// Returns the Docker package 
        /// </summary>
        public string DockerPackageUri { get; set; }
    }
}
