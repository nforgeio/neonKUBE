### Postgres schema for neon-identity-service/IdentityServer4

This directory holds the schema files required to install and upgraded the Postgres database for **neon-identity-service** and the embedded **IdentityServer4** library.  Installation and upgrades are handled by the `SchemaManager` class.

This database includes two sets of tables:

* **STS Tables:** The tables required for by IdentityServer4 for serving tokens to users and API clients.  These were transcribed manually from the storage class definitions.
* **User Tables:** The tables required to implement a user database.  These were adapted from the SQLExpress schema generated for a new ASPNET WebApplication.

Note that there are no foreign key relationships between the two table sets so these could in theory be deployed to different databases but we're going to consolidate them into a single database for simplicity.

Variables:
----------
**${database}** - replaced by the database name
**${sts_user}** - replaced by the username for the neon-identity-service account
**${sts_password}** - replaced by the password for the neon-identity-service account
