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
using Neon.SSH;

namespace Neon.Kube
{
    /// <summary>
    /// Describes the current state of a node during cluster setup.
    /// </summary>
    public class SetupNodeStatus : NotifyPropertyChanged
    {
        private bool            isClone;
        private string          status;
        private SetupStepState  stepState;
        private object          metadata;

        /// <summary>
        /// Default cluster used by <see cref="Clone"/> as well as for UX design mode.
        /// </summary>
        public SetupNodeStatus()
        {
            this.isClone = true;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="node">The source node.</param>
        /// <param name="metadata">The node metadata.</param>
        public SetupNodeStatus(LinuxSshProxy node, object metadata)
        {
            Covenant.Requires<ArgumentNullException>(node != null, nameof(node));
            Covenant.Requires<ArgumentNullException>(metadata != null, nameof(metadata));

            this.isClone   = false;
            this.Name      = node.Name;
            this.Metadata  = metadata;
            this.Status    = node.Status;

            if (!node.IsInvolved)
            {
                this.stepState = SetupStepState.NotInvolved;
            }
            else
            {
                if (node.IsConfiguring)
                {
                    this.stepState = SetupStepState.Running;
                }
                else if (node.IsReady)
                {
                    this.stepState = SetupStepState.Done;
                }
                else if (node.IsFaulted)
                {
                    this.stepState = SetupStepState.Failed;
                }
                else
                {
                    this.stepState = SetupStepState.Pending;
                }
            }

            this.StepState = stepState;
        }

        /// <summary>
        /// The node name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The node role.  This will be one of the <see cref="NodeRole"/> values.
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// The node status string.
        /// </summary>
        public string Status
        {
            get
            {
                // $hack(jefflill):
                //
                // The status will be prefixed by "[x] " for steps that have completed
                // and "[!] " for steps that failed.  This set by ancient code and is
                // used for the status displayed on the console.
                //
                // We're going to strip these off here and assume that the GUI apps
                // will use the new [StepStatus] property for this.

                if (status == null)
                {
                    return string.Empty;
                }

                if (status.StartsWith("[x] ") || status.StartsWith("[!] "))
                {
                    return status.Substring(4);
                }
                else
                {
                    return status;
                }
            }

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
        /// Indicates the setup step state for the node.
        /// </summary>
        public SetupStepState StepState
        {
            get => stepState;

            set
            {
                if (value != stepState)
                {
                    stepState = value;
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
                Status    = this.status,
                StepState = this.StepState,
                Metadata  = this.Metadata
            };
        }

        /// <summary>
        /// Copies the properties from the source status to this instance, raising
        /// <see cref="INotifyPropertyChanged"/> related events as required.
        /// </summary>
        /// <param name="source">The source instance.</param>
        public void UpdateFrom(SetupNodeStatus source)
        {
            Covenant.Requires<ArgumentNullException>(source != null, nameof(source));
            Covenant.Assert(this.isClone, "Target must be cloned.");
            Covenant.Assert(!source.isClone, "Source cannot be cloned.");

            this.Name     = source.Name;
            this.Status   = source.status;
            this.Metadata = source.Metadata;
        }
    }
}
