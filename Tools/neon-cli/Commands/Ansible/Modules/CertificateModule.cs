//-----------------------------------------------------------------------------
// FILE:	    CertificateModule.cs
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

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using ICSharpCode.SharpZipLib.Zip;

using Neon.Cryptography;
using Neon.Common;
using Neon.IO;
using Neon.Hive;
using Neon.Net;

namespace NeonCli.Ansible
{
    //---------------------------------------------------------------------
    // neon_certificate:
    //
    // Synopsis:
    // ---------
    //
    // Manages neonHIVE TLS certificates.
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
    // name         yes                                 neonHIVE certificate name
    //
    // state        no          present     present     indicates whether the certificate should
    //                                      absent      be created or removed
    //
    // value        see comment                         public certificate, any intermediate
    //                                                  certificates and the private key in PEM 
    //                                                  format.  Required when [state=present]
    //
    // force        no          false                   persists the certificate when [state=present]
    //                                                  even if the certificate is unchanged
    //
    // Check Mode:
    // -----------
    //
    // This module supports the [--check] Ansible command line option and [check_mode] task
    // property by determining whether any changes would have been made and also logging
    // a desciption of the changes when Ansible verbosity is increased.
    //
    // Examples:
    // ---------
    //
    // This example creates or updates an explicitly specified certificate:
    //
    //  - name: test
    //    hosts: localhost
    //    tasks:
    //      - name: foo.com certificate
    //        neon_certificate:
    //          name: foo.com
    //          state: present
    //          value: |
    //            -----BEGIN CERTIFICATE-----
    //            MIIFUTCCBDmgAwIBAgIQQAs/u3q0c8hRqxu20YgHWzANBgkqhkiG9w0BAQsFADCB
    //            kDELMAkGA1UEBhMCR0IxGzAZBgNVBAgTEkdyZWF0ZXIgTWFuY2hlc3RlcjEQMA4G
    //            A1UEBxMHU2FsZm9yZDEaMBgGA1UEChMRQ09NT0RPIENBIExpbWl0ZWQxNjA0BgNV
    //            ...
    //            -----END CERTIFICATE-----
    //
    // This example creates or updates a certificate from a variable:
    //
    //  - name: test
    //    hosts: localhost
    //    tasks:
    //      - name: foo.com certificate
    //        neon_certificate:
    //          name: foo.com
    //          state: present
    //          value: "{{ FOO_COM_CERTIFICATE }}"
    //
    // This example deletes a certificate if it exists:
    //
    //  - name: test
    //    hosts: localhost
    //    tasks:
    //      - name: foo.com certificate
    //        neon_certificate:
    //          name: foo.com
    //          state: absent

    /// <summary>
    /// Implements the <b>neon_certificate</b> Ansible module.
    /// </summary>
    public class CertificateModule : IAnsibleModule
    {
        private HashSet<string> validModuleArgs = new HashSet<string>()
        {
            "name",
            "state",
            "value",
            "force"
        };

        /// <inheritdoc/>
        public void Run(ModuleContext context)
        {
            var hive = HiveHelper.Hive;

            if (!context.ValidateArguments(context.Arguments, validModuleArgs))
            {
                context.Failed = true;
                return;
            }

            // Obtain common arguments.

            if (!context.Arguments.TryGetValue<string>("name", out var name))
            {
                throw new ArgumentException($"[name] module argument is required.");
            }

            if (!HiveDefinition.IsValidName(name))
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

            switch (state)
            {
                case "absent":

                    context.WriteLine(AnsibleVerbosity.Trace, $"Vault: checking for [{name}] certificate");

                    if (hive.Certificate.Get(name) != null)
                    {
                        context.WriteLine(AnsibleVerbosity.Trace, $"Vault: [{name}] certificate exists");

                        if (context.CheckMode)
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"Certificate [{name}] will be removed when CHECK-MODE is disabled.");
                        }
                        else
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"Removing [{name}] certyificate.");
                            hive.Certificate.Remove(name);
                            context.WriteLine(AnsibleVerbosity.Info, $"[{name}] certificate removed");
                        }

                        context.Changed = !context.CheckMode;
                    }
                    else
                    {
                        context.WriteLine(AnsibleVerbosity.Info, $"[{name}] certificate does not exist");
                    }
                    break;

                case "present":

                    if (!context.Arguments.TryGetValue<string>("value", out var value))
                    {
                        throw new ArgumentException($"[value] module argument is required.");
                    }

                    var certificate = TlsCertificate.Parse(value);    // This validates the certificate/private key

                    context.WriteLine(AnsibleVerbosity.Trace, $"Reading [{name}] certificate");

                    var existingCert = hive.Certificate.Get(name);
                    var changed      = false;

                    if (existingCert == null)
                    {
                        context.WriteLine(AnsibleVerbosity.Info, $"[{name}] certificate does not exist");
                        context.Changed = !context.CheckMode;

                        changed = true;
                    }
                    else if (!NeonHelper.JsonEquals(existingCert, certificate) || force)
                    {
                        context.WriteLine(AnsibleVerbosity.Info, $"[{name}] certificate does exists but is different");
                        context.Changed = !context.CheckMode;

                        changed = true;
                    }
                    else
                    {
                        context.WriteLine(AnsibleVerbosity.Info, $"[{name}] certificate is unchanged");
                    }

                    if (changed)
                    {
                        if (context.CheckMode)
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"Certificate [{name}] will be updated when CHECK-MODE is disabled.");
                        }
                        else
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"Saving [{name}] certificate");
                            hive.Certificate.Set(name, certificate);
                            context.WriteLine(AnsibleVerbosity.Info, $"[{name}] certificate saved");
                        }
                    }

                    break;

                default:

                    throw new ArgumentException($"[state={state}] is not one of the valid choices: [present] or [absent].");
            }
        }
    }
}
