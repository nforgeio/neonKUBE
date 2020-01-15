# Neon Release Process:

## Prepare

1. Merge all desired changes into the **MASTER** branch from the **JEFF** and/or other development branches.

## Release 

1. Select the new release branch and merge from **MASTER**.

2. Update `$/product-version.txt` (or `GitHub/product-version.txt` in the solution) with the 
   new package version as required.

3. Run all unit tests on Windows in **RELEASE** mode.

4. Public the nuget packages locally and then manually verify that they pass on OS/X:
   ```
   neon-nuget-local
   ```

5. Build and publish the Docker images, the nuget packages, code documentation, as well as the full RELEASE build:
   ```
   neon-publish-images -all
   neon-nuget-public
   neon-builder -all
   neon-release -codedoc
   ```

6. Update the **cadence-samples** solution to reference the new packages and verify that the samples work.

7. Verify that the new release installer works.

8. Push the `release-VERSION` branch to GitHub with a comment like: **RELEASE: v1.0.0**

9. GitHub Release: [link](https://help.github.com/articles/creating-releases/)

  a. Create the release if it doesn't already exist
  b. Set **Tag** to the version with a leading "v" (like **v1.0.0**)
  c. Set **Target** to the `release-VERSION` branch
  e: Check **This is a pre-release** as required
  f. Add the release setup binary named like: **neonKUBE-setup-1.0.0.exe**
  g. Add the OS/X neon-cli binary from **osx** folder as: **neon-osx**
  h. Add **neon.chm**
  i. Edit the release notes including adding the SHA512s for:
  ```
  %NF_BUILD%\neonKUBE-setup.sha512.txt
  %NF_BUILD%\osx\neon-1.0.0.sha512.txt
  %NF_BUILD%\neon.chm.sha512.txtl
  ```
  j. Publish the release

## Post Release

1. Merge any changes from the RELEASE branch back into MASTER.

2. Merge **MASTER** into the **JEFF** and/or any other development branches, as required.

3. Create the next release branch and push it.

4. Create a draft for the next GitHub release from: `$/Doc/RELEASE-TEMPLATE.md`

   **NOTE:** Be sure to set the branch as the new release.

5. Archive the source code:

  1. Close all Visual Studio windows.
  2. Run `neon-archive.cmd` in a command window.
  3. Archive `C:\neonKUBE.zip` to AWS S3 and the local disk.

 # Release Version Conventions

* Use semantic versioning.
* The MAJOR, MINOR, and PATCH versions work as defined: [here](https://semver.org/)
* Patch versions start at 0.
* Non-production releases that are stable enough for limited public consumption will look like: **MAJOR.MINOR.PATCH-preview.#**
* Near production release candidates will look like: **MAJOR.MINOR.PATCH-rc.#**
