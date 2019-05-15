//-----------------------------------------------------------------------------
// FILE:	    ProxyRequest.cs
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

using Neon.Cadence;
using Neon.Common;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// Base class for all proxy requests.
    /// </summary>
    [ProxyMessage(MessageTypes.Unspecified)]
    internal class ProxyRequest : ProxyMessage
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ProxyRequest()
        {
        }

        /// <summary>
        /// Uniquely identifies this request.
        /// </summary>
        public long RequestId
        {
            get => GetLongProperty("RequestId");
            set => SetLongProperty("RequestId", value);
        }

        /// <summary>
        /// <para>
        /// Optionally specifies the maximum time the operation may
        /// take before it should be aborted.  The operation reply 
        /// should specify a <see cref="CadenceError"/> of type
        /// <see cref="CadenceErrorTypes.Timeout"/> when this happens.
        /// </para>
        /// <note>
        /// A <see cref="TimeSpan.Zero"/> (the default) indicates that
        /// the operation may proceed indefinitely.
        /// </note>
        /// </summary>
        /// <remarks>
        /// Note that operations are not required to support this
        /// property when that's doesn't makes sense.
        /// </remarks>
        public TimeSpan Timeout
        {
            get => GetTimeSpanProperty("Timeout");
            set => SetTimeSpanProperty("Timeout", value);
        }

        /// <summary>
        /// Derived request types must return the type of the expected
        /// <see cref="ProxyReply"/> message.
        /// </summary>
        public virtual MessageTypes ReplyType => MessageTypes.Unspecified;

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new ProxyRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (ProxyRequest)target;

            typedTarget.RequestId = this.RequestId;
            typedTarget.Timeout   = this.Timeout;
        }
    }
}
