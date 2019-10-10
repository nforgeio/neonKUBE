using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace uuid_gen
{
    /// <summary>
    /// This tool simply generates a UUID and writes it to standard output.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static void Main(string[] args)
        {
            Console.Write(Guid.NewGuid().ToString("d"));
        }
    }
}
