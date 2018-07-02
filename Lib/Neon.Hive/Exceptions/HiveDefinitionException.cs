//-----------------------------------------------------------------------------
// FILE:	    HiveDefinitionException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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

namespace Neon.Hive
{
    /// <summary>
    /// Describes hive definition errors.
    /// </summary>
    public class HiveDefinitionException : Exception
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public HiveDefinitionException()
        {
        }

        /// <summary>
        /// Consstructs an instance with a message.
        /// </summary>
        /// <param name="message">The message.</param>
        public HiveDefinitionException(string message)
            : base(message)
        {
        }
    }
}
