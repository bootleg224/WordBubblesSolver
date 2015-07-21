using System;
using System.Collections.Generic;
using System.IO;

enum WordType : byte
{
    FullWord,
    PartialWord,
    FullWordAndPartialWord
}

class Program
{
    static Dictionary<string, WordType> _words = new Dictionary<string, WordType>(400000);
    static Dictionary<string, bool> _found = new Dictionary<string, bool>();
    const int _minLength = 4; // Minimum length of matching words.

    static string Input()
    {
        Console.WriteLine("Input lines of text and then a blank line.");
        string total = "";
        while (true)
        {
            // Get line.
            string line = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
            {
                return total.Trim();
            }
            total += line.Trim();
            total += "\r\n";
        }
    }

    static void Main()
    {
        // Read in dictionary.
        }
}