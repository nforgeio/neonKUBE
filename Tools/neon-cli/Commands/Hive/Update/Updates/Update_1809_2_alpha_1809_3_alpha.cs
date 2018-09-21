//-----------------------------------------------------------------------------
// FILE:	    Update_1809_2_alpha_1809_3_alpha.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ICSharpCode.SharpZipLib.Zip;

using Neon.Common;
using Neon.IO;
using Neon.Hive;
using Neon.Net;

namespace NeonCli
{
    /// <summary>
    /// Updates a hive from version <b>18.9.2-alpha</b> to <b>18.9.3-alpha</b>.
    /// </summary>
    [HiveUpdate]
    public class Update_1809_2_alpha_1809_3_alpha : HiveUpdate
    {
        /// <inheritdoc/>
        public override SemanticVersion FromVersion { get; protected set; } = SemanticVersion.Parse("18.9.2-alpha");

        /// <inheritdoc/>
        public override SemanticVersion ToVersion { get; protected set; } = SemanticVersion.Parse("18.9.3-alpha");

        /// <inheritdoc/>
        public override bool RestartRequired => true;

        /// <inheritdoc/>
        public override void AddUpdateSteps(SetupController<NodeDefinition> controller)
        {
            base.Initialize(controller);

            controller.AddGlobalStep(GetStepLabel("hive bootstrap"), () => UpdateHiveBootstrap());
            controller.AddStep(GetStepLabel("linux limits"), (node, stepDelay) => UpdateLimits(node));
            HiveUpdateManager.AddRestartClusterStep(base.Hive, controller, stepLabel: GetStepLabel("restart nodes"));
            controller.AddGlobalStep(GetStepLabel("hive version"), () => UpdateHiveVersion());
        }

        /// <summary>
        /// Updates the HiveMQ bootstrap settings.
        /// </summary>
        private void UpdateHiveBootstrap()
        {
            var firstManager = Hive.FirstManager;

            // HiveMQ Bootstrap settings: https://github.com/jefflill/NeonForge/issues/337

            firstManager.InvokeIdempotentAction(GetIdempotentTag("hivemq-bootstrap"),
                () =>
                {
                    firstManager.Status = "update: hivemq bootstrap";
                    Hive.HiveMQ.SaveBootstrapSettings();
                    firstManager.Status = string.Empty;
                });
        }

        /// <summary>
        /// Increases various Linux kernel limits.  This requires <b>node restart</b>.
        /// </summary>
        /// <param name="node">The target node.</param>
        private void UpdateLimits(SshProxy<NodeDefinition> node)
        {
            node.InvokeIdempotentAction(GetIdempotentTag("kernel-limits"),
                () =>
                {
                    var newLimits =
@"# /etc/security/limits.conf
#
#Each line describes a limit for a user in the form:
#
#<domain>        <type>  <item>  <value>
#
#Where:
#<domain> can be:
#        - a user name
#        - a group name, with @group syntax
#        - the wildcard *, for default entry
#        - the wildcard %, can be also used with %group syntax,
#                 for maxlogin limit
#        - NOTE: group and wildcard limits are not applied to root.
#          To apply a limit to the root user, <domain> must be
#          the literal username root.
#
#<type> can have the two values:
#        - ""soft"" for enforcing the soft limits
#        - ""hard"" for enforcing hard limits
#
#<item> can be one of the following:
#        - core - limits the core file size (KB)
#        - data - max data size (KB)
#        - fsize - maximum filesize (KB)
#        - memlock - max locked-in-memory address space (KB)
#        - nofile - max number of open files
#        - rss - max resident set size (KB)
#        - stack - max stack size (KB)
#        - cpu - max CPU time (MIN)
#        - nproc - max number of processes
#        - as - address space limit (KB)
#        - maxlogins - max number of logins for this user
#        - maxsyslogins - max number of logins on the system
#        - priority - the priority to run user process with
#        - locks - max number of file locks the user can hold
#        - sigpending - max number of pending signals
#        - msgqueue - max memory used by POSIX message queues (bytes)
#        - nice - max nice priority allowed to raise to values: [-20, 19]
#        - rtprio - max realtime priority
#        - chroot - change root to directory (Debian-specific)
#
#<domain>   <type>  <item>  <value>

root    soft    nofile  unlimited
root    hard    nofile  unlimited
root    soft    memlock unlimited
root    hard    memlock unlimited
root    soft    nproc   unlimited
root    hard    nproc   unlimited

*       soft    nofile  unlimited
*       hard    nofile  unlimited
*       soft    memlock unlimited
*       hard    memlock unlimited
*       soft    nproc   unlimited
*       hard    nproc   unlimited

# End of file
";
                    node.UploadText("/etc/security/limits.conf", newLimits);
                    node.SudoCommand("chmod 644 /etc/security/limits.conf");
                });

            node.InvokeIdempotentAction(GetIdempotentTag("systemd-limits"),
                () =>
                {
                    var newLimits =
@"
#  This file is part of systemd.
#
#  systemd is free software; you can redistribute it and/or modify it
#  under the terms of the GNU Lesser General Public License as published by
#  the Free Software Foundation; either version 2.1 of the License, or
#  (at your option) any later version.
#
# You can override the directives in this file by creating files in
# /etc/systemd/user.conf.d/*.conf.
#
# See systemd-user.conf(5) for details

[Manager]
DefaultLimitNOFILE=infinity
DefaultLimitNPROC=infinity
DefaultLimitMEMLOCK=infinity
";
                    node.UploadText("/etc/systemd/user.conf.d/50-neon.conf", newLimits);
                    node.SudoCommand("chmod 644 /etc/systemd/user.conf.d/50-neon.conf");
                });

            node.InvokeIdempotentAction(GetIdempotentTag("kernel-settings"),
                () =>
                {
                    var newSettings =
@"#
# /etc/sysctl.conf - Configuration file for setting system variables
# See /etc/sysctl.d/ for additional system variables.
# See sysctl.conf (5) for information.
#

#kernel.domainname = example.com

# Uncomment the following to stop low-level messages on console
#kernel.printk = 3 4 1 3

##############################################################3
# Functions previously found in netbase
#

# Uncomment the next two lines to enable Spoof protection (reverse-path filter)
# Turn on Source Address Verification in all interfaces to
# prevent some spoofing attacks
#net.ipv4.conf.default.rp_filter=1
#net.ipv4.conf.all.rp_filter=1

# Uncomment the next line to enable TCP/IP SYN cookies
# See http://lwn.net/Articles/277146/
# Note: This may impact IPv6 TCP sessions too
#net.ipv4.tcp_syncookies=1

# Docker overlay networks require TCP keepalive packets to be
# transmitted at least every 15 minutes on idle connections to
# prevent zombie connections that appear to be alive but don't
# actually transmit data.  This is described here:
#
#	https://github.com/moby/moby/issues/31208
#
# We're going to configure TCP connections to begin sending
# keepalives after 300 seconds (5 minutes) of being idle and 
# then every 30 seconds thereafter (regardless of any other
# traffic).  If keepalives are not ACKed after 30*5 seconds 
# (2.5 minutes), Linux will report the connection as closed 
# to the application layer.

net.ipv4.tcp_keepalive_time = 300
net.ipv4.tcp_keepalive_intvl = 30
net.ipv4.tcp_keepalive_probes = 5

###################################################################
# Additional settings - these settings can improve the network
# security of the host and prevent against some network attacks
# including spoofing attacks and man in the middle attacks through
# redirection. Some network environments, however, require that these
# settings are disabled so review and enable them as needed.
#
# Do not accept ICMP redirects (prevent MITM attacks)
#net.ipv4.conf.all.accept_redirects = 0
#net.ipv6.conf.all.accept_redirects = 0
# _or_
# Accept ICMP redirects only for gateways listed in our default
# gateway list (enabled by default)
# net.ipv4.conf.all.secure_redirects = 1
#
# Do not send ICMP redirects (we are not a router)
#net.ipv4.conf.all.send_redirects = 0
#
# Do not accept IP source route packets (we are not a router)
#net.ipv4.conf.all.accept_source_route = 0
#net.ipv6.conf.all.accept_source_route = 0
#
# Log Martian Packets
#net.ipv4.conf.all.log_martians = 1

###################################################################
# Fluentd/TD-Agent recommended settings:

net.ipv4.tcp_tw_recycle = 1
net.ipv4.tcp_tw_reuse = 1

###################################################################
# OpenVPN requires packet forwarding.

net.ipv4.ip_forward=1

###################################################################
# neonHIVE settings

# Explicitly set the maximum number of file descriptors for the
# entire system.  This looks like it defaults to [398327] for
# Ubuntu 16.04 so we're going to pin this value to enforce
# consistency across Linux updates, etc.

fs.file-max=398327
";
                    node.UploadText("/etc/sysctl.conf", newSettings);
                    node.SudoCommand("chmod 644 /etc/sysctl.conf");
                });
        }
    }
}
