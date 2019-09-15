//-----------------------------------------------------------------------------
// FILE:	    ActivityStub.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;

namespace Neon.Cadence
{
    /// <inheritdoc/>
    public class ActivityStub : IActivityStub
    {
        /// <inheritdoc/>
        public Task ExecuteAsync(string activityTypeName, params object[] args)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<TResult> ExecuteAsync<TResult>(string activityTypeName, params object[] args)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<object> ExecuteAsync(Type activityType, params object[] args)
        {
            throw new NotImplementedException();
        }
    }
}
