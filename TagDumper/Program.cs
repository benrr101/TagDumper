using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TagDumper
{
    class Program
    {
        static void Main(string[] args)
        {
            // Verify that there is a file path provided
            if (args.Length != 1)
            {
                Console.Error.WriteLine("*** File not provided");
                PrintUsage();
                return;
            }

            // Verify that the file path provided exists
            string filePath = args[0];
            if (!File.Exists(filePath))
            {
                Console.Error.WriteLine("*** File {0} does not exist", filePath);
                PrintUsage();
            }



        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage: TagDumper.exe file");
            Console.WriteLine("    file: path to file to dump tags for");
        }
    }
}
