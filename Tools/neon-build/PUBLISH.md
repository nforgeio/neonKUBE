# Instructions

You'll need to publish this project as **Release** to the `$\ToolBin\neon-build` directory after making any changes.  I generally, just publish the release to the default folder and then manually copy the files over, deleting the existing ones first.

Note that there's a bit of a circular reference because:

1. **neon-build** dependes on Neon.Kube
2. **Neon.Kube** uses **neon-build** to ensure that all resource files are actually embedded as a pre-compile step

You may need to temporarily disable the pre-build step by prefixing it with **echo**


