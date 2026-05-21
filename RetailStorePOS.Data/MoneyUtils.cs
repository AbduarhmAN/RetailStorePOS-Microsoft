namespace RetailStorePOS.Data;

public static class MoneyUtils
{
    public static long ToCents(decimal amount)
    {
        return (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
    }

    public static decimal FromCents(long cents)
    {
        return cents / 100m;
    }
}
