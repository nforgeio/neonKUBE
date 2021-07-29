//-----------------------------------------------------------------------------
// FILE:	    SetupNodeStatus.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Neon.Common;
using Neon.Data;

namespace Neon.Kube
{
    /// <summary>
    /// Describes the current state of a node during cluster setup.
    /// </summary>
    public class SetupNodeStatus : NotifyPropertyChanged
    {
        private bool        isClone;
        private bool        isReady;
        private bool        isFaulted;
        private string      status;
        private object      metadata;

        /// <summary>
        /// Default cluster used by <see cref="Clone"/>.
        /// </summary>
        private SetupNodeStatus()
        {
            this.isClone = true;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The node name.</param>
        /// <param name="status">The node status.</param>
        /// <param name="metadata">The node metadata.</param>
        public SetupNodeStatus(string name, string status, object metadata)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(metadata != null, nameof(metadata));

            this.isClone   = false;
            this.Name      = name;
            this.Metadata  = metadata;
            this.IsReady   = false;
            this.IsFaulted = false;
            this.status    = status;
        }

        /// <summary>
        /// Returns the node name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Indicates when the node has successfully completed the current setup step.
        /// Note that this will always return <c>false</c> when the node is faulted.
        /// </summary>
        public bool IsReady
        {
            get => isReady && !IsFaulted;

            set
            {
                if (value != isReady)
                {
                    isReady = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// Indicates whether a setup step failed on the node.
        /// </summary>
        public bool IsFaulted
        {
            get => isFaulted;

            set
            {
                if (value != isFaulted)
                {
                    isFaulted = value;

                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(IsReady));  // Raise this too because it depends on [IsFaulted].
                }
            }
        }

        /// <summary>
        /// The node status.
        /// </summary>
        public string Status
        {
            get => status;

            set
            {
                value ??= string.Empty;  // Status will never be set to NULL

                if (value != status)
                {
                    status = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// The node metadata as an object.  The actual type can be determined
        /// by examining <see cref="ISetupController.NodeMetadataType"/>.
        /// </summary>
        [JsonIgnore]
        public object Metadata
        {
            get => metadata;

            set
            {
                if (!ReferenceEquals(value, metadata))
                {
                    metadata = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// Returns a clone of the instance.
        /// </summary>
        /// <returns>The clone.</returns>
        public SetupNodeStatus Clone()
        {
            if (this.isClone)
            {
                throw new NotSupportedException("Cannot clone a cloned instance.");
            }

            return new SetupNodeStatus()
            {
                Name      = this.Name,
                IsFaulted = this.IsFaulted,
                Status    = this.status,
                Metadata  = this.Metadata
            };
        }

        /// <summary>
        /// Copies the properties from the source status to this instance, raising
        /// <see cref="INotifyPropertyChanged"/> related events as require.
        /// </summary>
        /// <param name="source">The source instance.</param>
        internal void UpdateFrom(SetupNodeStatus source)
        {
            Covenant.Requires<ArgumentNullException>(source != null, nameof(source));
            Covenant.Assert(this.isClone, "Target must be cloned.");
            Covenant.Assert(!source.isClone, "Source cannot be cloned.");

            this.Name      = source.Name;
            this.IsFaulted = source.IsFaulted;
            this.Status    = source.status;
            this.Metadata  = source.Metadata;

            if (this.isReady != source.isReady)
            {
                this.isReady = source.isReady;
                RaisePropertyChanged(nameof(isReady));
            }
        }
    }
}
