# Community vs. Premium and Common Code

The community and premium versions of this tool share quite a bit of common code, with premium 
eventually being a superset of the community client.  The premium code in our private repo links
to the common code here.

The **PREMIUM** build variable will be defined when building the premium version and will be
undefined for community.  The `Program.IsPremium` property returns **true** when **PREMIUM** is 
defined.  This can be used in code to easily change behavior based on the client version.
