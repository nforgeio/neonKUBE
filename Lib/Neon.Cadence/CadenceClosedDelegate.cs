//-----------------------------------------------------------------------------
// FILE:	    CadenceClosedDelegate.cs
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

namespace Neon.Cadence
{
    /// <summary>
    /// Delegate called by a <see cref="CadenceClient"/> when the connection is closed
    /// explicitly or where there's a problem communicating with the <b>cadence-proxy</b>.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="args">The event arguments.</param>
    public delegate void CadenceClosedDelegate(object sender, CadenceClientClosedArgs args);

    /// <summary>
    /// The event arguments sent when a <see cref="CadenceClient"/> is closed
    /// with a property indicating whether or not the connection was closed due
    /// to an error.
    /// </summary>
    public class CadenceClientClosedArgs : EventArgs
    {
        /// <summary>
        /// This will be set if the connection was closed due to an error
        /// or <c>null</c> when the connection was closed normally.
        /// </summary>
        public Exception Exception { get; internal  set; }
    }
}
