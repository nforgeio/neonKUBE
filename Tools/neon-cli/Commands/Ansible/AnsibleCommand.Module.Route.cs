//-----------------------------------------------------------------------------
// FILE:	    AnsibleCommand.Route.cs
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

namespace NeonCli
{
    public partial class AnsibleCommand : CommandBase
    {
        //---------------------------------------------------------------------
        // neon_route:
        //
        // Synopsis:
        // ---------
        //
        // Manage neonCLUSTER proxy routes.
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
        // name         yes                                 neonCLUSTER route name
        //
        // proxy        yes                     private     identifies the target proxy
        //                                      public
        //
        // route        see comment                         proxy route description formatted as
        //                                                  structured as YAML.  Required when
        //                                                  [state=present]
        //
        // state        no          present     absent      indicates whether the route should
        //                                      present     be created or removed
        //
        // force        no          false                   forces proxy rebuild when [state=present]
        //                                                  even if the route is unchanged

        /// <summary>
        /// Implements the built-in <b>neon_route</b> module.
        /// </summary>
        /// <param name="context">The module execution context.</param>
        private void RunRouteModule(ModuleContext context)
        {
            ProxyManager    proxyManager;

            // Obtain common arguments.

            context.WriteLine(Verbosity.Trace, $"Parsing [name]");

            if (!context.Arguments.TryGetValue<string>("name", out var name))
            {
                throw new ArgumentException($"[name] module argument is required.");
            }

            if (!ClusterDefinition.IsValidName(name))
            {
                throw new ArgumentException($"[name={name}] is not a valid proxy route name.");
            }

            context.WriteLine(Verbosity.Trace, $"Parsing [proxy]");

            if (!context.Arguments.TryGetValue<string>("proxy", out var proxy))
            {
                throw new ArgumentException($"[proxy] module argument is required.");
            }

            switch (proxy)
            {
                case "private":

                    proxyManager = NeonClusterHelper.Cluster.PrivateProxy;
                    break;

                case "public":

                    proxyManager = NeonClusterHelper.Cluster.PublicProxy;
                    break;

                default:

                    throw new ArgumentException($"[proxy={proxy}] is not a one of the valid choices: [private] or [public].");
            }

            context.WriteLine(Verbosity.Trace, $"Parsing [state]");

            if (!context.Arguments.TryGetValue<string>("state", out var state))
            {
                state = "present";
            }

            state = state.ToLowerInvariant();

            context.WriteLine(Verbosity.Trace, $"Parsing [force]");

            if (!context.Arguments.TryGetValue<bool>("force", out var force))
            {
                force = false;
            }

            // We have the required arguments, so perform the operation.

            switch (state)
            {
                case "absent":

                    context.WriteLine(Verbosity.Trace, $"Check if route [{name}] exists.");

                    if (proxyManager.GetRoute(name) != null)
                    {
                        context.WriteLine(Verbosity.Trace, $"Check route [{name}] does exist.");
                        context.WriteLine(Verbosity.Info, $"Deleting route [{name}].");

                        if (!context.CheckMode)
                        {
                            proxyManager.RemoveRoute(name);
                            context.WriteLine(Verbosity.Trace, $"Route [{name}] deleted.");
                        }

                        context.Changed = true;
                    }
                    else
                    {
                        context.WriteLine(Verbosity.Trace, $"Check route [{name}] does not exist.");
                    }
                    break;

                case "present":

                    context.WriteLine(Verbosity.Trace, $"Parsing [route]");

                    if (!context.Arguments.TryGetValue<JObject>("route", out var routeObject))
                    {
                        throw new ArgumentException($"[route] module argument is required.");
                    }

                    var routeText = routeObject.ToString();

                    context.WriteLine(Verbosity.Trace, "Parsing route");

                    var newRoute = ProxyRoute.Parse(routeText, strict: true);

                    context.WriteLine(Verbosity.Trace, "Route parsed successfully");

                    // Use the route name argument if the deserialized route doesn't
                    // have a name.  This will make it easier on operators because 
                    // they won't need to specify the name twice.

                    if (string.IsNullOrWhiteSpace(newRoute.Name))
                    {
                        newRoute.Name = name;
                    }

                    // Ensure that the route name passed as an argument and the
                    // name within the route definition match.

                    if (!string.Equals(name, newRoute.Name, StringComparison.InvariantCultureIgnoreCase))
                    {
                        throw new ArgumentException($"The [name={name}] argument and the route's [{nameof(ProxyRoute.Name)}={newRoute.Name}] property are not the same.");
                    }

                    context.WriteLine(Verbosity.Trace, "Route name matched.");

                    // Validate the route.

                    context.WriteLine(Verbosity.Trace, "Validating route.");

                    var proxySettings = proxyManager.GetSettings();

                    var validationContext = new ProxyValidationContext(name, proxySettings);

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

                    context.WriteLine(Verbosity.Trace, "Reading cluster certificates.");

                    using (var vault = NeonClusterHelper.OpenVault(Program.ClusterLogin.VaultCredentials.RootToken))
                    {
                        // List the certificate key/names and then fetch each one
                        // to capture details like the expiration date and covered
                        // host names.

                        foreach (var certName in vault.ListAsync("neon-secret/cert").Result)
                        {
                            context.WriteLine(Verbosity.Trace, $"Reading: {certName}");

                            var certificate = vault.ReadJsonAsync<TlsCertificate>(NeonClusterHelper.GetVaultCertificateKey(certName)).Result;

                            validationContext.Certificates.Add(certName, certificate);
                        }
                    }

                    context.WriteLine(Verbosity.Trace, $"[{validationContext.Certificates.Count}] cluster certificates downloaded.");

                    // Actually perform the route validation.

                    newRoute.Validate(validationContext);

                    if (validationContext.HasErrors)
                    {
                        context.WriteLine(Verbosity.Trace, $"[{validationContext.Errors.Count}] Route validation errors.");

                        foreach (var error in validationContext.Errors)
                        {
                            context.WriteErrorLine(error);
                        }

                        context.Failed = true;
                        return;
                    }

                    context.WriteLine(Verbosity.Trace, "Route validation completed.");

                    // Try reading any existing route with this name and then determine
                    // whether the two versions of the route are actually different. 

                    context.WriteLine(Verbosity.Trace, $"Looking for an existing route");

                    var existingRoute = proxyManager.GetRoute(name);

                    if (existingRoute != null)
                    {
                        context.WriteLine(Verbosity.Trace, $"Route exists.  Checking for differences.");

                        context.Changed = !NeonHelper.JsonEquals(newRoute, existingRoute);

                        if (context.Changed)
                        {
                            context.WriteLine(Verbosity.Trace, $"Routes are different.");
                        }
                        else
                        {
                            if (force)
                            {
                                context.Changed = true;
                                context.WriteLine(Verbosity.Trace, $"Routes are the same but since [force=true] we're going to update anyway.");
                            }
                            else
                            {
                                context.WriteLine(Verbosity.Info, $"Routes are the same.  No need to update.");
                            }
                        }
                    }
                    else
                    {
                        context.Changed = true;
                        context.WriteLine(Verbosity.Trace, $"Route [name={name}] does not exist.");
                    }

                    if (context.Changed)
                    {
                        context.WriteLine(Verbosity.Trace, $"Updating route.");
                        proxyManager.PutRoute(newRoute);
                        context.WriteLine(Verbosity.Info, $"Route updated.");
                    }

                    break;

                default:

                    throw new ArgumentException($"[state={state}] is not one of the valid choices: [absent] or [present].");
            }
        }
    }
}
