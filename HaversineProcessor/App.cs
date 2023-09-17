using Microsoft.Extensions.Logging;

using HaversineShared;
using System.Text.Json;
using System.Xml.XPath;
using System.Formats.Asn1;

public class HaversinePair
{
    public Double X0;
    public Double Y0;
    public Double X1;
    public Double Y1;
}

internal sealed class App
{
    private readonly ILogger<App> _logger;
    private readonly IHaversineFormula _formula;

    public App(
        ILogger<App> logger, IHaversineFormula formula) =>
            (_logger, _formula) = 
            ( logger,  formula);

    private List<HaversinePair> ParseHaversinePairs(string json)
    {
        List<HaversinePair> Result = new List<HaversinePair>();

        using JsonDocument document = JsonDocument.Parse(json);

        foreach(JsonElement pairElement in document.RootElement.GetProperty("pairs").EnumerateArray())
        {
            HaversinePair pair = new HaversinePair
            {
                X0 = pairElement.GetProperty("x0").GetDouble(),
                Y0 = pairElement.GetProperty("y0").GetDouble(),
                X1 = pairElement.GetProperty("x1").GetDouble(),
                Y1 = pairElement.GetProperty("y1").GetDouble()
            };

            Result.Add(pair);
        }

        return Result;

    }

    private Double SumHaversineDistances(List<HaversinePair> pairs)
    {
        Double Result = 0;

        Double SumCoef = 1 / (Double)pairs.Count;
        for(int PairIndex = 0;
            PairIndex < pairs.Count;
            ++PairIndex)
        {
            HaversinePair Pair = pairs[PairIndex];
            Double Distance = _formula.Reference(Pair.X0, Pair.Y0, Pair.X1, Pair.Y1);
            Result += SumCoef*Distance;
        }

        return Result;
    }

    public async Task RunAsync(string[] args)
    {
        if((args.Length == 1) || (args.Length == 2))
        {
            if(File.Exists(args[0]))
            {
                string inputJson = File.ReadAllText(args[0]);

                UInt32 MinimumJsonPairEncoding = 6*4;
                UInt64 MaxPairCount = (UInt64)inputJson.Length / MinimumJsonPairEncoding;

                if(MaxPairCount > 0)
                {
                    List<HaversinePair> Pairs = ParseHaversinePairs(inputJson);
                    
                    Double Sum = SumHaversineDistances(Pairs);

                    Console.WriteLine($"Input size: {inputJson.Length}");
                    Console.WriteLine($"Pair count: {Pairs.Count}");
                    Console.WriteLine($"Haversine sum: {Sum}");

                    if(args.Length == 2)
                    {
                        Console.WriteLine("");
                        Console.WriteLine("Validation:");

                        using FileStream answerFileStream = File.OpenRead(args[1]);
                        using BinaryReader answerReader = new BinaryReader(answerFileStream);

                        foreach(HaversinePair pair in Pairs)
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
        }
        else
        {
            _logger.LogError("Usage:\n\t{AppName} [haversine_input.json]\n\t{AppName} [haversine_input.json] [answers.f64]",
                             AppDomain.CurrentDomain.FriendlyName,
                             AppDomain.CurrentDomain.FriendlyName);
        }

        await Task.FromResult(0);
    }
}
