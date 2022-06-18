//-----------------------------------------------------------------------------
// FILE:	    IExtendedController.cs
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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube.Resources;
using Neon.Tasks;

using KubeOps.Operator;
using KubeOps.Operator.Builder;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Entities;

using k8s;
using Prometheus;
using k8s.Models;
using k8s.Autorest;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Optionally implemented by <see cref="ResourceManager{TEntity, TController}"/> controllers
    /// to provide extended functionality.
    /// </summary>
    /// <typeparam name="TEntity">Specifies the custom Kubernetes entity type.</typeparam>
    public interface IExtendedController<TEntity>
        where TEntity : CustomKubernetesEntity, new()
    {
        /// <summary>
        /// <para>
        /// Optionally creates a valid resource that will be persisted by the <see cref="ResourceManager{TEntity, TController}"/> 
        /// to ensure that at least one resource exists at all times.  This works around: https://github.com/nforgeio/neonKUBE/issues/1599.
        /// This resource will be named <see cref="KubeHelper.IgnorableResourceName"/> and will be ignored by the resource manager
        /// and its controllers.
        /// </para>
        /// <para>
        /// Controllers can return <c>null</c> here to prevent <see cref="ResourceManager{TEntity, TController}"/>
        /// from creating an ignorable resource.
        /// </para>
        /// <note>
        /// The controller needs to ensure that the resource returned as a valid specification property,
        /// including all required values.
        /// </note>
        /// </summary>
        /// <returns>The ignorable entity or <c>null</c>.</returns>
        TEntity CreateIgnorable();
    }
}
