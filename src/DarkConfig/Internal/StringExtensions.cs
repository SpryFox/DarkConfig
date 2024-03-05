namespace DarkConfig.Internal {
    static class StringExtensions {
        /// <summary>
        /// Returns the lowercase version of the key if <c>ignoreCase</c>, otherwise return the key itself
        /// </summary>
        internal static string CanonicalizeKey(this string key, bool ignoreCase) {
            if (ignoreCase) {
                return key.ToLowerInvariant();
            }

            return key;
        }

        /// <summary>
        /// Returns the hash of the lowercase version of the key if <c>ignoreCase</c>, otherwise return the hash of the key itself
        /// </summary>
        internal static int GetCanonicalHashCode(this string key, bool ignoreCase) {
            if (ignoreCase) {
                return key.ToLowerInvariant().GetHashCode();
            }

            return key.GetHashCode();
        }
    }
}
