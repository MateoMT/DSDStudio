using System.Text;

namespace Benchmark;

public sealed class Logger : IDisposable
{
    private readonly TextWriter originalOut;
    private readonly TextWriter originalError;
    private readonly StreamWriter? file;

    private Logger(TextWriter originalOut, TextWriter originalError, StreamWriter? file)
    {
        this.originalOut = originalOut;
        this.originalError = originalError;
        this.file = file;
    }

    public static Logger Start(Options options)
    {
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;

        if (options.NoLog)
            return new Logger(originalOut, originalError, null);

        string? dir = Path.GetDirectoryName(options.LogPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var file = new StreamWriter(options.LogPath, append: false, Encoding.UTF8)
        {
            AutoFlush = true
        };

        Console.SetOut(new TeeWriter(originalOut, file));
        Console.SetError(new TeeWriter(originalError, file));

        return new Logger(originalOut, originalError, file);
    }

    public void Dispose()
    {
        Console.SetOut(originalOut);
        Console.SetError(originalError);
        file?.Dispose();
    }

    private sealed class TeeWriter : TextWriter
    {
        private readonly TextWriter first;
        private readonly TextWriter second;

        public TeeWriter(TextWriter first, TextWriter second)
        {
            this.first = first;
            this.second = second;
        }

        public override Encoding Encoding => first.Encoding;

        public override void Write(char value)
        {
            first.Write(value);
            second.Write(value);
        }

        public override void Write(string? value)
        {
            first.Write(value);
            second.Write(value);
        }

        public override void WriteLine(string? value)
        {
            first.WriteLine(value);
            second.WriteLine(value);
        }

        public override void Flush()
        {
            first.Flush();
            second.Flush();
        }
    }
}
