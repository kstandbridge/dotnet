using HaversineShared;

using Microsoft.Extensions.Logging;
using System.Text.Json;

public class HaversinePair
{
    public Double X0;
    public Double Y0;
    public Double X1;
    public Double Y1;
}

public class HaversineDocument
{
    public List<HaversinePair> Pairs;
}
internal sealed class App
{
    private readonly ILogger<App> _logger;
    private readonly IHaversineFormula _formula;

    public App(
        ILogger<App> logger, IHaversineFormula formula) =>
            (_logger, _formula) = 
            ( logger,  formula);
    
    private static void GetMoreBytesFromStream(
            FileStream stream, ref byte[] buffer, ref Utf8JsonReader reader)
    {
        int bytesRead;
        if (reader.BytesConsumed < buffer.Length)
        {
            ReadOnlySpan<byte> leftover = buffer.AsSpan((int)reader.BytesConsumed);

            if (leftover.Length == buffer.Length)
            {
                Array.Resize(ref buffer, buffer.Length * 2);
                Console.WriteLine($"Increased buffer size to {buffer.Length}");
            }

            leftover.CopyTo(buffer);
            bytesRead = stream.Read(buffer.AsSpan(leftover.Length));
        }
        else
        {
            bytesRead = stream.Read(buffer);
        }
        Console.WriteLine($"String in buffer is: {System.Text.Encoding.UTF8.GetString(buffer)}");
        reader = new Utf8JsonReader(buffer, isFinalBlock: bytesRead == 0, reader.CurrentState);
    }

    private byte[] ReadEntireFile(string filePath)
    {
        FileInfo fileInfo = new FileInfo(filePath);
        using ProfileBlock profileBlock = new ProfileBlock(fileInfo.Length);

        byte[] result = new byte[fileInfo.Length];
        
        using FileStream fileStream = File.OpenRead(filePath);
        fileStream.Read(result);

        return result;
    }
        
    private List<HaversinePair> ParseHaversinePairs(string jsonPath)
    {
        using ProfileBlock profileBlock = new ProfileBlock();

        List<HaversinePair> result = new List<HaversinePair>();

        byte[] bytes = ReadEntireFile(jsonPath);

        MemoryStream memoryStream = new MemoryStream(bytes);
        Utf8JsonReader jsonReader = new Utf8JsonReader(bytes, isFinalBlock: false, state: default);

        while(jsonReader.Read())
        {
            if((jsonReader.TokenType == JsonTokenType.PropertyName) &&
                (jsonReader.ValueTextEquals("pairs")))
            {
                HaversinePair currentPair = null;
                while(jsonReader.Read())
                {
                    if(jsonReader.TokenType == JsonTokenType.StartObject)
                    {
                        currentPair = new HaversinePair();
                        result.Add(currentPair);
                    }
                    else if(jsonReader.TokenType == JsonTokenType.PropertyName)
                    {
                        if(currentPair != null)
                        {
                            if(jsonReader.ValueTextEquals("x0"))
                            {
                                jsonReader.Read();
                                currentPair.X0 = jsonReader.GetDouble();
                            }
                            else if(jsonReader.ValueTextEquals("y0"))
                            {
                                jsonReader.Read();
                                currentPair.Y0 = jsonReader.GetDouble();
                            }
                            else if(jsonReader.ValueTextEquals("x1"))
                            {
                                jsonReader.Read();
                                currentPair.X1 = jsonReader.GetDouble();
                            }
                            else if(jsonReader.ValueTextEquals("y1"))
                            {
                                jsonReader.Read();
                                currentPair.Y1 = jsonReader.GetDouble();
                            }
                        }
                    }

                }

            }
        }
        
        return result;
    }

    private Double SumHaversineDistances(List<HaversinePair> pairs)
    {
        using ProfileBlock FuncBlock = new ProfileBlock();

        Double Result = 0;

        Double SumCoef = 1 / (Double)pairs.Count;
        for(int PairIndex = 0;
            PairIndex < pairs.Count;
            ++PairIndex)
        {
            using ProfileBlock PairBlock = new ProfileBlock(0, "SumPair");

            HaversinePair Pair = pairs[PairIndex];
            Double Distance = _formula.Reference(Pair.X0, Pair.Y0, Pair.X1, Pair.Y1);
            Result += SumCoef*Distance;
        }

        return Result;
    }

    public async Task RunAsync(string[] args)
    {
        Console.WriteLine("");

        Profiler.BeginProfile();

        if((args.Length == 1) || (args.Length == 2))
        {
            if(File.Exists(args[0]))
            {
                FileInfo fileInfo = new FileInfo(args[0]);
                
                List<HaversinePair> pairs = ParseHaversinePairs(args[0]);
                
                Double Sum = SumHaversineDistances(pairs);
                Console.WriteLine($"Input size: {fileInfo.Length}");
                Console.WriteLine($"Pair count: {pairs.Count}");
                Console.WriteLine($"Haversine sum: {Sum}");

                if(args.Length == 2)
                {
                    fileInfo = new FileInfo(args[1]);

                    using ProfileBlock ProflileValidation = new ProfileBlock(fileInfo.Length, "Validation");

                    Console.WriteLine("");
                    Console.WriteLine("Validation:");

                    using FileStream answerFileStream = File.OpenRead(args[1]);
                    using BinaryReader answerReader = new BinaryReader(answerFileStream);

                    foreach(HaversinePair pair in pairs)
                    {
                        Double distance = _formula.Reference(pair.X0, pair.Y0, pair.X1, pair.Y1);
                        Double answer = answerReader.ReadDouble();
                        if(distance != answer)
                        {
                            Console.WriteLine($"Expected: \"{distance}\" Actual: \"{answer}\"");
                        }
                    }

                    Double RefSum = answerReader.ReadDouble();
                    Console.WriteLine($"Reference sum: {RefSum}");
                    Console.WriteLine($"Difference: {Sum}");
                }
            }
        }
        else
        {
            _logger.LogError("Usage:\n\t{AppName} [haversine_input.json]\n\t{AppName} [haversine_input.json] [answers.f64]",
                             AppDomain.CurrentDomain.FriendlyName,
                             AppDomain.CurrentDomain.FriendlyName);
        }

        Profiler.EndAndPrintProfile();

        await Task.Yield();
    }
}
