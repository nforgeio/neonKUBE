//-----------------------------------------------------------------------------
// FILE:	    ClusterDefinitionException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;

namespace Neon.Cluster
{
    /// <summary>
    /// Describes cluster definition errors.
    /// </summary>
    public class ClusterDefinitionException : Exception
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ClusterDefinitionException()
        {
        }

        /// <summary>
        /// Consstructs an instance with a message.
        /// </summary>
        /// <param name="message">The message.</param>
        public ClusterDefinitionException(string message)
            : base(message)
        {
        }
    }
}
