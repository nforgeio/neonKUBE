//-----------------------------------------------------------------------------
// FILE:	    HiveGlobals.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Hive
{
    /// <summary>
    /// Identifies the global hive Consul globals and settings.  These are located
    /// under <b>neon/global</b>.  Settings with constant names prefixed by <b>User</b>
    /// are considered to be user-modifiable.  The other settings are generally managed
    /// only by the hive and its tools.
    /// </summary>
    public static class HiveGlobals
    {
        /// <summary>
        /// Hive creation date (UTC).
        /// </summary>
        public const string CreateDateUtc = "create-date-utc";

        /// <summary>
        /// Current hive definition as compressed JSON text (UTF-8).
        /// </summary>
        public const string DefinitionDeflate = "definition-deflated";

        /// <summary>
        /// MD5 hash of the current hive definition.
        /// </summary>
        public const string DefinitionHash = "definition-hash";

        /// <summary>
        /// Minimum <b>neon-cli</b> version allowed to manage the hive.
        /// </summary>
        public const string NeonCli = "neon-cli";

        /// <summary>
        /// Current hive pets definition.
        /// </summary>
        public const string PetsDefinition = "pets-definition";

        /// <summary>
        /// Indicates whether hive setup is still in progress (bool).
        /// </summary>
        public const string SetupPending = "setup-pending";

        /// <summary>
        /// Hive globally unique ID assigned during hive setup.
        /// </summary>
        public const string Uuid = "uuid";

        /// <summary>
        /// Version of the hive.  This is actually the version of <b>neon-cli</b> 
        /// that created or last upgraded the hive.
        /// </summary>
        public const string Version = "version";

        /// <summary>
        /// Serialized <c>HiveMQSettings</c> for the <see cref="HiveConst.HiveMQAppUser"/> HiveMQ account.
        /// </summary>
        public const string HiveMQSettingsApp = "hivemq-settings-app";

        /// <summary>
        /// <para>
        /// Serialized <c>HiveMQSettings</c> that reference the RabbitMQ hosts directly rather 
        /// than via a <b>private</b> traffic manager rule.  This is updated periodically by
        /// <b>neon-proxy-manager</b> when it detects HiveMQ topology changes and is used
        /// by <b>neon-proxy-manager</b> and the <b>neon-proxy</b> instances to avoid a
        /// configuration cycle.
        /// </para>
        /// <note>
        /// The <c>HiveMQSettings</c> persisted here will not include any credentials
        /// (the username and password) will be <c>null</c>.  You'll generally need to
        /// combine these settings with the credentials obtained from the <see cref="HiveMQSettingsNeon"/>
        /// settings.
        /// </note>
        /// </summary>
        public const string HiveMQSettingsBootstrap = "hivemq-settings-bootstrap";

        /// <summary>
        /// Serialized <c>HiveMQSettings</c> for the <see cref="HiveConst.HiveMQNeonUser"/> HiveMQ account.
        /// </summary>
        public const string HiveMQSettingsNeon = "hivemq-settings-neon";

        /// <summary>
        /// Serialized <c>HiveMQSettings</c> for the <see cref="HiveMQOptions.SysadminPassword"/> HiveMQ account.
        /// </summary>
        public const string HiveMQSettingSysadmin = "hivemq-settings-sysadmin";

        //---------------------------------------------------------------------
        // These settings are considered to be user modifiable.

        /// <summary>
        /// Enables unit testing on the hive via <b>HiveFixture</b> (bool).
        /// </summary>
        public const string UserAllowUnitTesting = "allow-unit-testing";

        /// <summary>
        /// Disables automatic Vault unsealing (bool).
        /// </summary>
        public const string UserDisableAutoUnseal = "disable-auto-unseal";

        /// <summary>
        /// Specifies the number of days to retain <b>logstash</b> and
        /// <b>metricbeat</b> logs.
        /// </summary>
        public const string UserLogRetentionDays = "log-retention-days";
    }
}
