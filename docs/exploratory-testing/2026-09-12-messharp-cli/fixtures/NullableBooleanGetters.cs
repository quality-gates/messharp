namespace Fixture;

public sealed class NullableBooleanGetters
{
    public bool GetReady() => true;

    public bool? GetOptional() => true;

    public System.Boolean? GetSystemOptional() => true;

    public System.Nullable<bool> GetNullable() => true;

    public int GetNumber() => 1;
}
