namespace HaversineShared;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

public enum TestModeType
{
    Uninitialized,
    Testing,
    Completed,
    Error,
}

public enum RepetitionValueType
{
    TestCount = 0,

    CPUTimer = 1,
    MemPageFaults = 2,
    ByteCount = 3,

    Count = 4,
}

public class RepetitionValue
{
    public Int64[] E = new Int64[(UInt32)RepetitionValueType.Count];
}

public class RepetitionTestResults
{
    public RepetitionValue Total = new RepetitionValue();
    public RepetitionValue Min = new RepetitionValue();
    public RepetitionValue Max = new RepetitionValue();
}

public class RepetitionTester
{
    private Int64 _targetProcessedByteCount;
    private Int64 _tryForTime;
    private Int64 _testsStartedAt;

    private TestModeType _testModeType;
    private bool _printNewMinimums;
    private UInt32 _openBlockCount;
    private UInt32 _closeBlockCount;

    private RepetitionValue _accumlatedOnThisTest = new RepetitionValue();
    private RepetitionTestResults _results = new RepetitionTestResults();

    private void PrintValue(string label, RepetitionValue value)
    {
        Int64 testCount = value.E[(UInt32)RepetitionValueType.TestCount];
        Double divistor = (testCount > 0) ? (Double)testCount : 1;

        Int64[] e = new Int64[(UInt32)RepetitionValueType.Count];
        for(UInt32 eIndex = 0;
            eIndex < (UInt32)RepetitionValueType.Count;
            ++eIndex)
        {
            e[eIndex] = (Int64)((Double)value.E[eIndex] / divistor);
        }

        Console.Write($"{label}: {e[(UInt32)RepetitionValueType.CPUTimer]}");
        Double seconds = (Double)e[(UInt32)RepetitionValueType.CPUTimer] / (Double)Stopwatch.Frequency;
        Console.Write($" ({seconds*1000.0d:F2}ms)");

        if(e[(UInt32)RepetitionValueType.ByteCount] > 0)
        {
            Double gigabyte = (1024.0f*1024.0f*1024.0f);
            Double bandwidth = (Double)e[(UInt32)RepetitionValueType.ByteCount] / (gigabyte * seconds);
            Console.Write($" {bandwidth:F2}gb/s");
        }

        if(e[(UInt32)RepetitionValueType.MemPageFaults] > 0)
        {
            Int64 faults = e[(UInt32)RepetitionValueType.MemPageFaults];
            Double bytesPerFault = (Double)e[(UInt32)RepetitionValueType.ByteCount] / ((Double)faults * 1024.0d);
            Console.Write($" PF: {faults:F2} ({bytesPerFault:F4}k/fault)");
        }
    }

    private void PrintResults()
    {
        PrintValue("Min", _results.Min);
        Console.WriteLine("");
        PrintValue("Max", _results.Max);
        Console.WriteLine("");
        PrintValue("Avg", _results.Total);
        Console.WriteLine("");
    }

    private void Error(string Message)
    {
        _testModeType = TestModeType.Error;
        Console.WriteLine($"Error: {Message}");
    }


    public void NewTestWave(Int64 targetProcessedByteCount, Int32 secondsToTry = 10)
    {
        if(_testModeType == TestModeType.Uninitialized)
        {
            _testModeType = TestModeType.Testing;
            _targetProcessedByteCount = targetProcessedByteCount;
            _printNewMinimums = true;
            _results.Min.E[(UInt32)RepetitionValueType.CPUTimer] = Int64.MaxValue;
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


    public struct timeval
    {
        UInt64 tv_sec;     /* seconds */
        UInt64 tv_usec;    /* microseconds */
    };
    
    public struct rusage 
    {
        public timeval ru_utime; /* user CPU time used */
        public timeval ru_stime; /* system CPU time used */
        public long   ru_maxrss;        /* maximum resident set size */
        public long   ru_ixrss;         /* integral shared memory size */
        public long   ru_idrss;         /* integral unshared data size */
        public long   ru_isrss;         /* integral unshared stack size */
        public long   ru_minflt;        /* page reclaims (soft page faults) */
        public long   ru_majflt;        /* page faults (hard page faults) */
        public long   ru_nswap;         /* swaps */
        public long   ru_inblock;       /* block input operations */
        public long   ru_oublock;       /* block output operations */
        public long   ru_msgsnd;        /* IPC messages sent */
        public long   ru_msgrcv;        /* IPC messages received */
        public long   ru_nsignals;      /* signals received */
        public long   ru_nvcsw;         /* voluntary context switches */
        public long   ru_nivcsw;        /* involuntary context switches */
    };
    
    [DllImport("libc")]
    public static extern int getrusage(int who, out rusage usage);

    private Int32 ReadOSPageFaultCount()
    {
        Int32 Result;

        if(System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // TODO(kstandbridge): Get fault count win32
            Result = 0;
        }
        else if(System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            rusage Usage;
            getrusage(0, out Usage);
            Result = (Int32)Usage.ru_minflt + (Int32)Usage.ru_majflt;
        }
        else
        {
            // TODO(kstandbridge): Support other OS?
            Result = 0;
        }

        return Result;
    }

    public void BeginTime()
    {
        ++_openBlockCount;

        _accumlatedOnThisTest.E[(UInt32)RepetitionValueType.MemPageFaults] -= ReadOSPageFaultCount();
        _accumlatedOnThisTest.E[(UInt32)RepetitionValueType.CPUTimer] -= Stopwatch.GetTimestamp();
    }

    public void EndTime()
    {
        _accumlatedOnThisTest.E[(UInt32)RepetitionValueType.CPUTimer] += Stopwatch.GetTimestamp();
        _accumlatedOnThisTest.E[(UInt32)RepetitionValueType.MemPageFaults] += ReadOSPageFaultCount();

        ++_closeBlockCount;
    }

    public void CountBytes(Int64 byteCount)
    {
        _accumlatedOnThisTest.E[(UInt32)RepetitionValueType.ByteCount] += byteCount;
    }
    public bool IsTesting()
    {
        if(_testModeType == TestModeType.Testing)
        {
            // TODO(kstandbridge): Perhaps copy this?
            RepetitionValue accum = _accumlatedOnThisTest;
            Int64 currentTime = Stopwatch.GetTimestamp();

            // NOTE(kstandbridge): We don't count tests that had no timing blocks
            if(_openBlockCount > 0)
            {
                if(_openBlockCount != _closeBlockCount)
                {
                    Error("Unbalanced BeginTime/EndTime");
                }

                if(accum.E[(UInt32)RepetitionValueType.ByteCount] != _targetProcessedByteCount)
                {
                    Error("Processed byte count mismatch");
                }

                if(_testModeType == TestModeType.Testing)
                {
                    accum.E[(UInt32)RepetitionValueType.TestCount] = 1;
                    for(UInt32 eIndex = 0;
                        eIndex < (UInt32)RepetitionValueType.Count;
                        ++eIndex)
                    {
                        _results.Total.E[eIndex] += accum.E[eIndex];
                    }

                    if(_results.Max.E[(UInt32)RepetitionValueType.CPUTimer] < accum.E[(UInt32)RepetitionValueType.CPUTimer])
                    {
                        _results.Max = accum;
                    }

                    if(_results.Min.E[(UInt32)RepetitionValueType.CPUTimer] > accum.E[(UInt32)RepetitionValueType.CPUTimer])
                    {
                        _results.Min = accum;

                        // NOTE(kstandbridge): Found new min time so reset the clock for full test time
                        _testsStartedAt = currentTime;

                        if(_printNewMinimums)
                        {
                            PrintValue("Min", _results.Min);
                            Console.Write("                                   \r");
                        }
                    }

                    _openBlockCount = 0;
                    _closeBlockCount = 0;
                    _accumlatedOnThisTest = new RepetitionValue();
                }
            }

            if((currentTime - _testsStartedAt) > _tryForTime)
            {
                _testModeType = TestModeType.Completed;
                Console.Write("                                                          \r");
                PrintResults();
            }

        }

        bool Result = (_testModeType == TestModeType.Testing);
        return Result;
    }
}
