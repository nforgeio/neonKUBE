//-----------------------------------------------------------------------------
// FILE:	    TrafficManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

using Consul;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Cryptography;
using Neon.HiveMQ;
using Neon.Net;

namespace Neon.Hive
{
    /// <summary>
    /// Handles hive traffic manager related operations for a <see cref="HiveProxy"/>.
    /// </summary>
    public sealed class TrafficManager
    {
        private const string proxyManagerPrefix = "neon/service/neon-proxy-manager";
        private const string vaultCertPrefix    = "neon-secret/cert";

        private HiveProxy           hive;
        private bool                useBootstrap;
        private BroadcastChannel    proxyNotifyChannel;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="hive">The parent <see cref="HiveProxy"/>.</param>
        /// <param name="name">The traffic manager name (<b>public</b> or <b>private</b>).</param>
        /// <param name="useBootstrap">
        /// Optionally specifies that the instance should use the HiveMQ client
        /// to directly reference to the HiveMQ cluster nodes for broadcasting
        /// proxy update messages rather than routing traffic through the <b>private</b>
        /// traffic manager.  This is used internally to resolve chicken-and-the-egg
        /// dilemmas for the traffic manager and proxy implementations that rely on
        /// HiveMQ messaging.
        /// </param>
        internal TrafficManager(HiveProxy hive, string name, bool useBootstrap = false)
        {
            Covenant.Requires<ArgumentNullException>(hive != null);
            Covenant.Requires<ArgumentException>(name == "public" || name == "private");

            this.hive         = hive;
            this.Name         = name;
            this.useBootstrap = useBootstrap;
        }

        /// <summary>
        /// Returns the traffic manager name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Indicates whether the <b>neon-proxy-public</b> or <b>neon-proxy-private</b>
        /// proxy is being managed.
        /// </summary>
        public bool IsPublic => Name.Equals("public", StringComparison.InvariantCultureIgnoreCase);

        /// <summary>
        /// Returns the Consul key for the traffic manager's global settings.
        /// </summary>
        /// <returns>The Consul key path.</returns>
        private string GetProxySettingsKey()
        {
            return $"{proxyManagerPrefix}/conf/{Name}/settings";
        }

        /// <summary>
        /// Returns the Consul key for a traffic manager rule.
        /// </summary>
        /// <param name="ruleName">The rule name.</param>
        /// <returns>The Consul key path.</returns>
        private string GetProxyRuleKey(string ruleName)
        {
            return $"{proxyManagerPrefix}/conf/{Name}/rules/{ruleName}";
        }

        /// <summary>
        /// Returns the traffic manager settings.
        /// </summary>
        /// <returns>The <see cref="TrafficSettings"/>.</returns>
        public TrafficSettings GetSettings()
        {
            return hive.Consul.Client.KV.GetObject<TrafficSettings>(GetProxySettingsKey()).Result;
        }

        /// <summary>
        /// Updates the traffic manager settings.
        /// </summary>
        /// <param name="settings">The new settings.</param>
        public void UpdateSettings(TrafficSettings settings)
        {
            Covenant.Requires<ArgumentNullException>(settings != null);

            hive.Consul.Client.KV.PutObject(GetProxySettingsKey(), settings, Formatting.Indented).Wait();
            Update();
        }

        /// <summary>
        /// Returns the traffic manager definition including its settings and rules.
        /// </summary>
        /// <returns>The <see cref="TrafficDefinition"/>.</returns>
        /// <exception cref="HiveException">Thrown if the traffic manager definition could not be loaded.</exception>
        public TrafficDefinition GetDefinition()
        {
            // Fetch the proxy settings and all of its rules to create a full [TrafficManagerDefinition].

            var proxyDefinition  = new TrafficDefinition() { Name = this.Name };
            var proxySettingsKey = GetProxySettingsKey();

            if (hive.Consul.Client.KV.Exists(proxySettingsKey).Result)
            {
                proxyDefinition.Settings = TrafficSettings.ParseJson(hive.Consul.Client.KV.GetString(proxySettingsKey).Result);
            }
            else
            {
                throw new HiveException($"Settings for traffic manager [{Name}] do not exist or could not be loaded.");
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
                    proxyNotifyChannel = hive.HiveMQ.Internal.GetProxyNotifyChannel(useBootstrap: useBootstrap, publishOnly: true).Open();
                }

                return proxyNotifyChannel;
            }
        }

        /// <summary>
        /// Signals the <b>neon-proxy-manager</b> to immediately regenerate the traffic manager and proxy configurations,
        /// without waiting for the periodic change detection (that happens at a 60 second interval by default).
        /// </summary>
        public void Update()
        {
            ProxyNotifyChannel.Publish(
                new ProxyRegenerateMessage("Update")
                {
                    Reason = $"proactive update: {Name}"
                });
        }

        /// <summary>
        /// Deletes a traffic manager rule if it exists.
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
        /// Returns a traffic manager rule if it exists.
        /// </summary>
        /// <param name="ruleName">The rule name.</param>
        /// <returns>The <see cref="TrafficRule"/> or <c>null</c>.</returns>
        public TrafficRule GetRule(string ruleName)
        {
            Covenant.Requires<ArgumentException>(HiveDefinition.IsValidName(ruleName));

            var ruleKey = GetProxyRuleKey(ruleName);

            if (hive.Consul.Client.KV.Exists(ruleKey).Result)
            {
                return TrafficRule.ParseJson(hive.Consul.Client.KV.GetString(ruleKey).Result);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Adds or updates a traffic manager rule.
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
        /// if the traffic manager rule didn't already exist and was added.
        /// </returns>
        /// <exception cref="HiveDefinitionException">Thrown if the rule is not valid.</exception>
        public bool SetRule(TrafficRule rule, bool deferUpdate = false)
        {
            Covenant.Requires<ArgumentNullException>(rule != null);
            Covenant.Requires<ArgumentNullException>(HiveDefinition.IsValidName(rule.Name));

            if (!IsPublic)
            {
                // Ensure that the [PublicPort] is disabled for non-public rules
                // just to be absolutely sure that these endpoints are not exposed
                // to the Internet for cloud deployments and to avoid operators
                // being freaked out if they see a non-zero port here.

                var httpRule = rule as TrafficHttpRule;

                if (httpRule != null)
                {
                    foreach (var frontEnd in httpRule.Frontends)
                    {
                        frontEnd.PublicPort = 0;
                    }
                }
                else
                {
                    var tcpRule = rule as TrafficTcpRule;

                    if (tcpRule != null)
                    {
                        foreach (var frontEnd in tcpRule.Frontends)
                        {
                            frontEnd.PublicPort = 0;
                        }
                    }
                }
            }

            // $todo(jeff.lill):
            //
            // We're going to minimially ensure that the rule is valid.  It would
            // be better to do full server side validation.

            var context = new TrafficValidationContext(Name, GetSettings())
            {
                ValidateCertificates = false,   // Disable this because we didn't download the certs.
                ValidateResolvers    = false
            };

            rule.Validate(context);
            context.ThrowIfErrors();

            // Publish the rule.

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
        /// Lists the traffic manager rules.
        /// </summary>
        /// <param name="predicate">Optional predicate used to filter the output rules.</param>
        /// <returns>The <see cref="IEnumerable{T}"/> of traffic manager rules.</returns>
        public IEnumerable<TrafficRule> ListRules(Func<TrafficRule, bool> predicate = null)
        {
            var rulesResponse = hive.Consul.Client.KV.ListOrDefault<JObject>($"{proxyManagerPrefix}/conf/{Name}/rules/").Result;

            if (rulesResponse != null)
            {
                var rules = new List<TrafficRule>();

                foreach (var rulebject in rulesResponse)
                {
                    var rule = TrafficRule.ParseJson(rulebject.ToString());

                    if (predicate == null || predicate(rule))
                    {
                        rules.Add(rule);
                    }
                }

                return rules;
            }
            else
            {
                return new TrafficRule[0];
            }
        }

        /// <summary>
        /// Instructs the associated <b>neon-proxy-public-cache</b> or <b>neon-proxy-private-cache</b>
        /// services to purge the content specified.
        /// </summary>
        /// <param name="uriPatterns">
        /// <para>
        /// One or more patterns specifying which content is to be purged.  Each  of these is either an 
        /// HAProxy frontend URI, optionally including <b>"*"</b> or  <b>"**"</b> wildcards or this may 
        /// be set to <b>"ALL"</b> which specifies that all cached  content is to be purged.
        /// </para>
        /// <note>
        /// URI pattern matching is case-insensitive by default.
        /// </note>
        /// </param>
        /// <param name="caseSensitive">Optionally enables case sensitive matching.</param>
        /// <remarks>
        /// <note>
        /// <para>
        /// The URI pattern hostname and port needs to match a corresponding HAProxy frontend.
        /// This is exactly what you'd expect for <b>public</b> traffic manager frontends listening
        /// on ports 80 and 443 which will often cover all of your caching needs.
        /// </para>
        /// <para>
        /// For the <b>private</b> traffic manager or non-standard frontend ports on the <b>public</b>
        /// traffic manager, you'll need to explicitly specify the frontend port through which the
        /// original traffic was routed.
        /// </para>
        /// </note>
        /// </remarks>
        public void Purge(IEnumerable<string> uriPatterns, bool caseSensitive = false)
        {
            if (uriPatterns == null || uriPatterns.Count() == 0)
            {
                return; // NOP
            }

            var message = new ProxyPurgeMessage()
            {
                PublicCache   = IsPublic,
                PrivateCache  = !IsPublic,
                CaseSensitive = caseSensitive
            };

            foreach (var pattern in uriPatterns)
            {
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    continue;   // Ignore any blank patterns
                }

                if (pattern.Equals("all", StringComparison.InvariantCultureIgnoreCase))
                {
                    message.AddPurgeAll();
                }
                else
                {
                    message.AddPurgeOrigin(pattern);
                }
            }

            if (message.PurgeOperations.Count > 0)
            {
                ProxyNotifyChannel.Publish(message);
            }
        }

        /// <summary>
        /// Purges all cached items.
        /// </summary>
        public void PurgeAll()
        {
            Purge(new string[] { "all" });
        }
    }
}
