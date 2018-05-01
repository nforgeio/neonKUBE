//-----------------------------------------------------------------------------
// FILE:	    LoadBalanceManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using Consul;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Neon.Cluster
{
    /// <summary>
    /// Handles cluster load balancer related operations for a <see cref="ClusterProxy"/>.
    /// </summary>
    public sealed class LoadBalanceManager
    {
        private const string proxyManagerPrefix = "neon/service/neon-proxy-manager";
        private const string vaultCertPrefix    = "neon-secret/cert";

        private ClusterProxy cluster;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="cluster">The parent <see cref="ClusterProxy"/>.</param>
        /// <param name="name">The load balancer name (<b>public</b> or <b>private</b>).</param>
        internal LoadBalanceManager(ClusterProxy cluster, string name)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null);
            Covenant.Requires<ArgumentException>(name == "public" || name == "private");

            this.cluster = cluster;
            this.Name    = name;
        }

        /// <summary>
        /// Returns the load balancer name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Returns the Consul key for the load balancer's global settings.
        /// </summary>
        /// <returns>The Consul key path.</returns>
        private string GetProxySettingsKey()
        {
            return $"{proxyManagerPrefix}/conf/{Name}/settings";
        }

        /// <summary>
        /// Returns the Consul key for a load balancer rule.
        /// </summary>
        /// <param name="ruleName">The rule name.</param>
        /// <returns>The Consul key path.</returns>
        private string GetProxyRuleKey(string ruleName)
        {
            return $"{proxyManagerPrefix}/conf/{Name}/rules/{ruleName}";
        }

        /// <summary>
        /// Returns the load balancer settings.
        /// </summary>
        /// <returns>The <see cref="LoadBalancerSettings"/>.</returns>
        public LoadBalancerSettings GetSettings()
        {
            return cluster.Consul.KV.GetObject<LoadBalancerSettings>(GetProxySettingsKey()).Result;
        }

        /// <summary>
        /// Updates the load balancer settings.
        /// </summary>
        /// <param name="settings">The new settings.</param>
        public void UpdateSettings(LoadBalancerSettings settings)
        {
            Covenant.Requires<ArgumentNullException>(settings != null);

            cluster.Consul.KV.PutObject(GetProxySettingsKey(), settings, Formatting.Indented).Wait();
        }

        /// <summary>
        /// Returns the load balancer definition including its settings and rules.
        /// </summary>
        /// <returns>The <see cref="LoadBalancerDefinition"/>.</returns>
        /// <exception cref="NeonClusterException">Thrown if the load balancer definition could not be loaded.</exception>
        public LoadBalancerDefinition GetDefinition()
        {
            // Fetch the proxy settings and all of its rules to create a full [LoadBalancerDefinition].

            var proxyDefinition  = new LoadBalancerDefinition() { Name = this.Name };
            var proxySettingsKey = GetProxySettingsKey();

            if (cluster.Consul.KV.Exists(proxySettingsKey).Result)
            {
                proxyDefinition.Settings = LoadBalancerSettings.ParseJson(cluster.Consul.KV.GetString(proxySettingsKey).Result);
            }
            else
            {
                throw new NeonClusterException($"Settings for load balancer [{Name}] do not exist or could not be loaded.");
            }

            foreach (var rule in ListRules())
            {
                proxyDefinition.Rules.Add(rule.Name, rule);
            }

            return proxyDefinition;
        }

        /// <summary>
        /// Forces the <b>neon-proxy-manager</b> to regenerate the configuration for the load balancer.
        /// </summary>
        public void Build()
        {
            cluster.Consul.KV.PutString($"{proxyManagerPrefix}/proxies/{Name}/hash", Convert.ToBase64String(new byte[16])).Wait();
            cluster.Consul.KV.PutString($"{proxyManagerPrefix}/conf/reload", DateTime.UtcNow).Wait();
        }

        /// <summary>
        /// Deletes a load balancer rule if it exists.
        /// </summary>
        /// <param name="ruleName">The rule name.</param>
        /// <returns><c>true</c> if the rule existed and was deleted, <c>false</c> if it didn't exist.</returns>
        public bool RemoveRule(string ruleName)
        {
            Covenant.Requires<ArgumentException>(ClusterDefinition.IsValidName(ruleName));

            var ruleKey = GetProxyRuleKey(ruleName);

            if (cluster.Consul.KV.Exists(ruleKey).Result)
            {
                cluster.Consul.KV.Delete(ruleKey);

                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Returns a load balancer rule if it exists.
        /// </summary>
        /// <param name="ruleName">The rule name.</param>
        /// <returns>The <see cref="LoadBalancerRule"/> or <c>null</c>.</returns>
        public LoadBalancerRule GetRule(string ruleName)
        {
            Covenant.Requires<ArgumentException>(ClusterDefinition.IsValidName(ruleName));

            var ruleKey = GetProxyRuleKey(ruleName);

            if (cluster.Consul.KV.Exists(ruleKey).Result)
            {
                return LoadBalancerRule.ParseJson(cluster.Consul.KV.GetString(ruleKey).Result);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Adds or updates a load balancer rule.
        /// </summary>
        /// <param name="rule">The rule definition.</param>
        /// <returns>
        /// <c>true</c> if it existed and was updated, <b>false</b>
        /// if the load balancer rule didn't already exist and was added.
        /// </returns>
        /// <exception cref="ClusterDefinitionException">Thrown if the rule is not valid.</exception>
        public bool PutRule(LoadBalancerRule rule)
        {
            Covenant.Requires<ArgumentNullException>(rule != null);
            Covenant.Requires<ArgumentNullException>(ClusterDefinition.IsValidName(rule.Name));

            var ruleKey = GetProxyRuleKey(rule.Name);
            var update  = cluster.Consul.KV.Exists(ruleKey).Result;

            // Load the the full proxy definition and cluster certificates, add/replace
            // the rule being set and then verify that the rule is OK.

            var proxyDefinition = GetDefinition();
            var certificates    = cluster.Certificate.GetAll();

            proxyDefinition.Rules[rule.Name] = rule;
            proxyDefinition.Validate(certificates);

            var validationContext = proxyDefinition.Validate(certificates);

            validationContext.ThrowIfErrors();

            // Save the rule to the cluster.

            cluster.Consul.KV.PutObject(ruleKey, rule, Formatting.Indented).Wait();

            return update;
        }

        /// <summary>
        /// Lists the the load balancer rules.
        /// </summary>
        /// <param name="predicate">Optional predicate used to filter the output rules.</param>
        /// <returns>The <see cref="IEnumerable{T}"/> of load balancer rules.</returns>
        public IEnumerable<LoadBalancerRule> ListRules(Func<LoadBalancerRule, bool> predicate = null)
        {
            var rulesResponse = cluster.Consul.KV.ListOrDefault<JObject>($"{proxyManagerPrefix}/conf/{Name}/rules/").Result;

            if (rulesResponse != null)
            {
                var rules = new List<LoadBalancerRule>();

                foreach (var rulebject in rulesResponse)
                {
                    var rule = LoadBalancerRule.ParseJson(rulebject.ToString());

                    if (predicate == null || predicate(rule))
                    {
                        rules.Add(rule);
                    }
                }

                return rules;
            }
            else
            {
                return new LoadBalancerRule[0];
            }
        }
    }
}
