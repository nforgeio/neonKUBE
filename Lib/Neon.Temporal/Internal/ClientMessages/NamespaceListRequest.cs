//-----------------------------------------------------------------------------
// FILE:	    NamespaceListRequest.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
    /// <b>client --> proxy:</b> Requests a list of the Temporal namespaces.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.NamespaceListRequest)]
    internal class NamespaceListRequest : ProxyRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public NamespaceListRequest()
        {
            Type = InternalMessageTypes.NamespaceListRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.NamespaceListReply;

        /// <summary>
        /// Specifies the maximum number of items to be returned in the reponse.
        /// </summary>
        public int PageSize
        {
            get => GetIntProperty(PropertyNames.PageSize);
            set => SetIntProperty(PropertyNames.PageSize, value);
        }
        
        /// <summary>
        /// Optionally specifies the next page of results.  This will be <c>null</c>
        /// for the first page of results and can be set to the the value returned
        /// as <see cref="NamespaceListReply.NextPageToken"/> to retrieve the next page
        /// of results.  This should be considered to be an opaque value.
        /// </summary>
        public byte[] NextPageToken
        {
            get => GetBytesProperty(PropertyNames.NextPageToken);
            set => SetBytesProperty(PropertyNames.NextPageToken, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new NamespaceListRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (NamespaceListRequest)target;

            typedTarget.PageSize      = this.PageSize;
            typedTarget.NextPageToken = this.NextPageToken;
        }
    }
}
