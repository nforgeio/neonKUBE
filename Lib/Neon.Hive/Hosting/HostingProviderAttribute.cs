//-----------------------------------------------------------------------------
// FILE:	    HostingProviderAttribute.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Text;

namespace Neon.Hive
{
    /// <summary>
    /// Use this attribute to identify <see cref="IHostingManager"/> class implementations
    /// so they can be discovered by the <see cref="HostingManager"/> class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class HostingProviderAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="environment">Specifies the target hosting environment.</param>
        public HostingProviderAttribute(HostingEnvironments environment)
        {
            this.Environment = environment;
        }

        /// <summary>
        /// Returns the target hosting environment supported by the tagged <see cref="IHostingManager"/>.
        /// </summary>
        public HostingEnvironments Environment { get; private set; }
    }
}
