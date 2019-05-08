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
    /// <b>library --> proxy:</b> Requests the details for a named domain.
    /// </summary>
    [ProxyMessage(MessageTypes.DomainUpdateRequest)]
    internal class DomainUpdateRequest : ProxyRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public DomainUpdateRequest()
        {
            Type = MessageTypes.DomainUpdateRequest;
        }

        /// <inheritdoc/>
        public override MessageTypes ReplyType => MessageTypes.DomainUpdateReply;

        /// <summary>
        /// The target Cadence domain name.
        /// </summary>
        public string Name
        {
            get => GetStringProperty("Name");
            set => SetStringProperty("Name", value);
        }

        /// <summary>
        /// Specifies the new description.
        /// </summary>
        public string UpdatedInfoDescription
        {
            get => GetStringProperty("UpdatedInfoDescription");
            set => SetStringProperty("UpdatedInfoDescription", value);
        }

        /// <summary>
        /// Specifies the new owner's email address.
        /// </summary>
        public string UpdatedInfoOwnerEmail
        {
            get => GetStringProperty("UpdatedInfoOwnerEmail");
            set => SetStringProperty("UpdatedInfoOwnerEmail", value);
        }

        /// <summary>
        /// Specifies the metrics emission setting.
        /// </summary>
        public bool ConfigurationEmitMetrics
        {
            get => GetBoolProperty("ConfigurationEmitMetrics");
            set => SetBoolProperty("ConfigurationEmitMetrics", value);
        }

        /// <summary>
        /// Specifies the workfloy history retention period in days.
        /// </summary>
        public int ConfigurationRetentionDays
        {
            get => GetIntProperty("ConfigurationRetentionDays");
            set => SetIntProperty("ConfigurationRetentionDays", value);
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
        }
    }
}
