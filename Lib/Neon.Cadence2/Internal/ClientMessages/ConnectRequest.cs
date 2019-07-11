//-----------------------------------------------------------------------------
// FILE:	    ConnectRequest.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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

using Neon.Cadence;
using Neon.Common;

// $todo(jeff.lill): Investegate adding metrics details.

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// <b>client --> proxy:</b> Requests the proxy establish a connection with a Cadence cluster.
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
        /// <para>
        /// The Cadence server network endpoints separated by commas.
        /// These may include a DNS hostname or IP address with a
        /// network port, formatted like:
        /// </para>
        /// <code>
        /// my-server.nhive.io:5555
        /// 1.2.3.4:5555
        /// </code>
        /// </summary>
        public string Endpoints
        {
            get => GetStringProperty(PropertyNames.Endpoints);
            set => SetStringProperty(PropertyNames.Endpoints, value);
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
        /// The default Cadence domain.
        /// </summary>
        public string Domain
        {
            get => GetStringProperty(PropertyNames.Domain);
            set => SetStringProperty(PropertyNames.Domain, value);
        }

        /// <summary>
        /// Indicates whether the Cadence domain should be created if it
        /// doesn't already exist.
        /// </summary>
        public bool CreateDomain
        {
            get => GetBoolProperty(PropertyNames.CreateDomain);
            set => SetBoolProperty(PropertyNames.CreateDomain, value);
        }

        /// <summary>
        /// Specifies the number of time the client will attempt to connect
        /// to the Cadence cluster.
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

            typedTarget.Endpoints     = this.Endpoints;
            typedTarget.Identity      = this.Identity;
            typedTarget.ClientTimeout = this.ClientTimeout;
            typedTarget.Domain        = this.Domain;
            typedTarget.CreateDomain  = this.CreateDomain;
            typedTarget.Retries       = this.Retries;
            typedTarget.RetryDelay    = this.RetryDelay;
        }
    }
}
