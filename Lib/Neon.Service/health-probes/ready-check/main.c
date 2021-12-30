#include <stdio.h>                                            
#include <stdlib.h>                                           
#include <string.h>                                           
#include <unistd.h>                                           

int main()
{
    char    buffer[64];
    FILE*   file;
    char*   value;

    // Read the [health-status] file from the same directory as this executable.
    // We'll exit immediately if the file doesn't exist or we couldn't read the
    // first line.

    file = fopen("./health-status", "r");

    if (file == NULL)
    {
        return 1;
    }

    value = fgets(buffer, sizeof(buffer), file);

    fclose(file);

    if (value == NULL)
    {
        return 1;
    }

    // The value read may include a linefeed.  We'll trim that here.

    char* endingPos = strchr(value, '\n');

    if (endingPos != NULL)
    {
        *endingPos = 0;
    }

    // Determine whether the service is ready.

    if (strcmp(value, "running") == 0)
    {
        return 0;
    }

    return 1;
}


