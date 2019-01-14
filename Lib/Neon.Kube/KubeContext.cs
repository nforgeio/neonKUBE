//-----------------------------------------------------------------------------
// FILE:	    KubeContext.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Net;

// $todo(jeff.lill):
//
// We're currently identifying Kubernetes contexts by cluster name only.  We need
// to generalize this so this can also encode the user and namespace.

namespace Neon.Kube
{
    /// <summary>
    /// Manages Kubenetes client contexts.
    /// </summary>
    public class KubeContext
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the current Kubernetes context or <c>null</c>.
        /// </summary>
        public static KubeContext Current
        {
            get
            {
                // $todo(jeff.lill): Implement this.

                return null;
            }
        }

        /// <summary>
        /// Determines whether a Kubernetes context already exists.
        /// </summary>
        /// <param name="name">The context name.</param>
        /// <returns><c>true</c> if the context exists.</returns>
        public static bool Exists(string name)
        {
            // $todo(jeff.lill): Implement this.

            return false;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Private constructor.
        /// </summary>
        private KubeContext()
        {
        }
    }
}
