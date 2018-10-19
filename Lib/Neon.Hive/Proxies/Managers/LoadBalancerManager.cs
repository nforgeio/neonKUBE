//-----------------------------------------------------------------------------
// FILE:	    LoadBalancerManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using Consul;
using Neon.HiveMQ;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Neon.Hive
{
    /// <summary>
    /// Handles hive load balancer related operations for a <see cref="HiveProxy"/>.
    /// </summary>
    public sealed class LoadBalancerManager
    {
        private const string proxyManagerPrefix = "neon/service/neon-proxy-manager";
        private const string vaultCertPrefix    = "neon-secret/cert";

        private HiveProxy           hive;
        private BroadcastChannel    proxyNotifyChannel;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="hive">The parent <see cref="HiveProxy"/>.</param>
        /// <param name="name">The load balancer name (<b>public</b> or <b>private</b>).</param>
        internal LoadBalancerManager(HiveProxy hive, string name)
        {
            Covenant.Requires<ArgumentNullException>(hive != null);
            Covenant.Requires<ArgumentException>(name == "public" || name == "private");

            this.hive = hive;
            this.Name = name;
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
            return hive.Consul.Client.KV.GetObject<LoadBalancerSettings>(GetProxySettingsKey()).Result;
        }

        /// <summary>
        /// Updates the load balancer settings.
        /// </summary>
        /// <param name="settings">The new settings.</param>
        public void UpdateSettings(LoadBalancerSettings settings)
        {
            Covenant.Requires<ArgumentNullException>(settings != null);

            hive.Consul.Client.KV.PutObject(GetProxySettingsKey(), settings, Formatting.Indented).Wait();
            Update();
        }

        /// <summary>
        /// Returns the load balancer definition including its settings and rules.
        /// </summary>
        /// <returns>The <see cref="LoadBalancerDefinition"/>.</returns>
        /// <exception cref="HiveException">Thrown if the load balancer definition could not be loaded.</exception>
        public LoadBalancerDefinition GetDefinition()
        {
            // Fetch the proxy settings and all of its rules to create a full [LoadBalancerDefinition].

            var proxyDefinition  = new LoadBalancerDefinition() { Name = this.Name };
            var proxySettingsKey = GetProxySettingsKey();

            if (hive.Consul.Client.KV.Exists(proxySettingsKey).Result)
            {
                proxyDefinition.Settings = LoadBalancerSettings.ParseJson(hive.Consul.Client.KV.GetString(proxySettingsKey).Result);
            }
            else
            {
                throw new HiveException($"Settings for load balancer [{Name}] do not exist or could not be loaded.");
            }

            foreach (var rule in ListRules())
            {
                proxyDefinition.Rules.Add(rule.Name, rule);
            }

            return proxyDefinition;
        }

        /// <summary>
        /// Returns an opened <see cref="HiveMQChannels.ProxyNotify"/> channel configured 
        /// to allow only message publication (no message consumption).
        /// </summary>
        private BroadcastChannel ProxyNotifyChannel
        {
            get
            {
                if (proxyNotifyChannel == null)
                {
                    proxyNotifyChannel = hive.HiveMQ.Internal.GetProxyNotifyChannel(publishOnly: true).Open();
                }

                return proxyNotifyChannel;
            }
        }

        /// <summary>
        /// Signals the <b>neon-proxy-manager</b> to immediately regenerate the load balancer and proxy configurations,
        /// without waiting for the periodic change detection (that happens at a 60 second interval by default).
        /// </summary>
        public void Update()
        {
            ProxyNotifyChannel.Publish(
                new ProxyRegenerateMessage("Update")
                {
                    Reason = $"Proactive update: {Name}"
                });
        }

        /// <summary>
        /// Deletes a load balancer rule if it exists.
        /// </summary>
        /// <param name="ruleName">The rule name.</param>
        /// <param name="deferUpdate">
        /// <para>
        /// Optionally defers expicitly notifying the <b>neon-proxy-manager</b> of the
        /// change until <see cref="Update()"/> is called or the <b>neon-proxy-manager</b>
        /// performs the periodic check for changes (which defaults to 60 seconds).  You
        /// may consider passing <paramref name="deferUpdate"/><c>=true</c> when you are
        /// modifying a multiple rules at the same time to avoid making the proxy manager
        /// and proxy instances handle each rule change individually.
        /// </para>
        /// <para>
        /// Instead, you could pass <paramref name="deferUpdate"/><c>=true</c> for all of
        /// the rule changes and then call <see cref="Update()"/> afterwards.
        /// </para>
        /// </param>
        /// <returns><c>true</c> if the rule existed and was deleted, <c>false</c> if it didn't exist.</returns>
        public bool RemoveRule(string ruleName, bool deferUpdate = false)
        {
            Covenant.Requires<ArgumentException>(HiveDefinition.IsValidName(ruleName));

            var ruleKey = GetProxyRuleKey(ruleName);

            if (hive.Consul.Client.KV.Exists(ruleKey).Result)
            {
                hive.Consul.Client.KV.Delete(ruleKey);

                if (!deferUpdate)
                {
                    Update();
                }

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
            Covenant.Requires<ArgumentException>(HiveDefinition.IsValidName(ruleName));

            var ruleKey = GetProxyRuleKey(ruleName);

            if (hive.Consul.Client.KV.Exists(ruleKey).Result)
            {
                return LoadBalancerRule.ParseJson(hive.Consul.Client.KV.GetString(ruleKey).Result);
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
        /// <param name="deferUpdate">
        /// <para>
        /// Optionally defers expicitly notifying the <b>neon-proxy-manager</b> of the
        /// change until <see cref="Update()"/> is called or the <b>neon-proxy-manager</b>
        /// performs the periodic check for changes (which defaults to 60 seconds).  You
        /// may consider passing <paramref name="deferUpdate"/><c>=true</c> when you are
        /// modifying a multiple rules at the same time to avoid making the proxy manager
        /// and proxy instances handle each rule change individually.
        /// </para>
        /// <para>
        /// Instead, you could pass <paramref name="deferUpdate"/><c>=true</c> for all of
        /// the rule changes and then call <see cref="Update()"/> afterwards.
        /// </para>
        /// </param>
        /// <returns>
        /// <c>true</c> if it existed and was updated, <b>false</b>
        /// if the load balancer rule didn't already exist and was added.
        /// </returns>
        /// <exception cref="HiveDefinitionException">Thrown if the rule is not valid.</exception>
        public bool SetRule(LoadBalancerRule rule, bool deferUpdate = false)
        {
            Covenant.Requires<ArgumentNullException>(rule != null);
            Covenant.Requires<ArgumentNullException>(HiveDefinition.IsValidName(rule.Name));

            if (!Name.Equals("public", StringComparison.InvariantCultureIgnoreCase))
            {
                // Ensure that the [PublicPort] is disabled for non-public rules
                // just to be absolutely sure that these endpoints are not exposed
                // to the Internet for cloud deployments and to avoid operators
                // being freaked out if they see a non-zero port here.

                var httpRule = rule as LoadBalancerHttpRule;

                if (httpRule != null)
                {
                    foreach (var frontEnd in httpRule.Frontends)
                    {
                        frontEnd.PublicPort = 0;
                    }
                }
                else
                {
                    var tcpRule = rule as LoadBalancerTcpRule;

                    if (tcpRule != null)
                    {
                        foreach (var frontEnd in tcpRule.Frontends)
                        {
                            frontEnd.PublicPort = 0;
                        }
                    }
                }
            }

            var ruleKey = GetProxyRuleKey(rule.Name);
            var update  = hive.Consul.Client.KV.Exists(ruleKey).Result;

            // Load the full proxy definition and hive certificates, add/replace
            // the rule being set and then verify that the rule is OK.

            var proxyDefinition = GetDefinition();
            var certificates    = hive.Certificate.GetAll();

            proxyDefinition.Rules[rule.Name] = rule;
            proxyDefinition.Validate(certificates);

            var validationContext = proxyDefinition.Validate(certificates);

            validationContext.ThrowIfErrors();

            // Save the rule to the hive and signal that the
            // load balancers need to be updated.

            hive.Consul.Client.KV.PutObject(ruleKey, rule, Formatting.Indented).Wait();

            if (!deferUpdate)
            {
                Update();
            }

            return update;
        }

        /// <summary>
        /// Lists the load balancer rules.
        /// </summary>
        /// <param name="predicate">Optional predicate used to filter the output rules.</param>
        /// <returns>The <see cref="IEnumerable{T}"/> of load balancer rules.</returns>
        public IEnumerable<LoadBalancerRule> ListRules(Func<LoadBalancerRule, bool> predicate = null)
        {
            var rulesResponse = hive.Consul.Client.KV.ListOrDefault<JObject>($"{proxyManagerPrefix}/conf/{Name}/rules/").Result;

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
