//-----------------------------------------------------------------------------
// FILE:	    ProxyBridge.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

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

using Neon.Common;

namespace Neon.Cluster
{
    /// <summary>
    /// Describes how traffic from outside the Swarm cluster is to be bridged
    /// into the cluster.  This determines exactly which Swarm nodes will be 
    /// targeted with the traffic.
    /// </summary>
    public class ProxyBridge
    {
        /// <summary>
        /// Default constructor that initializes reasonable values.
        /// </summary>
        public ProxyBridge()
        {
        }

        /// <summary>
        /// Validates the instance.
        /// </summary>
        /// <param name="context">The validation context.</param>
        public void Validate(ProxyValidationContext context)
        {
        }
    }
}
