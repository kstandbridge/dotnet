using Microsoft.Extensions.Logging;

using HaversineShared;

public class RandomSeed
{
    private UInt64 A, B, C, D;

    public RandomSeed(UInt64 Value)
    {
        // NOTE(kstandbridge): Seed pattern for JSF generators
        this.A = 0xf1ea5eed;
        this.B = Value;
        this.C = Value;
        this.D = Value;

        int Count = 20;
        while(Count-- > 0)
        {
            UInt64();
        }
    }

    private UInt64 RotateLeft(UInt64 V, int Shift)
    {
        UInt64 Result = ((V << Shift) | (V >> (64-Shift)));
        return Result;
    }

    public UInt64 UInt64()
    {
        UInt64 A = this.A;
        UInt64 B = this.B;
        UInt64 C = this.C;
        UInt64 D = this.D;

        UInt64 E = A - RotateLeft(C, 17);

        A = (B ^ RotateLeft(C, 17));
        B = (C + D);
        C = (D + E);
        D = (E + A);

        this.A = A;
        this.B = B;
        this.C = C;
        this.D = D;

        return D;
    }

    public Double InRange(Double Min, Double Max)
    {
        Double A = UInt64();
        Double B = System.UInt64.MaxValue;
        Double t = A / B;
        Double Result = (1.0 - t)*Min + t*Max;

        return Result;
    }

    public Double Degree(Double Center, Double Radius, Double MaxAllowed)
    {
        Double MinValue = Center - Radius;
        if(MinValue < -MaxAllowed)
        {
            MinValue = -MaxAllowed;
        }
        
        Double MaxValue = Center + Radius;
        if(MaxValue > MaxAllowed)
        {
            MaxValue = MaxAllowed;
        }
    
        Double Result = InRange(MinValue, MaxValue);
        return Result;
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

    public async Task RunAsync(string[] args)
    {
        if(args.Length == 3)
        {
            UInt64 ClusterCountLeft = System.UInt64.MaxValue;
            Double MaxAllowedX = 180;
            Double MaxAllowedY = 90;

            Double XCenter = 0;
            Double YCenter = 0;
            Double XRadius = MaxAllowedX;
            Double YRadius = MaxAllowedY;

            string MethodName = args[0];
            if(MethodName.Equals("cluster", StringComparison.OrdinalIgnoreCase))
            {
                ClusterCountLeft = 0;
            }
            else if(!MethodName.Equals("uniform", StringComparison.OrdinalIgnoreCase))
            {
                MethodName = "uniform";
                _logger.LogWarning("Invalid method \"{Method}\" using uniform", args[0]);
            }

            if (!UInt64.TryParse(args[1], out UInt64 SeedValue))
            {
                _logger.LogWarning("Invalid random seed \"{RandomSeed}\"", args[1]);
            }
            if (!UInt64.TryParse(args[2], out UInt64 PairCount))
            {
                _logger.LogWarning("Invalid pair count \"{PairCount}\"", args[2]);
            }
            RandomSeed random = new RandomSeed(SeedValue);

            UInt64 MaxPairCount = (1UL << 34);
            if(PairCount < MaxPairCount)
            {
                UInt64 ClusterCountMax = 1 + (PairCount / 64);

                using StreamWriter jsonStream = new StreamWriter("data_flex.json");
                using FileStream answerStream = File.OpenWrite("data_answer.f64");
                
                await jsonStream.WriteAsync($"{{\n\t\"pairs\": [\n");
                Double Sum = 0;
                Double SumCoef = 1.0f / PairCount; 
                for(UInt64 PairIndex = 0;
                    PairIndex < PairCount;
                    ++PairIndex)
                {
                    if(ClusterCountLeft-- == 0)
                    {
                        ClusterCountLeft = ClusterCountMax;
                        XCenter = random.InRange(-MaxAllowedX, MaxAllowedX);
                        YCenter = random.InRange(-MaxAllowedY, MaxAllowedY);
                        XRadius = random.InRange(0, MaxAllowedX);
                        YRadius = random.InRange(0, MaxAllowedY);
                    }

                    Double X0 = random.Degree(XCenter, XRadius, MaxAllowedX);
                    Double Y0 = random.Degree(YCenter, YRadius, MaxAllowedY);
                    Double X1 = random.Degree(XCenter, XRadius, MaxAllowedX);
                    Double Y1 = random.Degree(YCenter, YRadius, MaxAllowedY);

                    Double HaversineDistance = _formula.Reference(X0, Y0, X1, Y1);

                    Sum += SumCoef*HaversineDistance;

                    string Seperator = (PairIndex == (PairCount - 1)) ? "\n" : ",\n";
                    await jsonStream.WriteAsync($"\t\t{{ \"x0\": {X0}, \"y0\": {Y0}, \"x1\": {X1}, \"y1\": {Y1} }}{Seperator}");

                    await answerStream.WriteAsync(BitConverter.GetBytes(HaversineDistance));
                }
                await jsonStream.WriteAsync($"\t]\n}}");
                await answerStream.WriteAsync(BitConverter.GetBytes(Sum));

                Console.WriteLine("");
                Console.WriteLine($"Method: {MethodName}");
                Console.WriteLine($"Random seed: {SeedValue}");
                Console.WriteLine($"Pair count: {PairCount}");
                Console.WriteLine($"Expected sum: {Sum}");
            }
            else
            {
                _logger.LogError("Avoid generating large files by using a pair count less than {MaxPairCount}", MaxPairCount);
            }
        }
        else
        {
            _logger.LogError("Usage {AppName} [uniform/cluster] [random seed] [number of coordinate pairs to generate]", AppDomain.CurrentDomain.FriendlyName);
        }

        await Task.FromResult(0);
    }
}