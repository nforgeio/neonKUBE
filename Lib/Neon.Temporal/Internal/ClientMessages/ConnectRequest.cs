//-----------------------------------------------------------------------------
// FILE:	    ConnectRequest.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel;

using Neon.Common;
using Neon.Temporal;

// $todo(jefflill): Investigate adding metrics details.

namespace Neon.Temporal.Internal
{
    /// <summary>
    /// <b>client --> proxy:</b> Requests the proxy establish a connection with a Temporal cluster.
    /// This maps to a <c>NewClient()</c> in the proxy.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.ConnectRequest)]
    internal class ConnectRequest : ProxyRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ConnectRequest()
        {
            Type = InternalMessageTypes.ConnectRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.ConnectReply;

        /// <summary>
        /// Specifies the Temporal server host and port being connected.  This is typically formatted
        /// as <b>host:port</b> where <b>host</b> is the IP address or hostname for the
        /// Temporal server.  Alternatively, this can be formatted as <b>dns:///host:port</b>
        /// to enable DNS round-robin lookups.  This defaults to <b>localhost:7233</b>.
        /// </summary>
        public string HostPort
        {
            get => GetStringProperty(PropertyNames.HostPort);
            set => SetStringProperty(PropertyNames.HostPort, value);
        }

        /// <summary>
        /// Optionally identifies the client application.
        /// </summary>
        public string Identity
        {
            get => GetStringProperty(PropertyNames.Identity);
            set => SetStringProperty(PropertyNames.Identity, value);
        }

        /// <summary>
        /// The default client timeout.
        /// </summary>
        public TimeSpan ClientTimeout
        {
            get => GetTimeSpanProperty(PropertyNames.ClientTimeout);
            set => SetTimeSpanProperty(PropertyNames.ClientTimeout, value);
        }

        /// <summary>
        /// The default Temporal namespace.
        /// </summary>
        public string Namespace
        {
            get => GetStringProperty(PropertyNames.Namespace);
            set => SetStringProperty(PropertyNames.Namespace, value ?? string.Empty);
        }

        /// <summary>
        /// Indicates whether the Temporal <see cref="Namespace"/> should be created 
        /// when it doesn't already exist.
        /// </summary>
        public bool CreateNamespace
        {
            get => GetBoolProperty(PropertyNames.CreateNamespace);
            set => SetBoolProperty(PropertyNames.CreateNamespace, value);
        }

        /// <summary>
        /// Specifies the number of time the client will attempt to connect
        /// to the Temporal cluster.
        /// </summary>
        public int Retries
        {
            get => GetIntProperty(PropertyNames.RetryAttempts);
            set => SetIntProperty(PropertyNames.RetryAttempts, value);
        }

        /// <summary>
        /// Specifies the time to delay before retrying to connect to the cluster.
        /// </summary>
        public TimeSpan RetryDelay
        {
            get => GetTimeSpanProperty(PropertyNames.RetryDelay);
            set => SetTimeSpanProperty(PropertyNames.RetryDelay, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new ConnectRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (ConnectRequest)target;

            typedTarget.HostPort         = this.HostPort;
            typedTarget.Identity         = this.Identity;
            typedTarget.ClientTimeout    = this.ClientTimeout;
            typedTarget.Namespace        = this.Namespace;
            typedTarget.CreateNamespace  = this.CreateNamespace;
            typedTarget.Retries          = this.Retries;
            typedTarget.RetryDelay       = this.RetryDelay;
        }
    }
}
