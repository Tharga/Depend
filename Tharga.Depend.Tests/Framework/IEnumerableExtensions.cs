namespace Tharga.Depend.Tests.Framework;

internal static class IEnumerableExtensions
{
    public static IEnumerable<T> TakeAllButLast<T>(this IEnumerable<T> items, int skipLastCount = 1)
    {
        var queue = new Queue<T>();

        foreach (var item in items)
        {
            queue.Enqueue(item);

            if (queue.Count > skipLastCount)
            {
                yield return queue.Dequeue();
            }
        }
    }
}