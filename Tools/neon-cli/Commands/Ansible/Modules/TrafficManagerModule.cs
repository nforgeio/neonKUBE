//-----------------------------------------------------------------------------
// FILE:	    TrafficManagerModule.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

using Neon.Cryptography;
using Neon.Common;
using Neon.IO;
using Neon.Hive;
using Neon.Net;

namespace NeonCli.Ansible
{
    //---------------------------------------------------------------------
    // neon_traffic_manager:
    //
    // Synopsis:
    // ---------
    //
    // Manages neonHIVE traffic manager rules.
    //
    // Requirements:
    // -------------
    //
    // This module runs only within the [neon-cli] container when invoked
    // by [neon ansible exec ...] or [neon ansible play ...].
    //
    // Options:
    // --------
    //
    // parameter    required    default     choices     comments
    // --------------------------------------------------------------------
    //
    // name         yes                     private     identifies the target traffic manager
    //                                      public      
    //
    // rule_name    see comment                         neonHIVE rule name, required when
    //                                                  [state=present/absent]
    //
    // rule         see comment                         traffic manager rule details
    //                                                  required when [state=present]
    //
    // state        no          present     absent      indicates whether the rule should
    //                                      present     be created or removed or that any
    //                                      purge       purges cached items (see purge_list)
    //                                      update      deferred updates should be processed
    //                                                  immediately
    //
    // purge_list   see comment                         specifies the origin server URIs to 
    //                                                  be purged when [state=purge].  These 
    //                                                  are URIs including optional "*" or
    //                                                  "**" wildcards.
    //
    //                                                  Use "ALL" to purge all cached content
    //                                                  across all rules handled by the traffic
    //                                                  manager.
    //
    // purge_case_sensitive no  false       true/false  indicates whether the purge_list URI
    //                                                  patterns are to be evaluated as case
    //                                                  sensitive
    //
    // defer_update no          false       true/false  see note below
    //
    // Deferred Updates
    // ----------------
    //
    // By default, each traffic manager rule change will immediatelly signal the 
    // [neon-proxy-manager] and all of the proxy and proxy-bridge instances to
    // rebuild and reload their configurations.  This can cause some unnecessary
    // thrashing when you need to make multiple rule changes.
    //
    // To avoid this, you can pass [defer_update=true] for each of rule changes
    // and then when done with those, invoke this module one last time with
    // [state=update].  Note that the proxy manager periodically checks for 
    // changes (defaults to a 60 seconds interval), so a separate update is 
    // not strictly required.
    //
    // Check Mode:
    // -----------
    //
    // This module supports the [--check] Ansible command line option and [check_mode] task
    // property by determining whether any changes would have been made and also logging
    // a desciption of the changes when Ansible verbosity is increased.
    //
    // Examples:
    // ---------
    //
    // This example creates a public HTTP rule that forwards
    // HTTP traffic for [http://test.com and http://www.test.com] 
    // to the TEST Docker service port 80.
    //
    //  - name: test
    //    hosts: localhost
    //    tasks:
    //      - name: traffic manager task
    //        neon_traffic_manager:
    //          name: public
    //          state: present
    //          rule_name: test
    //          rule:
    //            mode: http
    //            checkuri: /_health/check.php
    //            checkmethod: GET
    //            frontends:
    //              - host: test.com
    //              - host: www.test.com
    //            backends:
    //              - server: TEST
    //                port: 80
    //
    // This example enables caching for creates a public HTTP rule that 
    // forwards HTTP traffic for [http://test.com and http://www.test.com] 
    // to the TEST Docker service port 80.
    //
    //  - name: test
    //    hosts: localhost
    //    tasks:
    //      - name: traffic manager task
    //        neon_traffic_manager:
    //          name: public
    //          state: present
    //          rule_name: test
    //          rule:
    //            mode: http
    //            checkuri: /_health/check.php
    //            checkmethod: GET
    //            frontends:
    //              - host: test.com
    //              - host: www.test.com
    //            backends:
    //              - server: TEST
    //                port: 80
    //            cache:
    //                enable: true
    //
    // This example creates a public HTTP rule that terminates
    // HTTPS traffic for [https://test.com and https://www.test.com] using
    // the certificate saved to the Ansible TEST_COM_CERT variable and then
    // forwards the unencrypted traffic onto the TEST service.  The rule
    // is also configured have the client redirect any HTTP traffic to 
    // to HTTPS.
    //
    //  - name: test
    //    hosts: localhost
    //    tasks:
    //      - name: traffic manager task
    //        neon_lopad_balancer:
    //          name: public
    //          state: present
    //          rule_name: test
    //          rule:
    //            mode: http
    //            checkuri: /_health/check.php
    //            checkmethod: GET
    //            frontends:
    //              - host: test.com
    //                redirecturi: https://test.com
    //              - host: test.com
    //                certname: "{{ TEST_COM_CERT }}"
    //              - host: www.test.com
    //                certname: "{{ TEST_COM_CERT }}"
    //            backends:
    //              - server: TEST
    //                port: 80
    //
    // This example adds a public TCP rule that forwards traffic
    // sent to port 5120 to each of the host nodes in the [DATABASE]
    // hive host group on port 8080.
    //
    //  - name: test
    //    hosts: localhost
    //    tasks:
    //      - name: traffic manager task
    //        neon_traffic_manager:
    //          name: public
    //          state: present
    //          rule_name: test
    //          rule:
    //            mode: tcp
    //            frontends:
    //              - port: 5120
    //            backends:
    //              - group: DATABASE
    //                port: 8080
    //
    // This example removes the rule named [test].
    //
    //  - name: test
    //    hosts: localhost
    //    tasks:
    //      - name: traffic manager task
    //        neon_traffic_manager:
    //          name: public
    //          state: absent
    //          rule_name: test
    //
    // This example defers updates when two rules are added and then 
    // signals an explicit update afterwards.
    //
    //  - name: rule-1
    //    hosts: localhost
    //    tasks:
    //      - name: traffic manager task
    //        neon_traffic_manager:
    //          name: public
    //          state: present
    //          rule_name: test
    //          defer_update: true
    //          rule:
    //            mode: http
    //            checkuri: /_health/check.php
    //            checkmethod: GET
    //            frontends:
    //              - host: test.com
    //              - host: www.test.com
    //            backends:
    //              - server: TEST
    //                port: 80
    //  - name: rule-2
    //    hosts: localhost
    //    tasks:
    //      - name: traffic manager task
    //        neon_traffic_manager:
    //          name: public
    //          state: present
    //          rule_name: test
    //          defer_update: true
    //          rule:
    //            mode: tcp
    //            frontends:
    //              - port: 5120
    //            backends:
    //              - group: DATABASE
    //                port: 8080
    //  - name: update
    //    hosts: localhost
    //    tasks:
    //      - name: traffic manager task
    //        neon_traffic_manager:
    //          name: public
    //          state: update
    //
    // This example submits a request to purge cached content for specific origin
    // servers using glob patterns. This will purge [test.aspx] from [foo.com], all
    // cached content for [bar.com] and all JPG files from [foobar.com].
    //
    // Note that the URI scheme is ignored and that the host and port must match
    // what was submitted to the origin servers via Varnish.  These will often
    // match the public URI values but it's possible that proxy rules have
    // customized these mappings. 
    //
    //  - name: test
    //    hosts: localhost
    //    tasks:
    //      - name: purge
    //        neon_traffic_manager:
    //          name: public
    //          state: purge
    //          purge_list:
    //            - http://foo.com/test.aspx
    //            - http://bar.com/**
    //            - http://foobar.com/**/*.jpg
    //
    // This example purges all cached content.
    //
    //  - name: test
    //    hosts: localhost
    //    tasks:
    //      - name: purge
    //        neon_traffic_manager:
    //          name: public
    //          state: purge
    //          purge_list:
    //            - ALL

    /// <summary>
    /// Implements the <b>neon_traffic_manager</b> Ansible module.
    /// </summary>
    public class TrafficManagerModule : IAnsibleModule
    {
        private HashSet<string> validModuleArgs = new HashSet<string>()
        {
            "name",
            "rule_name",
            "rule",
            "state",
            "purge_list",
            "purge_case_sensitive",
            "defer_update"
        };

        /// <inheritdoc/>
        public void Run(ModuleContext context)
        {
            TrafficManager      trafficManager = null;
            bool                isPublic       = false;
            string              name           = null;
            string              ruleName       = null;
            bool                deferUpdate    = false;

            if (!context.ValidateArguments(context.Arguments, validModuleArgs))
            {
                context.Failed = true;
                return;
            }

            // Obtain common arguments.

            context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [state]");

            if (!context.Arguments.TryGetValue<string>("state", out var state))
            {
                state = "present";
            }

            state = state.ToLowerInvariant();

            if (context.HasErrors)
            {
                return;
            }

            context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [name]");

            if (!context.Arguments.TryGetValue<string>("name", out name))
            {
                throw new ArgumentException($"[name] module argument is required.");
            }

            switch (name)
            {
                case "private":

                    trafficManager = HiveHelper.Hive.PrivateTraffic;
                    isPublic       = false;
                    break;

                case "public":

                    trafficManager = HiveHelper.Hive.PublicTraffic;
                    isPublic       = true;
                    break;

                default:

                    throw new ArgumentException($"[name={name}] is not a one of the valid traffic manager names: [private] or [public].");
            }

            if (state == "present" || state == "absent")
            {
                context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [rule_name]");

                if (!context.Arguments.TryGetValue<string>("rule_name", out ruleName))
                {
                    throw new ArgumentException($"[rule_name] module argument is required.");
                }

                if (!HiveDefinition.IsValidName(ruleName))
                {
                    throw new ArgumentException($"[rule_name={ruleName}] is not a valid traffic manager rule name.");
                }

                context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [defer_update]");

                if (!context.Arguments.TryGetValue<bool>("defer_update", out deferUpdate))
                {
                    deferUpdate = false;
                }
            }

            // We have the required arguments, so perform the operation.

            switch (state)
            {
                case "absent":

                    context.WriteLine(AnsibleVerbosity.Trace, $"Check if rule [{ruleName}] exists.");

                    if (trafficManager.GetRule(ruleName) != null)
                    {
                        context.WriteLine(AnsibleVerbosity.Trace, $"Rule [{ruleName}] does exist.");
                        context.WriteLine(AnsibleVerbosity.Info, $"Deleting rule [{ruleName}].");

                        if (context.CheckMode)
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"Rule [{ruleName}] will be deleted when CHECK-MODE is disabled.");
                        }
                        else
                        {
                            trafficManager.RemoveRule(ruleName, deferUpdate: deferUpdate);
                            context.WriteLine(AnsibleVerbosity.Trace, $"Rule [{ruleName}] deleted.");
                            context.Changed = true;
                        }
                    }
                    else
                    {
                        context.WriteLine(AnsibleVerbosity.Trace, $"Rule [{ruleName}] does not exist.");
                    }
                    break;

                case "present":

                    context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [rule]");

                    if (!context.Arguments.TryGetValue<JObject>("rule", out var routeObject))
                    {
                        throw new ArgumentException($"[rule] module argument is required when [state={state}].");
                    }

                    var ruleText = routeObject.ToString();

                    context.WriteLine(AnsibleVerbosity.Trace, "Parsing rule");

                    var newRule = TrafficRule.Parse(ruleText, strict: true);

                    context.WriteLine(AnsibleVerbosity.Trace, "Rule parsed successfully");

                    // Use the name argument if the deserialized rule doesn't
                    // have a name.  This will make it easier on operators because 
                    // they won't need to specify the name twice.

                    if (string.IsNullOrWhiteSpace(newRule.Name))
                    {
                        newRule.Name = ruleName;
                    }

                    // Ensure that the name passed as an argument and the
                    // name within the rule definition match.

                    if (!string.Equals(ruleName, newRule.Name, StringComparison.InvariantCultureIgnoreCase))
                    {
                        throw new ArgumentException($"The [rule_name={ruleName}] argument and the rule's [{nameof(TrafficRule.Name)}={newRule.Name}] property are not the same.");
                    }

                    context.WriteLine(AnsibleVerbosity.Trace, "Rule name matched.");

                    // Validate the rule.

                    context.WriteLine(AnsibleVerbosity.Trace, "Validating rule.");

                    var proxySettings     = trafficManager.GetSettings();
                    var validationContext = new TrafficValidationContext(name, proxySettings);

                    // $hack(jeff.lill):
                    //
                    // This ensures that [proxySettings.Resolvers] is initialized with
                    // the built-in Docker DNS resolver.

                    proxySettings.Validate(validationContext);

                    // Load the TLS certificates into the validation context so we'll
                    // be able to verify that any referenced certificates mactually exist.

                    // $todo(jeff.lill):
                    //
                    // This code assumes that the operator is currently logged in with
                    // root Vault privileges.  We'll have to do something else for
                    // non-root logins.
                    //
                    // One idea might be to save two versions of the certificates.
                    // The primary certificate with private key in Vault and then
                    // just the public certificate in Consul and then load just
                    // the public ones here.
                    //
                    // A good time to make this change might be when we convert to
                    // use the .NET X.509 certificate implementation.

                    if (!context.Login.HasVaultRootCredentials)
                    {
                        throw new ArgumentException("Access Denied: Root Vault credentials are required.");
                    }

                    context.WriteLine(AnsibleVerbosity.Trace, "Reading hive certificates.");

                    using (var vault = HiveHelper.OpenVault(Program.HiveLogin.VaultCredentials.RootToken))
                    {
                        // List the certificate key/names and then fetch each one
                        // to capture details like the expiration date and covered
                        // hostnames.

                        foreach (var certName in vault.ListAsync("neon-secret/cert").Result)
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"Reading: {certName}");

                            var certificate = vault.ReadJsonAsync<TlsCertificate>(HiveHelper.GetVaultCertificateKey(certName)).Result;

                            validationContext.Certificates.Add(certName, certificate);
                        }
                    }

                    context.WriteLine(AnsibleVerbosity.Trace, $"[{validationContext.Certificates.Count}] hive certificates downloaded.");

                    // Actually perform the rule validation.

                    newRule.Validate(validationContext);

                    if (validationContext.HasErrors)
                    {
                        context.WriteLine(AnsibleVerbosity.Trace, $"[{validationContext.Errors.Count}] Route validation errors.");

                        foreach (var error in validationContext.Errors)
                        {
                            context.WriteLine(AnsibleVerbosity.Important, error);
                            context.WriteErrorLine(error);
                        }

                        context.Failed = true;
                        return;
                    }

                    context.WriteLine(AnsibleVerbosity.Trace, "Rule is valid.");

                    // Try reading any existing rule with this name and then determine
                    // whether the two versions of the rule are actually different. 

                    context.WriteLine(AnsibleVerbosity.Trace, $"Looking for existing rule [{ruleName}]");

                    var existingRule = trafficManager.GetRule(ruleName);
                    var changed      = false;

                    if (existingRule != null)
                    {
                        context.WriteLine(AnsibleVerbosity.Trace, $"Rule exists: checking for differences.");

                        // Normalize the new and existing rules so the JSON text comparision
                        // will work properly.

                        newRule.Normalize(isPublic);
                        existingRule.Normalize(isPublic);

                        changed = !NeonHelper.JsonEquals(newRule, existingRule);

                        if (changed)
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"Rules are different.");
                        }
                        else
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"Rules are the same.  No need to update.");
                        }
                    }
                    else
                    {
                        changed = true;
                        context.WriteLine(AnsibleVerbosity.Trace, $"Rule [name={ruleName}] does not exist.");
                    }
                     
                    if (changed)
                    {
                        if (context.CheckMode)
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"Rule [{ruleName}] will be updated when CHECK-MODE is disabled.");
                        }
                        else
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"Writing rule [{ruleName}].");
                            trafficManager.SetRule(newRule);
                            context.WriteLine(AnsibleVerbosity.Info, $"Rule updated.");
                            context.Changed = !context.CheckMode;
                       }
                    }
                    break;

                case "update":

                    trafficManager.Update();
                    context.Changed = true;
                    context.WriteLine(AnsibleVerbosity.Info, $"Update signalled.");
                    break;

                case "purge":

                    var purgeItems         = context.ParseStringArray("purge_list");
                    var purgeCaseSensitive = context.ParseBool("purge_case_sensitive");

                    if (!purgeCaseSensitive.HasValue)
                    {
                        purgeCaseSensitive = false;
                    }

                    if (purgeItems.Count == 0)
                    {
                        context.WriteLine(AnsibleVerbosity.Important, $"[purge_list] is missing or empty.");
                        break;
                    }

                    trafficManager.Purge(purgeItems.ToArray(), caseSensitive: purgeCaseSensitive.Value);

                    context.Changed = true;
                    context.WriteLine(AnsibleVerbosity.Info, $"Purge request submitted.");
                    break;

                default:

                    throw new ArgumentException($"[state={state}] is not one of the valid choices: [present], [absent], or [update].");
            }
        }
    }
}
