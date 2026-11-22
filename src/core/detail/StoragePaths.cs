using System;
using System.Collections.Generic;
using System.IO;

namespace PicoAssembly.Detail;

internal static class StoragePaths
{
    internal static StringComparer oNameComparer { get; } = StringComparer.OrdinalIgnoreCase;

    internal static StringComparer oPathOrder { get; } = StringComparer.Ordinal;

    // Default platform convention; individual volumes may differ.
    private static StringComparer oPathComparer { get; } =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    internal static bool bEquals(string strLeft, string strRight) =>
        oPathComparer.Equals(strLeft, strRight);

    internal static string strResolvePath(string strPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(strPath);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(strPath));
    }

    internal static string strRequireResolved(string strPath, string strParameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(strPath, strParameterName);
        if (!StringComparer.Ordinal.Equals(strPath, strResolvePath(strPath)))
            throw new ArgumentException("Resolve the path before using it.", strParameterName);
        return strPath;
    }

    internal static string strChild(string strRoot, string strName)
    {
        ValidateName(strName);
        return Path.Combine(strRequireResolved(strRoot, nameof(strRoot)), strName);
    }

    internal static string strRelativePath(string strRoot, string strPath) =>
        Path.GetRelativePath(strRoot, strPath).Replace(Path.DirectorySeparatorChar, '/');

    internal static string strName(string strPath) => Path.GetFileName(strPath);

    internal static string strNameWithoutExtension(string strPath) =>
        Path.GetFileNameWithoutExtension(strPath);

    internal static string strExtension(string strPath) => Path.GetExtension(strPath);

    internal static string strParent(string strPath) => Path.GetDirectoryName(strPath)
        ?? throw new ArgumentException("The path has no parent directory.", nameof(strPath));

    internal static IEnumerable<string> oEnumerateFiles(string strPath) =>
        Directory.EnumerateFiles(strPath, "*", SearchOption.AllDirectories);

    internal static string strFileName(string strName, string strFileExtension)
    {
        ValidateName(strName);
        ValidateName(strFileExtension);
        if (strFileExtension[0] == '.')
            throw new ArgumentException("Omit the leading dot.", nameof(strFileExtension));
        return $"{strName}.{strFileExtension}";
    }

    internal static void ValidateName(string strName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(strName);
        if (strName is "." or ".." || strName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || strName.IndexOfAny("<>:\"/\\|?*".ToCharArray()) >= 0 || strName.EndsWith('.') || strName.EndsWith(' '))
            throw new ArgumentException("Use a single file or directory name.", nameof(strName));
    }
}
