//-----------------------------------------------------------------------------
// FILE:	    LoginCommand.cs
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using k8s;
using Newtonsoft;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Kube;
using Neon.Kube.Config;
using Neon.Kube.Proxy;
using Neon.Kube.Glauth;
using System.Diagnostics.Contracts;
using Neon.Kube.ClusterDef;
using Neon.Kube.Deployment;
using System.Diagnostics.Eventing.Reader;
using Windows.AI.MachineLearning;
using System.Configuration;

// $todo(jefflill): This implementation is incomplete.
//
// This command supports specifying a Kubernetes context name or cluster
// hostname/domain as a command line argument, although we're only documenting
// the context name scenario until we complete the overall SSO auth feature.
//
// The cluster hostname will not include the [neon-sso] label because that
// is implied.  After talking to @marcusbooyah, I believe the conclusion
// was that we're always going to be authenticating through the cluster
// and the cluster will handle any indirection to upstream providers like
// GitHub, NEONCLOUD, etc.
//
// The command will need to disambiguate between context names or cluster
// hostname/domains.  If the parameter includes the [https://] URI scheme,
// we'll assume that it specifies the cluster domain.
//
// Otherwise, wWe're going to handle this by searching the existing contexts
// looking for one whose name matches, setting that as the current context
// if found.  If there's no match, we'll assume that the parameter specifies
// the cluster domain.
//
// Finally, the [-n] or [--namespace] options allow the user to set the
// current namespace when changing the context or update the namespace
// in the current context, when the context/domain parameter is missing.
//
// We may also want to allow [--user] and [--password] parameters.

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>login</b> command.
    /// </summary>
    [Command]
    public class LoginCommand : CommandBase
    {
        private const string usage = @"
Manages NEONKUBE contexts for the user on the local workstation.

USAGE:

    neon login [OPTIONS] CONTEXT-NAME
    neon login [OPTIONS] https://CLUSTER-DOMAIN
    neon login [OPTIONS] --sso CLUSTER-DOMAIN
    neon login --namespace=NAMESPACE
    neon login -n=NAMESPACE

    neon login delete|rm [--force] CONTEXT-NAME
    neon login export [--context=CONTEXT-NAME] [PATH]
    neon login import [--no-login] [--force] PATH
    neon login list|ls

    neon logout

ARGUMENTS:

    CONTEXT-NAME    - Specifies the name of an existing NEONKUBE context on
                      the workstation.  These are typically formatted like:

                          USER@CLUSTER-NAME

    CLUSTER-DOMAIN  - Specifies the cluster domain for single-sign-on (SSO)
                      authentication with the cluster

OPTIONS:

    --namespace|-n=NAMESPACE    - Optionally specifies the Kubernetes namespace

    --output=json|yaml          - Optionally specifies the format to print the
    -o=json|yaml                  current context and namespace.

    --show                      - Optionally prints the current context and
                                  namespace
 
    --sso                       - Perform SSO authentication against the cluster

REMARKS:

This command is used to switch between contexts on the local workstation.
One cluster and subcommands are available for other operations.

Scenarios:
----------

Select a NEONKUBE context on the workstation so that subsequent commands
will operate on the related NEONKUBE cluster.

    neon login [OPTIONS] CONFIGREF

Use single-sign-on (SSO) authentication to log into a new cluster for
which you don't already have that cluster's context information.  This
command displays a browser window from the target cluster that prompts
for your cluster SSO credentials.  You'll need the cluster domain and
can execute the command two ways:

    neon login [OPTIONS] https://CLUSTERDOMAIN
    neon login [OPTIONS] --sso CLUSTER-DOMAIN

You may use the [--namespace=NAMESPACE] or [-n=NAMESPACE] options by
themselves to switch the namespace for the current NEONKUBE cluster
or when switching contexts to set the current namespace afterwards.
";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "login" };

        /// <inheritdoc/>
        public override string[] ExtendedOptions => new string[]
        {
            "--current",
            "-c",
            "--namespace",
            "-n",
            "--sso",
            "--output",
            "-o"
        };

        /// <inheritdoc/>
        public override bool NeedsHostingManager => true;

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override async Task RunAsync(CommandLine commandLine)
        {
            if (commandLine.HasHelpOption)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            var orgContext = KubeHelper.CurrentContext;

            try
            {
                commandLine.DefineOption("--namespace", "-n");

                var contextOrClusterDomain = commandLine.Arguments.FirstOrDefault();
                var sso                    = commandLine.HasOption("--sso");
                var contextName            = (string)null;
                var clusterDomain          = (string)null;
                var @namespace             = commandLine.GetOption("--namespace");
                var outputFormat           = Program.GetOutputFormat(commandLine);

                //-------------------------------------------------------------
                // Just print the current context info when we're not changing
                // the namespace.

                if (contextOrClusterDomain == null && string.IsNullOrEmpty(@namespace))
                {
                    PrintContext(outputFormat);
                    return;
                }

                //-------------------------------------------------------------
                // Check the namespace if specified.

                if (!string.IsNullOrEmpty(@namespace) && !ClusterDefinition.DnsNameRegex.IsMatch(@namespace))
                {
                    Console.Error.WriteLine($"Invalid namespace: {@namespace}");
                    Program.Exit(-1);
                }

                //-------------------------------------------------------------
                // Switch contexts if there's a CONTEXTREF argument.

                if (contextOrClusterDomain != null)
                {
                    // Disambiguate between cluster context and cluster domain.

                    if (sso || contextOrClusterDomain.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase))
                    {
                        clusterDomain = contextOrClusterDomain;

                        if (clusterDomain.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase))
                        {
                            clusterDomain = clusterDomain.Substring("https://".Length);
                        }
                    }
                    else
                    {
                        if (KubeHelper.KubeConfig.GetContext(contextOrClusterDomain) == null)
                        {
                            clusterDomain = contextOrClusterDomain;
                        }
                        else
                        {
                            contextName = contextOrClusterDomain;
                        }
                    }

                    if (clusterDomain != null && !ClusterDefinition.DnsNameRegex.IsMatch(clusterDomain))
                    {
                        Console.Error.WriteLine($"*** ERROR: Invalid cluster hostname: {clusterDomain}");
                        Program.Exit(-1);
                    }

                    if (clusterDomain != null)
                    {
                        await SsoLoginAsync(clusterDomain);
                    }
                    else if (contextName != null)
                    {
                        await SetContextAsync(contextName, @namespace);
                    }
                }
                else
                {
                    await SetContextAsync(contextName, @namespace);
                }

                PrintContext(outputFormat);
            }
            catch (ProgramExitException)
            {
                throw;
            }
            catch (Exception e)
            {
                KubeHelper.SetCurrentContext(orgContext?.Name);

                Console.Error.WriteLine($"*** ERROR: Logging into cluster: {NeonHelper.ExceptionError(e)}");
                Program.Exit(1);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Logs into the cluster via SSO.
        /// </summary>
        /// <param name="clusterDomain">Specifies the cluster domain.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task SsoLoginAsync(string clusterDomain)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(clusterDomain), nameof(clusterDomain));

            var ssoHost = $"{ClusterHost.Sso}.{clusterDomain}";
            var ssoUri  = $"https://{ssoHost}";

            var result = await KubeHelper.LoginOidcAsync(
                authority: ssoUri,
                clientId:  KubeConst.NeonSsoPublicClientId,
                scopes:    new string[] { "openid", "email", "profile", "groups", "offline_access", "audience:server:client_id:neon-sso" });

            ClusterInfo         clusterInfo;
            ClusterDeployment   clusterDeployment;
            GlauthUser          registryUser = null;

            using var store = new X509Store(StoreName.CertificateAuthority,StoreLocation.CurrentUser);

            using (var k8s = new Kubernetes(new KubernetesClientConfiguration()
            {
                AccessToken   = result.AccessToken,
                SslCaCerts    = store.Certificates,
                SkipTlsVerify = false,
                Host          = ssoHost,
            },
                new KubernetesRetryHandler()))
            {
                clusterInfo       = (await k8s.CoreV1.ReadNamespacedTypedConfigMapAsync<ClusterInfo>(KubeConfigMapName.ClusterInfo, KubeNamespace.NeonStatus)).Data;
                clusterDeployment = (await k8s.CoreV1.ReadNamespacedTypedConfigMapAsync<ClusterDeployment>(KubeConfigMapName.ClusterDeployment, KubeNamespace.NeonStatus)).Data;

                try
                {
                    registryUser = await KubeHelper.GetClusterLdapUserAsync(k8s, "root");
                }
                catch
                {
                    // We're going to ignore any errors here and drop through to
                    // the code below to see if it can do something else.
                }
            }

            var user           = result.User;
            var userName       = user.Identity.Name.Split("via").First().Trim();
            var config         = KubeHelper.KubeConfig;
            var newContextName = $"{userName}@{clusterInfo.Name}";

            // $todo(jefflill): We may to revist this:

            // Convert any '@' and '/' characters in the user name to dashes.  We need to
            // do this because these characters have special meanings in context names.

            userName = userName.Replace('@', '-');
            userName = userName.Replace('/', '-');

            Console.WriteLine($"Login: {newContextName}...");

            // Add update the config cluster.

            var configCluster = config.GetCluster(clusterInfo.Name);

            var clusterConfig =
                new KubeConfigClusterConfig()
                {
                    Server                = clusterDomain,
                    InsecureSkipTlsVerify = false
                };

            if (configCluster == null)
            {
                config.Clusters.Add(
                    new KubeConfigCluster()
                    {
                        Name    = clusterInfo.Name,
                        Cluster = clusterConfig
                    });
            }
            else
            {
                configCluster.Cluster = clusterConfig;
            }

            configCluster.IsNeonDesktop      = clusterInfo.IsDesktop;
            configCluster.IsNeonKube         = true;
            configCluster.HostingEnvironment = clusterInfo.HostingEnvironment;
            configCluster.Hosting            = clusterDeployment.Hosting;
            configCluster.HostingNamePrefix  = clusterInfo.HostingNamePrefix;

            // Add/update the config user.

            var configUser   = config.GetUser(newContextName);
            var authProvider = new KubeConfigAuthProvider() { Name = "oidc" };

            authProvider.Config["client-id"]      = KubeConst.NeonSsoPublicClientId;
            authProvider.Config["idp-issuer-url"] = ssoUri.TrimEnd('/');
            authProvider.Config["refresh-token"]  = result.RefreshToken;
            authProvider.Config["id-token"]       = result.IdentityToken;

            var userConfig = new KubeConfigUserConfig()
            {
                AuthProvider = authProvider
            };

            if (configUser == null)
            {
                config.Users.Add(
                    new KubeConfigUser()
                    {
                        Name = newContextName,
                        User = userConfig,
                    });
            }
            else
            {
                configUser.User = userConfig;
            }

            // Add/update the config context.

            var configContext     = config.GetContext(newContextName);
            var contextProperties = new KubeConfigContextConfig
            {
                Cluster = clusterInfo.Name,
                User    = newContextName
            };

            if (configContext == null)
            {
                config.Contexts.Add(
                    new KubeConfigContext()
                    {
                        Name    = newContextName,
                        Context = contextProperties
                    });
            }
            else
            {
                configContext.Context = contextProperties;
            }

            // Save kubeconfig changes.

            config.CurrentContext = newContextName;
            config.Save();

            // Log Docker into the cluster's Harbor service.

            if (registryUser != null)
            {
                try
                {
                    if (!string.IsNullOrEmpty(NeonHelper.DockerCli))
                    {
                        Console.WriteLine($"Login: Docker to Harbor...");

                        NeonHelper.Execute(NeonHelper.VerifiedDockerCli,
                            new object[]
                            {
                                "login",
                                $"{ClusterHost.HarborRegistry}.{clusterDomain}",
                                "--username",
                                "root",
                                "--password-stdin"
                            },
                            input: new StringReader(registryUser.Password));
                    }
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"*** ERROR: logging into cluster registry: {NeonHelper.ExceptionError(e)}");
                    Program.Exit(1);
                }
            }
        }

        /// <summary>
        /// Changes the current Kubernetes context.
        /// </summary>
        /// <param name="contextName">
        /// Specifies the target context name.  This may be <c>null</c> when
        /// <paramref name="namespace"/> is passed.
        /// </param>
        /// <param name="namespace">Optionally specifies the new namespace.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task SetContextAsync(string contextName, string @namespace = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(contextName) || !string.IsNullOrEmpty(@namespace), nameof(contextName));

            var config = KubeHelper.KubeConfig;

            config.Reload();

            if (!string.IsNullOrEmpty(contextName))
            {
                config.CurrentContext = contextName;
            }

            if (string.IsNullOrEmpty(config.CurrentContext) && string.IsNullOrEmpty(contextName))
            {
                Console.Error.WriteLine("*** ERROR: Cannot switch namespace because is no current NEONKUBE cluster.");
                Program.Exit(1);
            }

            var context = config.GetContext(!string.IsNullOrEmpty(contextName) ? contextName : config.CurrentContext);

            if (context == null)
            {
                Console.Error.WriteLine($"*** ERROR: Cannot find NEONKUBE cluster: {contextName}");
                Program.Exit(1);
            }

            if (!string.IsNullOrEmpty(@namespace))
            {
                context.Context.Namespace = @namespace;
            }

            config.Save();

            await Task.CompletedTask;
        }

        /// <summary>
        /// Prints a kubeconfig context to the output using the specified format.
        /// </summary>
        /// <param name="outputFormat">Specifies the output format.</param>
        private void PrintContext(OutputFormat? outputFormat)
        {
            var config = KubeHelper.KubeConfig;

            config.Reload();

            var context = config.Context;

            if (!outputFormat.HasValue)
            {
                if (context == null)
                {
                    Console.WriteLine();
                    Console.WriteLine($"You are not logged into a NEONKUBE cluster");
                }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine($"Logged into: {context.Name} ({context.Context.Namespace ?? "default"})");
                }

                return;
            }

            object outputObject;

            if (context != null)
            {
                outputObject = new { context = context.Name, @namespace = context.Context.Namespace ?? "default" };
            }
            else
            {
                outputObject = null;
            }

            switch (outputFormat.Value)
            {
                case OutputFormat.Json:

                    Console.WriteLine(NeonHelper.JsonSerialize(outputObject, Formatting.Indented));
                    break;

                case OutputFormat.Yaml:

                    Console.WriteLine(NeonHelper.YamlSerialize(outputObject));
                    break;

                default:

                    throw new NotImplementedException();
            }
        }
    }
}
