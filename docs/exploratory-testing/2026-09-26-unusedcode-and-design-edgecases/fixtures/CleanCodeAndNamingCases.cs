namespace ExploratoryTesting.NamingAndClean;

public enum StatusEnum
{
    ActiveStatus,
    INACTIVE_STATUS,
    pending_status
}

public class FieldInitClass
{
    // Duplicate keys in a static field initializer
    private static readonly Dictionary<string, int> FieldDict = new()
    {
        ["duplicate_key"] = 1,
        ["duplicate_key"] = 2,
    };

    // Duplicate keys inside a method
    public void MethodDict()
    {
        var dict = new Dictionary<string, int>
        {
            ["duplicate_key"] = 1,
            ["duplicate_key"] = 2,
            [-1] = 10,
            [-1] = 20,
            [nameof(StatusEnum)] = 100,
            [nameof(StatusEnum)] = 200,
        };
    }

    // Boolean get methods
    public bool GetActive() => true;
    public bool IsActive() => true;
    public Task<bool> GetActiveAsync() => Task.FromResult(true);

    // Else expression
    public int ElseTest(int x)
    {
        if (x > 0)
        {
            return 1;
        }
        else
        {
            return 0;
        }
    }
}
