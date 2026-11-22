using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using PicoAssembly.Detail;
using static PicoAssembly.Detail.Hashing;
using static PicoAssembly.Test.TestAssert;

namespace PicoAssembly.Test.Unit.HashingTests;

internal static class HashingTests
{
    internal static void Run(TestSuite suite)
    {
        suite.Run("Hashing/hash_t round-trips canonical bytes", HashRoundTripsCanonicalBytes);
        suite.Run("Hashing/hash_t rejects malformed values", HashRejectsMalformedValues);
        suite.Run("Hashing/string hashing is SHA-256 over UTF-8", StringHashingUsesUtf8);
        suite.Run("Hashing/file hashing depends only on bytes", FileHashingUsesBytes);
        suite.Run("Hashing/coalescing hashes ordered digest bytes", CoalescingUsesOrderedDigestBytes);
        suite.Run("Hashing/coalescing rejects nulls", CoalescingRejectsNulls);
    }

    private static void HashRoundTripsCanonicalBytes()
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes("round trip"));
        var fromBytes = new hash_t(bytes);
        var fromLowercaseText = new hash_t(Convert.ToHexString(bytes).ToLowerInvariant());
        Equal(hash_t.nByteCount, fromBytes.aToBytes().Length);
        Equal(Convert.ToHexString(bytes), fromBytes.strValue);
        Equal(fromBytes, fromLowercaseText);
        True(bytes.AsSpan().SequenceEqual(fromLowercaseText.aToBytes()));
    }

    private static void HashRejectsMalformedValues()
    {
        Throws<ArgumentNullException>(() => new hash_t((byte[])null!));
        Throws<ArgumentException>(() => new hash_t(new byte[hash_t.nByteCount - 1]));
        Throws<ArgumentException>(() => new hash_t(new byte[hash_t.nByteCount + 1]));
        Throws<ArgumentNullException>(() => new hash_t((string)null!));
        Throws<ArgumentException>(() => new hash_t(new string('0', 63)));
        Throws<ArgumentException>(() => new hash_t(new string('G', 64)));
    }

    private static void StringHashingUsesUtf8()
    {
        const string value = "PicoGK — \r\n 🧱";
        Equal(new hash_t(SHA256.HashData(Encoding.UTF8.GetBytes(value))), uHashString(value));
        Throws<ArgumentNullException>(() => uHashString(null!));
    }

    private static void FileHashingUsesBytes()
    {
        using var temp = new TemporaryDirectory();
        string first = Path.Combine(temp.Path, "first.bin");
        string second = Path.Combine(temp.Path, "renamed.bin");
        byte[] contents = { 0, 1, 2, 13, 10, 255 };
        File.WriteAllBytes(first, contents);
        File.WriteAllBytes(second, contents);
        Equal(new hash_t(SHA256.HashData(contents)), uHashFile(first));
        Equal(uHashFile(first), uHashFile(second));
        Throws<FileNotFoundException>(() => uHashFile(Path.Combine(temp.Path, "missing")));
    }

    private static void CoalescingUsesOrderedDigestBytes()
    {
        hash_t first = uHashString("first");
        hash_t second = uHashString("second");
        byte[] concatenated = new byte[2 * hash_t.nByteCount];
        first.aToBytes().CopyTo(concatenated, 0);
        second.aToBytes().CopyTo(concatenated, hash_t.nByteCount);
        Equal(new hash_t(SHA256.HashData(concatenated)), uHashCoalesce(new[] { first, second }));
        True(uHashCoalesce(new[] { first, second }) != uHashCoalesce(new[] { second, first }));
        Equal(new hash_t(SHA256.HashData(Array.Empty<byte>())), uHashCoalesce(Array.Empty<hash_t>()));
    }

    private static void CoalescingRejectsNulls()
    {
        Throws<ArgumentNullException>(() => uHashCoalesce(null!));
        Throws<ArgumentNullException>(() => uHashCoalesce(new hash_t[] { null! }));
    }
}
