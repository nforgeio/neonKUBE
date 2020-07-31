1000.0.9-test

The version line above specifies the most recent version of locally published nuget packages used for testing packages befoire they are published to nuget.  This should be incremented each time you need to build and manually deploy packages after any changes.

This is required because Visual Studio caches packages and doesn't make it easy to reload packages with the same name and version into a solution.  Visual Studio for Windows does now have a **Clear All Cache(s)** button in **Tools/Nuget Package Manager/General** settings.

Here's (clunky) way to use this to run tests on OS/X against unpublished nuget packages:

**Windows:**

1. Increment the test version above.
2. Edit **neonLIBRARY-version.txt** by changing setting the version you just incremented.
3. Run: `neon-nuget-local`
4. Copy the packages from `$/Build/nuget` to a USB drive.

**Mac:**

1. Insert the USB drive.
2. Open the solution you're testing in Visual Studio for Mac. 
3. Setup a local nuget package source and copy the packages there or reference the USB directly.
4. Update the solution with the new packages.
5. Run your tests.

**IMPORTANT!**

Before commiting any changes on Windows, be sure to:

1. Undo changes to: **neonLIBRARY-version.txt**
2. Undo any changes to: **ALL PROJECT FILES**

The last step is very important because the `neon-nuget-local.ps1` script updates the package version for all published projects.
