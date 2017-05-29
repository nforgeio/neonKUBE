//-----------------------------------------------------------------------------
// FILE:	    HostingProviders.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Text;

namespace Neon.Cluster
{
    /// <summary>
    /// Enumerates the possible cluster hosting providers.
    /// </summary>
    public enum HostingProviders
    {
        /// <summary>
        /// Hosted on privately managed clusters such as colocation or on-premise.
        /// </summary>
        OnPremise = 0,

        /// <summary>
        /// Amazon Web Services.
        /// </summary>
        Aws,

        /// <summary>
        /// Microsoft Azure.
        /// </summary>
        Azure,

        /// <summary>
        /// Google Cloud Platform.
        /// </summary>
        Google
    }
}
