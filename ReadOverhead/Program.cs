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

public class ReadViaFile: IReadVia
{
    public RepetitionTester RepetitionTester { get; private set; }

    public ReadViaFile(RepetitionTester repetitionTester)
    {
        RepetitionTester = repetitionTester;
    }

    public string Name => "File";

    public void Read(ReadParameters readParameters)
    {
        while (RepetitionTester.IsTesting())
        {
            RepetitionTester.BeginTime();
            readParameters.Dest = File.ReadAllBytes(readParameters.FileName);
            RepetitionTester.EndTime();

            RepetitionTester.CountBytes((Int64)readParameters.Dest.Length);
        }
    }
}

public class ReadViaFileStream : IReadVia
{
    public RepetitionTester RepetitionTester { get; private set; }

    public ReadViaFileStream(RepetitionTester repetitionTester)
    {
        RepetitionTester = repetitionTester;
    }

    public string Name => "FileStream";

    public void Read(ReadParameters readParameters)
    {
        while (RepetitionTester.IsTesting())
        {
            using FileStream fileStream = File.OpenRead(readParameters.FileName);

            RepetitionTester.BeginTime();
            fileStream.Read(readParameters.Dest);
            RepetitionTester.EndTime();

            RepetitionTester.CountBytes((Int64)fileStream.Length);
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

        if (args.Length == 1)
        {
            string fileName = args[0];
            FileInfo fileInfo = new FileInfo(fileName);

            ReadParameters readParameters = new ReadParameters
            {
                Dest = new byte[fileInfo.Length],
                FileName = fileName
            };

            IReadVia[] testers = new IReadVia[]
            {
                new WriteToAllBytes(new RepetitionTester()),
                new ReadViaFile(new RepetitionTester()),
                new ReadViaFileStream(new RepetitionTester())
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
        else
        {
            _logger.LogError("Usage: {AppName} [input file]", AppDomain.CurrentDomain.FriendlyName);
        }

    }
}