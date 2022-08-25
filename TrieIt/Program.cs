using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using TrieIt.Indexes;

namespace TrieIt
{
    public class Program
    {
        public static void Main(string[] args)
        {
            AppDomain.MonitoringIsEnabled = true;

            var dict = new Dictionary<string, IWordIndex>
            {
                ["1"] = new Original(),
                ["2"] = new Trie(),
                ["3"] = new TrieCompressed(),
            };

            var count = 0;

            if (args.Length > 0 && dict.ContainsKey(args[0]))
            {
                var processor = dict[args[0]];
                
                foreach (var line in File.ReadLines("../../Words.txt"))
                {
                    processor.Add(line);

                    ++count;
                }

                Console.WriteLine(processor.GetType());
            }
            else
            {
                Console.WriteLine(string.Join(Environment.NewLine, dict.Keys));
                Environment.Exit(1);
            }

            Console.WriteLine($"Words: {count}");
            Console.WriteLine($"Took: {AppDomain.CurrentDomain.MonitoringTotalProcessorTime.TotalMilliseconds:#,###} ms");
            Console.WriteLine($"Allocated: {AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize / 1024:#,#} kb");
            Console.WriteLine($"Peak Working Set: {Process.GetCurrentProcess().PeakWorkingSet64 / 1024:#,#} kb");

            for (var index = 0; index <= GC.MaxGeneration; index++)
            {
                Console.WriteLine($"Gen {index} collections: {GC.CollectionCount(index)}");
            }

            Console.WriteLine(Environment.NewLine);
        }
    }

    public interface IWordIndex
    {
        void Add(string word);
    }
}
