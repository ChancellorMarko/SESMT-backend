namespace SESMT.Tests.Common;

public static class AssertExtensions
{
    public static void IsIn<T>(this T actual, params T[] expected)
        => Assert.Contains(actual, expected);

    public static void IsIn<T>(this T actual, IEnumerable<T> expected)
        => Assert.Contains(actual, expected);
}