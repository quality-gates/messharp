using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Shop;

public sealed class Billing
{
    private static int _invoiceCounter;
    private static readonly List<string> Audit = new();
    private const decimal VatRate = 0.2m;
    private readonly decimal _discount;

    public Billing(decimal discount) => _discount = discount;

    // Pure: explicit in, explicit out. Must be clean.
    public static decimal Gross(decimal net) => net * (1 + VatRate);

    // Implicit input: reads mutable static.
    public static string NextInvoiceId() => "INV-" + (++_invoiceCounter);

    // Implicit input: the clock.
    public static bool IsOverdue(DateTime due) => DateTime.Now > due;

    // Implicit output: mutates argument.
    public static void AddLine(List<decimal> lines, decimal amount) => lines.Add(amount);

    // Implicit output: console + static list.
    public static void Log(string msg)
    {
        Console.WriteLine(msg);
        Audit.Add(msg);
    }

    // Implicit output: file system.
    public static void Save(string path, string body) => File.WriteAllText(path, body);

    // out parameter.
    public static bool TryParse(string s, out decimal value) => decimal.TryParse(s, out value);

    // Instance read (strict only).
    public decimal Apply(decimal net) => net - _discount;

    [SuppressMessage("PHPMD", "ImplicitInput")]
    public static DateTime Stamp() => DateTime.UtcNow;
}
