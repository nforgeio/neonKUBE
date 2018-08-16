//-----------------------------------------------------------------------------
// FILE:	    XenObject.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Neon.Common;
using Neon.Hive;

namespace Neon.Xen
{
    /// <summary>
    /// Base class for all XenServer object that implements common properties.
    /// </summary>
    public abstract class XenObject
    {
        /// <summary>
        /// Constructs an instance from raw property values returned by the <b>xe CLI</b>.
        /// </summary>
        /// <param name="rawProperties">The raw object properties.</param>
        internal XenObject(IDictionary<string, string> rawProperties)
        {
            var properties = new Dictionary<string, string>();

            foreach (var item in rawProperties)
            {
                properties.Add(item.Key, item.Value);
            }

            this.Properties = new ReadOnlyDictionary<string, string>(properties);
        }

        /// <summary>
        /// Returns the read-only dictionary including all raw object properties.
        /// </summary>
        public IDictionary<string, string> Properties { get; private set; }
    }
}
