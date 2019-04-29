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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;

using Newtonsoft.Json;
using YamlDotNet.Serialization;

using Neon.Common;

// $todo(jeff.lill): Investegate adding metrics details.

namespace Neon.Cadence
{
    /// <summary>
    /// <b>library --> proxy:</b> Requests the proxy establish a connection with a Cadence cluster.
    /// This maps to a <c>NewClient()</c> in the proxy.
    /// </summary>
    [ProxyMessage(MessageTypes.ConnectRequest)]
    internal class ConnectRequest : ProxyRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ConnectRequest()
        {
            Type = MessageTypes.ConnectRequest;
        }

        /// <inheritdoc/>
        public override MessageTypes ReplyType => MessageTypes.ConnectReply;

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
            get => GetStringProperty("Endpoints");
            set => SetStringProperty("Endpoints", value);
        }

        /// <summary>
        /// Optionally identifies the client application.
        /// </summary>
        public string Identity
        {
            get => GetStringProperty("Identity");
            set => SetStringProperty("Identity", value);
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

            typedTarget.Endpoints = this.Endpoints;
            typedTarget.Identity  = this.Identity;
        }
    }
}
