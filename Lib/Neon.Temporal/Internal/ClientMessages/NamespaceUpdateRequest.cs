//-----------------------------------------------------------------------------
// FILE:	    NamespaceUpdateRequest.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
    /// <b>client --> proxy:</b> Requests the details for a named namespace.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.NamespaceUpdateRequest)]
    internal class NamespaceUpdateRequest : ProxyRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public NamespaceUpdateRequest()
        {
            Type = InternalMessageTypes.NamespaceUpdateRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.NamespaceUpdateReply;

        /// <summary>
        /// Name of the namespace to update.
        /// </summary>
        public string Name
        {
            get => GetStringProperty(PropertyNames.Name);
            set => SetStringProperty(PropertyNames.Name, value);
        }

        /// <summary>
        /// Specifies the updated namespace info.
        /// </summary>
        public UpdateNamespaceInfo UpdateNamespaceInfo
        {
            get => GetJsonProperty<UpdateNamespaceInfo>(PropertyNames.UpdateNamespaceInfo);
            set => SetJsonProperty<UpdateNamespaceInfo>(PropertyNames.UpdateNamespaceInfo, value);
        }

        /// <summary>
        /// Specifies the updated namespace config.
        /// </summary>
        public NamespaceConfig NamespaceConfig
        {
            get => GetJsonProperty<NamespaceConfig>(PropertyNames.NamespaceConfig);
            set => SetJsonProperty<NamespaceConfig>(PropertyNames.NamespaceConfig, value);
        }

        /// <summary>
        /// Specifies the updated namespace replication config.
        /// </summary>
        public NamespaceReplicationConfig NamespaceReplicationConfig
        {
            get => GetJsonProperty<NamespaceReplicationConfig>(PropertyNames.NamespaceReplicationConfig);
            set => SetJsonProperty<NamespaceReplicationConfig>(PropertyNames.NamespaceReplicationConfig, value);
        }

        /// <summary>
        /// Optional delete bad binary.
        /// </summary>
        public string DeleteBadBinary
        {
            get => GetStringProperty(PropertyNames.DeleteBadBinary);
            set => SetStringProperty(PropertyNames.DeleteBadBinary, value);
        }

        /// <summary>
        /// Optional security token.
        /// </summary>
        public string SecurityToken
        {
            get => GetStringProperty(PropertyNames.SecurityToken);
            set => SetStringProperty(PropertyNames.SecurityToken, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new NamespaceUpdateRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (NamespaceUpdateRequest)target;

            typedTarget.Name                       = this.Name;
            typedTarget.NamespaceConfig            = this.NamespaceConfig;
            typedTarget.NamespaceReplicationConfig = this.NamespaceReplicationConfig;
            typedTarget.UpdateNamespaceInfo        = this.UpdateNamespaceInfo;
            typedTarget.DeleteBadBinary            = this.DeleteBadBinary;
            typedTarget.SecurityToken              = this.SecurityToken;
        }
    }
}
