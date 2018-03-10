//-----------------------------------------------------------------------------
// FILE:	    DnsView.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Net;

namespace Neon.Cluster
{
    /// <summary>
    /// Specifies the DNS domains to be answered for requests coming from
    /// specific network subnets.
    /// </summary>
    public class DnsView
    {
        /// <summary>
        /// <para>
        /// Lists the query source subnets to be answered by this view.
        /// </para>
        /// <note>
        /// Use <see cref="NetworkCidr.All"/> to answer all source subnets.
        /// </note>
        /// </summary>
        public List<NetworkCidr> Subnets = new List<NetworkCidr>();

        /// <summary>
        /// Lists the domains associated with this view.
        /// </summary>
        public List<DnsDomain> Domains { get; set; } = new List<DnsDomain>();
    }
}
