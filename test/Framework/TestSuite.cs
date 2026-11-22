using System;

namespace PicoAssembly.Test;

internal sealed class TestSuite
{
    private int m_nPassed;
    private int m_nFailed;

    internal void Run(string name, Action test)
    {
        try
        {
            test();
            m_nPassed++;
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception error)
        {
            m_nFailed++;
            Console.Error.WriteLine($"FAIL {name}\n{error}");
        }
    }

    internal int Finish()
    {
        Console.WriteLine($"{m_nPassed} passed; {m_nFailed} failed.");
        return m_nFailed == 0 ? 0 : 1;
    }
}
