This folder contains the C source code for the health probe executables deployed by
NeonService when running on Linux.  These binaries are build using the **wsl-util**
tool before compiling the **Neon.Service** library so they can be included as
embedded resources.

* **health-check:** is intended to be used for startup and liveliness probes and reads
  the **health-status** file and returns a success (0) exit code when the status file is 
  present and the status is **running** or **not-running**.

* **ready-check:** is intended to be used for readiness probes and reads the **health-status** 
  file and returns a success (0) exit code when the status file is present and the status is 
  **running**.
