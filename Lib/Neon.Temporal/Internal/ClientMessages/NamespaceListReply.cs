//-----------------------------------------------------------------------------
// FILE:	    NamespaceListReply.cs
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

namespace Neon.Temporal.Internal
{
    /// <summary>
    /// <b>proxy --> client:</b> Answers a <see cref="NamespaceListRequest"/>.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.NamespaceListReply)]
    internal class NamespaceListReply : ProxyReply
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public NamespaceListReply()
        {
            Type = InternalMessageTypes.NamespaceListReply;
        }

        /// <summary>
        /// Lists information about the Temporal namespaces.
        /// </summary>
        public List<NamespaceDescription> Namespaces
        {
            get => GetJsonProperty<List<NamespaceDescription>>(PropertyNames.Namespaces);
            set => SetJsonProperty<List<NamespaceDescription>>(PropertyNames.Namespaces, value);
        }

        /// <summary>
        /// Returns an opaque token that can be used in a subsequent <see cref="NamespaceListRequest"/>
        /// to obtain the next page of results.  This will be <c>null</c> when there are no
        /// remaining results.  This should be considered to be an opaque value.
        /// </summary>
        public byte[] NextPageToken
        {
            get => GetBytesProperty(PropertyNames.NextPageToken);
            set => SetBytesProperty(PropertyNames.NextPageToken, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new NamespaceListReply();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (NamespaceListReply)target;

            typedTarget.Namespaces    = this.Namespaces;
            typedTarget.NextPageToken = this.NextPageToken;
        }
    }
}
