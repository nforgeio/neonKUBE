//-----------------------------------------------------------------------------
// FILE:        ContainerRegistryController.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using k8s;
using k8s.Models;

using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Resources.Cluster;
using Neon.Operator.Attributes;
using Neon.Operator.Controllers;
using Neon.Operator.Rbac;
using Neon.Operator.Util;
using Neon.Retry;
using Neon.Tasks;

using Newtonsoft.Json;

using OpenTelemetry.Trace;

using Prometheus;

using Tomlyn;

namespace NeonNodeAgent
{
    /// <summary>
    /// <para>
    /// Manages <see cref="V1NeonContainerRegistry"/> resources on the Kubernetes API Server.
    /// </para>
    /// <note>
    /// This controller relies on a lease named like <b>neon-node-agent.containerregistry-NODENAME</b>
    /// where <b>NODENAME</b> is the name of the node where the <b>neon-node-agent</b> operator
    /// is running.  This lease will be persisted in the <see cref="KubeNamespace.NeonSystem"/> 
    /// namespace and will be used to elect a leader for the node in case there happens to be two
    /// agents running on the same node for some reason.
    /// </note>
    /// </summary>
    /// <remarks>
    /// <para>
    /// This operator controller is responsible for managing the upstream CRI-O container registry
    /// configuration located at <b>/etc/containers/registries.conf.d/00-neon-cluster.conf</b>.
    /// on the host node.
    /// </para>
    /// <note>
    /// The host node file system is mounted into the container at: <see cref="Node.HostMount"/>.
    /// </note>
    /// <para>
    /// This works by monitoring by:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// Monitoring the <see cref="V1NeonContainerRegistry"/> resources for potential changes
    /// and then performing the steps below a change is detected.
    /// </item>
    /// <item>
    /// Regenerate the contents of the <b>/etc/containers/registries.conf.d/00-neon-cluster.conf</b> file.
    /// </item>
    /// <item>
    /// Compare the contents of the current file with the new generated config.
    /// </item>
    /// <item>
    /// If the contents differ, update the file on the host's filesystem and then signal
    /// CRI-O to reload its configuration.
    /// </item>
    /// </list>
    /// <note>
    /// This controller provides limited functionality when running on Windows to facilitate debugging.
    /// Node tasks on the host node will be simulated in this case by simply doing nothing.
    /// </note>
    /// </remarks>
    [RbacRule<V1NeonContainerRegistry>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster)]
    [ResourceController]
    public class ContainerRegistryController : ResourceControllerBase<V1NeonContainerRegistry>
    {
        /// <inheritdoc/>
        public new string LeaseName { get; } = $"{KubeService.NeonNodeAgent}.containerregistry-{Node.Name}";

        //---------------------------------------------------------------------
        // Local types

        /// <summary>
        /// Used to serialize the contents of a login file.
        /// </summary>
        internal class LoginData
        {
            [JsonProperty(PropertyName = "location", Required = Required.Always)]
            public string Location { get; set; }

            [JsonProperty(PropertyName = "username", Required = Required.Always)]
            public string Username { get; set; }

            [JsonProperty(PropertyName = "updatedUtc", Required = Required.Always)]
            public DateTime UpdatedUtc { get; set; }
        }

        /// <summary>
        /// Represents a container registry login file as described here: https://github.com/nforgeio/neonKUBE/issues/1591
        /// </summary>
        internal class LoginFile
        {
            //-----------------------------------------------------------------
            // Static members

            /// <summary>
            /// Reads a login file.  Note that this does not throw exceptions and instead deletes
            /// the existing file and returns <c>null</c>.
            /// </summary>
            /// <param name="path">Path to the file.</param>
            /// <returns>The <see cref="LoginFile"/> or <c>null</c> when it is missing or invalid.</returns>
            public static LoginFile Read(string path)
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path), nameof(path));

                var loginFile = new LoginFile()
                {
                    Path = path
                };

                try
                {
                    var loginData = NeonHelper.JsonDeserialize<LoginData>(File.ReadAllText(path));

                    loginFile.Sha256     = System.IO.Path.GetFileNameWithoutExtension(path);
                    loginFile.Location   = loginData.Location;
                    loginFile.Username   = loginData.Username;
                    loginFile.UpdatedUtc = loginData.UpdatedUtc;
                }
                catch
                {
                    NeonHelper.DeleteFile(path);
                    return null;
                }

                return loginFile;
            }

            /// <summary>
            /// Creates a new login file instance with parameters passed, but does not persist
            /// the file.  Use <see cref="Write()"/> to do this.
            /// </summary>
            /// <param name="loginFolder">Specifies the path to the login folder.</param>
            /// <param name="registryUri">Specifies the upstream registry URI.</param>
            /// <param name="userName">Specifies the user name used to login.</param>
            /// <param name="password">Specifies the password.</param>
            /// <returns>The <see cref="LoginFile"/>.</returns>
            public static LoginFile Create(string loginFolder, string registryUri, string userName, string password)
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(loginFolder), nameof(loginFolder));
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(registryUri), nameof(registryUri));
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(userName), nameof(userName));
                Covenant.Requires<ArgumentNullException>(password != null, nameof(password));

                var loginFile = new LoginFile()
                {
                    Location = registryUri,
                    Username = userName,
                    Password = password,
                    Sha256   = CryptoHelper.ComputeSHA256String($"{registryUri},{userName},{password}")
                };

                loginFile.Path = System.IO.Path.Combine(loginFolder, $"{loginFile.Sha256}.login");

                return loginFile;
            }

            //-----------------------------------------------------------------
            // Instance members.

            /// <summary>
            /// Private constructor.
            /// </summary>
            private LoginFile()
            {
            }

            /// <summary>
            /// Returns the path to the login file.
            /// </summary>
            public string Path { get; private set; }

            /// <summary>
            /// Returns the SHA-256 for the upstream registry URI and the credentials.
            /// </summary>
            public string Sha256 { get; private set; }

            /// <summary>
            /// Returns the upstream registry URI.
            /// </summary>
            public string Location { get; private set; }

            /// <summary>
            /// Returns the username used to login to the registry.
            /// </summary>
            public string Username { get; private set; }

            /// <summary>
            /// The password for instances created via <see cref="Create(string, string, string, string)"/>.
            /// </summary>
            public string Password { get; set; }

            /// <summary>
            /// Returns the time when the operator last logged into the container registry.
            /// </summary>
            public DateTime UpdatedUtc { get; private set; }

            /// <summary>
            /// Deletes the login file if mit exists.
            /// </summary>
            public void Delete()
            {
                NeonHelper.DeleteFile(Path);
            }

            /// <summary>
            /// Creates or updates the login file.
            /// </summary>
            public void Write()
            {
                this.UpdatedUtc = DateTime.UtcNow;

                var loginData = new LoginData()
                {
                    Location   = Location,
                    Username   = Username,
                    UpdatedUtc = UpdatedUtc
                };

                File.WriteAllText(Path, NeonHelper.JsonSerialize(loginData, Formatting.Indented));
            }
        }

        //---------------------------------------------------------------------
        // Static members

        private const string podmanPath = "/usr/bin/podman";

        private static readonly ILogger     log             = TelemetryHub.CreateLogger<ContainerRegistryController>();
        internal static string              configMountPath { get; } = LinuxPath.Combine(Node.HostMount, "etc/containers/registries.conf.d/00-neon-cluster.conf");
        private static readonly string      metricsPrefix   = "neonnodeagent";
        private static TimeSpan             reloginInterval;
        private static TimeSpan             reloginMaxRandomInterval;

        // Paths to relevant folders in the host file system.

        private static readonly string      hostNeonRunFolder;
        private static readonly string      hostContainerRegistriesFolder;

        // Metrics counters

        private static readonly Counter configUpdateCounter = Metrics.CreateCounter($"{metricsPrefix}_containerregistry_node_updated", "Number of node config updates.");
        private static readonly Counter loginErrorCounter   = Metrics.CreateCounter($"{metricsPrefix}_containerregistry_login_error", "Number of failed container registry logins.");

        /// <summary>
        /// Static constructor.
        /// </summary>
        static ContainerRegistryController()
        {
            if (NeonHelper.IsLinux)
            {
                hostNeonRunFolder             = Path.Combine(Node.HostMount, KubeNodeFolder.NeonRun.Substring(1));
                hostContainerRegistriesFolder = Path.Combine(hostNeonRunFolder, "container-registries");
            }
            else
            {
                // Configure a emulation directory on Windows.

                hostNeonRunFolder             = @"C:\Temp\neonkube";
                hostContainerRegistriesFolder = Path.Combine(hostNeonRunFolder, "container-registries");

                Directory.CreateDirectory(hostContainerRegistriesFolder);
            }
        }

        /// <summary>
        /// Starts the controller.
        /// </summary>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/>.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public override async Task StartAsync(IServiceProvider serviceProvider)
        {
            if (NeonHelper.IsLinux)
            {
                // Ensure that the [/var/run/neonkube/container-registries] folder exists on the node.

                var scriptPath = Path.Combine(Node.HostMount, $"tmp/node-agent-folder-{NeonHelper.CreateBase36Uuid()}.sh");
                var script =
$@"#!/bin/bash

set -euo pipefail

# Ensure that the nodetask runtime folders exist and have the correct permissions.

if [ ! -d {hostNeonRunFolder} ]; then

mkdir -p {hostNeonRunFolder}
chmod 700 {hostNeonRunFolder}
fi

if [ ! -d {hostContainerRegistriesFolder} ]; then

mkdir -p {hostContainerRegistriesFolder}
chmod 700 {hostContainerRegistriesFolder}
fi

# Remove this script.

rm $0
";
                File.WriteAllText(scriptPath, NeonHelper.ToLinuxLineEndings(script));
                try
                {
                    (await Node.BashExecuteCaptureAsync(scriptPath)).EnsureSuccess();
                }
                finally
                {
                    NeonHelper.DeleteFile(scriptPath);
                }
            }

            // Load the configuration settings.

            if (!TimeSpan.TryParse(Environment.GetEnvironmentVariable("CONTAINERREGISTRY_RELOGIN_INTERVAL"), out reloginInterval))
            {
                reloginInterval = TimeSpan.FromHours(24);
            }

            
            reloginMaxRandomInterval = reloginInterval.Divide(4);
        }
        
        //---------------------------------------------------------------------
        // Instance members

        private readonly IKubernetes k8s;

        /// <summary>
        /// Constructor.
        /// </summary>
        public ContainerRegistryController(
            IKubernetes k8s)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));

            this.k8s     = k8s;
        }

        /// <inheritdoc/>
        public async Task IdleAsync()
        {
            log.LogInformationEx("IDLE");
            await UpdateContainerRegistriesAsync();

            return;
        }

        /// <inheritdoc/>
        public override async Task<ResourceControllerResult> ReconcileAsync(V1NeonContainerRegistry resource)
        {
            await SyncContext.Clear;

            using var activity = TelemetryHub.ActivitySource?.StartActivity();

            Tracer.CurrentSpan?.AddEvent("reconcile", attributes => attributes.Add("resource", nameof(V1NeonContainerRegistry)));
            log?.LogInformationEx(() => $"Reconciling {resource.GetType().FullName} [{resource.Namespace()}/{resource.Name()}].");

            var crioConfigList = await k8s.CustomObjects.ListClusterCustomObjectAsync<V1CrioConfiguration>();

            V1CrioConfiguration crioConfig;
            if (crioConfigList.Items.IsEmpty())
            {
                crioConfig                 = new V1CrioConfiguration().Initialize();
                crioConfig.Metadata.Name   = KubeConst.ClusterCrioConfigName;
                crioConfig.Spec            = new V1CrioConfiguration.CrioConfigurationSpec();
                crioConfig.Spec.Registries = new List<KeyValuePair<string, V1NeonContainerRegistry.RegistrySpec>>();
            }
            else
            {
                crioConfig                   = crioConfigList.Items.Where(cfg => cfg.Metadata.Name == KubeConst.ClusterCrioConfigName).Single();
                crioConfig.Spec            ??= new V1CrioConfiguration.CrioConfigurationSpec();
                crioConfig.Spec.Registries ??= new List<KeyValuePair<string, V1NeonContainerRegistry.RegistrySpec>>();
            }

            if (crioConfig.Spec.Registries.IsEmpty())
            {
                crioConfig.Spec.Registries.Add(new KeyValuePair<string, V1NeonContainerRegistry.RegistrySpec>(resource.Uid(), resource.Spec));

                await k8s.CustomObjects.UpsertClusterCustomObjectAsync(body: crioConfig, name: crioConfig.Name());

                return null;
            }

            if (!crioConfig.Spec.Registries.Any(kvp => kvp.Key == resource.Uid()))
            {
                log?.LogInformationEx(() => $"Registry [{resource.Namespace()}/{resource.Name()}] deos not exist, adding.");

                var addPatch = OperatorHelper.CreatePatch<V1CrioConfiguration>();

                addPatch.Add(path => path.Spec.Registries, new KeyValuePair<string, V1NeonContainerRegistry.RegistrySpec>(resource.Uid(), resource.Spec));

                await k8s.CustomObjects.PatchClusterCustomObjectAsync<V1CrioConfiguration>(
                    patch: OperatorHelper.ToV1Patch<V1CrioConfiguration>(addPatch),
                    name:  crioConfig.Name());
            }
            else
            {
                log?.LogInformationEx(() => $"Registry [{resource.Namespace()}/{resource.Name()}] exists, checking for changes.");
                
                var registry = crioConfig.Spec.Registries.Where(kvp => kvp.Key == resource.Uid()).Single();

                if (registry.Value != resource.Spec)
                {
                    log?.LogInformationEx(() => $"Registry [{resource.Namespace()}/{resource.Name()}] changed, upserting.");
                 
                    crioConfig.Spec.Registries.Remove(registry);
                    crioConfig.Spec.Registries.Add(new KeyValuePair<string, V1NeonContainerRegistry.RegistrySpec>(resource.Uid(), resource.Spec));

                    var patch =  OperatorHelper.CreatePatch<V1CrioConfiguration>();

                    patch.Replace(path => path.Spec.Registries, crioConfig.Spec.Registries);

                    await k8s.CustomObjects.PatchClusterCustomObjectAsync<V1CrioConfiguration>(
                        patch: OperatorHelper.ToV1Patch<V1CrioConfiguration>(patch),
                        name: crioConfig.Name());
                }
            }

            log.LogInformationEx(() => $"RECONCILED: {resource.Name()}");

            return null;
        }

        /// <inheritdoc/>
        public override async Task DeletedAsync(V1NeonContainerRegistry resource)
        {
            await SyncContext.Clear;
            
            log.LogInformationEx(() => $"DELETED: {resource.Name()}");
        }

        /// <summary>
        /// Rebuilds the host node's <b>/etc/containers/registries.conf.d/00-neon-cluster.conf</b> file,
        /// using the container registries passed, signals CRI-O to reload any changes and also manages
        /// container registry logins.
        /// </summary>
        private async Task UpdateContainerRegistriesAsync()
        {
            var registries = (await k8s.CustomObjects.ListClusterCustomObjectAsync<V1NeonContainerRegistry>()).Items;

            // NOTE: Here's the documentation for the config file we're generating:
            //
            //      https://github.com/containers/image/blob/main/docs/containers-registries.conf.5.md
            //

            var sbRegistryConfig   = new StringBuilder();
            var sbSearchRegistries = new StringBuilder();

            // Configure any unqualified search registries.

            foreach (var registry in registries
                .Where(registry => registry.Spec.SearchOrder >= 0)
                .OrderBy(registry => registry.Spec.SearchOrder))
            {
                sbSearchRegistries.AppendWithSeparator($"\"{registry.Spec.Prefix}\"", ", ");
            }

            sbRegistryConfig.Append(
$@"unqualified-search-registries = [{sbSearchRegistries}]
");

            // Configure any container registries including the local cluster.

            foreach (var registry in registries)
            {
                sbRegistryConfig.Append(
$@"
[[registry]]
prefix   = ""{registry.Spec.Prefix}""
insecure = {NeonHelper.ToBoolString(registry.Spec.Insecure)}
blocked  = {NeonHelper.ToBoolString(registry.Spec.Blocked)}
");

                if (!string.IsNullOrEmpty(registry.Spec.Location))
                {
                    sbRegistryConfig.AppendLine($"location = \"{registry.Spec.Location}\"");
                }
            }

            if (NeonHelper.IsLinux)
            {
                // Read and parse the current configuration file to create list of the existing
                // configured upstream registries.

                var currentConfigText = string.Empty;

                if (File.Exists(configMountPath))
                {
                    currentConfigText = File.ReadAllText(configMountPath);

                    var currentConfig = Toml.Parse(currentConfigText);
                    var existingLocations = new List<string>();

                    foreach (var registryTable in currentConfig.Tables.Where(table => table.Name.Key.GetName() == "registry"))
                    {
                        var location = registryTable.Items.SingleOrDefault(key => key.Key.GetName() == "location")?.Value.GetValue();

                        if (!string.IsNullOrWhiteSpace(location))
                        {
                            existingLocations.Add(location);
                        }
                    }
                }
               
                // Convert the generated config to Linux line endings and then compare the new
                // config against what's already configured on the host node.  We'll rewrite the
                // host file and then signal CRI-O to reload its config when the files differ.

                var newConfigText = NeonHelper.ToLinuxLineEndings(sbRegistryConfig.ToString());

                if (currentConfigText != newConfigText)
                {
                    configUpdateCounter.Inc();

                    File.WriteAllText(configMountPath, newConfigText);
                    (await Node.ExecuteCaptureAsync("pkill", new object[] { "-HUP", "crio" })).EnsureSuccess();

                    // Wait a few seconds to give CRI-O a chance to reload its config.  This will
                    // help mitigate problems when managing logins below due to potential inconsistencies
                    // between CRI-O's currently loaded config and the new config we just saved.

                    await Task.Delay(TimeSpan.FromSeconds(15));
                }
            }

            //-----------------------------------------------------------------
            // We need to manage registry logins by logging into new registries,
            // logging out of deleted registries, relogging in with new credentials,
            // and periodically logging in with unchanged credentials to ensure that
            // we're actually logged in.  Here's how this works:
            //
            //      https://github.com/nforgeio/neonKUBE/issues/1591

            var retry = new LinearRetryPolicy(e => true, maxAttempts: 5, retryInterval: TimeSpan.FromSeconds(5));

            // Construct LoginFile instances for all specified upstream registries
            // that require credentials and add these to a dictionary keyed by SHA-256.

            var shaToRequiredLogins = new Dictionary<string, LoginFile>();

            foreach (var registry in registries.Where(registry => !string.IsNullOrEmpty(registry.Spec.Username)))
            {
                var loginFile = LoginFile.Create(hostContainerRegistriesFolder, registry.Spec.Location, registry.Spec.Username, registry.Spec.Password);

                shaToRequiredLogins.Add(loginFile.Sha256, loginFile);
            }

            // Read all existing login files on the node and add them to a dictionary
            // mapping their SHA-256s to the file.

            var shaToExistingLogins = new Dictionary<string, LoginFile>();

            foreach (var file in Directory.GetFiles(hostContainerRegistriesFolder, "*.login", SearchOption.TopDirectoryOnly))
            {
                var loginFile = LoginFile.Read(file);

                if (loginFile != null)
                {
                    shaToExistingLogins.Add(loginFile.Sha256, loginFile);
                }
            }

            // Look for any existing login files that are not present in the collection of
            // new logins.  These correspond to registries that have been deleted or whose
            // credentials have changed.  We're going to go ahead and log out of the related
            // registries and then delete these login files (we'll re-login with new
            // credentials below for the registries that weren't targeted for removal).

            foreach (var loginFile in shaToExistingLogins.Values
                .Where(login => !shaToRequiredLogins.ContainsKey(login.Sha256)))
            {
                try
                {
                    await retry.InvokeAsync(
                        async () =>
                        {
                            // Note that we're not ensuring success here because we may not be
                            // logged-in which is OK: we don't want to see that error.

                            log.LogInformationEx(() => $"{podmanPath} logout {loginFile.Location}");
                            
                            if (NeonHelper.IsLinux)
                            {
                                await Node.ExecuteCaptureAsync(podmanPath, new object[] { "logout", loginFile.Location });
                            }

                            loginFile.Delete();
                        });
                }
                catch (Exception e)
                {
                    loginErrorCounter.Inc();
                    log.LogErrorEx(e);
                }
            }

            // Look for any required logins that don't have an existing login file,
            // and then login the registry and then create the login file on success.

            foreach (var loginFile in shaToRequiredLogins.Values
                .Where(login => !shaToExistingLogins.ContainsKey(login.Sha256)))
            {
                try
                {
                    await retry.InvokeAsync(
                        async () =>
                        {
                            log.LogInformationEx(() => $"{podmanPath} login {loginFile.Location} --username {loginFile.Username} --password REDACTED");

                            if (NeonHelper.IsLinux)
                            {
                                (await Node.ExecuteCaptureAsync(podmanPath, new object[] { "login", loginFile.Location, "--username", loginFile.Username, "--password", loginFile.Password })).EnsureSuccess();
                            }
                        });

                    loginFile.Write();
                }
                catch (Exception e)
                {
                    loginErrorCounter.Inc();
                    log.LogErrorEx(e);
                }
            }

            //-----------------------------------------------------------------
            // Finally, we need to force a re-login for any existing logins that haven't
            // been explicitly logged into for a while.  Note that we're always going to
            // log into the local Harbor registry.

            foreach (var file in Directory.GetFiles(hostContainerRegistriesFolder, "*.login", SearchOption.TopDirectoryOnly))
            {
                // Read the next existing login file.

                var loginFile = LoginFile.Read(file);

                if (loginFile == null)
                {
                    continue;
                }

                // Update the login with the password from the corresponding container registry resource.

                var registry = registries.FirstOrDefault(registry => registry.Spec.Location == loginFile.Location);

                if (registry == null)
                {
                    log.LogWarningEx(() => $"Cannot locate [{nameof(V1NeonContainerRegistry)}] resource for [location={loginFile.Location}].");
                    continue;
                }

                loginFile.Password = registry.Spec.Password;

                // Perform the login.

                var scheduledLoginUtc = loginFile.UpdatedUtc + reloginInterval + NeonHelper.PseudoRandomTimespan(reloginMaxRandomInterval);

                if (DateTime.UtcNow <= scheduledLoginUtc || loginFile.Location == KubeConst.LocalClusterRegistryHostName)
                {
                    try
                    {
                        await retry.InvokeAsync(
                            async () =>
                            {
                                log.LogInformationEx(() => $"{podmanPath} login {loginFile.Location} --username {loginFile.Username} --password REDACTED");

                                if (NeonHelper.IsLinux)
                                {
                                    (await Node.ExecuteCaptureAsync(podmanPath, new object[] { "login", loginFile.Location, "--username", loginFile.Username, "--password", loginFile.Password })).EnsureSuccess();
                                }
                            });

                        loginFile.Write();
                    }
                    catch (Exception e)
                    {
                        loginErrorCounter.Inc();
                        log.LogErrorEx(e);
                    }
                }
            }
        }
    }
}
