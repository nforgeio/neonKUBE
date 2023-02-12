// FILE:	    ITestOperator.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Neon.Kube.Operator;
using Neon.Kube.Operator.Controller;
using Neon.Kube.Operator.Finalizer;
using Neon.Kube.Operator.Webhook;

namespace Neon.Kube.Xunit.Operator
{
    /// <summary>
    /// The operator used for testing.
    /// </summary>
    public interface ITestOperator
    {
        /// <summary>
        /// Starts the operator.
        /// </summary>
        /// <returns></returns>
        Task StartAsync();

        /// <summary>
        /// Starts the operator.
        /// </summary>
        /// <returns></returns>
        void Start();

        /// <summary>
        /// Adds an <see cref="IResourceController{TEntity}"/> to the test operator.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        IOperatorBuilder AddController<T>()
            where T : class;

        /// <summary>
        /// Adds an <see cref="IResourceFinalizer{TEntity}"/> to the test operator.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        IOperatorBuilder AddFinalizer<T>()
            where T : class;

        /// <summary>
        /// Adds an <see cref="IMutatingWebhook{TEntity}"/> to the test operator.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        IOperatorBuilder AddMutatingWebhook<T>()
            where T : class;

        /// <summary>
        /// Adds an <see cref="IValidatingWebhook{TEntity}"/> to the test operator.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        IOperatorBuilder AddValidatingWebhook<T>()
            where T : class;

        /// <summary>
        /// Adds an ngrok tunnel to the test operator.
        /// </summary>
        /// <returns></returns>
        IOperatorBuilder AddNgrokTunnnel();

        /// <summary>
        /// Gets a <see cref="IResourceController{TEntity}"/> from the operator.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        T GetController<T>();

        /// <summary>
        /// Gets a <see cref="IResourceFinalizer{TEntity}"/> from the operator.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        T GetFinalizer<T>();

        /// <summary>
        /// Gets a <see cref="IMutatingWebhook{TEntity}"/> from the operator.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        T GetMutatingWebhook<T>();

        /// <summary>
        /// Gets a <see cref="IValidatingWebhook{TEntity}"/> from the operator.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        T GetValidatingWebhook<T>();
    }
}
