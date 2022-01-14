//-----------------------------------------------------------------------------
// FILE:	    ResourceManager.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;

using KubeOps.Operator;
using KubeOps.Operator.Builder;
using KubeOps.Operator.Entities;
using System.Collections.Generic;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Used by custom operators to manage a collection of custom resources.
    /// </summary>
    /// <typeparam name="TCustomResource">The custom Kubernetes entity type.</typeparam>
    /// <remarks>
    /// <para>
    /// The KubeOps operator SDK periodically raises <b>reconciled</b> event, even when
    /// nothing has changed on the API server.  I believe this is due to Kubernetes
    /// watches expiring and being restarted combined with the KubeOps SDK not doing
    /// any caching to detect when nothing has changed to avoid raising <b>reconciled</b>
    /// and <b>deleted</b> events.  This may be the standard operator SDK behavior.
    /// </para>
    /// <para>
    /// This class makes it easy to manage the current set of custom resources.  Simply call
    /// <see cref="Reconciled(TCustomResource, Action{IEnumerable{TCustomResource}})"/>, 
    /// <see cref="Deleted(TCustomResource, Action{IEnumerable{TCustomResource}})"/>, and
    /// <see cref="StatusModified(TCustomResource, Action{IEnumerable{TCustomResource}})"/> 
    /// when your operator receives related events from the operator.  These methods return 
    /// <c>true</c> when the collection actually changed and <c>false</c> otherwise.
    /// Your operator can use this to decide when actions are actually required.
    /// </para>
    /// <note>
    /// This class is thread-safe and can be called directly within your operator controllers.
    /// You may also explicitly lock the instance to protect multiple operations. 
    /// </note>
    /// </remarks>
    /// <threadsafety instance="true"/>
    public class ResourceManager<TCustomResource>
        where TCustomResource : CustomKubernetesEntity
    {
        private Dictionary<string, TCustomResource> resources = new Dictionary<string, TCustomResource>(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ResourceManager()
        {
        }

        /// <summary>
        /// <para>
        /// Call this when your controller receives a <b>reconciled</b> event, passing the
        /// resource.  This method adds the resource to the collection if it  doesn't already 
        /// exist.  Operators can use a <c>true</c> result to detect when the collection has 
        /// changed and take appropriate actions.
        /// </para>
        /// <para>
        /// You may also pass a custom handler to will be called within the lock when the collection
        /// has changed, passing the current set of resources.  This is a convienent way to perform 
        /// actions that require the entire set of resources.
        /// </para>
        /// </summary>
        /// <param name="resource">The custom resource received.</param>
        /// <param name="handler">Optional handler to be called when the resource didn't already 
        /// exist, after the resource was added.
        /// </param>
        /// <returns>
        /// <c>true</c> when the resource wasn't already in the collection and was added or if
        /// the resource has been changed.  Operators can use a <c>true</c> result to determine
        /// when actions need to be taken.
        /// </returns>
        public bool Reconciled(TCustomResource resource, Action<IEnumerable<TCustomResource>> handler = null)
        {
            Covenant.Requires<ArgumentNullException>(resource != null, nameof(resource));

            lock (this)
            {
                if (TryGetResource(resource.Metadata.Name, out var existing))
                {
                    // Return TRUE if the existing resource is different from the new one.
                    // We're going to use the metadata generation field to detect changes.

                    return resource.Metadata.Generation != existing.Metadata.Generation;
                }

                resources[resource.Metadata.Name] = resource;

                handler?.Invoke(resources.Values);
            }

            return true;
        }

        /// <summary>
        /// <para>
        /// Call this when your controller receives a <b>deleted</b> event, passing the resource.
        /// This method removes the resource from the collection if it  exists.  Operators can a 
        /// <c>true</c> result to detect when the collection has changed and take appropriate actions.
        /// </para>
        /// <para>
        /// You may also pass a custom handler to will be called within the lock when the collection
        /// has changed, passing the current set of resources.  This is a convienent way to perform 
        /// actions that require the entire set of resources.
        /// </para>
        /// </summary>
        /// <param name="resource">The custom resource received.</param>
        /// <param name="handler">
        /// Optional handler to be called when the resource existed, after it was deleted.
        /// </param>
        /// <returns>
        /// <c>true</c> when the resource exists in the collection and was deleted, 
        /// indicating that the collection has changed.
        /// </returns>
        /// <exception cref="KeyNotFoundException">Thrown if the named resource is not currently present.</exception>
        public bool Deleted(TCustomResource resource, Action<IEnumerable<TCustomResource>> handler = null)
        {
            Covenant.Requires<ArgumentNullException>(resource != null, nameof(resource));

            lock (this)
            {
                if (!Contains(resource.Metadata.Name))
                {
                    return false;
                }

                resources.Remove(resource.Metadata.Name);
                handler?.Invoke(resources.Values);
            }

            return true;
        }

        /// <summary>
        /// <para>
        /// Call this when a <b>status-modified</b> event was received, passing the resource.
        /// This method will replace the existing resource with the same with the resource
        /// passed.
        /// </para>
        /// <para>
        /// You may also pass a custom handler to will be called after the collection of resources
        /// is updated, passing the current set of resources.  This is a convienent way to perform 
        /// actions that require the entire set of resources.
        /// </para>
        /// </summary>
        /// <param name="resource">The custom resource received.</param>
        /// <param name="handler">Optional handler to be called within the lock when the collection of resourcess changes.</param>
        /// <returns><c>true</c> when the resource exists.</returns>
        public bool StatusModified(TCustomResource resource, Action<IEnumerable<TCustomResource>> handler = null)
        {
            Covenant.Requires<ArgumentNullException>(resource != null, nameof(resource));

            lock (this)
            {
                if (!Contains(resource.Metadata.Name))
                {
                    return false;
                }

                resources[resource.Metadata.Name] = resource;

                handler?.Invoke(resources.Values);
            }

            return true;
        }

        /// <summary>
        /// Determines whether a custom resource with the specific name exists.
        /// </summary>
        /// <param name="name">The resource name.</param>
        /// <returns><c>true</c> when the name exists.</returns>
        public bool Contains(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            lock (this)
            {
                return resources.ContainsKey(name);
            }
        }

        /// <summary>
        /// Attempts to retrieve a custom resource by name.
        /// </summary>
        /// <param name="name">The resource name.</param>
        /// <param name="resource">Returns as the resource when found.</param>
        /// <returns><b>true</b> when the resource exists and was returned.</returns>
        public bool TryGetResource(string name, out TCustomResource resource)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            lock (this)
            {
                return resources.TryGetValue(name, out resource);
            }
        }

        /// <summary>
        /// Returns the current set of resources being managed.  This may be use for
        /// complex that cannot be performed via a handler callback.
        /// </summary>
        /// <returns>A copy of the current set of managed resources.</returns>
        /// <remarks>
        /// <para>
        /// You'll need to take care to ensure that any new events raised don't result in
        /// unexpected resource changes to the handled while you're working with the collection
        /// returned.  Consider locking the <see cref="ResourceManager{TCustomResource}"/> instance
        /// while you're processing the collection returned.
        /// </para>
        /// <note>
        /// We recommend that you avoid call this and use handler callbacks whenerver possible
        /// to keep things safe and simple.
        /// </note>
        /// </remarks>
        public IEnumerable<TCustomResource> GetResources()
        {
            lock (this)
            {
                return new List<TCustomResource>(resources.Values);
            }
        }
    }
}
