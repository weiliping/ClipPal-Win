namespace ClipPal.Services;

/// <summary>单实例保护：两个实例会同时监听剪贴板、互相覆盖 history.json。</summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;

    public bool IsFirstInstance { get; }

    public SingleInstance(string name)
    {
        _mutex = new Mutex(initiallyOwned: true, name, out var created);
        IsFirstInstance = created;
    }

    public void Dispose()
    {
        try
        {
            if (IsFirstInstance) _mutex.ReleaseMutex();
        }
        catch { }
        _mutex.Dispose();
    }
}
