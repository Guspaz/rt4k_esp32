using System;
using System.Collections;
using System.Text;

namespace rt4k_esp32
{
    internal class IniParser
    {
        public static Hashtable Parse(string input)
        {
            Hashtable result = new Hashtable();
            var lines = input.Split(new char[] { '\r', '\n' });

            foreach (var line in lines)
            {
                if (!string.IsNullOrEmpty(line) && line.Contains("="))
                {
                    var parts = line.Split(new char[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        result[parts[0].Trim()] = parts[1].Trim();
                    }
                }
            }

            return result;
        }
    }
}
