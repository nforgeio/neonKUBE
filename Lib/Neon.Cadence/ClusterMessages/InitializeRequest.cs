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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;

using Newtonsoft.Json;
using YamlDotNet.Serialization;

using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// <b>proxy --> library:</b> Signals the proxy that it should terminate gracefully.  The
    /// proxy should send a <see cref="TerminateReply"/> back to the library and
    /// then exit, terminating the process.
    /// </summary>
    [ProxyMessage(MessageTypes.InitializeRequest)]
    internal class InitializeRequest : ProxyRequest
    {
        /// <summary>
        /// The IP address where the Cadence Library is listening for proxy messages
        /// send by the Cadence Proxy.
        /// </summary>
        public string LibraryAddress
        {
            get => GetStringProperty("LibraryAddress");
            set => SetStringProperty("LibraryAddress", value);
        }

        /// <summary>
        /// The port where the Cadence Library is listening for proxy messages
        /// send by the Cadence Proxy.
        /// </summary>
        public int LibraryPort
        {
            get => GetIntProperty("LibraryPort");
            set => SetIntProperty("LibraryPort", value);
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
        }
    }
}
