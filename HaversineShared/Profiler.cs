using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HaversineShared;

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
