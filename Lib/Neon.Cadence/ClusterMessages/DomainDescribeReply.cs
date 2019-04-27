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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;

using Newtonsoft.Json;
using YamlDotNet.Serialization;

using Neon.Common;

// $todo(jeff.lill):
//
// There's several more fields that could be handled.

namespace Neon.Cadence
{
    /// <summary>
    /// <b>proxy --> library:</b> Answers a <see cref="DomainDescribeRequest"/>.
    /// </summary>
    [ProxyMessage(MessageTypes.DomainDescribeReply)]
    internal class DomainDescribeReply : ProxyReply
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public DomainDescribeReply()
        {
            Type = MessageTypes.DomainDescribeReply;
        }

        /// <summary>
        /// The domain name.
        /// </summary>
        public string Name
        {
            get => GetStringProperty("Name");
            set => SetStringProperty("Name", value);
        }

        /// <summary>
        /// Human readable description for the domain.
        /// </summary>
        public string Description
        {
            get => GetStringProperty("Description");
            set => SetStringProperty("Description", value);
        }

        /// <summary>
        /// The domain status.
        /// </summary>
        public string Status
        {
            get => GetStringProperty("Status");
            set => SetStringProperty("Status", value);
        }

        /// <summary>
        /// Owner email address.
        /// </summary>
        public string OwnerEmail
        {
            get => GetStringProperty("OwnerEmail");
            set => SetStringProperty("OwnerEmail", value);
        }

        /// <summary>
        /// The domain UUID.
        /// </summary>
        public string Uuid
        {
            get => GetStringProperty("Uuid");
            set => SetStringProperty("Uuid", value);
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

            typedTarget.Name        = this.Name;
            typedTarget.Description = this.Description;
            typedTarget.Status      = this.Status;
            typedTarget.OwnerEmail  = this.OwnerEmail;
            typedTarget.Uuid        = this.Uuid;
        }
    }
}
