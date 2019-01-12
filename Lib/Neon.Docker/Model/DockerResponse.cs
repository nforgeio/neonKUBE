//-----------------------------------------------------------------------------
// FILE:	    DockerResponse.cs
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
using Neon.Net;

namespace Neon.Docker
{
    /// <summary>
    /// The base Docker API response class.
    /// </summary>
    public class DockerResponse
    {
        /// <summary>
        /// Constructs the response from a lower-level <see cref="JsonResponse"/>.
        /// </summary>
        /// <param name="response"></param>
        internal DockerResponse(JsonResponse response)
        {
            var warnings = response.AsDynamic().Warnings;

            if (warnings != null)
            {
                foreach (string warning in warnings)
                {
                    Warnings.Add(warning);
                }
            }
        }

        /// <summary>
        /// Lists any warnings returned by the Docker engine.
        /// </summary>
        public List<string> Warnings { get; private set; } = new List<string>();
    }
}
