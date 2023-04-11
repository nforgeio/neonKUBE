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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using k8s;
using Newtonsoft;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Kube;
using Neon.Kube.Proxy;

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

            var currentContext = KubeHelper.CurrentContext;

            try
            {
                var newContextName = KubeContextName.Parse(commandLine.Arguments.First());

                // Ensure that the new context exists.

                if (KubeHelper.Config.GetContext(newContextName) == null)
                {
                    Console.Error.WriteLine($"*** Context [{newContextName}] not found.");
                    Program.Exit(1);
                }

                // Check whether we're already logged into the cluster.

                if (KubeHelper.CurrentContext != null && newContextName == KubeContextName.Parse(KubeHelper.CurrentContext.Name))
                {
                    Console.Error.WriteLine($"*** You are already logged into: {newContextName}");
                    Program.Exit(0);
                }

                // Logout of the current cluster.

                if (currentContext != null)
                {
                    Console.Error.WriteLine($"Logout: {currentContext.Name}...");
                    KubeHelper.SetCurrentContext((string)null);
                }

                // Log into the new context and then send a simple command to ensure
                // that cluster is ready.

                var orgContext = KubeHelper.CurrentContext;

                KubeHelper.SetCurrentContext(newContextName);
                Console.WriteLine($"Login: {newContextName}...");

                try
                {
                    using (var k8s = new Kubernetes(KubernetesClientConfiguration.BuildConfigFromConfigFile(KubeHelper.KubeConfigPath), new KubernetesRetryHandler()))
                    {
                        await k8s.CoreV1.ListNamespaceAsync();
                    }

                    var login      = KubeHelper.GetClusterLogin(KubeHelper.CurrentContextName);
                    var sshKeyPath = Path.Combine(NeonHelper.UserHomeFolder, ".ssh", KubeHelper.CurrentContextName.ToString());

                    Directory.CreateDirectory(Path.GetDirectoryName(sshKeyPath));
                    File.WriteAllText(sshKeyPath, login.SshKey.PrivatePEM);

                    if (!string.IsNullOrEmpty(NeonHelper.DockerCli))
                    {
                        Console.WriteLine($"Login: Docker to Harbor...");

                        NeonHelper.Execute(NeonHelper.DockerCli,
                            new object[]
                            {
                                "login",
                                $"{ClusterHost.HarborRegistry}.{login.ClusterDomain}",
                                "--username",
                                "root",
                                "--password-stdin"
                            },
                            input: new StringReader(login.SsoPassword));
                    }
                }
                catch (Exception e)
                {
                    KubeHelper.SetCurrentContext(orgContext?.Name);

                    Console.WriteLine("*** ERROR: Cluster is not responding.");
                    Console.WriteLine();
                    Console.WriteLine(NeonHelper.ExceptionError(e));
                    Console.WriteLine();
                    Program.Exit(1);
                }

            }
            catch (FormatException)
            {
                var server    = commandLine.Arguments.First();
                var uri       = new Uri(server);
                var clusterId = uri.Host.Split('.').FirstOrDefault();
                var ssoHost   = uri.Host;

                if (!ssoHost.StartsWith(ClusterHost.Sso))
                {
                    ssoHost = $"{ClusterHost.Sso}.{uri.Host}";
                }

                var result = await KubeHelper.LoginOidcAsync(
                    authority: $"https://{ssoHost}",
                    clientId:  ClusterConst.NeonSsoPublicClientId,
                    scopes:    new string[] { "openid", "email", "profile", "groups", "offline_access", "audience:server:client_id:neon-sso" });

                var user     = result.User;
                var userName = user.Identity.Name.Split("via").First().Trim();
                var config   = KubeHelper.Config;

                if (!config.Clusters.Any(cluster => cluster.Properties.Server == server))
                {
                    config.Clusters.Add(new KubeConfigCluster()
                    {
                        Name       = clusterId,
                        Properties = new KubeConfigClusterProperties()
                        {
                            Server                = $"{server}:6443",
                            InsecureSkipTlsVerify = true
                        }
                    });
                }

                if (!config.Users.Any(user => user.Name == $"{userName}@{clusterId}"))
                {
                    config.Users.Add(new KubeConfigUser()
                    {
                        Name       = $"{userName}@{clusterId}",
                        Properties = new KubeConfigUserProperties()
                        {
                            Token = result.AccessToken
                        }
                    });
                }

                if (!config.Contexts.Any(context => context.Name == $"{userName}@{clusterId}"))
                {
                    config.Contexts.Add(new KubeConfigContext()
                    {
                        Name       = $"{userName}@{clusterId}",
                        Properties = new KubeConfigContextProperties
                        {
                            Cluster = clusterId,
                            User    = $"{userName}@{clusterId}"
                        }
                    });
                }

                config.CurrentContext = $"{userName}@{clusterId}";

                config.Save();
            }
            catch (Exception)
            {
                Console.Error.WriteLine($"*** ERROR: Cannot log into cluster.");
                Program.Exit(1);
            }

            Console.WriteLine();
            Console.WriteLine($"Now logged into: {commandLine.Arguments.First()}");

            await Task.CompletedTask;
        }
    }
}
