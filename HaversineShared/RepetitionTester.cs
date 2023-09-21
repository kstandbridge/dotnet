namespace HaversineShared;

using System.Diagnostics;

public enum TestModeType
{
    Uninitialized,
    Testing,
    Completed,
    Error,
}

public class RepetitionTestResults
{
    public UInt64 TestCount;
    public Int64 TotalTime;
    public Int64 MaxTime;
    public Int64 MinTime;
}

public class RepetitionTester
{
    private UInt64 _targetProcessedByteCount;
    private Int64 _tryForTime;
    private Int64 _testsStartedAt;

    private TestModeType _testModeType;
    private bool _printNewMinimums;
    private UInt32 _openBlockCount;
    private UInt32 _closeBlockCount;
    private Int64 _timeAccumlatedOnThisTest;
    private UInt64 _bytesAccumlatedOnThisTest;

    private RepetitionTestResults _results = new RepetitionTestResults();

    private void Error(string Message)
    {
        _testModeType = TestModeType.Error;
        Console.WriteLine($"Error: {Message}");
    }

    private void PrintTime(string label, Double time, UInt64 byteCount)
    {
        Console.Write($"{label}: {time}");
        Double Miliseconds = time / Stopwatch.Frequency;
        Console.Write($" ({Miliseconds*1000.0d:F2}ms)");

        if(byteCount > 0)
        {
            Double gigabyte = (1024.0f*1024.0f*1024.0f);
            Double bestBandwidth = byteCount / (gigabyte * Miliseconds);
            Console.Write($" {bestBandwidth:F2}gb/s");
        }
    }

    private void PrintResults(UInt64 byteCount)
    {
        PrintTime("Min", _results.MinTime, byteCount);
        Console.WriteLine("");

        PrintTime("Max", _results.MaxTime, byteCount);
        Console.WriteLine("");

        if(_results.TestCount > 0)
        {
            PrintTime("Avg", (Double)_results.TotalTime / (Double)_results.TestCount, byteCount);
            Console.WriteLine("");
        }
    }

    public bool IsTesting()
    {
        if(_testModeType == TestModeType.Testing)
        {
            Int64 currentTime = Stopwatch.GetTimestamp();

            // NOTE(kstandbridge): We don't count tests that had no timing blocks
            if(_openBlockCount > 0)
            {
                if(_openBlockCount != _closeBlockCount)
                {
                    Error("Unbalanced BeginTime/EndTime");
                }

                if(_bytesAccumlatedOnThisTest != _targetProcessedByteCount)
                {
                    Error("Processed byte count mismatch");
                }

                if(_testModeType == TestModeType.Testing)
                {
                    Int64 elapsedTime = _timeAccumlatedOnThisTest;
                    _results.TestCount += 1;
                    _results.TotalTime += elapsedTime;
                    if(_results.MaxTime < elapsedTime)
                    {
                        _results.MaxTime = elapsedTime;
                    }

                    if(_results.MinTime > elapsedTime)
                    {
                        _results.MinTime = elapsedTime;

                        // NOTE(kstandbridge): Found new min time so reset the clock for full test time
                        _testsStartedAt = currentTime;

                        if(_printNewMinimums)
                        {
                            PrintTime("Min", _results.MinTime, _bytesAccumlatedOnThisTest);
                            Console.Write("               \r");
                        }
                    }

                    _openBlockCount = 0;
                    _closeBlockCount = 0;
                    _timeAccumlatedOnThisTest = 0;
                    _bytesAccumlatedOnThisTest = 0;
                }
            }

            if((currentTime - _testsStartedAt) > _tryForTime)
            {
                _testModeType = TestModeType.Completed;
                Console.Write("                                                          \r");
                PrintResults(_targetProcessedByteCount);
            }

        }

        bool Result = (_testModeType == TestModeType.Testing);
        return Result;
    }

    public void BeginTime()
    {
        ++_openBlockCount;
        _timeAccumlatedOnThisTest -= Stopwatch.GetTimestamp();
    }

    public void EndTime()
    {
        ++_closeBlockCount;
        _timeAccumlatedOnThisTest += Stopwatch.GetTimestamp();
    }

    public void CountBytes(UInt64 byteCount)
    {
        _bytesAccumlatedOnThisTest += byteCount;
    }

    public void NewTestWave(UInt64 targetProcessedByteCount, Int32 secondsToTry = 10)
    {
        if(_testModeType == TestModeType.Uninitialized)
        {
            _testModeType = TestModeType.Testing;
            _targetProcessedByteCount = targetProcessedByteCount;
            _printNewMinimums = true;
            _results.MinTime = Int64.MaxValue;
        }
        else if(_testModeType == TestModeType.Completed)
        {
            _testModeType = TestModeType.Testing;

            if(_targetProcessedByteCount != targetProcessedByteCount)
            {
                Error("TargetProcessedByteCount changed");
            }
        }

        _tryForTime = secondsToTry*Stopwatch.Frequency;
        _testsStartedAt = Stopwatch.GetTimestamp();
    }
}
