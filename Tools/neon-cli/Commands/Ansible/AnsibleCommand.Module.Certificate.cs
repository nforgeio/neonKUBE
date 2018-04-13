//-----------------------------------------------------------------------------
// FILE:	    AnsibleCommand.Module.Certificate.cs
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
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using ICSharpCode.SharpZipLib.Zip;

using Neon.Cluster;
using Neon.Cryptography;
using Neon.Common;
using Neon.IO;
using Neon.Net;

namespace NeonCli
{
    //---------------------------------------------------------------------
    // neon_certificate:
    //
    // Synopsis:
    // ---------
    //
    // Manages neonCLUSTER TLS certificates.
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
    // name         yes                                 neonCLUSTER certificate name
    //
    // value        see comment                         public certificate, any intermediate
    //                                                  certificates and the private key in PEM 
    //                                                  format.  Required when [state=present]
    //
    // state        no          present     absent      indicates whether the certificate should
    //                                      present     be created or removed
    //
    // force        no          false                   resaves the certificate when [state=present]
    //                                                  even if the certificate is the same

    public partial class AnsibleCommand : CommandBase
    {
        /// <summary>
        /// Implements the built-in <b>neon_certificate</b> module.
        /// </summary>
        /// <param name="context">The module context.</param>
        private void RunCertificateModule(ModuleContext context)
        {
            // Obtain common arguments.

            if (!context.Arguments.TryGetValue<string>("name", out var name))
            {
                throw new ArgumentException($"[name] module argument is required.");
            }

            if (!ClusterDefinition.IsValidName(name))
            {
                throw new ArgumentException($"[name={name}] is not a valid certificate name.");
            }

            if (!context.Arguments.TryGetValue<string>("state", out var state))
            {
                state = "present";
            }

            state = state.ToLowerInvariant();

            if (!context.Arguments.TryGetValue<bool>("force", out var force))
            {
                force = false;
            }

            if (context.HasErrors)
            {
                return;
            }

            // We have the required arguments, so perform the operation.

            if (!context.Login.HasVaultRootCredentials)
            {
                throw new ArgumentException("Access Denied: Root Vault credentials are required.");
            }

            var vaultPath = NeonClusterHelper.GetVaultCertificateKey(name);

            context.WriteLine(AnsibleVerbosity.Trace, $"Vault: Certificate path is [{vaultPath}]");
            context.WriteLine(AnsibleVerbosity.Trace, $"Vault: Opening Vault");

            using (var vault = NeonClusterHelper.OpenVault(context.Login.VaultCredentials.RootToken))
            {
                context.WriteLine(AnsibleVerbosity.Trace, $"Vault: Opened");

                switch (state)
                {
                    case "absent":

                        context.WriteLine(AnsibleVerbosity.Trace, $"Vault: checking for [{name}] certificate");

                        if (vault.ExistsAsync(vaultPath).Result)
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"Vault: [{name}] certificate exists");
                            context.WriteLine(AnsibleVerbosity.Trace, $"Vault: Deleting [{name}]");

                            if (context.CheckMode)
                            {
                                context.WriteLine(AnsibleVerbosity.Info, $"Vault: Certificate [{name}] would be deleted when CHECKMODE is disabled.");
                            }
                            else
                            {
                                vault.DeleteAsync(vaultPath).Wait();
                                context.WriteLine(AnsibleVerbosity.Info, $"Vault: [{name}] certificate deleted");

                                TouchCertChanged();
                                context.WriteLine(AnsibleVerbosity.Trace, $"Consul: Signal the certificate change");
                            }

                            context.Changed = true;
                        }
                        else
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"Vault: [{name}] certificate does not exist");
                        }
                        break;

                    case "present":

                        if (!context.Arguments.TryGetValue<string>("value", out var value))
                        {
                            throw new ArgumentException($"[value] module argument is required.");
                        }

                        var certificate = new TlsCertificate(value);    // This validates the certificate/private key

                        certificate.Parse();

                        context.WriteLine(AnsibleVerbosity.Trace, $"Vault: Reading [{name}]");

                        var existingCert = vault.ReadJsonAsync<TlsCertificate>(vaultPath, noException: true).Result;

                        if (existingCert == null)
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"Vault: [{name}] certificate does not exist");
                            context.Changed = true;
                        }
                        else if (!NeonHelper.JsonEquals(existingCert, certificate) || force)
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"Vault: [{name}] certificate does exists but is different");
                            context.Changed = true;
                        }
                        else
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"Vault: [{name}] certificate is unchanged");
                        }

                        if (context.Changed)
                        {
                            if (context.CheckMode)
                            {
                                context.WriteLine(AnsibleVerbosity.Info, $"Vault: Certificate [{name}] would be deleted when CHECKMODE is disabled.");
                            }
                            else
                            {
                                context.WriteLine(AnsibleVerbosity.Trace, $"Vault: Saving [{name}] certificate");
                                vault.WriteJsonAsync(vaultPath, certificate).Wait();
                                context.WriteLine(AnsibleVerbosity.Info, $"Vault: [{name}] certificate saved");
                            }
                        }

                        break;

                    default:

                        throw new ArgumentException($"[state={state}] is not one of the valid choices: [absent] or [present].");
                }
            }
        }

        /// <summary>
        /// Update the <b>neon-proxy-manager</b> Consul key to indicate that changes
        /// have been made to the cluster certificates.
        /// </summary>
        private void TouchCertChanged()
        {
            using (var consul = NeonClusterHelper.OpenConsul())
            {
                consul.KV.PutString("neon/service/neon-proxy-manager/conf/cert-update", DateTime.UtcNow).Wait();
            }
        }
    }
}
