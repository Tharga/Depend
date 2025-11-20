namespace Tharga.Depend.Framework;

internal static class ParamExtensions
{
    public static IEnumerable<string> NonOptionalParams(this IEnumerable<string> items)
    {
        foreach (var item in items)
        {
            if (!item.StartsWith("-"))
                yield return item;
            else
                break;
        }
    }
}