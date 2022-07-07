//------------------------------------------------------------------------------
// FILE:         BusyworkWorkflow.cs
// CONTRIBUTOR:  Marcus Bowyer
// COPYRIGHT:    Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Common;
using Neon.Diagnostics;
using Neon.Service;

using Test.Neon.Models.Cadence;

namespace CadenceService
{
    [Workflow(AutoRegister = true)]
    public class BusyworkWorkflow : WorkflowBase, IBusyworkWorkflow
    {
        /// <inheritdoc/>
        public async Task<string> DoItAsync(int iterations, TimeSpan sleepInterval, string message)
        {
            for (int i = 0; i < iterations; i++)
            {
                await Workflow.SleepAsync(sleepInterval);
            }

            return message;
        }
    }
}
