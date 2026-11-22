using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PicoAssembly.Detail;

// Canonical hex gives immutable storage and record equality by value.
public sealed record hash_t
{
    public const int nByteCount = 32;
    public readonly string strValue;

    public hash_t(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length != nByteCount)
            throw new ArgumentException("Expected a 32-byte SHA-256 digest.", nameof(bytes));
        strValue = Convert.ToHexString(bytes);
    }

    // Normalize hexadecimal input for value equality.
    public hash_t(string strValue)
    {
        ArgumentNullException.ThrowIfNull(strValue);
        if (strValue.Length != 2 * nByteCount || !strValue.All(Uri.IsHexDigit))
            throw new ArgumentException("Expected a 64-character SHA-256 digest.", nameof(strValue));
        this.strValue = strValue.ToUpperInvariant();
    }

    public byte[] aToBytes() => Convert.FromHexString(strValue);
}

public static class Hashing
{
    // Hash every byte through a stream; the filename is not part of the hash.
    public static hash_t uHashFile(string strFilepath)
    {
        using FileStream oStream = File.OpenRead(strFilepath);
        return new hash_t(SHA256.HashData(oStream));
    }

    // UTF-8 preserves whitespace, casing and line endings.
    public static hash_t uHashString(string str) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(str)));

    // Hash ordered 32-byte digests; fixed sizes make separators unnecessary.
    public static hash_t uHashCoalesce(IReadOnlyList<hash_t> oHashes)
    {
        ArgumentNullException.ThrowIfNull(oHashes);
        using IncrementalHash oHasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (hash_t uHash in oHashes)
        {
            ArgumentNullException.ThrowIfNull(uHash);
            oHasher.AppendData(uHash.aToBytes());
        }
        return new hash_t(oHasher.GetHashAndReset());
    }
}
