# Helm Charts Embedded Resources

This folder holds the Helm Charts required to deploy NeonKUBE clusters.  These files **must all** be configured to be **embedded resources**.  This is checked before the assembly is compiled.

## Helm files without extensions

The `Assembly.GetResourceFileSystem()` extension method has some limitations:

* All resource file names must include an extension.  When you really need a file without an extension, add the special **"._"** extension to the file. `Assembly.GetResourceFileSystem()` removes these extensions when it loads the resources.

* Resource file names must include **only a single period**, so file names like **file.1.txt** are not supported.  There is no workaround for this.

## Node provisioning

All of the Helm charts will be compiled into a ZIP archive and prepositioned on the NeonKUBE node image at: `/lib/neonkube/helm/charts.zip`.
