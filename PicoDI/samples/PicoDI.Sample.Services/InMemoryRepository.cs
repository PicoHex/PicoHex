namespace PicoDI.Sample.Services;

public class InMemoryRepository<T> : IRepository<T>
{
    private readonly ConcurrentBag<T> _items = new();

    public void Add(T item) => _items.Add(item);

    public IEnumerable<T> GetAll() => _items;
}
