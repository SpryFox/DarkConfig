using System.IO;

namespace DarkConfig.Internal {
    public static class ChecksumUtils {
        /// Computes an integer checksum of a string.
        public static int Checksum(string body) {
            byte[] input = System.Text.Encoding.UTF8.GetBytes(body);
            using (MemoryStream stream = new MemoryStream(input)) {
                int hash = MurMurHash3.Hash(stream);
                return hash;
            }
        }

        /// Computes an integer checksum of a stream.
        public static int Checksum(Stream stream) {
            return MurMurHash3.Hash(stream);
        }
    }
}
