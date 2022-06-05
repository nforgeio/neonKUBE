//-----------------------------------------------------------------------------
// FILE:	    Pod.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

namespace NeonNodeAgent
{
    /// <summary>
    /// Abstracts access to the executing pod properties.
    /// </summary>
    public static class Pod
    {
        /// <summary>
        /// Returns the Kubernetes namespace where the executing pod is running.
        /// </summary>
        public static readonly string Namespace = Environment.GetEnvironmentVariable("POD_NAMESPACE");

        /// <summary>
        /// Returns the name of the executing pod.
        /// </summary>
        public static readonly string Name = Environment.GetEnvironmentVariable("POD_NAME");
    }
}
