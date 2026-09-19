namespace Fixture;

public sealed class NullableOnly
{
    public bool? GetOptional() => true;

    public System.Boolean? GetSystemOptional() => true;

    public System.Nullable<bool> GetNullable() => true;
}
