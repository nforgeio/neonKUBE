//-----------------------------------------------------------------------------
// FILE:	    CadenceActivityExternalCompletionException.cs
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

using Newtonsoft.Json;

using Neon.Cadence;
using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// Thrown by activities that need to wait for an external event before the activity
    /// is considered to be complete.  These activities will call <see cref="Activity.CompleteExternallyAsync()"/> 
    /// within their <see cref="Activity.RunAsync(byte[])"/> methods which will throw this
    /// internal exception, exiting the run method.  This exception will be caught by
    /// the <see cref="Activity"/> base class and used to signal Cadence that the activity
    /// will be completed externally via a call to <see cref="CadenceClient.RespondActivityFailAsync(byte[], Exception)"/>.
    /// </summary>
    /// <remarks>
    /// <note>
    /// Activity entry points must allow this exception to be caught by the
    /// calling <see cref="CadenceClient"/> so that <see cref="Activity.CompleteExternallyAsync"/>
    /// will work properly.
    /// </note>
    /// </remarks>
    public class CadenceActivityExternalCompletionException : Exception
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public CadenceActivityExternalCompletionException()
            : base()
        {
        }
    }
}
