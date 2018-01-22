//-----------------------------------------------------------------------------
// FILE:	    XenTemplate.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;

using Neon.Cluster;
using Neon.Common;

namespace Neon.Cluster.XenServer
{
    /// <summary>
    /// Describes a XenServer virtual machine template.
    /// </summary>
    public class XenTemplate
    {
        /// <summary>
        /// Constructs an instance from raw property values returned by the <b>xe CLI</b>.
        /// </summary>
        /// <param name="rawProperties"></param>
        internal XenTemplate(IDictionary<string, string> rawProperties)
        {
            this.Uuid            = rawProperties["uuid"];
            this.NameLabel       = rawProperties["name-label"];
            this.NameDescription = rawProperties["name-description"];
        }

        /// <summary>
        /// The repository unique ID.
        /// </summary>
        public string Uuid { get; set; }

        /// <summary>
        /// The repository name.
        /// </summary>
        public string NameLabel { get; set; }

        /// <summary>
        /// The repository description.
        /// </summary>
        public string NameDescription { get; set; }
    }
}
