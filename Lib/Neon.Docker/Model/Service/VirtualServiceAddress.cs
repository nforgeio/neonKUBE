//-----------------------------------------------------------------------------
// FILE:	    VirtualServiceAddress.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Net;

namespace Neon.Docker
{
    /// <summary>
    /// Describes a Docker virtual service IP address.
    /// </summary>
    public class VirtualServiceAddress
    {
        /// <summary>
        /// Constructs an instance from the dynamic volume information returned by
        /// the Docker engine.
        /// </summary>
        /// <param name="source">The dynamic source value.</param>
        internal VirtualServiceAddress(dynamic source)
        {
            this.Inner      = source;
            this.NetworkId  = source.NetworkID;
            this.Address    = NetworkCidr.Parse(source.Addr);
        }

        /// <summary>
        /// Returns the raw <v>dynamic</v> object actually returned by Docker.
        /// You may use this to access newer Docker properties that have not
        /// yet been wrapped by this class.
        /// </summary>
        public dynamic Inner { get; private set; }

        /// <summary>
        /// Returns the ID of the attached network.
        /// </summary>
        public string NetworkId { get; private set; }

        /// <summary>
        /// Returns the IP address in CIDR form.
        /// </summary>
        public NetworkCidr Address { get; private set; }
    }
}
