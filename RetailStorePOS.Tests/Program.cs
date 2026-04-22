using System;
using RetailStorePOS.Data;

namespace RetailStorePOS.Tests;

class Program
{
    static int _failedTests = 0;

    static void Main(string[] args)
    {
        Console.WriteLine("Running MoneyUtils Tests...");

        // Tests for ToCents
        AssertEqual(0L, MoneyUtils.ToCents(0m), "ToCents(0m)");
        AssertEqual(100L, MoneyUtils.ToCents(1.00m), "ToCents(1.00m)");
        AssertEqual(123L, MoneyUtils.ToCents(1.23m), "ToCents(1.23m)");
        AssertEqual(123L, MoneyUtils.ToCents(1.234m), "ToCents(1.234m)");
        AssertEqual(124L, MoneyUtils.ToCents(1.235m), "ToCents(1.235m)");
        AssertEqual(-123L, MoneyUtils.ToCents(-1.23m), "ToCents(-1.23m)");
        AssertEqual(1050L, MoneyUtils.ToCents(10.50m), "ToCents(10.50m)");

        // Tests for FromCents
        AssertEqual(0m, MoneyUtils.FromCents(0), "FromCents(0)");
        AssertEqual(1.00m, MoneyUtils.FromCents(100), "FromCents(100)");
        AssertEqual(1.23m, MoneyUtils.FromCents(123), "FromCents(123)");
        AssertEqual(-1.23m, MoneyUtils.FromCents(-123), "FromCents(-123)");
        AssertEqual(10.50m, MoneyUtils.FromCents(1050), "FromCents(1050)");
        AssertEqual(0.01m, MoneyUtils.FromCents(1), "FromCents(1)");
        AssertEqual(0.10m, MoneyUtils.FromCents(10), "FromCents(10)");

        if (_failedTests == 0)
        {
            Console.WriteLine("All tests passed!");
            Environment.Exit(0);
        }
        else
        {
            Console.WriteLine($"{_failedTests} tests failed.");
            Environment.Exit(1);
        }
    }

    static void AssertEqual<T>(T expected, T actual, string testName)
    {
        if (!Equals(expected, actual))
        {
            Console.WriteLine($"FAILED: {testName}. Expected: {expected}, Actual: {actual}");
            _failedTests++;
        }
        else
        {
            Console.WriteLine($"PASSED: {testName}");
        }
    }
}
