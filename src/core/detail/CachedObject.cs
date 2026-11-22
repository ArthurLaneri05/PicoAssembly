using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using static PicoAssembly.Detail.Hashing;

namespace PicoAssembly.Detail;

/// <summary>Builds dependencies first, then restores or regenerates a cached snapshot.</summary>
/// <remarks>
/// Builds are synchronous and require exclusive access to their cache roots.
/// Cache publication is atomic; geometry publication is not.
/// Hidden inputs must be represented by tracked files, dependencies or global hashes.
/// Root paths must already be resolved; symbolic links remain the caller's responsibility.
/// </remarks>
public abstract class CachedObject
{
    private const string strCacheFileName = "cache.bin";
    private const string strTemporaryCacheFileName = "cache.bin.tmp";

    public readonly string strRootPath;
    public string strName => StoragePaths.strName(strRootPath);
    public string strPathToCache => StoragePaths.strChild(strRootPath, strCacheFileName);
    public IReadOnlyList<CachedObject> oDependencies { get; }

    private string strPathToTemporaryCache =>
        StoragePaths.strChild(strRootPath, strTemporaryCacheFileName);

    protected CachedObject(string strPathToObject, params CachedObject[] oDependencies)
    {
        ArgumentNullException.ThrowIfNull(oDependencies);
        if (oDependencies.Any(oDependency => oDependency is null))
            throw new ArgumentException("Dependencies cannot contain null.", nameof(oDependencies));

        this.oDependencies = Array.AsReadOnly(oDependencies.ToArray());
        strRootPath = StoragePaths.strRequireResolved(strPathToObject, nameof(strPathToObject));
        if (strName.Length == 0)
            throw new ArgumentException("A cached object requires a named directory.", nameof(strPathToObject));

        Directory.CreateDirectory(strRootPath);
    }

    private sealed record BuildEntry(CachedObject oObject)
    {
        public hash_t? hCacheKey { get; set; }
    }

    public void Build(IReadOnlyList<hash_t> oGlobalHashes, bool bForceRebuild = false)
    {
        ArgumentNullException.ThrowIfNull(oGlobalHashes);
        hBuild(oGlobalHashes, bForceRebuild, new List<BuildEntry>());
    }

    private hash_t hBuild(IReadOnlyList<hash_t> oGlobalHashes, bool bForceRebuild,
        List<BuildEntry> oEntries)
    {
        BuildEntry? oExisting = oEntries.Find(oCandidate =>
            StoragePaths.bEquals(oCandidate.oObject.strRootPath, strRootPath));
        if (oExisting is not null)
        {
            if (oExisting.hCacheKey is not hash_t hExistingKey)
            {
                string strCycle = string.Join(" -> ", oEntries
                    .Where(oEntry => oEntry.hCacheKey is null)
                    .Select(oEntry => oEntry.oObject.strName).Append(strName));
                throw new InvalidOperationException($"Dependency cycle: {strCycle}");
            }

            if (!ReferenceEquals(oExisting.oObject, this))
                throw new InvalidOperationException(
                    $"Multiple object instances use cache root {strRootPath}. Reuse one instance per build.");

            return hExistingKey;
        }

        BuildEntry oCurrent = new(this);
        oEntries.Add(oCurrent);

        List<hash_t> oDependencyHashes = new(oDependencies.Count);
        foreach (CachedObject oDependency in oDependencies)
            oDependencyHashes.Add(oDependency.hBuild(oGlobalHashes, bForceRebuild, oEntries));

        ValidateBeforeBuild();

        hash_t? hCacheKey = bForceRebuild ? null : hReadCacheKey();
        if (hCacheKey is not null && bCacheMatches(hCacheKey, oGlobalHashes, oDependencyHashes))
        {
            Restore(oGlobalHashes);
        }
        else
        {
            File.Delete(strPathToCache);
            Generate(oGlobalHashes);
            hCacheKey = hCalculateCacheKey(oGlobalHashes, oDependencyHashes);
            SaveCache(hCacheKey);
        }

        oCurrent.hCacheKey = hCacheKey;
        return hCacheKey;
    }

    /// <summary>Loads a validated snapshot without modifying tracked files.</summary>
    protected abstract void Restore(IReadOnlyList<hash_t> oGlobalHashes);

    /// <summary>Generates, validates and saves the complete snapshot.</summary>
    protected abstract void Generate(IReadOnlyList<hash_t> oGlobalHashes);

    protected virtual void ValidateBeforeBuild() { }

    /// <summary>Supplies the current tracked file paths, including files outside the cache root.</summary>
    /// <remarks>Paths must be resolved. Called again after generation; internal cache files are ignored.</remarks>
    protected abstract IEnumerable<string> oTrackedFiles();

    private bool bCacheMatches(hash_t hCacheKey, IReadOnlyList<hash_t> oGlobalHashes,
        IReadOnlyList<hash_t> oDependencyHashes)
    {
        try
        {
            return hCacheKey == hCalculateCacheKey(oGlobalHashes, oDependencyHashes);
        }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }

    private hash_t hCalculateCacheKey(IReadOnlyList<hash_t> oGlobalHashes,
        IReadOnlyList<hash_t> oDependencyHashes)
    {
        List<hash_t> oHashes = new()
        {
            uHashString(GetType().FullName!),
            uHashString(strName),
            uHashCoalesce(oGlobalHashes),
            uHashCoalesce(oDependencyHashes)
        };

        string strCachePath = strPathToCache;
        string strTemporaryPath = strPathToTemporaryCache;

        var oFiles = oTrackedFiles()
            .Select(strFile => StoragePaths.strRequireResolved(strFile, nameof(oTrackedFiles)))
            .Where(strFile => !StoragePaths.bEquals(strFile, strCachePath)
                && !StoragePaths.bEquals(strFile, strTemporaryPath))
            .Distinct(StoragePaths.oPathOrder)
            .Select(strFile => (strFile, strRelativePath: StoragePaths.strRelativePath(strRootPath, strFile)))
            .OrderBy(oFile => oFile.strRelativePath, StoragePaths.oPathOrder);

        foreach (var oFile in oFiles)
            oHashes.Add(uHashCoalesce(new[]
            {
                uHashString(oFile.strRelativePath),
                uHashFile(oFile.strFile)
            }));

        return uHashCoalesce(oHashes);
    }

    private hash_t? hReadCacheKey()
    {
        try
        {
            using FileStream oStream = File.OpenRead(strPathToCache);
            if (oStream.Length != hash_t.nByteCount)
                return null;

            byte[] aBytes = new byte[hash_t.nByteCount];
            oStream.ReadExactly(aBytes);
            return new hash_t(aBytes);
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private void SaveCache(hash_t hCacheKey)
    {
        string strTemporaryPath = strPathToTemporaryCache;
        try
        {
            File.WriteAllBytes(strTemporaryPath, hCacheKey.aToBytes());
            File.Move(strTemporaryPath, strPathToCache, overwrite: true);
        }
        catch
        {
            try { File.Delete(strTemporaryPath); }
            catch { }
            throw;
        }
    }
}
