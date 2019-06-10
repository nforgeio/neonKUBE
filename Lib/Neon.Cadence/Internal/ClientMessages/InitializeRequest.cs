//-----------------------------------------------------------------------------
// FILE:	    InitializeRequest.cs
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

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// <b>client --> proxy:</b> Informs the proxy of the network endpoint
    /// where the client is listening for proxy messages.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.InitializeRequest)]
    internal class InitializeRequest : ProxyRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public InitializeRequest()
        {
            Type = InternalMessageTypes.InitializeRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.InitializeReply;

        /// <summary>
        /// The IP address where the Cadence client is listening for proxy messages
        /// send by the <b>cadence-proxy</b>.
        /// </summary>
        public string LibraryAddress
        {
            get => GetStringProperty(PropertyNames.LibraryAddress);
            set => SetStringProperty(PropertyNames.LibraryAddress, value);
        }

        /// <summary>
        /// The port where the Cadence client is listening for proxy messages
        /// send by the <b>cadence-proxy</b>.
        /// </summary>
        public int LibraryPort
        {
            get => GetIntProperty(PropertyNames.LibraryPort);
            set => SetIntProperty(PropertyNames.LibraryPort, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new InitializeRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (InitializeRequest)target;

            typedTarget.LibraryAddress = this.LibraryAddress;
            typedTarget.LibraryPort    = this.LibraryPort;
        }
    }
}
