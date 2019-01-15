//-----------------------------------------------------------------------------
// FILE:	    KubeConfig.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

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
    /// <note>
    /// This implementation currently supports only the a single kubeconfig
    /// located at <c>$HOME/.kube/config</c> (within the current user's
    /// HOME folder).  The <c>KUBECONFIG</c> environment variable is ignored.
    /// </note>
    /// </remarks>
    public class KubeConfig
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Reads and returns the current KubeConfig.
        /// </summary>
        /// <returns>The parsed <see cref="KubeConfig"/> or an empty config if the file doesn't exist.</returns>
        /// <exception cref="KubeException">Thrown when the current config is invalid.</exception>
        public static KubeConfig Load()
        {
            var configPath = KubeHelper.KubeConfigPath;

            if (File.Exists(configPath))
            {
                var config = NeonHelper.YamlDeserialize<KubeConfig>(File.ReadAllText(configPath));

                config.Validate();

                // Load any related neonKUBE context extension information.

                foreach (var context in config.Contexts)
                {
                    var extensionPath = Path.Combine(KubeHelper.ClustersFolder, $"{context.Name}.context.yaml");

                    if (File.Exists(extensionPath))
                    {
                        context.Properties.Extensions = NeonHelper.YamlDeserialize<KubeContextExtension>(File.ReadAllText(extensionPath));
                    }
                    else
                    {
                        context.Properties.Extensions = new KubeContextExtension();
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
        /// The name of the current context or <c>null</c> when there is no current context.
        /// </summary>
        [JsonProperty(PropertyName = "current-context", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "current-context")]
        [DefaultValue(null)]
        public string CurrentContext { get; set; }

        /// <summary>
        /// The cluster API server protocol version (defaults to <b>v1</b>).
        /// </summary>
        [JsonProperty(PropertyName = "apiVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "apiVersion")]
        [DefaultValue("v1")]
        public string ApiVersion { get; set; } = "v1";

        /// <summary>
        /// Identifies the document type: <b>Config</b>.
        /// </summary>
        [JsonProperty(PropertyName = "kind", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "kind")]
        [DefaultValue("Config")]
        public string Kind { get; set; } = "Config";

        /// <summary>
        /// The list of cluster configurations.
        /// </summary>
        [JsonProperty(PropertyName = "clusters", Required = Required.Always)]
        [YamlMember(Alias = "clusters")]
        public List<KubeConfigCluster> Clusters { get; set; } = new List<KubeConfigCluster>();

        /// <summary>
        /// The list of user configurations.
        /// </summary>
        [JsonProperty(PropertyName = "users", Required = Required.Always)]
        [YamlMember(Alias = "users")]
        public List<KubeConfigUser> Users { get; set; } = new List<KubeConfigUser>();

        /// <summary>
        /// The list of config contexts.
        /// </summary>
        [JsonProperty(PropertyName = "contexts", Required = Required.Always)]
        [YamlMember(Alias = "contexts")]
        public List<KubeConfigContext> Contexts { get; set; } = new List<KubeConfigContext>();

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
        /// The optional dictionary of preferences.
        /// </summary>
        [JsonProperty(PropertyName = "preferences", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "preferences")]
        [DefaultValue(null)]
        public Dictionary<string, string> Preferences { get; set; } = null;

        /// <summary>
        /// Returns the named cluster.
        /// </summary>
        /// <param name="name">The cluster name.</param>
        /// <returns>The <see cref="KubeConfigCluster"/> or <c>null</c>.</returns>
        public KubeConfigCluster GetCluster(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            return Clusters.SingleOrDefault(c => c.Name == name);
        }

        /// <summary>
        /// Returns the named user.
        /// </summary>
        /// <param name="name">The user name.</param>
        /// <returns>The <see cref="KubeConfigUser"/> or <c>null</c>.</returns>
        public KubeConfigUser GetUser(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            return Users.SingleOrDefault(c => c.Name == name);
        }

        /// <summary>
        /// Returns the named context.
        /// </summary>
        /// <param name="name">The context name.</param>
        /// <returns>The <see cref="KubeConfigContext"/> or <c>null</c>.</returns>
        public KubeConfigContext GetContext(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            return Contexts.SingleOrDefault(c => c.Name == name);
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
            Covenant.Requires<ArgumentNullException>(context != null);
            Covenant.Requires<ArgumentNullException>(cluster != null);
            Covenant.Requires<ArgumentNullException>(user != null);
            Covenant.Requires(context.Properties.Cluster == cluster.Name);
            Covenant.Requires(context.Properties.User == user.Name);

            var updated = false;

            for (int i = 0; i < Contexts.Count; i++)
            {
                if (Contexts[i].Name == context.Name)
                {
                    Contexts[i] = context;
                    updated = true;
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
                    updated = true;
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
                    updated = true;
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
        /// Removes a kubecontext if it exists.
        /// </summary>
        /// <param name="context">The context to be removed.</param>
        /// <param name="noSave">Optionally prevent context save after the change.</param>
        public void RemoveContext(KubeConfigContext context, bool noSave = false)
        {
            Covenant.Requires<ArgumentNullException>(context != null);

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

            // Persist as required.

            if (!noSave)
            {
                Save();

                // We need to remove the extension file too (if one exists).

                var extensionPath = Path.Combine(KubeHelper.ClustersFolder, $"{context.Name}.context.yaml");

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
        /// Validates the configuration.
        /// </summary>
        /// <exception cref="KubeException">Thrown when the current config is invalid.</exception>
        public void Validate()
        {
            if (Kind != "Config")
            {
                throw new KubeException($"Invalid [{nameof(Kind)}={Kind}].");
            }

            // Ensure that the current context exists.

            if (!string.IsNullOrEmpty(CurrentContext) && GetContext(CurrentContext) == null)
            {
                throw new KubeException($"Current context [{CurrentContext}] doesn't exist.");
            }

            // Ensure that referenced users and clusters actually exist.

            foreach (var context in Contexts)
            {
                if (!string.IsNullOrEmpty(context.Properties.User) && GetUser(context.Properties.User) == null)
                {
                    throw new KubeException($"Context [{context.Name}] references [User={context.Properties.User}] that doesn't exist.");
                }

                if (!string.IsNullOrEmpty(context.Properties.Cluster) && GetCluster(context.Properties.Cluster) != null)
                {
                    throw new KubeException($"Cluster [{context.Properties.Cluster}] doesn't exist.");
                }
            }
        }

        /// <summary>
        /// Sets the current context.
        /// </summary>
        /// <param name="contextName">The name of the current context or <c>null</c> to deselect the context.</param>
        /// <exception cref="KubeException">Thrown if the context does not exist.</exception>
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
                    throw new KubeException($"Context [{contextName}] does not exist.");
                }
            }
        }

        /// <summary>
        /// Persists the KubeContext along with any neonKUBE extension information to the
        /// local user folder.
        /// </summary>
        public void Save()
        {
            // Persist the KubeConfig.

            var configPath = KubeHelper.KubeConfigPath;

            File.WriteAllText(configPath, NeonHelper.YamlSerialize(this));

            // Persist any extensions.

            foreach (var context in Contexts.Where(c => c.Properties.Extensions != null))
            {
                var extensionPath = Path.Combine(KubeHelper.ClustersFolder, $"{context.Name}.context.yaml");

                File.WriteAllText(extensionPath, NeonHelper.YamlSerialize(context.Properties.Extensions));
            }
        }
    }
}
