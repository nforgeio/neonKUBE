//-----------------------------------------------------------------------------
// FILE:	    SetupProgressDelegate.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Collections;
using Neon.Common;
using Neon.SSH;

namespace Neon.Kube
{
    /// <summary>
    /// Holds setup progress message details.
    /// </summary>
    public class SetupProgressMessage
    {
        /// <summary>
        /// Returns the node associated with this message, if any.
        /// </summary>
        public object Node { get; set; }

        /// <summary>
        /// Returns the verb associated with this message, if any.
        /// </summary>
        public string Verb { get; set; }

        /// <summary>
        /// Returns the message text.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Indicates whether the message describes an error.
        /// </summary>
        public bool IsError { get; set; }

        /// <summary>
        /// Indicates whether the setup controller has been signalled to
        /// cancel the entire operation.
        /// </summary>
        public bool CancelPending { get; set; }
    }

    /// <summary>
    /// Used for raising the <see cref="ISetupController.ProgressEvent"/>.
    /// </summary>
    /// <param name="message">The status message.</param>
    public delegate void SetupProgressDelegate(SetupProgressMessage message);
}
