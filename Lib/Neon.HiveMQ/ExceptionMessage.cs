//-----------------------------------------------------------------------------
// FILE:	    QueryChannelException.cs
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
    /// Used by <see cref="QueryChannel"/> to serialize exceptions thrown
    /// by remote request handlers.
    /// </summary>
    public class ExceptionMessage
    {
        /// <summary>
        /// The remote exception message.
        /// </summary>
        [JsonProperty(PropertyName = "Message")]
        public string Message { get; set; }

        /// <summary>
        /// The fully qualified remote exception type name.
        /// </summary>
        [JsonProperty(PropertyName = "TypeName")]
        public string TypeName { get; set; }
    }
}
