//-----------------------------------------------------------------------------
// FILE:	    WaitForSignalReplyException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

using Neon.Cadence.Internal;

namespace Neon.Cadence
{
    /// <summary>
    /// <para>
    /// <b>EXPERIMENTAL:</b> Thrown by workflow synchronous signal methods when the
    /// signal has been marshalled to the workflow method via a <see cref="WorkflowQueue{T}"/>
    /// and the workflow method will handle the signal reply.
    /// </para>
    /// <note>
    /// Synchronous signals are an experimental feature that will likely be replaced
    /// in the coming months by a new Cadence feature.
    /// </note>
    /// </summary>
    public class WaitForSignalReplyException : Exception
    {
    }
}
