using HaversineShared;

using Microsoft.Extensions.Logging;
public class ReadParameters
{
    public byte[] Dest;
    public string FileName;
}

public interface IReadVia
{
    public string Name { get; }
    public void Read(ReadParameters readParameters);
    public RepetitionTester RepetitionTester { get; }
}

public class WriteToAllBytes : IReadVia
{
    public RepetitionTester RepetitionTester { get; private set; }

    public WriteToAllBytes(RepetitionTester repetitionTester)
    {
        RepetitionTester = repetitionTester;
    }

    public string Name => "WriteAllBytes";

    public void Read(ReadParameters readParameters)
    {
        while (RepetitionTester.IsTesting())
        {
            RepetitionTester.BeginTime();
            for(Int64 Index = 0;
                Index < readParameters.Dest.Length;
                ++Index)
            {
                readParameters.Dest[Index] = (byte)Index;
            }
            RepetitionTester.EndTime();

            RepetitionTester.CountBytes((Int64)readParameters.Dest.Length);
        }
    }
}

public class WriteToAllBytesBackwards : IReadVia
{
    public RepetitionTester RepetitionTester { get; private set; }

    public WriteToAllBytesBackwards(RepetitionTester repetitionTester)
    {
        RepetitionTester = repetitionTester;
    }

    public string Name => "WriteToAllBytesBackwards";

    public void Read(ReadParameters readParameters)
    {
        while (RepetitionTester.IsTesting())
        {
            RepetitionTester.BeginTime();
            for(Int64 Index = 0;
                Index < readParameters.Dest.Length;
                ++Index)
            {
                readParameters.Dest[(readParameters.Dest.Length - Index - 1)] = (byte)Index;
            }
            RepetitionTester.EndTime();

            RepetitionTester.CountBytes((Int64)readParameters.Dest.Length);
        }
    }
}

internal class Program
{
    private static void Main(string[] args)
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(
            builder => builder.AddConsole()
                              .AddDebug()
                              .SetMinimumLevel(LogLevel.Debug));

        ILogger<Program> _logger = loggerFactory.CreateLogger<Program>();

        ReadParameters readParameters = new ReadParameters
        {
            Dest = new byte[1024*1024*1024],
            FileName = "/dev/null"
        };

        IReadVia[] testers = new IReadVia[]
        {
            new WriteToAllBytes(new RepetitionTester()),
            new WriteToAllBytesBackwards(new RepetitionTester()),
        };

        for(;;)
        {
            foreach(IReadVia tester in testers)
            {
                Console.Write($"\n--- {tester.Name} ---\n");
                tester.RepetitionTester.NewTestWave((Int64)readParameters.Dest.Length);
                tester.Read(readParameters);
            }
        }
    }
}