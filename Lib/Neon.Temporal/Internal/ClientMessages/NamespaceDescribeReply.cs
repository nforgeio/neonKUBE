//-----------------------------------------------------------------------------
// FILE:	    NamespaceDescribeReply.cs
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
        /// The namespace name.
        /// </summary>
        public string NamespaceInfoName
        {
            get => GetStringProperty(PropertyNames.NamespaceInfoName);
            set => SetStringProperty(PropertyNames.NamespaceInfoName, value);
        }

        /// <summary>
        /// Human readable description for the namespace.
        /// </summary>
        public string NamespaceInfoDescription
        {
            get => GetStringProperty(PropertyNames.NamespaceInfoDescription);
            set => SetStringProperty(PropertyNames.NamespaceInfoDescription, value);
        }

        /// <summary>
        /// The namespace status.
        /// </summary>
        public NamespaceStatus NamespaceInfoStatus
        {
            get => GetEnumProperty<NamespaceStatus>(PropertyNames.NamespaceInfoStatus);
            set => SetEnumProperty<NamespaceStatus>(PropertyNames.NamespaceInfoStatus, value);
        }

        /// <summary>
        /// Owner email address.
        /// </summary>
        public string NamespaceInfoOwnerEmail
        {
            get => GetStringProperty(PropertyNames.NamespaceInfoOwnerEmail);
            set => SetStringProperty(PropertyNames.NamespaceInfoOwnerEmail, value);
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
            var clone = new NamespaceDescribeReply();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (NamespaceDescribeReply)target;

            typedTarget.ConfigurationRetentionDays = this.ConfigurationRetentionDays;
            typedTarget.ConfigurationEmitMetrics   = this.ConfigurationEmitMetrics;
            typedTarget.NamespaceInfoName          = this.NamespaceInfoName;
            typedTarget.NamespaceInfoDescription   = this.NamespaceInfoDescription;
            typedTarget.NamespaceInfoStatus        = this.NamespaceInfoStatus;
            typedTarget.NamespaceInfoOwnerEmail    = this.NamespaceInfoOwnerEmail;
        }
    }
}
