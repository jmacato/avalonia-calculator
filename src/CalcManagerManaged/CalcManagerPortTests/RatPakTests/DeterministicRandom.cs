namespace CalcManagerPortTests.RatPakTests;

internal sealed class DeterministicRandom
{
    private const int Mbig = int.MaxValue;
    private const int Mseed = 161803398;

    private readonly int[] _seedArray = new int[56];
    private int _inext;
    private int _inextp;

    public DeterministicRandom(int seed)
    {
        int subtraction = seed == int.MinValue ? int.MaxValue : Math.Abs(seed);
        int mj = Mseed - subtraction;
        _seedArray[55] = mj;
        int mk = 1;
        int ii = 0;
        for (int i = 1; i < 55; i++)
        {
            if ((ii += 21) >= 55)
            {
                ii -= 55;
            }

            _seedArray[ii] = mk;
            mk = mj - mk;
            if (mk < 0)
            {
                mk += Mbig;
            }

            mj = _seedArray[ii];
        }

        for (int k = 1; k < 5; k++)
        {
            for (int i = 1; i < 56; i++)
            {
                int n = i + 30;
                if (n >= 55)
                {
                    n -= 55;
                }

                _seedArray[i] -= _seedArray[1 + (n % 55)];
                if (_seedArray[i] < 0)
                {
                    _seedArray[i] += Mbig;
                }
            }
        }

        _inext = 0;
        _inextp = 21;
    }

    public DeterministicRandom()
        : this(Environment.TickCount)
    {
    }

    public int Next()
    {
        return InternalSample();
    }

    public int Next(int maxValue)
    {
        return (int)(Sample() * maxValue);
    }

    public int Next(int minValue, int maxValue)
    {
        long range = (long)maxValue - minValue;
        return range <= int.MaxValue
            ? (int)(Sample() * range) + minValue
            : (int)((long)(GetSampleForLargeRange() * range) + minValue);
    }

    public double NextDouble()
    {
        return Sample();
    }

    public void NextBytes(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (byte)InternalSample();
        }
    }

    private double Sample()
    {
        return InternalSample() * (1.0 / Mbig);
    }

    private double GetSampleForLargeRange()
    {
        int result = InternalSample();
        if (InternalSample() % 2 == 0)
        {
            result = -result;
        }

        double d = result;
        d += int.MaxValue - 1;
        d /= (2.0 * int.MaxValue) - 1;
        return d;
    }

    private int InternalSample()
    {
        int locINext = _inext;
        int locINextp = _inextp;
        if (++locINext >= 56)
        {
            locINext = 1;
        }

        if (++locINextp >= 56)
        {
            locINextp = 1;
        }

        int retVal = _seedArray[locINext] - _seedArray[locINextp];
        if (retVal == Mbig)
        {
            retVal--;
        }

        if (retVal < 0)
        {
            retVal += Mbig;
        }

        _seedArray[locINext] = retVal;
        _inext = locINext;
        _inextp = locINextp;
        return retVal;
    }
}
