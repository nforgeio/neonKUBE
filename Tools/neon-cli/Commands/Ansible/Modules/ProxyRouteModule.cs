//-----------------------------------------------------------------------------
// FILE:	    ProxyRouteModule.cs
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

using Neon.Cluster;
using Neon.Cryptography;
using Neon.Common;
using Neon.IO;
using Neon.Net;

namespace NeonCli.Ansible
{
    //---------------------------------------------------------------------
    // neon_load_balancer:
    //
    // Synopsis:
    // ---------
    //
    // Manages neonCLUSTER load balancer rules.
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
    // name         yes                                 neonCLUSTER rule name
    //
    // load_balancer yes                    private     identifies the target load balancer
    //                                      public
    //
    // rule         see comment                         load balancer rule description
    //                                                  required when [state=present]
    //
    // state        no          present     absent      indicates whether the rule should
    //                                      present     be created or removed
    //
    // force        no          false                   forces proxy rebuild when [state=present]
    //                                                  even if the rule is unchanged
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
    // This example creates a public HTTP rule listening that forwards
    // HTTP traffic for [http://test.com and http://www.test.com] 
    // to the TEST Docker service port 80.
    //
    //  - name: test
    //    hosts: localhost
    //    tasks:
    //      - name: load balancer task
    //        neon_load_balancer:
    //          name: test
    //          proxy: public
    //          state: present
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
    // This example creates a public HTTP rule listening that terminates
    // HTTPS traffic for [https://test.com and https://www.test.com] using
    // the certificate saved to the Ansible TEST_COM_CERT variable and then
    // forwards the unencrypted traffic onto the TEST service.  The rule
    // is also configured have the client redirect any HTTP traffic to 
    // to HTTPS.
    //
    //  - name: test
    //    hosts: localhost
    //    tasks:
    //      - name: load balancer task
    //        neon_lopad_balancer:
    //          name: test
    //          proxy: public
    //          state: present
    //          httpsredirect: yes
    //          rule:
    //            mode: http
    //            checkuri: /_health/check.php
    //            checkmethod: GET
    //            frontends:
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
    // cluster host group on port 8080.
    //
    //  - name: test
    //    hosts: localhost
    //    tasks:
    //      - name: load balancer task
    //        neon_load_balancer:
    //          name: test
    //          proxy: public
    //          state: present
    //          rule:
    //            mode: tcp
    //            frontends:
    //              - port: 5120
    //            backends:
    //              - group: DATABASE
    //                port: 8080
    //
    // This example removes any existing rule named TEST.
    //
    //  - name: test
    //    hosts: localhost
    //    tasks:
    //      - name: load balancer task
    //        neon_load_balancer:
    //          name: test
    //          proxy: public
    //          state: absent

    /// <summary>
    /// Implements the <b>neon_load_balancer</b> Ansible module.
    /// </summary>
    public class LoadBalancerModule : IAnsibleModule
    {
        /// <inheritdoc/>
        public void Run(ModuleContext context)
        {
            LoadBalanceManager  loadBalancer;

            // Obtain common arguments.

            context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [name]");

            if (!context.Arguments.TryGetValue<string>("name", out var name))
            {
                throw new ArgumentException($"[name] module argument is required.");
            }

            if (!ClusterDefinition.IsValidName(name))
            {
                throw new ArgumentException($"[name={name}] is not a valid load balancer rule name.");
            }

            context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [proxy]");

            if (!context.Arguments.TryGetValue<string>("proxy", out var proxy))
            {
                throw new ArgumentException($"[proxy] module argument is required.");
            }

            switch (proxy)
            {
                case "private":

                    loadBalancer = NeonClusterHelper.Cluster.PrivateLoadBalancer;
                    break;

                case "public":

                    loadBalancer = NeonClusterHelper.Cluster.PublicLoadBalancer;
                    break;

                default:

                    throw new ArgumentException($"[proxy={proxy}] is not a one of the valid choices: [private] or [public].");
            }

            context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [state]");

            if (!context.Arguments.TryGetValue<string>("state", out var state))
            {
                state = "present";
            }

            state = state.ToLowerInvariant();

            context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [force]");

            if (!context.Arguments.TryGetValue<bool>("force", out var force))
            {
                force = false;
            }

            if (context.HasErrors)
            {
                return;
            }

            // We have the required arguments, so perform the operation.

            switch (state)
            {
                case "absent":

                    context.WriteLine(AnsibleVerbosity.Trace, $"Check if rule [{name}] exists.");

                    if (loadBalancer.GetRule(name) != null)
                    {
                        context.WriteLine(AnsibleVerbosity.Trace, $"Rule [{name}] does exist.");
                        context.WriteLine(AnsibleVerbosity.Info, $"Deleting rule [{name}].");

                        if (context.CheckMode)
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"Rule [{name}] will be deleted when CHECK-MODE is disabled.");
                        }
                        else
                        {
                            loadBalancer.RemoveRule(name);
                            context.WriteLine(AnsibleVerbosity.Trace, $"Rule [{name}] deleted.");
                        }

                        context.Changed = !context.CheckMode;
                    }
                    else
                    {
                        context.WriteLine(AnsibleVerbosity.Trace, $"Rule [{name}] does not exist.");
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

                    var newRule = LoadBalancerRule.Parse(ruleText, strict: true);

                    context.WriteLine(AnsibleVerbosity.Trace, "Rule parsed successfully");

                    // Use the name argument if the deserialized rule doesn't
                    // have a name.  This will make it easier on operators because 
                    // they won't need to specify the name twice.

                    if (string.IsNullOrWhiteSpace(newRule.Name))
                    {
                        newRule.Name = name;
                    }

                    // Ensure that the name passed as an argument and the
                    // name within the rule definition match.

                    if (!string.Equals(name, newRule.Name, StringComparison.InvariantCultureIgnoreCase))
                    {
                        throw new ArgumentException($"The [name={name}] argument and the rule's [{nameof(LoadBalancerRule.Name)}={newRule.Name}] property are not the same.");
                    }

                    context.WriteLine(AnsibleVerbosity.Trace, "Rule name matched.");

                    // Validate the rule.

                    context.WriteLine(AnsibleVerbosity.Trace, "Validating rule.");

                    var proxySettings     = loadBalancer.GetSettings();
                    var validationContext = new LoadBalancerValidationContext(proxy, proxySettings);

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

                    context.WriteLine(AnsibleVerbosity.Trace, "Reading cluster certificates.");

                    using (var vault = NeonClusterHelper.OpenVault(Program.ClusterLogin.VaultCredentials.RootToken))
                    {
                        // List the certificate key/names and then fetch each one
                        // to capture details like the expiration date and covered
                        // host names.

                        foreach (var certName in vault.ListAsync("neon-secret/cert").Result)
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"Reading: {certName}");

                            var certificate = vault.ReadJsonAsync<TlsCertificate>(NeonClusterHelper.GetVaultCertificateKey(certName)).Result;

                            validationContext.Certificates.Add(certName, certificate);
                        }
                    }

                    context.WriteLine(AnsibleVerbosity.Trace, $"[{validationContext.Certificates.Count}] cluster certificates downloaded.");

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

                    context.WriteLine(AnsibleVerbosity.Trace, $"Looking for existing rule [{name}]");

                    var existingRule = loadBalancer.GetRule(name);
                    var changed      = false;

                    if (existingRule != null)
                    {
                        context.WriteLine(AnsibleVerbosity.Trace, $"Rule exists: checking for differences.");

                        changed = !NeonHelper.JsonEquals(newRule, existingRule);

                        if (changed)
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"Rules are different.");
                        }
                        else
                        {
                            if (force)
                            {
                                changed = true;
                                context.WriteLine(AnsibleVerbosity.Trace, $"Rules are the same but since [force=true] we're going to update anyway.");
                            }
                            else
                            {
                                context.WriteLine(AnsibleVerbosity.Info, $"Rules are the same.  No need to update.");
                            }
                        }
                    }
                    else
                    {
                        changed = true;
                        context.WriteLine(AnsibleVerbosity.Trace, $"Rule [name={name}] does not exist.");
                    }
                     
                    if (changed)
                    {
                        if (context.CheckMode)
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"Rule [{name}] will be updated when CHECK-MODE is disabled.");
                        }
                        else
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"Updating rule [{name}].");
                            loadBalancer.PutRule(newRule);
                            context.WriteLine(AnsibleVerbosity.Info, $"Rule updated.");
                            context.Changed = !context.CheckMode;
                       }
                    }
                    break;

                default:

                    throw new ArgumentException($"[state={state}] is not one of the valid choices: [present] or [absent].");
            }
        }
    }
}
