# neon-exec

**NOTE:** I couldn't get this to work.  It appears that Windows won't automatically start an app requiring elevated permissions, even when UAC is disabled.

Sometimes it's necessary to automatically run a CMD script with elevated permissions when the user logs into a Windows session.  
Starting a script automatcically can be done by copying the CMD file into:
```
C:\Users\devbot.JOBRUNNER\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup
```
but the problem is that it's not possible to set _Run As Administrator_ on script files.  This tool can be used to 
workaround this limitation.

**Here's how to configure this**

1. Relocate your CMD script somewhere outside of the Windows startup folder
2. Copy the **neon-exec.exe** binary from one of the bin folders (doesn't matter which one)
3. Copy **neon-exec.exe** to the Windows startup folder 
4. Add a **neon-exec.ini** file to the startup folder and add the fully qualified path to
   your CMD file as the first line
5. Configure **neon-exec.exe** to _Run As Administrator_

That's it.  If you need to do this for multiple scripts, you can copy new exe and ini files
with different names to the folder, like **myapp.exe** and **myapp.ini**.

**NOTE:** This app looks for an INI file with the same name as the executable.