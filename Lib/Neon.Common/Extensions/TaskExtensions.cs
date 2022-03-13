//-----------------------------------------------------------------------------
// FILE:	    TaskExtensions.cs
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
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace System.Threading.Tasks
{
    /// <summary>
    /// <see cref="Task"/> extension methods.
    /// </summary>
    public static class TaskExtensions
    {
        /// <summary>
        /// Waits for the task to complete but rethrows original exceptions rather
        /// than a wrapper <see cref="ArgumentException"/>.  Otherwise, this is 
        /// a replacement for <see cref="Task.Wait()"/>.
        /// </summary>
        /// <param name="task">The task</param>
        public static void WaitWithoutAggregate(this Task task)
        {
            task.GetAwaiter().GetResult();
        }

        /// <summary>
        /// Waits for the task to complete and then returns the result but rethrows 
        /// original exceptions rather than a wrapper <see cref="ArgumentException"/>.
        /// Otherwise, this is a replacement for <see cref="Task{T}.Result()"/>.
        /// </summary>
        /// <typeparam name="TResult">The task result type.</typeparam>
        /// <param name="task">The task</param>
        public static TResult ResultWithoutAggregate<TResult>(this Task<TResult> task)
        {
            return task.GetAwaiter().GetResult();
        }
    }
}
