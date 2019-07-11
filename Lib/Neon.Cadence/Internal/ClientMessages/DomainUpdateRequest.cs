//-----------------------------------------------------------------------------
// FILE:	    DomainUpdateRequest.cs
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
    /// <b>client --> proxy:</b> Requests the details for a named domain.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.DomainUpdateRequest)]
    internal class DomainUpdateRequest : ProxyRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public DomainUpdateRequest()
        {
            Type = InternalMessageTypes.DomainUpdateRequest;
        }

        /// <inheritdoc/>
        public override InternalMessageTypes ReplyType => InternalMessageTypes.DomainUpdateReply;

        /// <summary>
        /// The target Cadence domain name.
        /// </summary>
        public string Name
        {
            get => GetStringProperty(PropertyNames.Name);
            set => SetStringProperty(PropertyNames.Name, value);
        }

        /// <summary>
        /// Specifies the new description.
        /// </summary>
        public string UpdatedInfoDescription
        {
            get => GetStringProperty(PropertyNames.UpdatedInfoDescription);
            set => SetStringProperty(PropertyNames.UpdatedInfoDescription, value);
        }

        /// <summary>
        /// Specifies the new owner's email address.
        /// </summary>
        public string UpdatedInfoOwnerEmail
        {
            get => GetStringProperty(PropertyNames.UpdatedInfoOwnerEmail);
            set => SetStringProperty(PropertyNames.UpdatedInfoOwnerEmail, value);
        }

        /// <summary>
        /// Specifies the metrics emission setting.
        /// </summary>
        public bool ConfigurationEmitMetrics
        {
            get => GetBoolProperty(PropertyNames.ConfigurationEmitMetrics);
            set => SetBoolProperty(PropertyNames.ConfigurationEmitMetrics, value);
        }

        /// <summary>
        /// Specifies the workfloy history retention period in days.
        /// </summary>
        public int ConfigurationRetentionDays
        {
            get => GetIntProperty(PropertyNames.ConfigurationRetentionDays);
            set => SetIntProperty(PropertyNames.ConfigurationRetentionDays, value);
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
            var clone = new DomainUpdateRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (DomainUpdateRequest)target;

            typedTarget.Name                       = this.Name;
            typedTarget.ConfigurationEmitMetrics   = this.ConfigurationEmitMetrics;
            typedTarget.ConfigurationRetentionDays = this.ConfigurationRetentionDays;
            typedTarget.UpdatedInfoDescription     = this.UpdatedInfoDescription;
            typedTarget.UpdatedInfoOwnerEmail      = this.UpdatedInfoOwnerEmail;
            typedTarget.SecurityToken              = this.SecurityToken;
        }
    }
}
