using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PicoAssembly.Detail;
using static PicoAssembly.Detail.Hashing;
using static PicoAssembly.Test.TestAssert;

namespace PicoAssembly.Test.Integration.CachedObjectTests;

internal static class CachedObjectTests
{
    private static readonly hash_t[] s_globals = { uHashString("runtime-v1") };

    internal static void Run(TestSuite suite)
    {
        suite.Run("CachedObject/first build publishes binary cache then restores", FirstBuildThenRestore);
        suite.Run("CachedObject/inputs and globals invalidate cache", InputsAndGlobalsInvalidateCache);
        suite.Run("CachedObject/force and corrupt caches regenerate", ForceAndCorruptCachesRegenerate);
        suite.Run("CachedObject/cache files are never tracked", CacheFilesAreIgnored);
        suite.Run("CachedObject/external tracked files participate", ExternalTrackedFilesParticipate);
        suite.Run("CachedObject/dependency graph builds once in order", DependencyGraphBuildsOnceInOrder);
        suite.Run("CachedObject/dependency changes invalidate consumers", DependencyChangesInvalidateConsumers);
        suite.Run("CachedObject/cycles and duplicate roots are rejected", InvalidGraphsAreRejected);
        suite.Run("CachedObject/failed generation publishes no cache", FailedGenerationPublishesNoCache);
        suite.Run("CachedObject/validation precedes cache selection", ValidationPrecedesCacheSelection);
        suite.Run("CachedObject/arguments and tracked paths are validated", ArgumentsAndPathsAreValidated);
    }

    private static void FirstBuildThenRestore()
    {
        using var temp = new TemporaryDirectory();
        var events = new List<string>();
        var node = NewNode(temp, "node", events);
        Equal("node", node.strName);
        True(Directory.Exists(node.strRootPath));
        True(!File.Exists(node.strPathToCache));

        node.Build(s_globals);
        Equal("G:node", string.Join(',', events));
        Equal(hash_t.nByteCount, (int)new FileInfo(node.strPathToCache).Length);

        events.Clear();
        node.Build(s_globals);
        Equal("R:node", string.Join(',', events));
    }

    private static void InputsAndGlobalsInvalidateCache()
    {
        using var temp = new TemporaryDirectory();
        var events = new List<string>();
        var node = NewNode(temp, "node", events);
        node.Build(s_globals);

        events.Clear();
        File.WriteAllText(node.InputPath, "changed");
        node.Build(s_globals);
        Equal("G:node", string.Join(',', events));

        events.Clear();
        node.Build(new[] { uHashString("runtime-v2") });
        Equal("G:node", string.Join(',', events));
    }

    private static void ForceAndCorruptCachesRegenerate()
    {
        using var temp = new TemporaryDirectory();
        var events = new List<string>();
        var node = NewNode(temp, "node", events);
        node.Build(s_globals);

        events.Clear();
        node.Build(s_globals, bForceRebuild: true);
        Equal("G:node", string.Join(',', events));

        File.WriteAllBytes(node.strPathToCache, new byte[] { 1, 2, 3 });
        events.Clear();
        node.Build(s_globals);
        Equal("G:node", string.Join(',', events));
        Equal(hash_t.nByteCount, (int)new FileInfo(node.strPathToCache).Length);
    }

    private static void CacheFilesAreIgnored()
    {
        using var temp = new TemporaryDirectory();
        var events = new List<string>();
        var node = NewNode(temp, "node", events);
        node.TrackCacheFiles = true;
        node.Build(s_globals);
        events.Clear();
        node.Build(s_globals);
        Equal("R:node", string.Join(',', events));
    }

    private static void ExternalTrackedFilesParticipate()
    {
        using var temp = new TemporaryDirectory();
        var events = new List<string>();
        string external = Path.Combine(temp.Path, "shared.input");
        File.WriteAllText(external, "one");
        var node = NewNode(temp, "node", events);
        node.ExtraTrackedFiles.Add(external);
        node.Build(s_globals);
        events.Clear();
        File.WriteAllText(external, "two");
        node.Build(s_globals);
        Equal("G:node", string.Join(',', events));
    }

    private static void DependencyGraphBuildsOnceInOrder()
    {
        using var temp = new TemporaryDirectory();
        var events = new List<string>();
        var shared = NewNode(temp, "shared", events);
        var left = NewNode(temp, "left", events, shared);
        var right = NewNode(temp, "right", events, shared);
        var root = NewNode(temp, "root", events, left, right);
        root.Build(s_globals);
        Equal("G:shared,G:left,G:right,G:root", string.Join(',', events));

        events.Clear();
        root.Build(s_globals);
        Equal("R:shared,R:left,R:right,R:root", string.Join(',', events));
    }

    private static void DependencyChangesInvalidateConsumers()
    {
        using var temp = new TemporaryDirectory();
        var events = new List<string>();
        var dependency = NewNode(temp, "dependency", events);
        var root = NewNode(temp, "root", events, dependency);
        root.Build(s_globals);
        events.Clear();
        File.WriteAllText(dependency.InputPath, "new dependency input");
        root.Build(s_globals);
        Equal("G:dependency,G:root", string.Join(',', events));
    }

    private static void InvalidGraphsAreRejected()
    {
        using var temp = new TemporaryDirectory();
        var events = new List<string>();
        string aPath = StoragePaths.strChild(temp.Path, "a");
        var firstA = new ProbeNode(aPath, events);
        var b = NewNode(temp, "b", events, firstA);
        var secondA = new ProbeNode(aPath, events, b);
        InvalidOperationException cycle = Throws<InvalidOperationException>(() => secondA.Build(s_globals));
        True(cycle.Message.Contains("a -> b -> a", StringComparison.Ordinal));

        var shared = NewNode(temp, "shared", events);
        var alias = new ProbeNode(shared.strRootPath, events);
        var root = NewNode(temp, "root", events, shared, alias);
        Throws<InvalidOperationException>(() => root.Build(s_globals));
    }

    private static void FailedGenerationPublishesNoCache()
    {
        using var temp = new TemporaryDirectory();
        var events = new List<string>();
        var node = NewNode(temp, "node", events);
        node.FailGeneration = true;
        Throws<IOException>(() => node.Build(s_globals));
        True(!File.Exists(node.strPathToCache));

        node.FailGeneration = false;
        node.Build(s_globals);
        node.FailGeneration = true;
        Throws<IOException>(() => node.Build(s_globals, bForceRebuild: true));
        True(!File.Exists(node.strPathToCache));
        True(!File.Exists(node.strPathToCache + ".tmp"));
    }

    private static void ValidationPrecedesCacheSelection()
    {
        using var temp = new TemporaryDirectory();
        var events = new List<string>();
        var node = NewNode(temp, "node", events);
        node.Build(s_globals);
        node.FailValidation = true;
        events.Clear();
        Throws<InvalidOperationException>(() => node.Build(s_globals));
        Equal(0, events.Count);
        True(File.Exists(node.strPathToCache));
    }

    private static void ArgumentsAndPathsAreValidated()
    {
        using var temp = new TemporaryDirectory();
        var events = new List<string>();
        var dependency = NewNode(temp, "dependency", events);
        CachedObject[] dependencies = { dependency };
        var node = new ProbeNode(StoragePaths.strChild(temp.Path, "node"), events, dependencies);
        dependencies[0] = node;
        True(ReferenceEquals(dependency, node.oDependencies[0]));
        Throws<NotSupportedException>(() => ((IList<CachedObject>)node.oDependencies)[0] = node);
        Throws<ArgumentException>(() => new ProbeNode("relative", events));
        Throws<ArgumentException>(() => new ProbeNode(Path.GetPathRoot(temp.Path)!, events));
        Throws<ArgumentNullException>(() => node.Build(null!));
        Throws<ArgumentNullException>(() => new ProbeNode(StoragePaths.strChild(temp.Path, "nulls"), events, null!));
        Throws<ArgumentException>(() => new ProbeNode(StoragePaths.strChild(temp.Path, "null"), events,
            new CachedObject[] { null! }));

        node.ExtraTrackedFiles.Add("relative");
        Throws<ArgumentException>(() => node.Build(s_globals));
        True(!File.Exists(node.strPathToCache));
    }

    private static ProbeNode NewNode(TemporaryDirectory temp, string name, List<string> events,
        params CachedObject[] dependencies) =>
        new(StoragePaths.strChild(temp.Path, name), events, dependencies);

    private sealed class ProbeNode : CachedObject
    {
        private readonly List<string> m_events;
        internal string InputPath { get; }
        internal List<string> ExtraTrackedFiles { get; } = new();
        internal bool TrackCacheFiles { get; set; }
        internal bool FailGeneration { get; set; }
        internal bool FailValidation { get; set; }

        internal ProbeNode(string path, List<string> events, params CachedObject[] dependencies)
            : base(path, dependencies)
        {
            m_events = events;
            InputPath = Path.Combine(strRootPath, "input.txt");
            if (!File.Exists(InputPath)) File.WriteAllText(InputPath, "initial");
        }

        protected override void ValidateBeforeBuild()
        {
            if (FailValidation) throw new InvalidOperationException("validation failed");
        }

        protected override void Generate(IReadOnlyList<hash_t> globalHashes)
        {
            m_events.Add("G:" + strName);
            if (FailGeneration) throw new IOException("generation failed");
        }

        protected override void Restore(IReadOnlyList<hash_t> globalHashes) =>
            m_events.Add("R:" + strName);

        protected override IEnumerable<string> oTrackedFiles()
        {
            yield return InputPath;
            foreach (string path in ExtraTrackedFiles) yield return path;
            if (TrackCacheFiles)
            {
                yield return strPathToCache;
                yield return strPathToCache + ".tmp";
            }
        }
    }
}
