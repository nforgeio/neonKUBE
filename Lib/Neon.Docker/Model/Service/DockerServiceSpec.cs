//-----------------------------------------------------------------------------
// FILE:	    DockerServiceSpec.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Docker
{
    /// <summary>
    /// Describes a Docker service specification.
    /// </summary>
    public class DockerServiceSpec : INormalizable
    {
        /// <summary>
        /// Default constructor.,
        /// </summary>
        public DockerServiceSpec()
        {
        }

        /// <summary>
        /// Constructs an instance from the dynamic volume information 
        /// returned by the Docker engine or from a <b>docker service inspect</b>
        /// command.
        /// </summary>
        /// <param name="source">The dynamic source value.</param>
        public DockerServiceSpec(dynamic source)
        {
        }

        /// <summary>
        /// The service labels formatted as <b>LABEL=VALUE</b>.
        /// </summary>
        public List<string> Labels { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            Labels = Labels ?? new List<string>();
        }
    }
}
