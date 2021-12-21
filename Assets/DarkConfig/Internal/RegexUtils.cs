using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DarkConfig.Internal {
    public static class RegexUtils {
        /// Returns a list of all the strings in the given list that match the given regex.
        public static List<string> FilterMatching(Regex pattern, List<string> strings) {
            var result = new List<string>();
            foreach (string str in strings) {
                if (pattern.IsMatch(str)) {
                    result.Add(str);
                }
            }
            return result;
        }

        public static List<string> FilterMatchingGlob(string glob, List<string> strings) {
            return FilterMatching(GlobToRegex(glob), strings);
        }

        /// Converts a glob-style expression into a file path regex
        ///  '*' matches any sequence of characters, but stops at slashes
        ///  '?' matches a single character, except a slash
        ///  '**' matches any sequence of characters, including slashes
        public static Regex GlobToRegex(string glob) {
            var regexString = Regex.Escape(glob)
                .Replace(@"\*\*", @".*")
                .Replace(@"\*", @"[^/]*")
                .Replace(@"\?", @"[^/]");
            var regex = new Regex("^" + regexString + "$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return regex;
        }
    }
}
