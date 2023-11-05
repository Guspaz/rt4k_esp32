using System.Text;

namespace rt4k_esp32
{
    // nanoFramework doesn't support some string functions so we have to re-implement them
    public static class StringExtensions
    {
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

        public static string Replace(this string current, string oldValue, string newValue)
        {
            var result = new StringBuilder();
            int startIndex = 0;
            int nextIndex;
            int oldLen = oldValue.Length;

            while ((nextIndex = current.IndexOf(oldValue, startIndex)) != -1)
            {
                result.Append(current, startIndex, nextIndex - startIndex);
                result.Append(newValue);
                startIndex = nextIndex + oldLen;
            }

            result.Append(current, startIndex, current.Length - startIndex);

            return result.ToString();
        }
    }
}