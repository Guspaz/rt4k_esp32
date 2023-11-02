using System.Text;

namespace rt4k_esp32
{
    // nanoFramework doesn't support some string functions so we have to re-implement them
    public static class StringExtensions
    {
        public static string Replace(this string input, string oldValue, string newValue) => new StringBuilder(input).Replace(oldValue, newValue).ToString();

        public static string Replace(this string current, char oldChar, char newChar)
        {
            char[] result = current.ToCharArray();
            int length = result.Length;

            for (int i = 0; i < length; i++)
            {
                if (result[i] == oldChar)
                    result[i] = newChar;
            }

            return new string(result);
        }
    }
}