//-----------------------------------------------------------------------------
// FILE:	    KubeConfig.cs
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Cryptography;

namespace Neon.Kube
{
    /// <summary>
    /// Used to manage serialization of Kubernetes <b>kubeconfig</b> files. 
    /// These are used to manage cluster contexts on client machines:
    /// <a href="https://github.com/eBay/Kubernetes/blob/master/docs/user-guide/kubeconfig-file.md">more information</a>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// neonKUBE client side tools like <b>neon-cli</b> and <b>neonDESKTOP</b> maintain 
    /// cluster login information within 
    /// </para>
    /// </remarks>
    public class KubeConfig
    {
        //---------------------------------------------------------------------
        // Static members

        private static object syncRoot = new object();

        /// <summary>
        /// Reads and returns information loaded from the current <b>~/.kube/config</b> file.
        /// </summary>
        /// <returns>The parsed <see cref="KubeConfig"/> or an empty config if the file doesn't exist.</returns>
        /// <exception cref="NeonKubeException">Thrown when the current config is invalid.</exception>
        public static KubeConfig Load()
        {
            var configPath = KubeHelper.KubeConfigPath;

            if (File.Exists(configPath))
            {
                var config = NeonHelper.YamlDeserialize<KubeConfig>(KubeHelper.ReadFileTextWithRetry(configPath));

                config.Validate();

                // Load any related neonKUBE cluster logins.

                foreach (var context in config.Contexts)
                {
                    var extensionPath = Path.Combine(KubeHelper.LoginsFolder, $"{context.Name}.login.yaml");

                    if (File.Exists(extensionPath))
                    {
                        context.Extension = NeonHelper.YamlDeserialize<ClusterLogin>(KubeHelper.ReadFileTextWithRetry(extensionPath));
                    }
                    else
                    {
                        context.Extension = new ClusterLogin();
                    }
                }
                
                return config;
            }
            else
            {
                return new KubeConfig();
            }
        }

        //---------------------------------------------------------------------
        // Instance members.

        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeConfig()
        {
        }

        /// <summary>
        /// The cluster API server protocol version (defaults to <b>v1</b>).
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
        /// The list of cluster configurations.
        /// </summary>
        [JsonProperty(PropertyName = "clusters", Required = Required.Always)]
        [YamlMember(Alias = "clusters", ApplyNamingConventions = false)]
        public List<KubeConfigCluster> Clusters { get; set; } = new List<KubeConfigCluster>();

        /// <summary>
        /// The list of config contexts.
        /// </summary>
        [JsonProperty(PropertyName = "contexts", Required = Required.Always)]
        [YamlMember(Alias = "contexts", ApplyNamingConventions = false)]
        public List<KubeConfigContext> Contexts { get; set; } = new List<KubeConfigContext>();

        /// <summary>
        /// The name of the current context or <c>null</c> when there is no current context.
        /// </summary>
        [JsonProperty(PropertyName = "current-context", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "current-context", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string CurrentContext { get; set; }

        /// <summary>
        /// The optional dictionary of preferences.
        /// </summary>
        [JsonProperty(PropertyName = "preferences", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "preferences", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public Dictionary<string, string> Preferences { get; set; } = null;

        /// <summary>
        /// The list of user configurations.
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
        /// Returns the named neonKUBE related cluster.
        /// </summary>
        /// <param name="name">The cluster name.</param>
        /// <returns>The <see cref="KubeConfigCluster"/> or <c>null</c>.</returns>
        public KubeConfigCluster GetCluster(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            return Clusters.SingleOrDefault(context => context.Name == name);
        }

        /// <summary>
        /// Returns the named user.
        /// </summary>
        /// <param name="name">The user name.</param>
        /// <returns>The <see cref="KubeConfigUser"/> or <c>null</c>.</returns>
        public KubeConfigUser GetUser(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            return Users.SingleOrDefault(context => context.Name == name);
        }

        /// <summary>
        /// Returns the named neonKUBE related context (using a raw context name).
        /// </summary>
        /// <param name="rawName">The raw context name.</param>
        /// <returns>The <see cref="KubeConfigContext"/> or <c>null</c>.</returns>
        public KubeConfigContext GetContext(string rawName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(rawName), nameof(rawName));

            return Contexts.SingleOrDefault(context => context.Name == rawName && context.IsNeonKube);
        }

        /// <summary>
        /// Returns the named neonKUBE related context (using a structured context name).
        /// </summary>
        /// <param name="name">The raw context name.</param>
        /// <returns>The <see cref="KubeConfigContext"/> or <c>null</c>.</returns>
        public KubeConfigContext GetContext(KubeContextName name)
        {
            Covenant.Requires<ArgumentNullException>(name != null, nameof(name));

            var rawName = name.ToString();

            return Contexts.SingleOrDefault(context => context.Name == rawName && context.IsNeonKube);
        }

        /// <summary>
        /// Adds or updates a kubecontext.
        /// </summary>
        /// <param name="context">The new context.</param>
        /// <param name="cluster">The context cluster information.</param>
        /// <param name="user">The context user information.</param>
        /// <param name="noSave">Optionally prevent context save after the change.</param>
        public void SetContext(KubeConfigContext context, KubeConfigCluster cluster, KubeConfigUser user, bool noSave = false)
        {
            Covenant.Requires<ArgumentNullException>(context != null, nameof(context));
            Covenant.Requires<ArgumentNullException>(cluster != null, nameof(cluster));
            Covenant.Requires<ArgumentNullException>(user != null, nameof(user));
            Covenant.Requires<ArgumentNullException>(context.Properties.Cluster == cluster.Name, nameof(context));
            Covenant.Requires<ArgumentNullException>(context.Properties.User == user.Name, nameof(context));

            var updated = false;

            for (int i = 0; i < Contexts.Count; i++)
            {
                if (Contexts[i].Name == context.Name)
                {
                    Contexts[i] = context;
                    updated     = true;
                    break;
                }
            }

            if (!updated)
            {
                Contexts.Add(context);
            }

            // We also need to add or update the referenced cluster and user properties.

            updated = false;

            for (int i = 0; i < Clusters.Count; i++)
            {
                if (Clusters[i].Name == context.Properties.Cluster)
                {
                    Clusters[i] = cluster;
                    updated     = true;
                    break;
                }
            }

            if (!updated)
            {
                Clusters.Add(cluster);
            }

            updated = false;

            for (int i = 0; i < Users.Count; i++)
            {
                if (Users[i].Name == context.Properties.User)
                {
                    Users[i] = user;
                    updated  = true;
                    break;
                }
            }

            if (!updated)
            {
                Users.Add(user);
            }

            // Persist as required.

            if (!noSave)
            {
                Save();
            }
        }

        /// <summary>
        /// Removes a neonKUBE related kubecontext if it exists.
        /// </summary>
        /// <param name="name">The context name.</param>
        /// <param name="noSave">Optionally prevent context save after the change.</param>
        public void RemoveContext(KubeContextName name, bool noSave = false)
        {
            var context = GetContext(name);

            if (context != null)
            {
                RemoveContext(context);
            }
            else
            {
                NeonHelper.DeleteFile(KubeHelper.GetClusterLoginPath(name));
            }
        }

        /// <summary>
        /// Removes a neonKUBE related kubecontext if it exists.
        /// </summary>
        /// <param name="context">The context to be removed.</param>
        /// <param name="noSave">Optionally prevent context save after the change.</param>
        public void RemoveContext(KubeConfigContext context, bool noSave = false)
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

            // Remove the referenced cluster and user if they're not
            // referenced by another context (to prevent orphans).

            for (int i = 0; i < Clusters.Count; i++)
            {
                if (Clusters[i].Name == context.Properties.Cluster)
                {
                    Clusters.RemoveAt(i);
                    break;
                }
            }

            for (int i = 0; i < Users.Count; i++)
            {
                if (Users[i].Name == context.Properties.User)
                {
                    Users.RemoveAt(i);
                    break;
                }
            }

            // Clear the current context if the removed context was the current one.

            if (CurrentContext == context.Name)
            {
                CurrentContext = null;
            }

            // Persist as required.

            if (!noSave)
            {
                Save();

                // We need to remove the extension file too (if one exists).

                var extensionPath = Path.Combine(KubeHelper.LoginsFolder, $"{context.Name}.login.yaml");

                try
                {
                    File.Delete(extensionPath);
                }
                catch (IOException)
                {
                    // Intentially ignoring this.
                }
            }
        }

        /// <summary>
        /// Validates the configuration and also prunes any non-neonKUBE contexts.
        /// </summary>
        /// <exception cref="NeonKubeException">Thrown when the current config is invalid.</exception>
        public void Validate()
        {
            if (Kind != "Config")
            {
                throw new NeonKubeException($"Invalid [{nameof(Kind)}={Kind}].");
            }

            // Ensure that the current context exists.

            if (!string.IsNullOrEmpty(CurrentContext) && GetContext(CurrentContext) == null)
            {
                CurrentContext = null;
            }

            // Prune any non-neonKUBE or invalid contexts.

            var prunedConfigs = new List<KubeConfigContext>();
            var clusterRefs    = new HashSet<string>();

            foreach (var context in Contexts)
            {
                if (!context.IsNeonKube)
                {
                    prunedConfigs.Add(context);
                    continue;
                }

                if (!string.IsNullOrEmpty(context.Properties.Cluster) && GetCluster(context.Properties.Cluster) == null)
                {
                    prunedConfigs.Add(context);
                    continue;
                }

                clusterRefs.Add(context.Properties.Cluster);
            }

            foreach (var context in prunedConfigs)
            {
                Contexts.Remove(context);
            }

            // Prune any non-neonKUBE clusters that are not referenced by a 
            // neonKUBE context.

            var prunedClusters = new List<KubeConfigCluster>();

            foreach (var cluster in Clusters)
            {
                if (!clusterRefs.Contains(cluster.Name))
                {
                    prunedClusters.Add(cluster);
                }
            }

            foreach (var cluster in prunedClusters)
            {
                Clusters.Remove(cluster);
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
        /// Persists the KubeContext along with any neonKUBE extension information to the
        /// local user folder.
        /// </summary>
        public void Save()
        {
            lock (syncRoot)
            {
                // Persist the KubeConfig.

                var configPath = KubeHelper.KubeConfigPath;

                File.WriteAllText(configPath, NeonHelper.YamlSerialize(this));

                // Persist any cluster logins.

                foreach (var context in Contexts.Where(context => context.Extension != null))
                {
                    var extensionPath = Path.Combine(KubeHelper.LoginsFolder, $"{context.Name}.login.yaml");

                    File.WriteAllText(extensionPath, NeonHelper.YamlSerialize(context.Extension));
                }

                // Delete any existing cluster login files that don't have a corresponding
                // context in the kubeconfig.

                var fileExtension = ".login.yaml";

                foreach (var extensionPath in Directory.GetFiles(KubeHelper.LoginsFolder, $"*{fileExtension}"))
                {
                    var fileName    = Path.GetFileName(extensionPath);
                    var contextName = fileName.Substring(0, fileName.Length - fileExtension.Length);

                    if (GetContext(contextName) == null)
                    {
                        File.Delete(extensionPath);
                    }
                }
            }
        }
    }
}
