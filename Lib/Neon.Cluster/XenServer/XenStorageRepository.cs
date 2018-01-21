//-----------------------------------------------------------------------------
// FILE:	    XenStorageRepository.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Cluster;
using Neon.Common;

namespace Neon.Cluster.XenServer
{
    /// <summary>
    /// Describes a XenServer storage repository.
    /// </summary>
    public class XenStorageRepository
    {
        /// <summary>
        /// Constructs an instance from raw property values returned by the 
        /// <b>xe CLI</b>.
        /// </summary>
        /// <param name="rawProperties"></param>
        internal XenStorageRepository(IDictionary<string, string> rawProperties)
        {
            this.Uuid            = rawProperties["uuid"];
            this.NameLabel       = rawProperties["name-label"];
            this.NameDescription = rawProperties["name-description"];
            this.Host            = rawProperties["host"];
            this.Type            = rawProperties["type"];
            this.ContentType     = rawProperties["content-type"];
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

        /// <summary>
        /// The XenServer host.
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// The repository type.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// The type of content stored in the repository.
        /// </summary>
        public string ContentType { get; set; }
    }
}
