//-----------------------------------------------------------------------------
// FILE:	    SshProxyExtension.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Neon.Cluster;
using Neon.Common;
using Neon.IO;
using Neon.Net;

namespace NeonCli
{
    /// <summary>
    /// <see cref="SshProxy{T}"/> extension methods.
    /// </summary>
    public static class SshProxyExtension
    {
        /// <summary>
        /// Converts a string into a value suitable for use in a Bash script.
        /// </summary>
        /// <param name="value">The value to be made safe,</param>
        /// <returns>The safe value.</returns>
        private static string BashSafeValue(object value)
        {
            if (value == null)
            {
                return "\"\"";
            }

            if (value is bool)
            {
                return (bool)value ? "true" : "false";
            }
            else if (value is int)
            {
                return value.ToString();
            }
            else
            {
                // We need to escape single and double quotes.

                var stringValue = value.ToString();

                stringValue = stringValue.Replace("'", "\\'");
                stringValue = stringValue.Replace("\"", "\\\"");

                return $"\"{stringValue}\"";
            }
        }

        /// <summary>
        /// Sets a variable in a <see cref="PreprocessReader"/> such that the value will be safe
        /// to be included in a Bash variable set statement.
        /// </summary>
        /// <param name="preprocessReader">The reader.</param>
        /// <param name="name">The variable name.</param>
        /// <param name="value">The variable value.</param>
        private static void SetBashVariable(PreprocessReader preprocessReader, string name, object value)
        {
            Covenant.Requires<ArgumentNullException>(preprocessReader != null);
            Covenant.Requires<ArgumentNullException>(name != null);

            if (value == null)
            {
                preprocessReader.Set(name, value);
            }
            else
            {
                if (value is bool)
                {
                    value = (bool)value ? "true" : "false";
                }
                else if (value is int)
                {
                    value = value.ToString();
                }
                else
                {
                    // We need to escape single and double quotes.

                    var stringValue = value.ToString();

                    stringValue = stringValue.Replace("'", "\\'");
                    stringValue = stringValue.Replace("\"", "\\\"");

                    value = $"\"{stringValue}\"";
                }

                preprocessReader.Set(name, value);
            }
        }

        /// <summary>
        /// Returns the IP address for a node suitable for including in the
        /// <b>/etc/hosts</b> file.  
        /// </summary>
        /// <param name="nodeDefinition">The target node definition.</param>
        /// <returns>
        /// The IP address, left adjusted with necessary spaces so that the
        /// host definitions will align nicely.
        /// </returns>
        private static string GetHostsFormattedAddress(NodeDefinition nodeDefinition)
        {
            const string ip4Max = "255.255.255.255";

            var address = nodeDefinition.PrivateAddress.ToString();

            if (address.Length < ip4Max.Length)
            {
                address += new string(' ', ip4Max.Length - address.Length);
            }

            return address;
        }

        /// <summary>
        /// Generates the PowerDNS Recursor hosts file for a node.  This will be uploaded
        /// to <b>/etc/powerdns/hosts</b>.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <param name="nodeDefinition">The target node definition.</param>
        /// <returns>The host definitions.</returns>
        private static string GetPowerDnsHosts(ClusterDefinition clusterDefinition, NodeDefinition nodeDefinition)
        {
            var sbHosts = new StringBuilder();

            sbHosts.AppendLineLinux("# PowerDNS Recursor authoritatively answers for [*.cluster] hostnames.");
            sbHosts.AppendLineLinux("# on the local node using these mappings.");
            sbHosts.AppendLineLinux();

            sbHosts.AppendLineLinux($"{GetHostsFormattedAddress(nodeDefinition)} {NeonHosts.Consul}");

            sbHosts.AppendLineLinux();
            sbHosts.AppendLineLinux("# Internal cluster Vault mappings:");
            sbHosts.AppendLineLinux();
            sbHosts.AppendLineLinux($"{GetHostsFormattedAddress(nodeDefinition)} {NeonHosts.Vault}");

            foreach (var manager in clusterDefinition.Managers)
            {
                sbHosts.AppendLineLinux($"{GetHostsFormattedAddress(manager)} {manager.Name}.{NeonHosts.Vault}");
            }

            if (clusterDefinition.Docker.RegistryCache)
            {
                sbHosts.AppendLineLinux();
                sbHosts.AppendLineLinux("# Internal cluster registry cache related mappings:");
                sbHosts.AppendLineLinux();

                foreach (var manager in clusterDefinition.Managers)
                {
                    sbHosts.AppendLineLinux($"{GetHostsFormattedAddress(manager)} {manager.Name}.{NeonHosts.RegistryCache}");
                }
            }

            if (clusterDefinition.Log.Enabled)
            {
                sbHosts.AppendLineLinux();
                sbHosts.AppendLineLinux("# Internal cluster log pipeline related mappings:");
                sbHosts.AppendLineLinux();

                sbHosts.AppendLineLinux($"{GetHostsFormattedAddress(nodeDefinition)} {NeonHosts.LogEsData}");
            }

            return sbHosts.ToString();
        }

        /// <summary>
        /// Sets cluster definition related variables for a <see cref="PreprocessReader"/>.
        /// </summary>
        /// <param name="preprocessReader">The reader.</param>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <param name="nodeDefinition">The target node definition.</param>
        private static void SetClusterVariables(PreprocessReader preprocessReader, ClusterDefinition clusterDefinition, NodeDefinition nodeDefinition)
        {
            Covenant.Requires<ArgumentNullException>(preprocessReader != null);
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null);

            // Generate the manager node variables in sorted order.  The variable 
            // names will be formatted as:
            //
            //      NEON_MANAGER_#
            //
            // where [#] is the zero-based index of the node.  This is compatible
            // with the [getmanager] function included the script.
            //
            // Each variable defines an associative array with [name] and [address]
            // properties.
            //
            // Then generate the NEON_MANAGER_NAMES and NEON_MANAGER_ADDRESSES arrays.
            //
            // NOTE: We need to use Linux-style line endings.

            var sbManagers                  = new StringBuilder();
            var sbManagerNamesArray         = new StringBuilder();
            var sbManagerAddressesArray     = new StringBuilder();
            var sbPeerManagerAddressesArray = new StringBuilder();
            var sbManagerNodesSummary       = new StringBuilder();
            var index                       = 0;
            var managerNameWidth            = 0;

            sbManagerNamesArray.Append("(");
            sbManagerAddressesArray.Append("(");
            sbPeerManagerAddressesArray.Append("(");

            foreach (var manager in clusterDefinition.SortedManagers)
            {
                sbManagers.Append($"declare -x -A NEON_MANAGER_{index}\n");
                sbManagers.Append($"NEON_MANAGER_{index}=( [\"name\"]=\"{manager.Name}\" [\"address\"]=\"{manager.PrivateAddress}\" )\n");
                sbManagers.Append("\n");
                index++;

                sbManagerNamesArray.Append($" \"{manager.Name}\"");
                sbManagerAddressesArray.Append($" \"{manager.PrivateAddress}\"");

                if (manager != nodeDefinition)
                {
                    sbPeerManagerAddressesArray.Append($" \"{manager.PrivateAddress}\"");
                }

                managerNameWidth = Math.Max(manager.Name.Length, managerNameWidth);
            }

            sbManagerNamesArray.Append(" )");
            sbManagerAddressesArray.Append(" )");
            sbPeerManagerAddressesArray.Append(" )");

            foreach (var manager in clusterDefinition.SortedManagers)
            {
                var nameField = manager.Name;

                if (nameField.Length < managerNameWidth)
                {
                    nameField += new string(' ', managerNameWidth - nameField.Length);
                }

                // The blanks below are just enough so that the "=" sign lines up
                // with the summary output from [cluster.conf.sh].

                if (sbManagerNodesSummary.Length == 0)
                {
                    sbManagerNodesSummary.Append($"    echo \"NEON_MANAGER_NODES                 = {nameField}: {manager.PrivateAddress}\" 1>&2\n");
                }
                else
                {
                    sbManagerNodesSummary.Append($"    echo \"                                     {nameField}: {manager.PrivateAddress}\" 1>&2\n");
                }
            }

            foreach (var manager in clusterDefinition.SortedManagers)
            {
                sbManagers.Append($"declare -x -A NEON_MANAGER_{index}\n");
                sbManagers.Append($"NEON_MANAGER_{index}=( [\"name\"]=\"{manager.Name}\" [\"address\"]=\"{manager.PrivateAddress}\" )\n");
                index++;
            }

            sbManagers.Append("\n");
            sbManagers.Append($"declare -x NEON_MANAGER_NAMES={sbManagerNamesArray}\n");
            sbManagers.Append($"declare -x NEON_MANAGER_ADDRESSES={sbManagerAddressesArray}\n");

            sbManagers.Append("\n");

            if (clusterDefinition.Managers.Count() > 1)
            {
                sbManagers.Append($"declare -x NEON_MANAGER_PEERS={sbPeerManagerAddressesArray}\n");
            }
            else
            {
                sbManagers.Append("export NEON_MANAGER_PEERS=\"\"\n");
            }

            // Generate the manager and worker NTP time sources.

            var managerTimeSources = string.Empty;
            var workerTimeSources  = string.Empty;

            if (clusterDefinition.TimeSources != null)
            {
                foreach (var source in clusterDefinition.TimeSources)
                {
                    if (string.IsNullOrWhiteSpace(source))
                    {
                        continue;
                    }

                    if (managerTimeSources.Length > 0)
                    {
                        managerTimeSources += " ";
                    }

                    managerTimeSources += $"\"{source}\"";
                }
            }

            foreach (var manager in clusterDefinition.SortedManagers)
            {
                if (workerTimeSources.Length > 0)
                {
                    workerTimeSources += " ";
                }

                workerTimeSources += $"\"{manager.PrivateAddress}\"";
            }

            if (string.IsNullOrWhiteSpace(managerTimeSources))
            {
                // Default to reasonable public time sources.

                managerTimeSources = "\"pool.ntp.org\"";
            }

            // Generate the Docker daemon command line options.  The [/etc/default/docker] script uses this.

            var sbDockerOptions = new StringBuilder();

            if (Program.ServiceManager == ServiceManager.Systemd)
            {
                sbDockerOptions.AppendWithSeparator($"-H unix:///var/run/docker.sock");

                if (clusterDefinition.Log.Enabled)
                {
                    // Metricbeat needs Docker API access.

                    sbDockerOptions.AppendWithSeparator($"-H {NeonClusterConst.DockerApiInternalEndpoint}");
                }
            }
            else
            {
                throw new NotImplementedException();
            }

            if (clusterDefinition.DebugMode)
            {
                // Expose the Docker Swarm REST API on the node's internal cluster IP address so it
                // can be reached by apps like [neon-proxy-manager] running off the manager node
                // (potentially in the debugger).

                sbDockerOptions.AppendWithSeparator($"-H tcp://{nodeDefinition.PrivateAddress}:{NetworkPorts.Docker}");
            }

            preprocessReader.Set("docker.options", sbDockerOptions);
            
            // Define the Consul command line options.

            var consulOptions = string.Empty;

            if (clusterDefinition.Dashboard.Consul)
            {
                if (consulOptions.Length > 0)
                {
                    consulOptions += " ";
                }

                consulOptions += "-ui";
            }

            // Format the network upstream nameservers as semicolon separated
            // to be compatible with the PowerDNS Recursor [forward-zones-recurse]
            // configuration setting.

            if (clusterDefinition.Network?.Nameservers == null)
            {
                // $hack(jeff.lill): 
                //
                // [Network] will be null if we're just preparing servers, not doing full setup
                // so we'll set this to the defaults to avoid null references below.

                clusterDefinition.Network = new NetworkOptions();
            }

            var nameservers = string.Empty;

            for (int i = 0; i < clusterDefinition.Network.Nameservers.Length; i++)
            {
                if (i > 0)
                {
                    nameservers += ";";
                }

                nameservers += clusterDefinition.Network.Nameservers[i].Trim();
            }

            // Set the variables.

            preprocessReader.Set("load-cluster-config", NodeHostFolders.Config + "/cluster.conf.sh --echo-summary");
            preprocessReader.Set("load-cluster-config-quiet", NodeHostFolders.Config + "/cluster.conf.sh");

            SetBashVariable(preprocessReader, "cluster.provisioner", clusterDefinition.Provisioner);
            SetBashVariable(preprocessReader, "cluster.rootuser", Program.MachineUsername);

            SetBashVariable(preprocessReader, "node.driveprefix", clusterDefinition.DrivePrefix);

            SetBashVariable(preprocessReader, "neon.folders.config", NodeHostFolders.Config);
            SetBashVariable(preprocessReader, "neon.folders.secrets", NodeHostFolders.Secrets);
            SetBashVariable(preprocessReader, "neon.folders.setup", NodeHostFolders.Setup);
            SetBashVariable(preprocessReader, "neon.folders.tools", NodeHostFolders.Tools);
            SetBashVariable(preprocessReader, "neon.folders.state", NodeHostFolders.State);
            SetBashVariable(preprocessReader, "neon.folders.secrets", NodeHostFolders.Secrets);
            SetBashVariable(preprocessReader, "neon.folders.scripts", NodeHostFolders.Scripts);
            SetBashVariable(preprocessReader, "neon.folders.archive", NodeHostFolders.Archive);
            SetBashVariable(preprocessReader, "neon.folders.exec", NodeHostFolders.Exec);

            preprocessReader.Set("neon.hosts.neon-log-es-data", NeonHosts.LogEsData);

            SetBashVariable(preprocessReader, "nodes.manager.count", clusterDefinition.Managers.Count());
            preprocessReader.Set("nodes.managers", sbManagers);
            preprocessReader.Set("nodes.manager.summary", sbManagerNodesSummary);

            SetBashVariable(preprocessReader, "ntp.manager.sources", managerTimeSources);
            SetBashVariable(preprocessReader, "ntp.worker.sources", workerTimeSources);

            if (!clusterDefinition.BareDocker)
            {
                // When we're not deploying bare Docker, the manager nodes will use the 
                // configured name servers as the cluster's upstream DNS and the worker
                // nodes will be configured to query the name servers.

                if (nodeDefinition.IsManager)
                {
                    preprocessReader.Set("net.nameservers", nameservers);
                }
                else
                {
                    var managerNameservers = string.Empty;

                    foreach (var manager in clusterDefinition.Managers)
                    {
                        if (managerNameservers.Length > 0)
                        {
                            managerNameservers += ";";
                        }

                        managerNameservers += manager.PrivateAddress.ToString();
                    }

                    preprocessReader.Set("net.nameservers", managerNameservers);
                }
            }
            else
            {
                // All servers use the configured upstream nameservers when we're not
                // deploying the Dynamic DNS.

                preprocessReader.Set("net.nameservers", nameservers);
            }

            SetBashVariable(preprocessReader, "net.powerdns.recursor.package.uri", clusterDefinition.Network.PdnsRecursorPackageUri);
            preprocessReader.Set("net.powerdns.recursor.hosts", GetPowerDnsHosts(clusterDefinition, nodeDefinition));

            SetBashVariable(preprocessReader, "docker.version", clusterDefinition.Docker.PackageVersion);

            SetBashVariable(preprocessReader, "consul.version", clusterDefinition.Consul.Version);
            SetBashVariable(preprocessReader, "consul.options", consulOptions);
            SetBashVariable(preprocessReader, "consul.address", $"{NeonHosts.Consul}:{clusterDefinition.Consul.Port}");
            SetBashVariable(preprocessReader, "consul.fulladdress", $"http://{NeonHosts.Consul}:{clusterDefinition.Consul.Port}");
            SetBashVariable(preprocessReader, "consul.hostname", NeonHosts.Consul);
            SetBashVariable(preprocessReader, "consul.port", clusterDefinition.Consul.Port);
            SetBashVariable(preprocessReader, "consul.tlsdisabled", true);

            SetBashVariable(preprocessReader, "vault.version", clusterDefinition.Vault.Version);

            SetBashVariable(preprocessReader, "vault.download", $"https://releases.hashicorp.com/vault/{clusterDefinition.Vault.Version}/vault_{clusterDefinition.Vault.Version}_linux_amd64.zip");
            SetBashVariable(preprocessReader, "vault.hostname", NeonHosts.Vault);
            SetBashVariable(preprocessReader, "vault.port", clusterDefinition.Vault.Port);
            SetBashVariable(preprocessReader, "vault.consulpath", "vault/");
            SetBashVariable(preprocessReader, "vault.maximumlease", clusterDefinition.Vault.MaximimLease);
            SetBashVariable(preprocessReader, "vault.defaultlease", clusterDefinition.Vault.DefaultLease);

            SetBashVariable(preprocessReader, "log.enabled", clusterDefinition.Log.Enabled);
        }

        /// <summary>
        /// Uploads a resource file to the remote server after performing any necessary preprocessing.
        /// </summary>
        /// <typeparam name="TMetadata">The node metadata type.</typeparam>
        /// <param name="node">The remote node.</param>
        /// <param name="clusterDefinition">The cluster definition or <c>null</c>.</param>
        /// <param name="file">The resource file.</param>
        /// <param name="targetPath">The target path on the remote server.</param>
        private static void UploadFile<TMetadata>(this SshProxy<TMetadata> node, ClusterDefinition clusterDefinition, ResourceFiles.File file, string targetPath)
            where TMetadata : class
        {
            using (var input = file.ToStream())
            {
                if (file.HasVariables)
                {
                    // We need to expand any variables.  Note that if we don't have a
                    // cluster definition or for undefined variables, we're going to 
                    // have the variables expand to the empty string.

                    using (var msExpanded = new MemoryStream())
                    {
                        using (var writer = new StreamWriter(msExpanded))
                        {
                            var preprocessReader =
                                new PreprocessReader(new StreamReader(input))
                                {
                                    DefaultVariable = string.Empty,
                                    ExpandVariables = true,
                                    ProcessCommands = false,
                                    StripComments   = false
                                };

                            if (clusterDefinition != null)
                            {
                                SetClusterVariables(preprocessReader, clusterDefinition, node.Metadata as NodeDefinition);
                            }

                            foreach (var line in preprocessReader.Lines())
                            {
                                writer.WriteLine(line);
                            }

                            writer.Flush();

                            msExpanded.Position = 0;
                            node.UploadText(targetPath, msExpanded, tabStop: 4, outputEncoding: Encoding.UTF8);
                        }
                    }
                }
                else
                {
                    node.UploadText(targetPath, input, tabStop: 4, outputEncoding: Encoding.UTF8);
                }
            }
        }

        /// <summary>
        /// Uploads the configuration files for the target operating system to the server.
        /// </summary>
        /// <typeparam name="Metadata">The node metadata type.</typeparam>
        /// <param name="node">The remote node.</param>
        /// <param name="clusterDefinition">The cluster definition or <c>null</c>.</param>
        public static void UploadConfigFiles<Metadata>(this SshProxy<Metadata> node, ClusterDefinition clusterDefinition = null)
            where Metadata : class
        {
            Covenant.Requires<ArgumentNullException>(node != null);

            // Clear the contents of the configuration folder.

            node.Status = $"clear: {NodeHostFolders.Config}";
            node.SudoCommand($"rm -rf {NodeHostFolders.Config}/*.*");

            // Upload the files.

            node.Status = "upload: config files";

            foreach (var file in Program.LinuxFolder.GetFolder("conf").Files())
            {
                node.UploadFile(clusterDefinition, file, $"{NodeHostFolders.Config}/{file.Name}");
            }

            // Secure the files and make the scripts executable.

            node.SudoCommand($"chmod 600 {NodeHostFolders.Config}/*.*");
            node.SudoCommand($"chmod 700 {NodeHostFolders.Config}/*.sh");

            node.Status = "copied";
        }

        /// <summary>
        /// Uploads the setup and other scripts and tools for the target operating system to the server.
        /// </summary>
        /// <typeparam name="TMetadata">The server's metadata type.</typeparam>
        /// <param name="server">The remote server.</param>
        /// <param name="clusterDefinition">The cluster definition or <c>null</c>.</param>
        public static void UploadTools<TMetadata>(this SshProxy<TMetadata> server, ClusterDefinition clusterDefinition = null)
            where TMetadata : class
        {
            Covenant.Requires<ArgumentNullException>(server != null);

            //-----------------------------------------------------------------
            // Clear the contents of the setup scripts folder.

            server.Status = $"clear: {NodeHostFolders.Setup}";
            server.SudoCommand($"rm -rf {NodeHostFolders.Setup}/*.*");

            // Upload the setup files.

            server.Status = "upload: setup files";

            foreach (var file in Program.LinuxFolder.GetFolder("setup").Files())
            {
                server.UploadFile(clusterDefinition, file, $"{NodeHostFolders.Setup}/{file.Name}");
            }

            // Make the scripts executable.

            server.SudoCommand($"chmod 700 {NodeHostFolders.Setup}/*");

            //-----------------------------------------------------------------
            // Clear the contents of the tools folder.

            server.Status = $"clear: {NodeHostFolders.Tools}";
            server.SudoCommand($"rm -rf {NodeHostFolders.Tools}/*.*");

            // Upload the tool files.  Note that we're going to strip out the [.sh] 
            // file type to make these easier to run.

            server.Status = "upload: tool files";

            foreach (var file in Program.LinuxFolder.GetFolder("tools").Files())
            {
                server.UploadFile(clusterDefinition, file, $"{NodeHostFolders.Tools}/{file.Name.Replace(".sh", string.Empty)}");
            }

            // Make the scripts executable.

            server.SudoCommand($"chmod 700 {NodeHostFolders.Tools}/*");
        }
    }
}
