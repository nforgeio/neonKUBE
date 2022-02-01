# Community vs. Premium: Common Code

The community and premium versions of this tool share quite a bit of common code, with premium 
being a superset of the community client.

We ended up just manually coping the common code from neonKUBE to neonCLOUD (the private repo where 
our premium related code is located) and we'll need to manually ensure that any changes in one repo 
are replicated in the other, as required.  This isn't entirely satisfying, but it's simple and we don't
expect **neon-cli** to change that much after the first release or two.

We're going to use two conventions to help manage the duplicated code:

* The **PREMIUM** build variable will be defined when building the premium version and will be
undefined for community.

* The `Program.IsPremium` property returns **true** when **PREMIUM** is defined.  This can be
used in code to easily change behavior based on the client version.

* The **Unique** folder exists to hold source code that is unique to the specific client version.  
The idea here is that developers can generally copy files that are not in the **Unique** folder
between repos, but should be very careful about copying the unique files.
