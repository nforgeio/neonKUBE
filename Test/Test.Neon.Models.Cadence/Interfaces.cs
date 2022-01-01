//-----------------------------------------------------------------------------
// FILE:        Interfaces.cs
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
using System.Linq;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Common;

namespace Test.Neon.Models.Cadence
{
    /// <summary>
    /// Defines a workflow that performs various activities intended for
    /// testing Neon Cadence support via the <b>test-cadence</b> Docker
    /// image.
    /// </summary>
    public interface IBusyworkWorkflow : IWorkflow
    {
        /// <summary>
        /// This workflow loops the specified number of times specified, sleeping
        /// for the period specified for each iteration.
        /// </summary>
        /// <param name="iterations">Number of iterations.</param>
        /// <param name="sleepInterval">The sleep interval.</param>
        /// <param name="message">The message string to be returned.</param>
        /// <returns>The <paramref name="message"/> passed.</returns>
        [WorkflowMethod]
        Task<string> DoItAsync(int iterations, TimeSpan sleepInterval, string message);
    }
}
