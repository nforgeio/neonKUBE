//-----------------------------------------------------------------------------
// FILE:	    ILockProvider.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using k8s;
using k8s.Models;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Provides resource locks on Custom Resources.
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    public interface ILockProvider<TEntity>
        where TEntity : IKubernetesObject<V1ObjectMeta>
    {
        /// <summary>
        /// Waits for a lock on a specific resource.
        /// </summary>
        /// <param name="entityId"></param>
        /// <returns></returns>
        Task WaitAsync(string entityId);


        /// <summary>
        /// Releases a lock on a specific resource.
        /// </summary>
        /// <param name="entityId"></param>
        void Release(string entityId);
    }
}
