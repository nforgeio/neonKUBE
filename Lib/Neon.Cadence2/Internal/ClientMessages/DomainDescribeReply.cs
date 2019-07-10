//-----------------------------------------------------------------------------
// FILE:	    DomainDescribeReply.cs
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
    /// <b>proxy --> client:</b> Answers a <see cref="DomainDescribeRequest"/>.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.DomainDescribeReply)]
    internal class DomainDescribeReply : ProxyReply
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public DomainDescribeReply()
        {
            Type = InternalMessageTypes.DomainDescribeReply;
        }

        /// <summary>
        /// The domain name.
        /// </summary>
        public string DomainInfoName
        {
            get => GetStringProperty(PropertyNames.DomainInfoName);
            set => SetStringProperty(PropertyNames.DomainInfoName, value);
        }

        /// <summary>
        /// Human readable description for the domain.
        /// </summary>
        public string DomainInfoDescription
        {
            get => GetStringProperty(PropertyNames.DomainInfoDescription);
            set => SetStringProperty(PropertyNames.DomainInfoDescription, value);
        }

        /// <summary>
        /// The domain status.
        /// </summary>
        public DomainStatus DomainInfoStatus
        {
            get => GetEnumProperty<DomainStatus>(PropertyNames.DomainInfoStatus);
            set => SetEnumProperty<DomainStatus>(PropertyNames.DomainInfoStatus, value);
        }

        /// <summary>
        /// Owner email address.
        /// </summary>
        public string DomainInfoOwnerEmail
        {
            get => GetStringProperty(PropertyNames.DomainInfoOwnerEmail);
            set => SetStringProperty(PropertyNames.DomainInfoOwnerEmail, value);
        }

        /// <summary>
        /// The workflow history retention period in days.
        /// </summary>
        public int ConfigurationRetentionDays
        {
            get => GetIntProperty(PropertyNames.ConfigurationRetentionDays);
            set => SetIntProperty(PropertyNames.ConfigurationRetentionDays, value);
        }

        /// <summary>
        /// Enables metric generation.
        /// </summary>
        public bool ConfigurationEmitMetrics
        {
            get => GetBoolProperty(PropertyNames.ConfigurationEmitMetrics);
            set => SetBoolProperty(PropertyNames.ConfigurationEmitMetrics, value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new DomainDescribeReply();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (DomainDescribeReply)target;

            typedTarget.ConfigurationRetentionDays = this.ConfigurationRetentionDays;
            typedTarget.ConfigurationEmitMetrics   = this.ConfigurationEmitMetrics;
            typedTarget.DomainInfoName             = this.DomainInfoName;
            typedTarget.DomainInfoDescription      = this.DomainInfoDescription;
            typedTarget.DomainInfoStatus           = this.DomainInfoStatus;
            typedTarget.DomainInfoOwnerEmail       = this.DomainInfoOwnerEmail;
        }
    }
}
