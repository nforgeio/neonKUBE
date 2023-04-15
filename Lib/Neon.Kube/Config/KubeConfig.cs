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
        /// Reads and returns information loaded from the user's <b>~/.kube/config</b> file.
        /// </summary>
        /// <returns>The parsed <see cref="KubeConfig"/> or an empty config if the file doesn't exist.</returns>
        /// <exception cref="NeonKubeException">Thrown when the current config is invalid.</exception>
        public static KubeConfig Load()
        {
            var configPath = KubeHelper.KubeConfigPath;

            if (File.Exists(configPath))
            {
                var config = NeonHelper.YamlDeserialize<KubeConfig>(KubeHelper.ParseTextFileWithRetry(configPath));

                config.Validate();
                
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
        /// Optional dictionary of preferences.
        /// </summary>
        [JsonProperty(PropertyName = "preferences", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "preferences", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public Dictionary<string, string> Preferences { get; set; } = null;

        /// <summary>
        /// Lists the user configurations.
        /// </summary>
        [JsonProperty(PropertyName = "users", Required = Required.Always)]
        [YamlMember(Alias = "users", ApplyNamingConventions = false)]
        public List<KubeConfigUser> Users { get; set; } = new List<KubeConfigUser>();

        /// <summary>
        /// Lists any custom extension properties.  Extensions are name/value pairs added
        /// by vendors to hold arbitrary information.  Take care to choose property names
        /// that are unlikely to conflict with properties created by other vendors by adding
        /// a custom suffix like <b>my-property.neonkube.io</b>, where <b>my-property</b> 
        /// identifies the property and <b>neonkibe.io</b> helps avoid conflicts.
        /// </summary>
        [JsonProperty(PropertyName = "Extensions", Required = Required.Default)]
        [YamlMember(Alias = "extensions", ApplyNamingConventions = false)]
        public List<NamedExtension> Extensions { get; set; } = new List<NamedExtension>();

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
        /// Removes a Kubernetes context, if it exists.
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

            // Clear the current context if the removed context was the current one.

            if (CurrentContext == context.Name)
            {
                CurrentContext = null;
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
        /// <exception cref="NeonKubeException">Thrown when the current config is invalid.</exception>
        public void Validate()
        {
            if (Kind != "Config")
            {
                throw new NeonKubeException($"Invalid [{nameof(Kind)}={Kind}].");
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
        /// Persists the KubeConfig.
        /// </summary>
        public void Save()
        {
            lock (syncRoot)
            {
                // Persist the KubeConfig.

                var configPath = KubeHelper.KubeConfigPath;

                File.WriteAllText(configPath, NeonHelper.YamlSerialize(this));
            }
        }
    }
}
