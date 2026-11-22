using System;
using System.Collections.Generic;

namespace PicoAssembly.Test;

internal static class TestAssert
{
    internal static void True(bool condition, string message = "Expected true.")
    {
        if (!condition) throw new Exception(message);
    }

    internal static void Equal<T>(T expected, T actual, string? message = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception(message ?? $"Expected '{expected}', got '{actual}'.");
    }

    internal static T Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T error) { return error; }
        throw new Exception($"Expected {typeof(T).Name}.");
    }
}
