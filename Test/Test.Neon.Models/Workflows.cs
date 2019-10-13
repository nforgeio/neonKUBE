//-----------------------------------------------------------------------------
// FILE:        Workflows.cs
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
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Neon.Cadence;

using Newtonsoft.Json;

namespace Test.Neon.Workflows
{
    // NOTE: These are test workflow definitions implemented by the [test-cadence] service.

    [WorkflowInterface]
    public interface IHelloWorkflow : IWorkflow
    {
        /// <summary>
        /// Returns a string like <b>"Hello NAME!"</b> where <i>NAME</i>
        /// is the parameter passed.
        /// </summary>
        /// <param name="name">The input name.</param>
        /// <returns>The hello string.</returns>
        Task<string> HelloAsync(string name);
    }
}
