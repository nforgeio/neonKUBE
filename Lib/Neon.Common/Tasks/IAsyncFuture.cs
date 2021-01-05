//-----------------------------------------------------------------------------
// FILE:        IAsyncFuture.cs
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
//
// Code based on a MSDN article by Stephen Toub (MSFT):
// http://blogs.msdn.com/b/pfxteam/archive/2012/02/12/10267069.aspx

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Neon.Tasks
{
    /// <summary>
    /// Defines an interface that completes a future operation asynchronously.
    /// </summary>
    public interface IAsyncFuture
    {
        /// <summary>
        /// Returns when the asynchronous operation has completed.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        Task GetAsync();
    }

    /// <summary>
    /// Defines an interface that returns the value from the asynchronous operation.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    public interface IAsyncFuture<T>
    {
        /// <summary>
        /// Returns the value from the operation.
        /// </summary>
        /// <returns></returns>
        Task<T> GetAsync();
    }
}
