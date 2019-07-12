//-----------------------------------------------------------------------------
// FILE:	    ProxyReply.cs
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
    /// Base class for all proxy requests.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.Unspecified)]
    internal class ProxyReply : ProxyMessage
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ProxyReply()
        {
        }

        /// <summary>
        /// Uniquely identifies the request this reply answers.
        /// </summary>
        public long RequestId
        {
            get => GetLongProperty(PropertyNames.RequestId);
            set => SetLongProperty(PropertyNames.RequestId, value);
        }

        /// <summary>
        /// Optionally indicates that the request failed.
        /// </summary>
        public CadenceError Error
        {
            get => GetJsonProperty<CadenceError>(PropertyNames.Error);
            set => SetJsonProperty<CadenceError>(PropertyNames.Error, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new ProxyReply();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (ProxyReply)target;

            typedTarget.RequestId = this.RequestId;
            typedTarget.Error     = this.Error;
        }

        /// <summary>
        /// Throws the related exception if the reply is reporting an error.
        /// </summary>
        public void ThrowOnError()
        {
            var error = this.Error;

            if (error != null)
            {
                throw error.ToException();
            }
        }
    }
}
