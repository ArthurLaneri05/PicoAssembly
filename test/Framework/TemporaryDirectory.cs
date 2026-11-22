using System;
using System.IO;

namespace PicoAssembly.Test;

internal sealed class TemporaryDirectory : IDisposable
{
    internal string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "pico-assembly-test-" + Guid.NewGuid().ToString("N"));

    internal TemporaryDirectory() => Directory.CreateDirectory(Path);

    internal string CreateDirectory(string name)
    {
        string path = System.IO.Path.Combine(Path, name);
        Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
