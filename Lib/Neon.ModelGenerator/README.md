Neon.ModelGenerator
===================

Extends the **Neon.ModelGen** library by encapsulating the **neon-model** client application in the nuget package.  This is deployed as a cross-platform DLL which can be executed by your project build targets, as required.

Most projects will want to reference **Neon.ModelGenerator** rather than **Neon.ModelGen** because the latter includes just the modelgen library which cannot be executed directly by build targets.


Maintainer Notes
================

This project includes a pre-build target that copies the binary files from the `$/Tools/neon-model/bin/CONFIGURATION/netcoreapp3.1/` directory to the **Neon.ModelGenerator/neon-model** build project folder before **Neon.ModelGenerator** builds so we can include these binaries in the nuget package.

**NOTE:** We don't copy the localization assemblies because we don't need them and also to save space and we've also added rules to `.gitignore` so these files won't be commited to GitHub.

This means that you'll need to manually include all of these binaries into the project, setting:

* **Build Action: Content**
* **Copy to Output Directory: Copy if newer**

for each of these binaries after building **Neon.ModelGenerator** for the first time.  You'll also need to manual edit **Neon.ModelGenerator.cs** to add `<CopyToPublishDirectory>true</CopyToPublishDirectory>` to each of these files (so they'll be included in the nuget package) like:

```
<Content Include="neon-model\Humanizer.dll">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <CopyToPublishDirectory>true</CopyToPublishDirectory>
</Content>
```

The **neon-model* tool may add and/or remove binary files in the future.  This should happen rarely but if it does, you'll need to:

1. Remove all of these binary files from `$/neon-model`
2. Build the project to copy the new files
3. Configure the files as content as described above
