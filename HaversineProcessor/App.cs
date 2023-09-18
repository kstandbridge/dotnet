using HaversineShared;

using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Diagnostics;
using System.Runtime.CompilerServices;


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

public class ProfileAnchor
{
    public Int64 TSCElapsedExclusive;
    public Int64 TSCElapsedInclusive;
    public Int64 HitCount;
    public Int64 ProcessedByteCount;
    public string Label;
}

public class ProfileBlock : IDisposable
{
    private string _label;
    private Int64 _oldTSCElapsedInclusive;
    private Int64 _startTSC;
    private UInt64 _parentIndex;
    private UInt64 _anchorIndex;

    private UInt64 GetHash(string Path, Int32 Line)
    {
        UInt64 Result = (UInt32)Line;
        
        for(int Index = 0;
            Index < Path.Length;
            ++Index)
        {
            Result = 65599*Result + Path[Index];
        }

        return Result;
    }
    
    public ProfileBlock(Int64 ByteCount = 0,
                        [CallerMemberName]string Label = "",
                        [CallerFilePath]string Path = "",
                        [CallerLineNumber]Int32 Line = 0)
    {
        _parentIndex = Profiler.ProfilerParent;

        UInt64 hash = GetHash(Path, Line);
        _anchorIndex = hash % (UInt64)Profiler.Anchors.Length;

        _label = Label;

        ProfileAnchor anchor = Profiler.Anchors[_anchorIndex];
        _oldTSCElapsedInclusive = anchor.TSCElapsedInclusive;
        anchor.ProcessedByteCount += ByteCount;

        Profiler.ProfilerParent = _anchorIndex;
        _startTSC = Stopwatch.GetTimestamp();
    }

    public void Dispose()
    {
        Int64 Elapsed = Stopwatch.GetTimestamp() - _startTSC;
        Profiler.ProfilerParent = _parentIndex;

        
        ProfileAnchor Parent = Profiler.Anchors[_parentIndex];
        ProfileAnchor Anchor = Profiler.Anchors[_anchorIndex];

        Parent.TSCElapsedExclusive -= Elapsed;
        Anchor.TSCElapsedExclusive += Elapsed;
        Anchor.TSCElapsedInclusive = _oldTSCElapsedInclusive + Elapsed;
        ++Anchor.HitCount;

        Anchor.Label = _label;
    }
}
public static class Profiler
{
    public static ProfileAnchor[] Anchors = new ProfileAnchor[4096];
    public static UInt64 ProfilerParent;
    private static Int64 _startTimestamp;
    private static Int64 _endTimestamp;

    public static void BeginProfile()
    {
        for(int AnchorIndex = 0;
            AnchorIndex < Anchors.Length;
            ++AnchorIndex)
        {
            Anchors[AnchorIndex] = new ProfileAnchor();
        }

        _startTimestamp = Stopwatch.GetTimestamp();
    }

    public static void EndAndPrintProfile()
    {
        _endTimestamp = Stopwatch.GetTimestamp();
        Int64 TotalElapsed = _endTimestamp - _startTimestamp;

        Console.WriteLine("");
        Console.WriteLine($"Total time: {(Double)TotalElapsed/(Double)Stopwatch.Frequency*1000d:F4}ms");
        Console.WriteLine("");

        foreach(ProfileAnchor Anchor in Profiler.Anchors)
        {
            if(Anchor.TSCElapsedInclusive > 0)
            {
                double Percent = 100.0d * ((Double)Anchor.TSCElapsedExclusive) / ((Double)TotalElapsed);
                Console.Write($"\t{Anchor.Label}[{Anchor.HitCount}]: {Anchor.TSCElapsedExclusive} ({Percent:F2}%");
                if(Anchor.TSCElapsedInclusive != Anchor.TSCElapsedExclusive)
                {
                    Double PercentWithChildren = 100.0d * ((Double)Anchor.TSCElapsedInclusive / (Double)TotalElapsed);
                    Console.Write($", {PercentWithChildren:F2}% w/children");
                }
                Console.Write(")");

                if(Anchor.ProcessedByteCount > 0)
                {
                    Double Megabyte = 1024.0d*1024.0d;
                    Double Gigabyte = Megabyte*1024.0d;

                    Double Seconds = (Double)Anchor.TSCElapsedInclusive / (Double)Stopwatch.Frequency*1000d;
                    Double BytesPerSecond = (Double)Anchor.ProcessedByteCount / Seconds;
                    Double Megabytes = (Double)Anchor.ProcessedByteCount / (Double)Megabyte;
                    Double GigabytesPerSecond = BytesPerSecond / Gigabyte;

                    Console.Write($"\t{Megabytes:F3}mb at {GigabytesPerSecond:F2}gb/s");
                }


                Console.WriteLine("");
            }
        }
    }
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
