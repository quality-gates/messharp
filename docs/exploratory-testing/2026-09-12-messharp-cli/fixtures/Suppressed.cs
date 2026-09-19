using System.Diagnostics.CodeAnalysis;

namespace Fixture;

public sealed class Suppressed
{
    [SuppressMessage("MessSharp", "BooleanGetMethodName")]
    public bool GetReady() => true;
}
