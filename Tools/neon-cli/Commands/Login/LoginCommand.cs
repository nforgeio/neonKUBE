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

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>login</b> command.
    /// </summary>
    [Command]
    public class LoginCommand : CommandBase
    {
        private const string usage = @"
Manages Kubernetes contexts for the user on the local workstation.

USAGE:

    neon login              USER@CLUSTER[/NAMESPACE]
    neon login export       USER@CLUSTER[/NAMESPACE] [PATH]
    neon login import       PATH
    neon login list|ls
    neon login delete       USER@CLUSTER[/NAMESPACE]

    neon logout

ARGUMENTS:

    PATH                        - Path to an exported login file.
    USER@CLUSTER[/NAMESPACE]    - Kubernetes user, cluster and optional namespace
";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "login" };

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
            if (commandLine.HasHelpOption || commandLine.Arguments.Length == 0)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            Console.Error.WriteLine();

            var orgContext = KubeHelper.CurrentContext;

            try
            {
                var server = commandLine.Arguments.First();

                if (!server.StartsWith("https://"))
                {
                    server = $"https://{server}";
                }

                var serverUri = new Uri(server);
                var clusterId = serverUri.Host.Split('.').FirstOrDefault();
                var ssoUri    = new Uri($"https://{ClusterHost.Sso}.{serverUri.Host}");

                var result = await KubeHelper.LoginOidcAsync(
                    authority: ssoUri.ToString(),
                    clientId:  KubeConst.NeonSsoPublicClientId,
                    scopes:    new string[] { "openid", "email", "profile", "groups", "offline_access", "audience:server:client_id:neon-sso" });
                
                ClusterInfo clusterInfo;
                GlauthUser  registryUser = null;

                using var store = new X509Store(StoreName.CertificateAuthority,StoreLocation.CurrentUser);

                using (var k8s = new Kubernetes(new KubernetesClientConfiguration()
                    {
                        AccessToken   = result.AccessToken,
                        SslCaCerts    = store.Certificates,
                        SkipTlsVerify = false,
                        Host          = serverUri.ToString(),
                    },
                    new KubernetesRetryHandler()))
                {
                    var configMap = await k8s.CoreV1.ReadNamespacedConfigMapAsync(KubeConfigMapName.ClusterInfo, KubeNamespace.NeonStatus);

                    clusterInfo = TypedConfigMap<ClusterInfo>.From(configMap).Data;

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
                var config         = KubeHelper.Config;
                var newContextName = $"{userName}@{clusterInfo.Name}";

                Console.WriteLine($"Login: {newContextName}...");

                var configCluster = config.Clusters.Where(cluster => cluster.Name == clusterInfo.Name).FirstOrDefault();

                var clusterProperties = 
                    new KubeConfigClusterConfig()
                    {
                        Server                = serverUri.ToString(),
                        InsecureSkipTlsVerify = false
                    };

                if (configCluster == null)
                {
                    config.Clusters.Add(
                        new KubeConfigCluster()
                        {
                            Name       = clusterInfo.Name,
                            Config = clusterProperties
                        });
                }
                else
                {
                    configCluster.Config = clusterProperties;
                }

                var configUser   = config.Users.Where(user => user.Name == newContextName).FirstOrDefault();
                var authProvider = new KubeConfigAuthProvider() { Name = "oidc" };

                authProvider.Config["client-id"]      = KubeConst.NeonSsoPublicClientId;
                authProvider.Config["idp-issuer-url"] = ssoUri.ToString().TrimEnd('/');
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
                            Name   = newContextName,
                            Config = userConfig,
                        });
                }
                else
                {
                    configUser.Config = userConfig;
                }

                var configContext = config.Contexts.Where(context => context.Name == newContextName).FirstOrDefault();

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
                            Name       = newContextName,
                            Config = contextProperties
                        });
                }
                else
                {
                    configContext.Config = contextProperties;
                }

                config.CurrentContext = newContextName;

                config.Save();

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
                                    $"{ClusterHost.HarborRegistry}.{serverUri.Host}",
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
                    }
                }
            }
            catch (Exception e)
            {
                KubeHelper.SetCurrentContext(orgContext?.Name);

                Console.Error.WriteLine($"*** ERROR: logging into cluster: {NeonHelper.ExceptionError(e)}");
                Program.Exit(1);
            }

            Console.WriteLine();
            Console.WriteLine($"Logged into: {commandLine.Arguments.First()}");

            await Task.CompletedTask;
        }
    }
}
