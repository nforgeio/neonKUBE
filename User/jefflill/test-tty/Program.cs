using System;
using System.IO;

using Neon.WinTTY;

namespace test_tty
{
    class Program
    {
        static void Main(string[] args)
        {
            var tty = new ConsoleTTY();

            tty.Run(@"kubectl exec -it playground -- /bin/bash");
        }
    }
}
