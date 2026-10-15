using System.Runtime.CompilerServices;

namespace MessSharp.Rules.Explicitness;

/// <summary>A unique text key per object instance, so reference identity can appear in a text key.</summary>
internal static class InstanceKeys
{
    private static readonly ConditionalWeakTable<object, string> Keys = new();

    public static string For(object instance) => Keys.GetValue(instance, _ => Guid.NewGuid().ToString("N"));
}
