namespace Shop.Suppress;

using System.Diagnostics.CodeAnalysis;

public class Settings
{
    [SuppressMessage("PHPMD", "CamelCasePropertyName")]
    public int retry_count { get; set; }

    public int other_count { get; set; }

    [SuppressMessage("PHPMD", "CamelCaseMethodName")]
    public void do_work() { }

    public void Run()
    {
        [SuppressMessage("PHPMD", "CamelCaseMethodName")]
        void local_helper() { }

        local_helper();
    }

    /// <summary>Docs.</summary>
    /// @SuppressWarnings(PHPMD.CamelCaseMethodName)
    public void do_more() { }
}
