# Code Document Generation

We're using Sandcastle Help Filer Builder (SHFB) for generating our code document website and offline help file.  We also use the `neon-build shfb...` command line tool to perform some post-processing:

1. Insert Google Analytics `gtag.js` scripts
2. Convert GUID based HTML file names to something more permalink friendly
3. Move the HTML pages up from the `html/*` folder to the root site folder also more permalink friendly)

This all happens as part of the `neon-builder` script.

## Conceptual Content 

Stock SHFB generates GUIDs for all conceptual content and then generates HTML pages using the GUID in the name.  Although this works as a permalink, it's not very friendly.

We address this by looking for a HTML comment in the conceptual topic MAML files like:
```
<?xml version="1.0" encoding="utf-8"?>
<!-- topic-filename="Neon.Cadence-Overview" -->
<topic id="b4a13879-cf87-43e2-8b6e-a122d8809d7a" revisionNumber="1">
...
</topic>
```
When present, the `neon-build shfb...` command will use the specified string as the generated topic file name instead of the GUID.

**NOTE:** Conceptual topic file names must be unique.
