//-----------------------------------------------------------------------------
// FILE:	    DomainListReply.cs
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
    /// <b>proxy --> client:</b> Answers a <see cref="DisconnectRequest"/>.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.DomainListReply)]
    internal class DomainListReply : ProxyReply
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public DomainListReply()
        {
            Type = InternalMessageTypes.DomainListReply;
        }

        /// <summary>
        /// The domain information.
        /// </summary>
        public InternalDomainInfo DomainInfo
        {
            get => GetJsonProperty<InternalDomainInfo>(PropertyNames.DomainInfo);
            set => SetJsonProperty<InternalDomainInfo>(PropertyNames.DomainInfo, value);
        }

        /// <summary>
        /// The domain configuration.
        /// </summary>
        public InternalDomainConfiguration Configuration
        {
            get => GetJsonProperty<InternalDomainConfiguration>(PropertyNames.Configuration);
            set => SetJsonProperty<InternalDomainConfiguration>(PropertyNames.Configuration, value);
        }

        /// <summary>
        /// $todo(jefflill): Don't know what this is.
        /// </summary>
        public long FailoverVersion
        {
            get => GetLongProperty(PropertyNames.FailoverVersion);
            set => SetLongProperty(PropertyNames.FailoverVersion, value);
        }

        /// <summary>
        /// Indicates whether the domain is global.
        /// </summary>
        public bool IsGlobalDomain
        {
            get => GetBoolProperty(PropertyNames.IsGlobalDomain);
            set => SetBoolProperty(PropertyNames.IsGlobalDomain, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new DomainListReply();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (DomainListReply)target;

            typedTarget.DomainInfo      = this.DomainInfo;
            typedTarget.Configuration   = this.Configuration;
            typedTarget.FailoverVersion = this.FailoverVersion;
            typedTarget.IsGlobalDomain  = this.IsGlobalDomain;
        }
    }
}
