//-----------------------------------------------------------------------------
// FILE:        NodeSshProxy.NodePrepare.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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

// This file includes node configuration methods executed while setting
// up a NEONKUBE cluster.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;
using Neon.Kube.ClusterDef;
using Neon.Kube.Hosting;
using Neon.Kube.Proxy;
using Neon.Kube.Setup;
using Neon.SSH;
using Neon.Tasks;

namespace Neon.Kube.SSH
{
    public partial class NodeSshProxy<TMetadata> : LinuxSshProxy<TMetadata>
        where TMetadata : class
    {
        /// <summary>
        /// Removes the Linux swap file if present.
        /// </summary>
        /// <param name="controller">Specifies the setup controller.</param>
        public void RemoveSwapFile(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            InvokeIdempotent("setup/swap-remove",
                () =>
                {
                    SudoCommand("rm -f /swap.img");
                });
        }

        /// <summary>
        /// Installs the NEONKUBE related tools to the <see cref="KubeNodeFolder.Bin"/> folder.
        /// </summary>
        /// <param name="controller">Specifies the setup controller.</param>
        public void NodeInstallTools(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            InvokeIdempotent("setup/tools",
                () =>
                {
                    controller.LogProgress(this, verb: "setup", message: "tools (node)");

                    foreach (var file in KubeHelper.Resources.GetDirectory("/Tools").GetFiles())
                    {
                        // Upload each tool script, removing the extension.

                        var targetName = LinuxPath.GetFileNameWithoutExtension(file.Path);
                        var targetPath = LinuxPath.Combine(KubeNodeFolder.Bin, targetName);

                        UploadText(targetPath, file.ReadAllText(), permissions: "774", owner: KubeConst.SysAdminUser);
                    }
                });
        }

        /// <summary>
        /// <para>
        /// Configures a node's host public SSH key during node provisioning.
        /// </para>
        /// </summary>
        /// <param name="controller">Specifies the setup controller.</param>
        public void ConfigureSshKey(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);

            // Write the SSHD subconfig file and configure the SSH certificate
            // credentials for [sysadmin] on the node.

            InvokeIdempotent("prepare/ssh",
                () =>
                {
                    CommandBundle bundle;

                    // Upload our custom SSH config files.

                    controller.LogProgress(this, verb: "update", message: "ssh config");

                    var configText = KubeHelper.OpenSshConfig;

                    UploadText("/etc/ssh/sshd_config", NeonHelper.ToLinuxLineEndings(configText), permissions: "644");

                    var subConfigText = KubeHelper.GetOpenSshPrepareSubConfig(allowPasswordAuth: true);

                    UploadText("/etc/ssh/sshd_config.d/50-neonkube.conf", NeonHelper.ToLinuxLineEndings(subConfigText), permissions: "644"); 

                    // Here's some information explaining what how this works:
                    //
                    //      https://help.ubuntu.com/community/SSH/OpenSSH/Configuring
                    //      https://help.ubuntu.com/community/SSH/OpenSSH/Keys

                    controller.LogProgress(this, verb: "configure", message: "ssh key");

                    // Enable the public key by appending it to [$HOME/.ssh/authorized_keys],
                    // creating the file if necessary.  Note that we support only a single
                    // authorized key.

                    var addKeyScript =
$@"
chmod go-w ~/
mkdir -p $HOME/.ssh
chmod 700 $HOME/.ssh
touch $HOME/.ssh/authorized_keys
cat ssh-key.ssh2 > $HOME/.ssh/authorized_keys
chmod 600 $HOME/.ssh/authorized_keys
";
                    bundle = new CommandBundle("./addkeys.sh");

                    bundle.AddFile("addkeys.sh", addKeyScript, isExecutable: true);
                    bundle.AddFile("ssh-key.ssh2", cluster.SetupState.SshKey.PublicPUB);

                    // $note(jefflill):
                    //
                    // I'm explicitly not running the bundle as [sudo] because the OpenSSH
                    // server is very picky about the permissions on the user's [$HOME]
                    // and [$HOME/.ssl] folder and contents.  This took me a couple 
                    // hours to figure out.

                    RunCommand(bundle);

                    // Upload the server key and edit the [sshd] config to disable all host keys 
                    // except for RSA.

                    var configScript =
$@"
# Disable all host keys except for RSA.

sed -i 's!^\HostKey /etc/ssh/ssh_host_dsa_key$!#HostKey /etc/ssh/ssh_host_dsa_key!g' /etc/ssh/sshd_config
sed -i 's!^\HostKey /etc/ssh/ssh_host_ecdsa_key$!#HostKey /etc/ssh/ssh_host_ecdsa_key!g' /etc/ssh/sshd_config
sed -i 's!^\HostKey /etc/ssh/ssh_host_ed25519_key$!#HostKey /etc/ssh/ssh_host_ed25519_key!g' /etc/ssh/sshd_config

# Restart SSHD to pick up the changes.

systemctl restart ssh
";
                    bundle = new CommandBundle("./config.sh");

                    bundle.AddFile("config.sh", configScript, isExecutable: true);
                    SudoCommand(bundle, RunOptions.FaultOnError);
                });

            // Verify that we can login with the new SSH private key and also verify that
            // the password still works.

            controller.LogProgress(this, verb: "verify", message: "ssh keys");

            Disconnect();
            UpdateCredentials(SshCredentials.FromPrivateKey(KubeConst.SysAdminUser, cluster.SetupState.SshKey.PrivatePEM));
            WaitForBoot();
        }

        /// <summary>
        /// Disables the <b>snapd</b> service.
        /// </summary>
        /// <param name="controller">Specifies the setup controller.</param>
        public void DisableSnap(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            InvokeIdempotent("base/disable-snap",
                () =>
                {
                    controller.LogProgress(this, verb: "disable", message: "snapd.service");

                    //-----------------------------------------------------------------
                    // We're going to stop and mask the [snapd.service] if it's running
                    // because we don't want it to randomlly update apps on cluster nodes.

                    var disableSnapScript =
@"
# Stop and mask [snapd.service] when it's not already masked.

systemctl status --no-pager snapd.service

if [ $? ]; then
    systemctl stop snapd.service
    systemctl mask snapd.service
fi
";
                    SudoCommand(CommandBundle.FromScript(disableSnapScript), RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Installs NFS.
        /// </summary>
        /// <param name="controller">Specifies the setup controller.</param>
        public void InstallNFS(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            InvokeIdempotent("base/nfs",
                () =>
                {
                    controller.LogProgress(this, verb: "configure", message: "nfs");

                    //-----------------------------------------------------------------
                    // We need to install nfs-common tools for NFS to work.

                    var InstallNfsScript =
$@"
set -euo pipefail

{KubeConst.SafeAptGetTool} update -y
{KubeConst.SafeAptGetTool} install -y nfs-common
";
                    SudoCommand(CommandBundle.FromScript(InstallNfsScript), RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Configures <b>journald</b>.
        /// </summary>
        /// <param name="controller">Specifies the setup controller.</param>
        public void ConfigureJournald(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            InvokeIdempotent("base/journald",
                () =>
                {
                    controller.LogProgress(this, verb: "configure", message: "journald filters");

                    var filterScript =
@"
# NEONKUBE: Filter [rsyslog.service] log events we don't care about.

cat <<EOF > /etc/rsyslog.d/60-filter.conf
if $programname == ""systemd"" and ($msg startswith ""Created slice "" or $msg startswith ""Removed slice "") then stop
EOF

systemctl restart rsyslog.service
";
                    SudoCommand(CommandBundle.FromScript(filterScript), RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Installs the Cilium and Hubble CLIs.
        /// </summary>
        /// <param name="controller"></param>
        public void InstallCiliumCli(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            InvokeIdempotent("base/cilium-cli",
                () =>
                {
                    controller.LogProgress(this, verb: "configure", message: "journald filters");

                    var script =
$@"
set -euo pipefail
pushd /tmp

# Configure download details.

OS=linux
ARCH=amd64

# Install the Cilium CLI.

CILIUM_CLI_VERSION={KubeVersions.CiliumCli}
curl -L --remote-name-all https://github.com/cilium/cilium-cli/releases/download/$CILIUM_CLI_VERSION/cilium-$OS-$ARCH.tar.gz{{,.sha256sum}}
sha256sum --check cilium-$OS-$ARCH.tar.gz.sha256sum
tar -C /usr/local/bin -xzvf cilium-$OS-$ARCH.tar.gz
rm cilium-$OS-$ARCH.tar.gz{{,.sha256sum}}

# Install the Hubble CLI.

HUBBLE_CLI_VERSION={KubeVersions.CiliumHubbleCli}
curl -L --fail --remote-name-all https://github.com/cilium/hubble/releases/download/$HUBBLE_CLI_VERSION/hubble-$OS-$ARCH.tar.gz{{,.sha256sum}}
sha256sum --check hubble-$OS-$ARCH.tar.gz.sha256sum
tar xzvfC hubble-$OS-$ARCH.tar.gz /usr/local/bin
rm hubble-$OS-$ARCH.tar.gz{{,.sha256sum}}

popd
";
                    SudoCommand(CommandBundle.FromScript(script))
                        .EnsureSuccess();
                });
        }

        /// <summary>
        /// Initializes a near virgin server with the basic capabilities required
        /// for a cluster node.
        /// </summary>
        /// <param name="controller">Specifies the setup controller.</param>
        public void PrepareNode(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            var hostingManager    = controller.Get<IHostingManager>(KubeSetupProperty.HostingManager);
            var clusterDefinition = cluster.SetupState.ClusterDefinition;

            InvokeIdempotent("base/prepare-node",
                () =>
                {
                    controller.LogProgress(this, verb: "prepare", message: "node");

                    controller.ThrowIfCancelled();
                    UpdateRootCertificates(aptGetTool: $"{KubeConst.SafeAptGetTool}");

                    controller.ThrowIfCancelled();
                    RemoveSwapFile(controller);

                    controller.ThrowIfCancelled();
                    NodeInstallTools(controller);

                    controller.ThrowIfCancelled();
                    BaseConfigureApt(controller, clusterDefinition.NodeOptions.PackageManagerRetries, clusterDefinition.NodeOptions.AllowPackageManagerIPv6);

                    controller.ThrowIfCancelled();
                    DisableSnap(controller);

                    controller.ThrowIfCancelled();
                    ConfigureJournald(controller);

                    controller.ThrowIfCancelled();
                    InstallNFS(controller);
                });
        }

        /// <summary>
        /// Disables the <b>neon-init</b> service during cluster setup because it is no
        /// longer necessary after the node first boots and its credentials and network
        /// settings have been configured.
        /// </summary>
        /// <param name="controller">Specifies the setup controller.</param>
        public void NodeDisableNeonInit(ISetupController controller)
        {
            Covenant.Requires<ArgumentException>(controller != null, nameof(controller));

            InvokeIdempotent("base/disable-neon-init",
                () =>
                {
                    controller.LogProgress(this, verb: "disable", message: "neon-init.service");

                    SudoCommand("systemctl disable neon-init.service", RunOptions.Defaults | RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Installs the necessary packages and configures setup for <b>IPVS</b>.
        /// </summary>
        /// <param name="controller">Specifies the setup controller.</param>
        public void NodeInstallIPVS(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            var hostEnvironment = controller.Get<HostingEnvironment>(KubeSetupProperty.HostingEnvironment);

            InvokeIdempotent("setup/ipvs",
                () =>
                {
                    controller.LogProgress(this, verb: "setup", message: "ipvs");

                    var setupScript =
$@"
set -euo pipefail

{KubeConst.SafeAptGetTool} update -y
{KubeConst.SafeAptGetTool} install -y ipset ipvsadm
";
                    SudoCommand(CommandBundle.FromScript(setupScript), RunOptions.Defaults | RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Installs the <b>CRI-O</b> container runtime.
        /// </summary>
        /// <param name="controller">Specifies the setup controller.</param>
        /// <param name="clusterManifest">Specifies the cluster manifest.</param>
        public void NodeInstallCriO(ISetupController controller, ClusterManifest clusterManifest)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(clusterManifest != null, nameof(clusterManifest));

            var hostEnvironment = controller.Get<HostingEnvironment>(KubeSetupProperty.HostingEnvironment);
            var reconfigureOnly = !controller.Get<bool>(KubeSetupProperty.Preparing, true) && !cluster.DebugMode;

            controller.LogProgress(this, verb: "setup", message: "cri-o");

            // $note(jefflill):
            //
            // We used to perform the configuration operation below as an
            // idempotent operation but we need to relax that so that we
            // can apply configuration changes on new nodes created from
            // the node image.

            if (!reconfigureOnly)
            {
                var moduleScript =
@"
set -euo pipefail

# Create the .conf file to load required modules during boot.

cat <<EOF > /etc/modules-load.d/crio.conf
overlay
br_netfilter
EOF

# ...and load them explicitly now.

modprobe overlay
modprobe br_netfilter

sysctl --quiet --load /etc/sysctl.conf
";          
                SudoCommand(CommandBundle.FromScript(moduleScript));
            }

            //-----------------------------------------------------------------
            // Generate the container registry config file contents for:
            //
            //      /etc/containers/registries.conf.d/00-neon-cluster.conf
            //
            // NOTE: We're only generating the reference to the built-in local
            //       Harbor registry here.  Any additional registries will be
            //       configured during cluster setup as custom [ContainerRegistry]
            //       resources to be configured by the [neon-node-agent].
            //
            // We'll add this to the installation script below.

            // $hack(jefflill):
            //
            // [cluster] will be NULL when preparing a node image so we'll set the
            // default here and this will be reconfigured during cluster setup.

            var sbRegistryConfig = new StringBuilder();

            sbRegistryConfig.Append(
$@"
unqualified-search-registries = []

[[registry]]
prefix   = ""{KubeConst.LocalClusterRegistryHostName}""
insecure = true
blocked  = false
location = ""{KubeConst.LocalClusterRegistryHostName}""
");

            //-----------------------------------------------------------------
            // Install and configure CRI-O.

            var crioVersionFull    = Version.Parse(KubeVersions.Crio);
            var crioVersionNoPatch = new Version(crioVersionFull.Major, crioVersionFull.Minor);
            var install            = reconfigureOnly ? "false" : "true";

            var setupScript =
$@"#!/bin/bash

set -euo pipefail

if [ ""{install}"" = ""true"" ]; then

    # Install the packaging dependencies.

    {KubeConst.SafeAptGetTool} update
    {KubeConst.SafeAptGetTool} install -y curl software-properties-common

    # Configure Kubernetes and CRI-O repositories:
    #
    #       https://kubernetes.io/blog/2023/10/10/cri-o-community-package-infrastructure/#add-the-cri-o-repository

    VERSION={crioVersionNoPatch}

    mkdir -p /etc/apt/keyrings

    rm -f /etc/apt/keyrings/kubernetes-apt-keyring.gpg
    rm -f /etc/apt/sources.list.d/kubernetes.list

    curl -fsSL https://pkgs.k8s.io/core:/stable:/v$VERSION/deb/Release.key | gpg --dearmor -o /etc/apt/keyrings/kubernetes-apt-keyring.gpg
    echo ""deb [signed-by=/etc/apt/keyrings/kubernetes-apt-keyring.gpg] https://pkgs.k8s.io/core:/stable:/v$VERSION/deb/ /"" | tee /etc/apt/sources.list.d/kubernetes.list

    rm -f /etc/apt/keyrings/cri-o-apt-keyring.gpg
    rm -f /etc/apt/sources.list.d/cri-o.list

    curl -fsSL https://pkgs.k8s.io/addons:/cri-o:/prerelease:/main/deb/Release.key | gpg --dearmor -o /etc/apt/keyrings/cri-o-apt-keyring.gpg
    echo ""deb [signed-by=/etc/apt/keyrings/cri-o-apt-keyring.gpg] https://pkgs.k8s.io/addons:/cri-o:/prerelease:/main/deb/ /"" | tee /etc/apt/sources.list.d/cri-o.list

    # Install CRI-O.

    {KubeConst.SafeAptGetTool} update
    {KubeConst.SafeAptGetTool} install -y cri-o --allow-change-held-packages
fi

# Generate the CRI-O configuration.

cat <<EOF > /etc/containers/registries.conf.d/00-neon-cluster.conf
{sbRegistryConfig}
EOF

cat <<EOF > /etc/crio/crio.conf
# The CRI-O configuration file specifies all of the available configuration
# options and command-line flags for the crio(8) OCI Kubernetes Container Runtime
# daemon, but in a TOML format that can be more easily modified and versioned.
#
# Please refer to crio.conf(5) for details of all configuration options.

# CRI-O supports partial configuration reload during runtime, which can be
# done by sending SIGHUP to the running process. Currently supported options
# are explicitly mentioned with: 'This option supports live configuration
# reload'.

# CRI-O reads its storage defaults from the containers-storage.conf(5) file
# located at /etc/containers/storage.conf. Modify this storage configuration if
# you want to change the system's defaults. If you want to modify storage just
# for CRI-O, you can change the storage configuration options here.
[crio]

# Path to the ""root directory"". CRI-O stores all of its data, including
# containers images, in this directory.
# root = ""/var/lib/containers/storage""

# Path to the ""run directory"". CRI-O stores all of its state in this directory.
# runroot = ""/var/run/containers/storage""

# Storage driver used to manage the storage of images and containers. Please
# refer to containers-storage.conf(5) to see all available storage drivers.
# storage_driver = """"

# List to pass options to the storage driver. Please refer to
# containers-storage.conf(5) to see all available storage options.
#storage_option = [
#]

# The default log directory where all logs will go unless directly specified by
# the kubelet. The log directory specified must be an absolute directory.
log_dir = ""/var/log/crio/pods""

# Location for CRI-O to lay down the temporary version file.
# It is used to check if crio wipe should wipe containers, which should
# always happen on a node reboot
version_file = ""/var/run/crio/version""

# Location for CRI-O to lay down the persistent version file.
# It is used to check if crio wipe should wipe images, which should
# only happen when CRI-O has been upgraded
version_file_persist = ""/var/lib/crio/version""

# The crio.api table contains settings for the kubelet/gRPC interface.
[crio.api]

# Path to AF_LOCAL socket on which CRI-O will listen.
listen = ""/var/run/crio/crio.sock""

# IP address on which the stream server will listen.
stream_address = ""127.0.0.1""

# The port on which the stream server will listen. If the port is set to ""0"", then
# CRI-O will allocate a random free port number.
stream_port = ""0""

# Enable encrypted TLS transport of the stream server.
stream_enable_tls = false

# Path to the x509 certificate file used to serve the encrypted stream. This
# file can change, and CRI-O will automatically pick up the changes within 5
# minutes.
stream_tls_cert = """"

# Path to the key file used to serve the encrypted stream. This file can
# change and CRI-O will automatically pick up the changes within 5 minutes.
stream_tls_key = """"

# Path to the x509 CA(s) file used to verify and authenticate client
# communication with the encrypted stream. This file can change and CRI-O will
# automatically pick up the changes within 5 minutes.
stream_tls_ca = """"

# Maximum grpc send message size in bytes. If not set or <=0, then CRI-O will default to 16 * 1024 * 1024.
grpc_max_send_msg_size = 16777216

# Maximum grpc receive message size. If not set or <= 0, then CRI-O will default to 16 * 1024 * 1024.
grpc_max_recv_msg_size = 16777216

# The crio.runtime table contains settings pertaining to the OCI runtime used
# and options for how to set up and manage the OCI runtime.
[crio.runtime]

# A list of ulimits to be set in containers by default, specified as
# ""<ulimit name>=<soft limit>:<hard limit>"", for example:
# ""nofile=1024:2048""
# If nothing is set here, settings will be inherited from the CRI-O daemon
# default_ulimits = [
#]

# default_runtime is the _name_ of the OCI runtime to be used as the default.
# The name is matched against the runtimes map below.
default_runtime = ""runc""

# If true, the runtime will not use pivot_root, but instead use MS_MOVE.
no_pivot = false

# decryption_keys_path is the path where the keys required for
# image decryption are stored. This option supports live configuration reload.
decryption_keys_path = ""/etc/crio/keys/""

# Path to the conmon binary, used for monitoring the OCI runtime.
# Will be searched for using $PATH if empty.
conmon = """"

# Cgroup setting for conmon
conmon_cgroup = ""system.slice""

# Environment variable list for the conmon process, used for passing necessary
# environment variables to conmon or the runtime.
conmon_env = [
    ""PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin"",
]

# Additional environment variables to set for all the
# containers. These are overridden if set in the
# container image spec or in the container runtime configuration.
default_env = [
]

# If true, SELinux will be used for pod separation on the host.
selinux = false

# Path to the seccomp.json profile which is used as the default seccomp profile
# for the runtime. If not specified, then the internal default seccomp profile
# will be used. This option supports live configuration reload.
seccomp_profile = """"

# Used to change the name of the default AppArmor profile of CRI-O. The default
# profile name is ""crio-default"". This profile only takes effect if the user
# does not specify a profile via the Kubernetes Pod's metadata annotation. If
# the profile is set to ""unconfined"", then this equals to disabling AppArmor.
# This option supports live configuration reload.
apparmor_profile = ""crio-default""

# Cgroup management implementation used for the runtime.
cgroup_manager = ""systemd""

# List of default capabilities for containers. If it is empty or commented out,
# only the capabilities defined in the containers json file by the user/kube
# will be added.
#
# NEONKUBE NOTE:
#
# We added the SYS_CHROOT capability so that containers can host SSHD servers.
# This is disabled by default for CRI-O but works for Docker and Containerd
# based CSI implementations.
default_capabilities = [
    ""CHOWN"",
    ""DAC_OVERRIDE"",
    ""FSETID"",
    ""FOWNER"",
    ""SETGID"",
    ""SETUID"",
    ""SETPCAP"",
    ""NET_BIND_SERVICE"",
    ""KILL"",
    ""SYS_CHROOT"",
]

# List of default sysctls. If it is empty or commented out, only the sysctls
# defined in the container json file by the user/kube will be added.
#
# NEONKUBE NOTE:
#
# We added ""net.ipv4.ping_group_range"" so ping will work from inside pods.
default_sysctls = [
    ""net.ipv4.ping_group_range=0 2147483647"",
]

# List of additional devices. specified as
# ""<device-on-host>:<device-on-container>:<permissions>"", for example: ""--device=/dev/sdc:/dev/xvdc:rwm"".
# If it is empty or commented out, only the devices
# defined in the container json file by the user/kube will be added.
additional_devices = [
]

# Path to OCI hooks directories for automatically executed hooks. If one of the
# directories does not exist, then CRI-O will automatically skip them.
hooks_dir = [
    ""/usr/share/containers/oci/hooks.d"",
]

# List of default mounts for each container. **Deprecated:** this option will
# be removed in future versions in favor of default_mounts_file.
default_mounts = [
]

# Path to the file specifying the defaults mounts for each container. The
# format of the config is /SRC:/DST, one mount per line. Notice that CRI-O reads
# its default mounts from the following two files:
#
#   1) /etc/containers/mounts.conf (i.e., default_mounts_file): This is the
# override file, where users can either add in their own default mounts, or
# override the default mounts shipped with the package.
#
#   2) /usr/share/containers/mounts.conf: This is the default file read for
# mounts. If you want CRI-O to read from a different, specific mounts file,
# you can change the default_mounts_file. Note, if this is done, CRI-O will
# only add mounts it finds in this file.
#
# default_mounts_file = """"

# Maximum number of processes allowed in a container.
pids_limit = 1024

# Maximum sized allowed for the container log file. Negative numbers indicate
# that no size limit is imposed. If it is positive, it must be >= 8192 to
# match/exceed conmon's read buffer. The file is truncated and re-opened so the
# limit is never exceeded.
log_size_max = -1

# Whether container output should be logged to journald in addition to the kuberentes log file
log_to_journald = false

# Path to directory in which container exit files are written to by conmon.
container_exits_dir = ""/var/run/crio/exits""

# Path to directory for container attach sockets.
container_attach_socket_dir = ""/var/run/crio""

# The prefix to use for the source of the bind mounts.
bind_mount_prefix = """"

# If set to true, all containers will run in read-only mode.
read_only = false

# Changes the verbosity of the logs based on the level it is set to. Options
# are fatal, panic, error, warn, info, debug and trace. This option supports
# live configuration reload.
log_level = ""info""

# Filter the log messages by the provided regular expression.
# This option supports live configuration reload.
log_filter = """"

# The UID mappings for the user namespace of each container. A range is
# specified in the form containerUID:HostUID:Size. Multiple ranges must be
# separated by comma.
uid_mappings = """"

# The GID mappings for the user namespace of each container. A range is
# specified in the form containerGID:HostGID:Size. Multiple ranges must be
# separated by comma.
gid_mappings = """"

# The minimal amount of time in seconds to wait before issuing a timeout
# regarding the proper termination of the container. The lowest possible
# value is 30s, whereas lower values are not considered by CRI-O.
ctr_stop_timeout = 30

# **DEPRECATED** this option is being replaced by manage_ns_lifecycle, which is described below.
# manage_network_ns_lifecycle = false

# manage_ns_lifecycle determines whether we pin and remove namespaces
# and manage their lifecycle
manage_ns_lifecycle = false

# The directory where the state of the managed namespaces gets tracked.
# Only used when manage_ns_lifecycle is true.
namespaces_dir = ""/var/run""

# pinns_path is the path to find the pinns binary, which is needed to manage namespace lifecycle
pinns_path = """"

# The ""crio.runtime.runtimes"" table defines a list of OCI compatible runtimes.
# The runtime to use is picked based on the runtime_handler provided by the CRI.
# If no runtime_handler is provided, the runtime will be picked based on the level
# of trust of the workload. Each entry in the table should follow the format:
#
#[crio.runtime.runtimes.runtime-handler]
# runtime_path = ""/path/to/the/executable""
# runtime_type = ""oci""
# runtime_root = ""/path/to/the/root""
#
# Where:
# - runtime-handler: name used to identify the runtime
# - runtime_path (optional, string): absolute path to the runtime executable in
# the host filesystem. If omitted, the runtime-handler identifier should match
# the runtime executable name, and the runtime executable should be placed
# in $PATH.
# - runtime_type (optional, string): type of runtime, one of: ""oci"", ""vm"". If
# omitted, an ""oci"" runtime is assumed.
# - runtime_root (optional, string): root directory for storage of containers
# state.

[crio.runtime.runtimes.runc]
runtime_path = """"
runtime_type = ""oci""
runtime_root = ""/run/runc""

# Kata Containers is an OCI runtime, where containers are run inside lightweight
# VMs. Kata provides additional isolation towards the host, minimizing the host attack
# surface and mitigating the consequences of containers breakout.

# Kata Containers with the default configured VMM
#[crio.runtime.runtimes.kata-runtime]

# Kata Containers with the QEMU VMM
#[crio.runtime.runtimes.kata-qemu]

# Kata Containers with the Firecracker VMM
#[crio.runtime.runtimes.kata-fc]

# The crio.image table contains settings pertaining to the management of OCI images.
#
# CRI-O reads its configured registries defaults from the system wide
# containers-registries.conf(5) located in /etc/containers/registries.conf. If
# you want to modify just CRI-O, you can change the registries configuration in
# this file. Otherwise, leave insecure_registries and registries commented out to
# use the system's defaults from /etc/containers/registries.conf.
[crio.image]

# Default transport for pulling images from a remote container storage.
default_transport = ""docker://""

# The path to a file containing credentials necessary for pulling images from
# secure registries. The file is similar to that of /var/lib/kubelet/config.json
global_auth_file = """"

# The image used to instantiate infra containers.
# This option supports live configuration reload.
pause_image = ""{KubeConst.LocalClusterRegistry}/pause:{KubeVersions.Pause}""

# The path to a file containing credentials specific for pulling the pause_image from
# above. The file is similar to that of /var/lib/kubelet/config.json
# This option supports live configuration reload.
pause_image_auth_file = """"

# The command to run to have a container stay in the paused state.
# When explicitly set to """", it will fallback to the entry point and command
# specified in the pause image. When commented out, it will fallback to the
# default: ""/pause"". This option supports live configuration reload.
pause_command = ""/pause""

# Path to the file which decides what sort of policy we use when deciding
# whether or not to trust an image that we've pulled. It is not recommended that
# this option be used, as the default behavior of using the system-wide default
# policy (i.e., /etc/containers/policy.json) is most often preferred. Please
# refer to containers-policy.json(5) for more details.
signature_policy = """"

# List of registries to skip TLS verification for pulling images. Please
# consider configuring the registries via /etc/containers/registries.conf before
# changing them here.
# insecure_registries = ""[]""

# Controls how image volumes are handled. The valid values are mkdir, bind and
# ignore; the latter will ignore volumes entirely.
image_volumes = ""mkdir""

# List of registries to be used when pulling an unqualified image (e.g.,
# ""alpine:latest""). By default, registries is set to ""docker.io"" for
# compatibility reasons. Depending on your workload and usecase you may add more
# registries (e.g., ""quay.io"", ""registry.fedoraproject.org"",
# ""registry.opensuse.org"", etc.).
# registries = []

# The crio.network table containers settings pertaining to the management of
# CNI plugins.
[crio.network]

# The default CNI network name to be selected. If not set or """", then
# CRI-O will pick-up the first one found in network_dir.
# cni_default_network = """"

# Path to the directory where CNI configuration files are located.
network_dir = ""/etc/cni/net.d/""

# Paths to directories where CNI plugin binaries are located.
plugin_dirs = [""/opt/cni/bin/""]

# A necessary configuration for Prometheus based metrics retrieval
[crio.metrics]

# Globally enable or disable metrics support.
enable_metrics = true

# The port on which the metrics server will listen.
metrics_port = 9090
EOF

# Remove shortnames.

rm -f /etc/containers/registries.conf.d/000-shortnames.conf

# Configure CRI-O to start on boot and then restart it to pick up the new configuration.

systemctl daemon-reload
systemctl enable crio
systemctl restart crio

# Prevent the package manager from automatically upgrading these components.

set +e      # Don't exit if the next command fails
apt-mark hold cri-o
";
            // The CRI-O apt package mirror has been quite unreliable over the years, 
            // so we're going to retry the operation in the hope that it will work
            // eventually.

            try
            {
                retry.Invoke(
                    () =>
                    {
                        SudoCommand(CommandBundle.FromScript(setupScript), RunOptions.Defaults)
                            .EnsureSuccess();
                    });
            }
            catch (Exception e)
            {
                Fault(e.Message);
            }

            // $todo(jefflill): Remove this hack when/if it's no longer necessary.
            //
            // We need to install a slightly customized version of CRI-O that has
            // a somewhat bastardized implementation of container image "pinning",
            // to prevent Kubelet from evicting critical setup images that cannot
            // be pulled automatically from GHCR.
            //
            // The customized version is built by [neon-image build cri-o ...]
            // and [neon-image prepare node ...] (by default) with the binary
            // being uploaded to S3.
            //
            // The custom CRI-O loads images references from [/etc/neonkube/pinned-images]
            // (one per line) and simply prevents any listed images from being deleted.
            // As of CRI API 1.23+, the list-images endpoint can return a pinned field
            // which Kubelet will honor when handling disk preassure, but unfortunately
            // CRI-O as of 1.23.0 doesn't appear to have vendored the updated CRI yet.
            // It does look like pinning is coming soon, so we can probably stick with
            // this delete based approach until then.

            if (!reconfigureOnly)
            {
                var crioUpdateScript =
$@"
set -euo pipefail

# Install the [pinned-images] config file.

cp pinned-images /etc/neonkube/pinned-images
chmod 664 /etc/neonkube/pinned-images

# Replace the CRI-O binary with our custom one.

systemctl stop crio
curl {KubeHelper.CurlOptions} {KubeDownloads.NeonKubeStageBucketUri}/cri-o/cri-o.{KubeVersions.Crio}.gz | gunzip --stdout > /usr/bin/crio
systemctl start crio
";
                var bundle         = CommandBundle.FromScript(crioUpdateScript);
                var sbPinnedImages = new StringBuilder();

                // Generate the [/etc/neonkube/pinned-images] config file listing the 
                // the container images that our modified version of CRI-O will consider
                // as pinned and will not allow these images to be removed by Kubelet
                // and probably via podman as well.

                sbPinnedImages.AppendLine("# WARNING: automatic generation");
                sbPinnedImages.AppendLine();
                sbPinnedImages.AppendLine("# This file is generated automatically during cluster setup as well as by the");
                sbPinnedImages.AppendLine("# [neon-node-agent] so any manual changes may be overwitten at any time.");
                sbPinnedImages.AppendLine();
                sbPinnedImages.AppendLine("# This file specifies the container image references to be treated as pinned");
                sbPinnedImages.AppendLine("# by a slightly customized version of CRI-O which blocks the removal of any");
                sbPinnedImages.AppendLine("# image references listed here.");

                foreach (var image in clusterManifest.ContainerImages)
                {
                    sbPinnedImages.AppendLine();
                    sbPinnedImages.AppendLine(image.SourceRef);
                    sbPinnedImages.AppendLine(image.SourceDigestRef);
                    sbPinnedImages.AppendLine(image.InternalRef);
                    sbPinnedImages.AppendLine(image.InternalDigestRef);

                    // We need to extract the image ID from the internal digest reference which
                    // looks something like this:
                    //
                    //      registry.neon.local/redis@sha256:561AABD123...
                    //
                    // We need to extract the HEX bytes after the [@sha256:] prefix

                    var idPrefix    = "@sha256:";
                    var idPrefixPos = image.InternalDigestRef.IndexOf(idPrefix);

                    Covenant.Assert(idPrefixPos != -1);

                    sbPinnedImages.AppendLine(image.InternalDigestRef.Substring(idPrefixPos + idPrefix.Length));
                }

                bundle.AddFile("pinned-images", sbPinnedImages.ToString());

                SudoCommand(bundle).EnsureSuccess();
            }
        }

        /// <summary>
        /// Installs the <b>podman</b> CLI for managing <b>CRI-O</b>.
        /// </summary>
        /// <param name="controller">Specifies the setup controller.</param>
        public void NodeInstallPodman(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            InvokeIdempotent("setup/podman",
                () =>
                {
                    controller.LogProgress(this, verb: "setup", message: "podman");

                    var setupScript =
$@"
# $todo(jefflill):
#
# [podman] installation is having some trouble with a couple package depedencies,
# which causes the install command to return a non-zero exit code.  This appears
# to be a problem with two package dependencies trying to update the same file.
#
# We're going to use an option to force the file overwrite and then ignore any
# errors for now and hope for the best.  This isn't as bad as it sounds because 
# for NEONKUBE we're only calling this method while creating node images, so we'll
# should be well aware of any problems while completing the node image configuration
# and then deploying test clusters.
#
#       https://github.com/nforgeio/neonKUBE/issues/1571
#       https://github.com/containers/podman/issues/14367
#
# I'm going to hack this for now by using this option:
#
#   -o Dpkg::Options::=""--force-overwrite""
#
# and ignoring errors from the command.

set -euo pipefail

{KubeConst.SafeAptGetTool} update -q
set +e
{KubeConst.SafeAptGetTool} install -yq podman -o Dpkg::Options::=""--force-overwrite""
{KubeConst.SafeAptGetTool} install -yq skopeo
ln -s /usr/bin/podman /bin/docker

# Prevent the package manager from automatically upgrading these components.

set +e      # Don't exit if the next command fails
apt-mark hold podman 
apt-mark hold skopeo 

# podman installs a config files with the CNI version set to 1.0.0 which is not compatible
# with Kubernetes 1.28 which expects the CNI version to be 0.4.0.  We're going to replace
# these files and then and reset podman.

rm /etc/cni/net.d/*

cat <<EOF > /etc/cni/net.d/100-crio-bridge.conflist
{{
  ""cniVersion"": ""0.4.0"",
  ""name"": ""crio"",
  ""plugins"": [
    {{
      ""type"": ""bridge"",
      ""bridge"": ""cni0"",
      ""isGateway"": true,
      ""ipMasq"": true,
      ""hairpinMode"": true,
      ""ipam"": {{
        ""type"": ""host-local"",
        ""routes"": [
            {{ ""dst"": ""0.0.0.0/0"" }},
            {{ ""dst"": ""::/0"" }}
        ],
        ""ranges"": [
            [{{ ""subnet"": ""10.85.0.0/16"" }}],
            [{{ ""subnet"": ""1100:200::/24"" }}]
        ]
      }}
    }}
  ]
}}
EOF

cat <<EOF > 200-loopback.conflist
{{
    ""cniVersion"": ""1.0.0"",
    ""name"": ""loopback"",
    ""plugins"": [
        {{
            ""type"": ""loopback""
        }}
    ]
}}
EOF

# podman complains about this config file so we'll remove it before
# resetting podman.

/etc/containers/storage.conf

chmod 644 /etc/cni/net.d/*
podman system reset --force
";
                    // The PODMAN apt package mirror has been quite unreliable over the years, 
                    // so we're going to retry the operation in the hope that it may work
                    // eventually.

                    try
                    {
                        retry.Invoke(
                            () =>
                            {
                                SudoCommand(CommandBundle.FromScript(setupScript), RunOptions.Defaults)
                                    .EnsureSuccess();
                            });
                    }
                    catch (Exception e)
                    {
                        Fault(e.Message);
                    }
                });
        }

        /// <summary>
        /// Installs the <b>Helm</b> client.
        /// </summary>
        /// <param name="controller">Specifies the setup controller.</param>
        public void NodeInstallHelm(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            InvokeIdempotent("setup/helm-client",
                () =>
                {
                    controller.LogProgress(this, verb: "setup", message: "helm client");

                    var script =
$@"
set -euo pipefail

cd /tmp
curl {KubeHelper.CurlOptions} {KubeDownloads.HelmLinuxUri} > helm.tar.gz
tar xvf helm.tar.gz
cp linux-amd64/helm /usr/local/bin
chmod 770 /usr/local/bin/helm
rm -f helm.tar.gz
rm -rf linux-amd64
";
                    SudoCommand(CommandBundle.FromScript(script), RunOptions.Defaults | RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Loads the docker images onto the node. This is used for debug mode only.
        /// </summary>
        /// <param name="controller">Specifies the setup controller.</param>
        /// <param name="downloadParallel">Optionally specifies the limit for parallelism when downloading images from GitHub registry.</param>
        /// <param name="loadParallel">Optionally specifies the limit for parallelism when loading images into the cluster.</param>
        public async Task NodeDebugLoadImagesAsync(
            ISetupController    controller, 
            int                 downloadParallel = 5, 
            int                 loadParallel     = 2)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            await InvokeIdempotentAsync("setup/debug-load-images",
                async () =>
                {
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NC_ROOT")))
                    {
                        await Task.CompletedTask;
                        return;
                    }

                    controller.LogProgress(this, verb: "load", message: "container images (debug mode)");

                    var dockerPath = NeonHelper.VerifiedDockerCli;
                    var images     = new List<NodeImageInfo>();

                    var setupImageFolders = Directory.EnumerateDirectories($"{Environment.GetEnvironmentVariable("NC_ROOT")}/Images/setup")
                        .Select(path => Path.GetFullPath(path))
                        .ToArray();

                    var registry       = controller.Get<bool>(KubeSetupProperty.ReleaseMode) ? "ghcr.io/neonkube" : "ghcr.io/neonkube-stage";
                    var pullImageTasks = new List<Task>();

                    foreach (var imageFolder in setupImageFolders)
                    {
                        var imageName   = Path.GetFileName(imageFolder);
                        var versionPath = Path.Combine(imageFolder, ".version");
                        var importPath  = Path.Combine(imageFolder, ".import");
                        var targetTag   = (string)null;

                        if (File.Exists(versionPath))
                        {
                            targetTag = File.ReadAllLines(versionPath).First().Trim();

                            if (string.IsNullOrEmpty(targetTag))
                            {
                                throw new FileNotFoundException($"Setup image folder [{imageFolder}] has an empty a [.version] file.");
                            }
                        }
                        else
                        {
                            Covenant.Assert(File.Exists(importPath));

                            targetTag = $"neonkube-{KubeVersions.NeonKube}{KubeVersions.BranchPart}";
                        }

                        var sourceImage = $"{registry}/{imageName}:{targetTag}";

                        while (pullImageTasks.Where(task => task.Status < TaskStatus.RanToCompletion).Count() >= downloadParallel)
                        {
                            await Task.Delay(100);
                        }

                        images.Add(new NodeImageInfo(imageFolder, imageName, targetTag, registry));
                        pullImageTasks.Add(NeonHelper.ExecuteCaptureAsync(dockerPath, new string[] { "pull", sourceImage }));
                    }

                    await NeonHelper.WaitAllAsync(pullImageTasks);

                    var loadImageTasks = new List<Task>();

                    foreach (var image in images)
                    {
                        while (loadImageTasks.Where(task => task.Status < TaskStatus.RanToCompletion).Count() >= loadParallel)
                        {
                            await Task.Delay(100);
                        }

                        loadImageTasks.Add(LoadImageAsync(image));
                    }

                    await NeonHelper.WaitAllAsync(loadImageTasks);

                    SudoCommand($"rm -rf /tmp/container-image-*");
                });
        }

        /// <summary>
        /// Method to load specific container image onto the the node.
        /// </summary>
        /// <param name="image">The image.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task LoadImageAsync(NodeImageInfo image)
        {
            await SyncContext.Clear;
            await Task.Yield();

            var dockerPath = NeonHelper.VerifiedDockerCli;

            await InvokeIdempotentAsync($"setup/debug-load-images-{image.Name}",
                async () =>
                {
                    await Task.CompletedTask;

                    var id = Guid.NewGuid().ToString("d");

                    using (var tempFile = new TempFile(suffix: ".image.tar", null))
                    {
                        NeonHelper.ExecuteCapture(dockerPath,
                            new string[] {
                                "save",
                                "--output", tempFile.Path,
                                image.SourceImage }).EnsureSuccess();

                        using (var imageStream = new FileStream(tempFile.Path, FileMode.Open, FileAccess.ReadWrite))
                        {
                            Upload($"/tmp/container-image-{id}.tar", imageStream);

                            SudoCommand("podman load", RunOptions.Defaults | RunOptions.FaultOnError,
                                new string[]
                                {
                                    "--input", $"/tmp/container-image-{id}.tar"
                                });

                            SudoCommand("podman tag", RunOptions.Defaults | RunOptions.FaultOnError,
                                new string[]
                                {
                                    image.SourceImage, image.TargetImage
                                });

                            SudoCommand($"rm /tmp/container-image-{id}.tar");
                        }
                    }
                });
        }
  
        /// <summary>
        /// Installs the Kubernetes components: <b>kubeadm</b>, <b>kubectl</b>, and <b>kubelet</b>.
        /// </summary>
        /// <param name="controller">Specifies the setup controller.</param>
        public void NodeInstallKubernetes(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            InvokeIdempotent("base/kubernetes",
                () =>
                {
                    controller.LogProgress(this, "install", "kubernetes");

                    var hostingEnvironment = controller.Get<HostingEnvironment>(KubeSetupProperty.HostingEnvironment);

                    // We ran into a problem downloading the Google [apt-key.gpg] file which
                    // caused node image build failures:
                    //
                    //      https://github.com/nforgeio/neonKUBE/issues/1754
                    //      https://github.com/kubernetes/kubernetes/issues/116068
                    //
                    // It looks like Kubernetes has an alternate URI for this key that hits
                    // their release website, so it should be safe.  We're going to use this
                    // alternate URI if the primary fails.
                    //
                    // The Kubernetes package mirror can have problems, so we're going to 
                    // workaround this with a retry policy.

                    retry.Invoke(
                        () =>
                        {
                            // Perform the install.

                            var kubernetesFullVersion    = SemanticVersion.Parse(KubeVersions.Kubernetes);
                            var kubernetesVersionNoPatch = new Version(kubernetesFullVersion.Major, kubernetesFullVersion.Minor);

                            var mainScript =
$@"
set -euo pipefail

set +e
curl {KubeHelper.CurlOptions} https://packages.cloud.google.com/apt/doc/apt-key.gpg | apt-key add -
haveKey=$? 

if [ ! $haveKey ]; then
    
    # The primary apt-key URI failed, so we're going to use the alternate, after
    # re-enabling error checking.

    set -euo pipefail
    curl {KubeHelper.CurlOptions} https://dl.k8s.io/apt/doc/apt-key.gpg | apt-key add -    
fi
set -e

# Configure the APT signing key and configure the Kubernetes APT repository.
#
#       https://kubernetes.io/blog/2023/10/10/cri-o-community-package-infrastructure/#add-the-kubernetes-repository

VERSION={kubernetesVersionNoPatch}

mkdir -p /etc/apt/keyrings
rm -f /etc/apt/keyrings/kubernetes-apt-keyring.gpg
rm -f /etc/apt/sources.list.d/kubernetes.list

curl -fsSL https://pkgs.k8s.io/core:/stable:/v$VERSION/deb/Release.key | gpg --dearmor -o /etc/apt/keyrings/kubernetes-apt-keyring.gpg
echo ""deb [signed-by=/etc/apt/keyrings/kubernetes-apt-keyring.gpg] https://pkgs.k8s.io/core:/stable:/v$VERSION/deb/ /"" | tee /etc/apt/sources.list.d/kubernetes.list

# Install: kubelet, kubeadm, and kubectl.

{KubeConst.SafeAptGetTool} update
{KubeConst.SafeAptGetTool} install -yq kubelet kubeadm kubectl

# Prevent the package manager from upgrading these components.

set +e      # Don't exit if the next command fails
apt-mark hold kubeadm kubectl kubelet
set -euo pipefail

# We need to restart CRI-O here.  I'm not entirely sure why.

systemctl restart cri-o

# Pull the core Kubernetes container images (kube-scheduler, kube-proxy,...) to ensure they'll 
# be present on all node images.  Note that we need to use a config file to ensure that container
# images are pulled from [registry.k8s.io] rather than [k8s.gcr.io] which used to be the default
# but was depreciated in 2022 and stopped receiving updates in 2023.

cat <<EOF > kubeadm.config.yaml
apiVersion: kubeadm.k8s.io/v1beta3
kind: ClusterConfiguration
imageRepository: registry.k8s.io
dns:
  imageRepository: registry.k8s.io/coredns
EOF

kubeadm config images pull --config=kubeadm.config.yaml 
rm kubeadm.config.yaml

# Configure kubelet:

mkdir -p /opt/cni/bin
mkdir -p /etc/cni/net.d

echo KUBELET_EXTRA_ARGS=--container-runtime-endpoint='unix:///var/run/crio/crio.sock' --node-ip='{NodeDefinition.Address}' > /etc/default/kubelet

# Stop and disable [kubelet] for now.  We'll re-enable this later during cluster setup.

systemctl daemon-reload
systemctl stop kubelet
systemctl disable kubelet
";
                            controller.LogProgress(this, verb: "install", message: "kubernetes");

                            SudoCommand(CommandBundle.FromScript(mainScript), RunOptions.Defaults | RunOptions.FaultOnError);
                        });
                });
        }
    }
}
