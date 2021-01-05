//-----------------------------------------------------------------------------
// FILE:        FactAttributes.cs
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Data;
using Neon.IO;
using Neon.Tasks;
using Neon.Temporal;
using Neon.Temporal.Internal;
using Neon.Xunit;
using Neon.Xunit.Temporal;

using Newtonsoft.Json;
using Xunit;

namespace TestTemporal
{
    // These are some temporary [Fact] attributes used to categorize failing 
    // unit tests while we work through fixing them.  [SlowFact] isn't really 
    // a good attribute for this because we use this to tag slow working tests
    // so we don't run them all of the time.
    //
    // You can re-enable tests with these attributes by setting [Skip = null]
    // in the constructors.

    /// <summary>
    /// Use this to disable tests failing due to problems with our Temporal 
    /// error handling implementation.
    /// </summary>
    public class Fact_Failing_Errors : FactAttribute
    {
        public Fact_Failing_Errors()
        {
            Skip = "Failing due to our error handling implementation.";
            //Skip = null;
        }
    }

    /// <summary>
    /// Use this to disable tests failing due to other JSON serialization implementations
    /// (or lack thereof).
    /// </summary>
    public class Fact_Failing_Json : FactAttribute
    {
        public Fact_Failing_Json()
        {
            Skip = "Failing due to JSON serialization.";
            //Skip = null;
        }
    }

    /// <summary>
    /// Use this to disable tests failing due to incomplete interop test implementation.
    /// </summary>
    public class Fact_Failing_Interop : FactAttribute
    {
        public Fact_Failing_Interop()
        {
            Skip = "Failing due to incomplete interop test implementation.";
            //Skip = null;
        }
    }

    /// <summary>
    /// Use this to disable tests failing due to workerId==0.
    /// </summary>
    public class Fact_BadWorkerId : FactAttribute
    {
        public Fact_BadWorkerId()
        {
            Skip = "Failing due to: workerId=0.";
            //Skip = null;
        }
    }
}
