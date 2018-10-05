//-----------------------------------------------------------------------------
// FILE:	    QueryException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using EasyNetQ;
using EasyNetQ.DI;
using EasyNetQ.Logging;
using EasyNetQ.Management.Client;

using RabbitMQ;
using RabbitMQ.Client;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Net;

namespace Neon.HiveMQ
{
    /// <summary>
    /// Thrown by a <see cref="QueryChannel.Query{TRequest, TResponse}(TRequest, TimeSpan)"/>
    /// or <see cref="QueryChannel.QueryAsync{TRequest, TResponse}(TRequest, TimeSpan, CancellationToken)"/>
    /// method when the remote request handler throws an exception.
    /// </summary>
    public class QueryException : Exception
    {
        /// <summary>
        /// Constructs an instance by gather the important properties from 
        /// an exception thrown by q query handler.
        /// </summary>
        /// <param name="message">The remote exception message.</param>
        /// <param name="typeName">The remote exception's fully qualified type name.</param>
        public QueryException(string message, string typeName)
            : base(message)
        {
            Covenant.Requires<ArgumentNullException>(message != null);
            Covenant.Requires<ArgumentNullException>(typeName != null);

            this.TypeName = typeName;
        }

        /// <summary>
        /// Returns the remote exception's fully qualified type name.
        /// </summary>
        public string TypeName { get; set; }
    }
}
