//-----------------------------------------------------------------------------
// FILE:	    SingletonControllerFactory.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Reflection;
using System.Threading;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;

namespace Neon.Web
{
    /// <summary>
    /// Implements an ASP.NET controller factory that always returns a 
    /// controller with type <typeparamref name="TController"/>.
    /// </summary>
    /// <typeparam name="TController">The controller type.</typeparam>
    internal class SingletonControllerFactory<TController> : IControllerFactory
        where TController : ControllerBase
    {
        private DefaultControllerFactory defaultFactory;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="controllerActivator">The controller activator.</param>
        /// <param name="propertyActivators">The property activators.</param>
        public SingletonControllerFactory(IControllerActivator controllerActivator, IEnumerable<IControllerPropertyActivator> propertyActivators)
        {
            this.defaultFactory = new DefaultControllerFactory(controllerActivator, propertyActivators);
        }

        /// <inheritdoc/>
        public object CreateController(ControllerContext context)
        {
            context.ActionDescriptor.ControllerTypeInfo = typeof(TController).GetTypeInfo();

            var controller = defaultFactory.CreateController(context);

            return controller;
        }

        /// <inheritdoc/>
        public void ReleaseController(ControllerContext context, object controller)
        {
            defaultFactory.ReleaseController(context, controller);
        }
    }
}
