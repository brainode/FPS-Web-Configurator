namespace control_panel.Tests.TestSupport;

internal sealed class TemporaryDirectory : IDisposable
{
    private TemporaryDirectory(string path)
    {
        Path = path;
        Directory.CreateDirectory(path);
    }

    public string Path { get; }

    public static TemporaryDirectory Create()
    {
        var root = System.IO.Path.Combine(
            AppContext.BaseDirectory,
            "test-tmp",
            Guid.NewGuid().ToString("N"));

        return new TemporaryDirectory(root);
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
