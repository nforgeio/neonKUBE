# Neon Release Process:

## Prepare

1. Merge all desired changes into the **MASTER** branch from the **JEFF** and/or other development branches.

## Release 

1. Select the new release branch and merge from **MASTER**.

2. Update `$/product-version.txt` (or `GitHub/product-version.txt` in the solution) with the 
   new release version as required.

3. Run all unit tests on Windows in **RELEASE** mode.

4. Close Visual Studio.  It seems to be locking some `obj` directories which prevents the **Snippits** project from building below.

5. Publish the nuget packages locally and then manually verify that they pass on OS/X:
   ```
   neon-nuget-local
   ```

6. Build and publish the nuget packages and code documentation:
   ```
   neon-nuget-public
   neon-builder -all
   neon-release -codedoc
   ```

7. Update sample repos:
   a. Update the **cadence-samples** solution to reference the new packages and verify that the samples work.
   b. Update the **temporal-samples** solution to reference the new packages and verify that the samples work.

8. Push the `release-neonLIBRARY-VERSION` branch to GitHub with a comment like: **RELEASE: neonLIBRARY-v1.0.0**

9. Publish the release.

## Post Release

1. Merge any changes from the RELEASE branch back into MASTER.

2. Merge **MASTER** into the **JEFF** and/or any other development branches, as required.

3. Create the next release branch and push it. This should be named like: **release-neonLIBRARY-v1.0.0**

4. Create the next draft release on GitHub:
  a. Copy `$/doc/neonLIBRARY-RELEASE-TEMPLATE.md` as the initial release text
  b. Set **Tag** to the version with a leading "v" (like **neonLIBRARY-v1.0.0**)
  c. Set **Target** to the `release-neonLIBRARY-VERSION` branch
  d. Set **Title** like: **neonLIBRARY-v1.0.0**
  e. Check **This is a pre-release** as required

5. Archive the source code:

  1. Close all Visual Studio windows.
  2. Run `neon-archive.cmd` in a command window.
  3. Archive `C:\neonKUBE.zip` to AWS S3 and the local disk.

## Release Version Conventions

* Use semantic versioning.
* The MAJOR, MINOR, and PATCH versions work as defined: [here](https://semver.org/)
* Patch versions start at 0.
* Non-production releases that are stable enough for limited public consumption will look like: **MAJOR.MINOR.PATCH-preview.#**
* Near production release candidates will look like: **MAJOR.MINOR.PATCH-rc.#**
