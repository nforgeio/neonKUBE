//-----------------------------------------------------------------------------
// FILE:	    TemporalInternalException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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

using Neon.Common;
using Neon.Temporal;

namespace Neon.Temporal
{
    /// <summary>
    /// Base class for Temporal exceptions that must not be caught and handled
    /// by workflow entry point methods.  The Temporal client must be allowed
    /// to handle these.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If your workflow needs a general exception handler, you should include
    /// a <c>catch</c> clause that catches and rethrows any derived exceptions
    /// before your custom handler.  This will look something like:
    /// </para>
    /// <code language="c#">
    /// public class MyWorkflow
    /// {
    ///     public Task Entrypoint()
    ///     {
    ///         try
    ///         {
    ///             // Workflow implementation.
    ///         }
    ///         catch (TemporalInternalException)
    ///         {
    ///             // Rethrow so Temporal can handle these exceptions.        
    /// 
    ///             throw;
    ///         }
    ///         catch (Exception e)
    ///         {
    ///             // Your exception handler.
    ///         }
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public abstract class TemporalInternalException : Exception
    {
    }
}
