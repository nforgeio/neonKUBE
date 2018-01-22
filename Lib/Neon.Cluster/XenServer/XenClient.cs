//-----------------------------------------------------------------------------
// FILE:	    XenClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Cluster;
using Neon.Common;

namespace Neon.Cluster.XenServer
{
    /// <summary>
    /// This class provides a simple light-weight XenServer API that
    /// connects to the the XenServer host operating system via SSH
    /// and executes commands using the <b>xe</b> XenServer client
    /// tool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ideally, we'd use the XenServer .NET API but at this time (Jan 2018),
    /// the API is not compatible with .NET Core which neonCLUSTER <b>neon-cli</b>
    /// requires because it needs to run on Windows, OSX, and perhaps some day
    /// within the Ubuntu based tool container.
    /// </para>
    /// <para>
    /// The workaround is to simnply connect to the XenServer host via SSH
    /// and perform commands using the <b>xe</b> command line tool installed
    /// with XenServer.  We're going to take advantage of the <see cref="NodeProxy{TMetadata}"/>
    /// class to handle the SSH connection and command execution.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="false"/>
    public sealed class XenClient : IDisposable
    {
        // Implementation Note:
        // --------------------
        // The following PDF documents are handy resources for learning about the
        // XE command line tool.
        //
        //      https://docs.citrix.com/content/dam/docs/en-us/xenserver/current-release/downloads/xenserver-vm-users-guide.pdf
        //      https://docs.citrix.com/content/dam/docs/en-us/xenserver/xenserver-7-0/downloads/xenserver-7-0-management-api-guide.pdf

        private NodeProxy<object>   server;

        /// <summary>
        /// Constructor.  Note that you should dispose the instance when you're finished with it.
        /// </summary>
        /// <param name="addressOrFQDN">The target XenServer IP address or FQDN.</param>
        /// <param name="username">The user name.</param>
        /// <param name="password">The password.</param>
        public XenClient(string addressOrFQDN, string username, string password)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(username));

            if (!IPAddress.TryParse(addressOrFQDN, out var address))
            {
                var hostEntry = Dns.GetHostEntry(addressOrFQDN);

                if (hostEntry.AddressList.Length == 0)
                {
                    throw new XenException($"[{addressOrFQDN}] is not a valid IP address or fully qualified domain name of a XenServer host.");
                }

                address = hostEntry.AddressList.First();
            }

            server = new NodeProxy<object>(addressOrFQDN, null, address, SshCredentials.FromUserPassword(username, password));
        }

        /// <summary>
        /// Releases any resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            if (server == null)
            {
                server.Dispose();
                server = null;
            }
        }

        /// <summary>
        /// Verifies that that the instance hasn't been disposed.
        /// </summary>
        private void VerifyNotDisposed()
        {
            if (server == null)
            {
                throw new ObjectDisposedException(nameof(XenClient));
            }
        }

        /// <summary>
        /// Invokes a low-level <b>xe CLI</b> command on the remote XenServer host
        /// that returns text.
        /// </summary>
        /// <param name="command">The <b>xe CLI</b> command.</param>
        /// <param name="args">The optional arguments formatted as <b>name=value</b>.</param>
        /// <returns>The command response.</returns>
        public CommandResponse Invoke(string command, params string[] args)
        {
            return server.RunCommand($"xe {command}", args);
        }

        /// <summary>
        /// Invokes a low-level <b>xe CLI</b> command on the remote XenServer host
        /// that returns a list of items.
        /// </summary>
        /// <param name="command">The <b>xe CLI</b> command.</param>
        /// <param name="args">The optional arguments formatted as <b>name=value</b>.</param>
        /// <returns>The command <see cref="XenResponse"/>.</returns>
        public XenResponse InvokeItems(string command, params string[] args)
        {
            return new XenResponse(server.RunCommand($"xe {command}", args));
        }

        /// <summary>
        /// Invokes a low-level <b>xe CLI</b> command on the remote XenServer host
        /// that returns text, throwing an exception on failure.
        /// </summary>
        /// <param name="command">The <b>xe CLI</b> command.</param>
        /// <param name="args">The optional arguments formatted as <b>name=value</b>.</param>
        /// <returns>The command response.</returns>
        /// <exception cref="XenException">Thrown if the operation failed.</exception>
        public CommandResponse SafeInvoke(string command, params string[] args)
        {
            var response = server.RunCommand($"xe {command}", args);

            if (response.ExitCode != 0)
            {
                throw new XenException(response.ErrorText);
            }

            return response;
        }

        /// <summary>
        /// Invokes a low-level <b>xe CLI</b> command on the remote XenServer host
        /// that returns a list of items, throwing an exception on failure.
        /// </summary>
        /// <param name="command">The <b>xe CLI</b> command.</param>
        /// <param name="args">The optional arguments formatted as <b>name=value</b>.</param>
        /// <returns>The command <see cref="XenResponse"/>.</returns>
        /// <exception cref="XenException">Thrown if the operation failed.</exception>
        public XenResponse SafeInvokeItems(string command, params string[] args)
        {
            return new XenResponse(SafeInvoke(command, args));
        }

        /// <summary>
        /// Lists the XenServer storage repositories.
        /// </summary>
        /// <returns>The list of storage repositories.</returns>
        public List<XenStorageRepository> ListStorageRepositories()
        {
            var response     = InvokeItems("sr-list");
            var repositories = new List<XenStorageRepository>();

            if (response.ExitCode != 0)
            {
                return repositories;
            }

            foreach (var result in response.Results)
            {
                repositories.Add(new XenStorageRepository(result));
            }

            return repositories;
        }

        /// <summary>
        /// Finds a specific storage repository by name or unique ID.
        /// </summary>
        /// <param name="name">Specifies the target name.</param>
        /// <param name="uuid">Specifies the target unique ID.</param>
        /// <returns>The named item or <c>null</c> if it doesn't exist.</returns>
        /// <remarks>
        /// <note>
        /// One of <paramref name="name"/> or <paramref name="uuid"/> must be specified.
        /// </note>
        /// </remarks>
        public XenStorageRepository FindStorageRepository(string name = null, string uuid = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(uuid));

            if (!string.IsNullOrWhiteSpace(name))
            {
                return ListStorageRepositories().FirstOrDefault(item => item.NameLabel == name);
            }
            else if (!string.IsNullOrWhiteSpace(uuid))
            {
                return ListStorageRepositories().FirstOrDefault(item => item.Uuid == uuid);
            }
            else
            {
                throw new ArgumentException($"One of [{nameof(name)}] or [{nameof(uuid)}] must be specified.");
            }
        }

        /// <summary>
        /// Returns the local XenServer storage repository.
        /// </summary>
        /// <returns>The local storage repository.</returns>
        /// <exception cref="XenException">Thrown if the operation failed.</exception>
        public XenStorageRepository GetLocalStorageRepository()
        {
            // We're going to first look for a repo named "Local storage"
            // and if that doesn't exist, we'll return the first SR with
            // type==lvm.

            var repos = ListStorageRepositories();
            var sr    = repos.FirstOrDefault(item => item.NameLabel == "Local storage");

            if (sr != null)
            {
                return sr;
            }

            sr = repos.FirstOrDefault(item => item.Type == "lvm");

            if (sr != null)
            {
                return sr;
            }
            else
            {
                throw new XenException("Cannot find the [Local storage] repository.");
            }
        }

        /// <summary>
        /// Lists the XenServer virtual machine templates.
        /// </summary>
        /// <returns>The list of templates.</returns>
        /// <exception cref="XenException">Thrown if the operation failed.</exception>
        public List<XenTemplate> ListTemplates()
        {
            var response  = SafeInvokeItems("template-list");
            var templates = new List<XenTemplate>();

            if (response.ExitCode != 0)
            {
                return templates;
            }

            foreach (var result in response.Results)
            {
                templates.Add(new XenTemplate(result));
            }

            return templates;
        }

        /// <summary>
        /// Finds a specific virtual machine by name or unique ID.
        /// </summary>
        /// <param name="name">Specifies the target name.</param>
        /// <param name="uuid">Specifies the target unique ID.</param>
        /// <returns>The named item or <c>null</c> if it doesn't exist.</returns>
        /// <exception cref="XenException">Thrown if the operation failed.</exception>
        /// <remarks>
        /// <note>
        /// One of <paramref name="name"/> or <paramref name="uuid"/> must be specified.
        /// </note>
        /// </remarks>
        public XenTemplate FindTemplate(string name = null, string uuid = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(uuid));

            if (!string.IsNullOrWhiteSpace(name))
            {
                return ListTemplates().FirstOrDefault(item => item.NameLabel == name);
            }
            else if (!string.IsNullOrWhiteSpace(uuid))
            {
                return ListTemplates().FirstOrDefault(item => item.Uuid == uuid);
            }
            else
            {
                throw new ArgumentException($"One of [{nameof(name)}] or [{nameof(uuid)}] must be specified.");
            }
        }

        /// <summary>
        /// Downloads and installs an XVA or OVA virtual machine template, optionally renaming it.
        /// </summary>
        /// <param name="uri">The HTTP or FTP URI for the template file.</param>
        /// <param name="name">The optional template name.</param>
        /// <param name="sr">Optionally specifies the target storage repository.  This defaults to <b>Local storage</b>.</param>
        /// <returns>The installed template.</returns>
        /// <exception cref="XenException">Thrown if the operation failed.</exception>
        public XenTemplate InstallTemplate(string uri, string name = null, XenStorageRepository sr = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri));

            if (!Uri.TryCreate(uri, UriKind.Absolute, out var uriParsed))
            {
                throw new ArgumentException($"[{uri}] is not a valid URI");
            }

            if (sr == null)
            {
                sr = GetLocalStorageRepository();
            }

            if (uriParsed.Scheme != "http" && uriParsed.Scheme != "ftp")
            {
                throw new ArgumentException($"[{uri}] uses an unsupported scheme.  Only [http/ftp] are allowed.");
            }

            var response = SafeInvoke("vm-import", $"url={uri}", $"sr-uuid={sr.Uuid}");
            var uuid     = response.OutputText.Trim();
            var template = FindTemplate(uuid: uuid);

            if (!string.IsNullOrEmpty(name))
            {
                template = RenameTemplate(template, name);
            }

            return template;
        }

        /// <summary>
        /// Renames a virtual machine template.
        /// </summary>
        /// <param name="template">The target template.</param>
        /// <param name="newName">The new template name.</param>
        /// <returns>The modified template.</returns>
        /// <exception cref="XenException">Thrown if the operation failed.</exception>
        public XenTemplate RenameTemplate(XenTemplate template, string newName)
        {
            Covenant.Requires<ArgumentNullException>(template != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(newName));

            SafeInvoke("template-param-set", $"uuid={template.Uuid}", $"name-label={newName}");

            return FindTemplate(uuid: template.Uuid);
        }

        /// <summary>
        /// Removes a virtual machine template.
        /// </summary>
        /// <param name="template">The target template.</param>
        /// <exception cref="XenException">Thrown if the operation failed.</exception>
        public void DestroyTemplate(XenTemplate template)
        {
            Covenant.Requires<ArgumentNullException>(template != null);

            SafeInvoke("vm-destroy", $"uuid={template.Uuid}");
        }
    }
}

// xe vm-import url=http://s3-us-west-2.amazonaws.com/neonforge/neoncluster/ubuntu-16.04.latest-prep.xva force=true sr-uuid=1aedccc5-8b18-4fc8-b498-e776a5ae2702
// b2ae3104-60d3-3ab3-b846-9c23e1bb0b85
