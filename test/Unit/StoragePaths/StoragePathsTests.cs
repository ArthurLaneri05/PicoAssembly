using System;
using System.IO;
using System.Linq;
using PicoAssembly.Detail;
using static PicoAssembly.Test.TestAssert;

namespace PicoAssembly.Test.Unit.StoragePathsTests;

internal static class StoragePathsTests
{
    internal static void Run(TestSuite suite)
    {
        suite.Run("StoragePaths/paths resolve to one canonical form", PathsResolveCanonically);
        suite.Run("StoragePaths/resolved paths are required", ResolvedPathsAreRequired);
        suite.Run("StoragePaths/names and children are validated", NamesAndChildrenAreValidated);
        suite.Run("StoragePaths/path parts and relative paths are exposed", PathPartsAreExposed);
        suite.Run("StoragePaths/files are recursively enumerated", FilesAreRecursivelyEnumerated);
        suite.Run("StoragePaths/platform comparison policy is consistent", ComparisonPolicyIsConsistent);
    }

    private static void PathsResolveCanonically()
    {
        using var temp = new TemporaryDirectory();
        Equal(temp.Path, StoragePaths.strResolvePath(Path.Combine(temp.Path, "child", "..")));
        Equal(temp.Path, StoragePaths.strResolvePath(temp.Path + Path.DirectorySeparatorChar));
        Throws<ArgumentNullException>(() => StoragePaths.strResolvePath(null!));
        Throws<ArgumentException>(() => StoragePaths.strResolvePath(" "));
    }

    private static void ResolvedPathsAreRequired()
    {
        using var temp = new TemporaryDirectory();
        Equal(temp.Path, StoragePaths.strRequireResolved(temp.Path, "value"));
        Equal("value", Throws<ArgumentException>(() =>
            StoragePaths.strRequireResolved("relative", "value")).ParamName);
        Throws<ArgumentException>(() => StoragePaths.strRequireResolved(
            temp.Path + Path.DirectorySeparatorChar, "value"));
    }

    private static void NamesAndChildrenAreValidated()
    {
        using var temp = new TemporaryDirectory();
        Equal(Path.Combine(temp.Path, "part"), StoragePaths.strChild(temp.Path, "part"));
        Equal("part.step", StoragePaths.strFileName("part", "step"));
        foreach (string invalid in new[] { "", " ", ".", "..", "a/b", "a\\b", "name.", "name " })
            Throws<ArgumentException>(() => StoragePaths.ValidateName(invalid));
        Throws<ArgumentException>(() => StoragePaths.strFileName("part", ".step"));
        Throws<ArgumentException>(() => StoragePaths.strChild("relative", "part"));
    }

    private static void PathPartsAreExposed()
    {
        using var temp = new TemporaryDirectory();
        string nested = Path.Combine(temp.Path, "folder", "part.step");
        Equal("part.step", StoragePaths.strName(nested));
        Equal("part", StoragePaths.strNameWithoutExtension(nested));
        Equal(".step", StoragePaths.strExtension(nested));
        Equal(Path.Combine(temp.Path, "folder"), StoragePaths.strParent(nested));
        Equal("folder/part.step", StoragePaths.strRelativePath(temp.Path, nested));
    }

    private static void FilesAreRecursivelyEnumerated()
    {
        using var temp = new TemporaryDirectory();
        string nested = temp.CreateDirectory("nested");
        string first = Path.Combine(temp.Path, "first.txt");
        string second = Path.Combine(nested, "second.txt");
        File.WriteAllText(first, "first");
        File.WriteAllText(second, "second");
        string[] files = StoragePaths.oEnumerateFiles(temp.Path).OrderBy(x => x).ToArray();
        Equal(2, files.Length);
        True(files.Contains(first));
        True(files.Contains(second));
    }

    private static void ComparisonPolicyIsConsistent()
    {
        True(StoragePaths.bEquals("part", "part"));
        Equal(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS(),
            StoragePaths.bEquals("PART", "part"));
        True(StoragePaths.oNameComparer.Equals("PART", "part"));
        True(StoragePaths.oPathOrder.Compare("A", "a") < 0);
    }
}
