//-----------------------------------------------------------------------------
// FILE:	    KubeConfig.cs
// CONTRIBUTOR: Jeff Lill
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using k8s.KubeConfigModels;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Cryptography;
using Neon.Kube;

namespace Neon.Kube.Config
{
    /// <summary>
    /// Used to manage serialization of Kubernetes <b>kubeconfig</b> files. 
    /// These are used to manage cluster contexts on client machines:
    /// <a href="https://github.com/eBay/Kubernetes/blob/master/docs/user-guide/kubeconfig-file.md">more information</a>.
    /// </summary>
    public class KubeConfig
    {
        //---------------------------------------------------------------------
        // Static members

        private static object syncRoot = new object();

        /// <summary>
        /// Reads and returns information loaded from the user's <b>~/.kube/config</b> file
        /// or the specified file.
        /// </summary>
        /// <param name="configPath">
        /// Optionally specifies the location of the kubeconfig file.  This defaults to the
        /// user's <b>~/.kube/config</b> file.
        /// </param>
        /// <returns>The parsed <see cref="KubeConfig"/> or an empty config if the file doesn't exist.</returns>
        /// <exception cref="NeonKubeException">Thrown when the current config is invalid.</exception>
        public static KubeConfig Load(string configPath = null)
        {
            configPath = configPath ?? KubeHelper.KubeConfigPath;

            if (File.Exists(configPath))
            {
                var config = NeonHelper.YamlDeserialize<KubeConfig>(KubeHelper.ParseTextFileWithRetry(configPath));

                config.path = configPath;

                config.Validate();

                return config;
            }
            else
            {
                return new KubeConfig()
                {
                    path = configPath
                };
            }
        }

        //---------------------------------------------------------------------
        // Instance members.

        private string path;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeConfig()
        {
        }

        /// <summary>
        /// Specifies cluster API server protocol version (defaults to <b>v1</b>).
        /// </summary>
        [JsonProperty(PropertyName = "apiVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "apiVersion", ApplyNamingConventions = false)]
        [DefaultValue("v1")]
        public string ApiVersion { get; set; } = "v1";

        /// <summary>
        /// Identifies the document type: <b>Config</b>.
        /// </summary>
        [JsonProperty(PropertyName = "kind", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "kind", ApplyNamingConventions = false)]
        [DefaultValue("Config")]
        public string Kind { get; set; } = "Config";

        /// <summary>
        /// Lists cluster configurations.
        /// </summary>
        [JsonProperty(PropertyName = "clusters", Required = Required.Always)]
        [YamlMember(Alias = "clusters", ApplyNamingConventions = false)]
        public List<KubeConfigCluster> Clusters { get; set; } = new List<KubeConfigCluster>();

        /// <summary>
        /// Lists config contexts.
        /// </summary>
        [JsonProperty(PropertyName = "contexts", Required = Required.Always)]
        [YamlMember(Alias = "contexts", ApplyNamingConventions = false)]
        public List<KubeConfigContext> Contexts { get; set; } = new List<KubeConfigContext>();

        /// <summary>
        /// Specifies the name of the current context or <c>null</c> when there is no current context.
        /// </summary>
        [JsonProperty(PropertyName = "current-context", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "current-context", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string CurrentContext { get; set; }

        /// <summary>
        /// Optional global kubeconfig preferences.
        /// </summary>
        [JsonProperty(PropertyName = "preferences", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "preferences", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public KubeConfigPreferences Preferences { get; set; } = null;

        /// <summary>
        /// Lists the user configurations.
        /// </summary>
        [JsonProperty(PropertyName = "users", Required = Required.Always)]
        [YamlMember(Alias = "users", ApplyNamingConventions = false)]
        public List<KubeConfigUser> Users { get; set; } = new List<KubeConfigUser>();

        /// <summary>
        /// Returns the current context or <c>null</c>.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public KubeConfigContext Context
        {
            get
            {
                if (string.IsNullOrEmpty(CurrentContext))
                {
                    return null;
                }
                else
                {
                    return GetContext(CurrentContext);
                }
            }
        }

        /// <summary>
        /// Returns the current cluster or <c>null</c>.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public KubeConfigCluster Cluster
        {
            get
            {
                if (Context == null)
                {
                    return null;
                }

                return GetCluster(Context.Cluster);
            }
        }

        /// <summary>
        /// Returns the current user or <c>null</c>.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public KubeConfigUser User
        {
            get
            {
                if (Context == null)
                {
                    return null;
                }

                return GetUser(Context.User);
            }
        }

        /// <summary>
        /// Returns a Kubernetes context by name.
        /// </summary>
        /// <param name="name">The cluster name.</param>
        /// <returns>The <see cref="KubeConfigCluster"/> or <c>null</c>.</returns>
        public KubeConfigCluster GetCluster(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            return Clusters.SingleOrDefault(context => context.Name == name);
        }

        /// <summary>
        /// Returns a Kubernetes user by name.
        /// </summary>
        /// <param name="name">The user name.</param>
        /// <returns>The <see cref="KubeConfigUser"/> or <c>null</c>.</returns>
        public KubeConfigUser GetUser(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            return Users.SingleOrDefault(context => context.Name == name);
        }

        /// <summary>
        /// Returns a Kubernetes context using a raw context name.
        /// </summary>
        /// <param name="rawName">The raw context name.</param>
        /// <returns>The <see cref="KubeConfigContext"/> or <c>null</c>.</returns>
        public KubeConfigContext GetContext(string rawName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(rawName), nameof(rawName));

            return Contexts.SingleOrDefault(context => context.Name == rawName);
        }

        /// <summary>
        /// Returns a Kubernetes context by name.
        /// </summary>
        /// <param name="name">The raw context name.</param>
        /// <returns>The <see cref="KubeConfigContext"/> or <c>null</c>.</returns>
        public KubeConfigContext GetContext(KubeContextName name)
        {
            Covenant.Requires<ArgumentNullException>(name != null, nameof(name));

            var rawName = name.ToString();

            return Contexts.SingleOrDefault(context => context.Name == rawName);
        }

        /// <summary>
        /// Removes a Kubernetes context  by name, if it exists.
        /// </summary>
        /// <param name="name">The context name.</param>
        /// <param name="noSave">Optionally prevent context save after the change.</param>
        public void RemoveContext(KubeContextName name, bool noSave = false)
        {
            var context = GetContext(name);

            if (context != null)
            {
                RemoveContext(context, noSave: noSave);
            }

            if (!noSave)
            {
                // Also the setup state file for this cluster if it exists.  This may be
                // present if cluster prepare/setup was interrupted for the cluster.

                KubeSetupState.Delete(name.ToString());
            }
        }

        /// <summary>
        /// Obtains the current context, cluster, and user in one go.
        /// </summary>
        /// <param name="context">Returns as the current context or <c>null</c>.</param>
        /// <param name="cluster">Returns as the current cluster or <c>null</c>.</param>
        /// <param name="user">Returns as the current user or <c>null</c>.</param>
        /// <exception cref="InvalidDataException">
        /// Thrown when one or both of the referenced current context, cluster or
        /// user doesn't exist.
        /// </exception>
        /// <remarks>
        /// <note>
        /// This method returns <c>null</c> for all values when there is no current context
        /// and when there is a current context, it ensures that the referenced context, cluster
        /// and user actually exists, throwing an <see cref="InvalidDataException"/> when any
        /// are missing.
        /// </note>
        /// </remarks>
        public void GetCurrent(out KubeConfigContext context, out KubeConfigCluster cluster, out KubeConfigUser user)
        {
            context = null;
            cluster = null;
            user    = null;

            if (CurrentContext == null)
            {
                return;
            }

            context = GetContext(CurrentContext);

            if (context == null)
            {
                throw new InvalidDataException($"KubeConfig [context={CurrentContext}] does not exist.");
            }

            cluster = GetCluster(context.Cluster);

            if (cluster == null)
            {
                throw new InvalidDataException($"KubeConfig [cluster={context.Cluster}] does not exist.");
            }

            user = GetUser(context.User);

            if (user == null)
            {
                throw new InvalidDataException($"KubeConfig [user={context.User}] does not exist.");
            }
        }

        /// <summary>
        /// Removes a Kubernetes context, if it exists.
        /// </summary>
        /// <param name="context">The context to be removed.</param>
        /// <param name="removeClusterAndUser">
        /// Optionally disable removal of the referenced cluster and user if
        /// they're not referenced elsewhere (defaults to <c>true</c>).
        /// </param>
        /// <param name="noSave">Optionally prevent context save after the change.</param>
        public void RemoveContext(KubeConfigContext context, bool removeClusterAndUser = true, bool noSave = false)
        {
            Covenant.Requires<ArgumentNullException>(context != null, nameof(context));

            for (int i = 0; i < Contexts.Count; i++)
            {
                if (Contexts[i].Name == context.Name)
                {
                    Contexts.RemoveAt(i);
                    break;
                }
            }

            // Clear the current context if the removed context was the current one.

            if (CurrentContext == context.Name)
            {
                CurrentContext = null;
            }

            // Remove the referenced cluster and user if enabled and when they're
            // not referenced by another context.

            if (removeClusterAndUser)
            {
                if (!Contexts.Any(ctx => ctx.Cluster == context.Cluster))
                {
                    for (int i = 0; i < Clusters.Count; i++)
                    {
                        if (Clusters[i].Name == context.Cluster)
                        {
                            Clusters.RemoveAt(i);
                            break;
                        }
                    }
                }

                if (!Contexts.Any(ctx => ctx.User == context.User))
                {
                    for (int i = 0; i < Users.Count; i++)
                    {
                        if (Users[i].Name == context.User)
                        {
                            Users.RemoveAt(i);
                            break;
                        }
                    }
                }
            }

            // Persist as required.

            if (!noSave)
            {
                Save();
            }
        }

        /// <summary>
        /// Validates the kubeconfig.
        /// </summary>
        /// <param name="needsCurrentCluster">
        /// Optionally specifies that the config must have a current context identifying the
        /// cluster and user.
        /// </param>
        /// <exception cref="NeonKubeException">Thrown when the current config is invalid.</exception>
        public void Validate(bool needsCurrentCluster = false)
        {
            if (Kind != "Config")
            {
                throw new NeonKubeException($"Invalid [{nameof(Kind)}={Kind}].");
            }

            if (needsCurrentCluster)
            {
                if (Context == null)
                {
                    throw new NeonKubeException($"Kubeconfig does not specify a current context.");
                }

                if (Cluster == null)
                {
                    throw new NeonKubeException($"Kubeconfig context [{Context.Name}] references the [{Context.Cluster}] cluster which cannot be found.");
                }

                if (User == null)
                {
                    throw new NeonKubeException($"Kubeconfig context [{Context.Name}] references the [{Context.User}] user which cannot be found.");
                }
            }
        }

        /// <summary>
        /// Sets the current context.
        /// </summary>
        /// <param name="contextName">The name of the current context or <c>null</c> to deselect the context.</param>
        /// <exception cref="NeonKubeException">Thrown if the context does not exist.</exception>
        public void SetContext(string contextName = null)
        {
            if (string.IsNullOrEmpty(contextName))
            {
                CurrentContext = null;
            }
            else
            {
                if (GetContext(contextName) == null)
                {
                    throw new NeonKubeException($"Context [{contextName}] does not exist.");
                }

                CurrentContext = contextName;
            }

            Save();
        }

        /// <summary>
        /// Reloads the kubeconfig from the global config file.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the config is not backed by a file.</exception>
        public void Reload()
        {
            lock (syncRoot)
            {
                if (path == null)
                {
                    throw new InvalidOperationException($"Cannot reload [{nameof(KubeConfig)}] without a file path.");
                }

                // We're going to read into a temporary instance and then update
                // this instance's properties from the loaded instance.

                var config = Load(path);

                this.Preferences    = config.Preferences;
                this.CurrentContext = config.CurrentContext;
                this.Users          = config.Users;
                this.Contexts       = config.Contexts;
                this.Clusters       = config.Clusters;
            }
        }

        /// <summary>
        /// Persists the KubeConfig to the user's <b>~/.kube/config</b> file
        /// or the specified file. 
        /// </summary>
        /// <param name="configPath">
        /// Optionally specifies the location of the kubeconfig file.  This defaults to the
        /// path the config was loaded from.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the config was not loaded from a file and <paramref name="configPath"/>
        /// is not specified.
        /// </exception>
        public void Save(string configPath = null)
        {
            lock (syncRoot)
            {
                // Persist the KubeConfig.

                path = configPath ?? path;

                if (path == null)
                {
                    throw new InvalidOperationException($"Cannot save [{nameof(KubeConfig)}] without a file path.");
                }

                File.WriteAllText(path, NeonHelper.YamlSerialize(this));
            }
        }

        /// <summary>
        /// Constructs a deep clone of the instance.
        /// </summary>
        /// <param name="currentOnly">Optionally strips out the non-current contexts, clusters, and users.</param>
        /// <returns>The deep cloned <see cref="KubeConfig"/>.</returns>
        public KubeConfig Clone(bool currentOnly = false)
        {
            var clone = NeonHelper.JsonClone(this);

            if (currentOnly && clone.CurrentContext != null)
            {
                var delContexts = clone.Contexts.Where(context => context.Name != clone.CurrentContext).ToArray();
                var delClusters = clone.Clusters.Where(cluster => cluster.Name != clone.Context.Cluster).ToArray();
                var delUsers    = clone.Users.Where(user => user.Name != clone.Context.User).ToArray();

                foreach (var context in delContexts)
                {
                    clone.Contexts.Remove(context);
                }

                foreach (var cluster in delClusters)
                {
                    clone.Clusters.Remove(cluster);
                }

                foreach (var user in delUsers)
                {
                    clone.Users.Remove(user);
                }
            }

            return clone;
        }

        /// <summary>
        /// <para>
        /// Searches the config for the named context.  If it's present, the method will clone
        /// the config, make the named context as current and then remove all other contexts and
        /// users.  <c>null</c> will be returned if the named context doesn't exist.
        /// </para>
        /// <para>
        /// This is handy when you need to operate on a cluster that's not the current one.
        /// </para>
        /// </summary>
        /// <param name="contextName"></param>
        /// <returns>
        /// The new <see cref="KubeConfig"/> with the specified context set or <c>null</c>
        /// when the desired context does exist.
        /// </returns>
        public KubeConfig CloneAndSetContext(string contextName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(contextName), nameof(contextName));

            if (GetContext(contextName) == null)
            {
                return null;
            }

            var clone = Clone();

            clone.SetContext(contextName);

            return clone.Clone(currentOnly: true);
        }
    }
}
