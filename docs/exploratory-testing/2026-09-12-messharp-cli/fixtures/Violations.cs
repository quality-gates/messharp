namespace Fixture;

public class Violations
{
    private int unusedField;

    private void NeverCalled()
    {
    }

    public void Heavy(
        bool first,
        bool second,
        bool third,
        bool fourth,
        bool fifth,
        bool sixth,
        bool seventh,
        bool eighth,
        bool ninth,
        bool tenth)
    {
        int unusedLocal = 0;
        if (first) { }
        if (second) { }
        if (third) { }
        if (fourth) { }
        if (fifth) { }
        if (sixth) { }
        if (seventh) { }
        if (eighth) { }
        if (ninth) { }
        if (tenth) { }
    }

    public bool GetReady() => true;

    public void TakeFlag(bool flag)
    {
    }
}
