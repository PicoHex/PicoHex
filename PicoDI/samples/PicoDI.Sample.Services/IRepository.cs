namespace PicoDI.Sample.Services;

public interface IRepository<T>
{
    void Add(T item);
    IEnumerable<T> GetAll();
}
