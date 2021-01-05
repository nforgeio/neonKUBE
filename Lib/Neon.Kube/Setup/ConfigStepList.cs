//-----------------------------------------------------------------------------
// FILE:	    ConfigStepList.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Kube
{
    /// <summary>
    /// Implements a list of <see cref="ConfigStep"/>s to be performed
    /// on a cluster.
    /// </summary>
    public class ConfigStepList : List<ConfigStep>
    {
        /// <summary>
        /// Adds a set of configuration steps to the list.
        /// </summary>
        /// <param name="steps">The steps.</param>
        public void Add(IEnumerable<ConfigStep> steps)
        {
            if (steps == null)
            {
                return;
            }

            foreach (var step in steps)
            {
                base.Add(step);
            }
        }
    }
}
