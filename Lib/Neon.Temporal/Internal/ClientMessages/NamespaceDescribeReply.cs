//-----------------------------------------------------------------------------
// FILE:	    NamespaceDescribeReply.cs
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
    /// <b>proxy --> client:</b> Answers a <see cref="NamespaceDescribeRequest"/>.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.NamespaceDescribeReply)]
    internal class NamespaceDescribeReply : ProxyReply
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public NamespaceDescribeReply()
        {
            Type = InternalMessageTypes.NamespaceDescribeReply;
        }

        /// <summary>
        /// The namespace info.
        /// </summary>
        public NamespaceInfo NamespaceInfo
        {
            get => GetJsonProperty<NamespaceInfo>(PropertyNames.NamespaceInfo);
            set => SetJsonProperty<NamespaceInfo>(PropertyNames.NamespaceInfo, value);
        }

        /// <summary>
        /// The namespace configuration.
        /// </summary>
        public NamespaceConfig NamespaceConfig
        {
            get => GetJsonProperty<NamespaceConfig>(PropertyNames.NamespaceConfig);
            set => SetJsonProperty<NamespaceConfig>(PropertyNames.NamespaceConfig, value);
        }

        /// <summary>
        /// The namespace replication configuration.
        /// </summary>
        public NamespaceReplicationConfig NamespaceReplicationConfig
        {
            get => GetJsonProperty<NamespaceReplicationConfig>(PropertyNames.NamespaceReplicationConfig);
            set => SetJsonProperty<NamespaceReplicationConfig>(PropertyNames.NamespaceReplicationConfig, value);
        }

        /// <summary>
        /// The failover version for the namespace.
        /// </summary>
        public long FailoverVersion
        {
            get => GetLongProperty(PropertyNames.FailoverVersion);
            set => SetLongProperty(PropertyNames.FailoverVersion, value);
        }

        /// <summary>
        /// Indicates whether the namespace is a global namespace.
        /// </summary>
        public bool IsGlobalNamespace
        {
            get => GetBoolProperty(PropertyNames.IsGlobalNamespace);
            set => SetBoolProperty(PropertyNames.IsGlobalNamespace, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new NamespaceDescribeReply();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (NamespaceDescribeReply)target;

            typedTarget.NamespaceInfo              = this.NamespaceInfo;
            typedTarget.NamespaceConfig            = this.NamespaceConfig;
            typedTarget.NamespaceReplicationConfig = this.NamespaceReplicationConfig;
            typedTarget.FailoverVersion            = this.FailoverVersion;
            typedTarget.IsGlobalNamespace          = this.IsGlobalNamespace;
        }
    }
}
