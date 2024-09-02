namespace DarkConfig.Internal {
    static class StringExtensions {
        /// Returns the lowercase version of the key if <c>ignoreCase</c>, otherwise return the key itself
        internal static string CanonicalizeKey(this string key, bool ignoreCase) {
            return ignoreCase ? key.ToLowerInvariant() : key;
        }

        /// Returns the hash of the lowercase version of the key if <c>ignoreCase</c>, otherwise return the hash of the key itself
        internal static int GetCanonicalHashCode(this string key, bool ignoreCase) {
            return ignoreCase ? key.ToLowerInvariant().GetHashCode() : key.GetHashCode();
        }
    }
}
