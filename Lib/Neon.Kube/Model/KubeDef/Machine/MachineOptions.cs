//-----------------------------------------------------------------------------
// FILE:	    MachineOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Net;

namespace Neon.Kube
{
    /// <summary>
    /// Specifies hosting settings for bare metal or virtual machines.
    /// </summary>
    public class MachineOptions
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public MachineOptions()
        {
        }

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="KubeDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(KubeDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null);

            clusterDefinition.ValidatePrivateNodeAddresses();   // Private node IP addresses must be assigned and valid.
        }
    }
}
