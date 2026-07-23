// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalcEngine;

internal sealed class RatPakNumberPool
{
    private const int BucketCount = 32;
    private const int MaximumMantissaLength = 1_024;
    private const int BytesPerBucket = 16 * 1_024;

    private readonly int[] _mantissaLengths = new int[BucketCount];
    private readonly PNUMBER?[] _heads = new PNUMBER?[BucketCount];
    private readonly byte[] _counts = new byte[BucketCount];

    public PNUMBER Rent(int mantissaLength)
    {
        int bucket = FindOrAddBucket(mantissaLength);
        if (bucket >= 0 && _heads[bucket] is { } value)
        {
            _heads[bucket] = value._poolNext;
            value._poolNext = null;
            _counts[bucket]--;
            return value;
        }

        return new PNUMBER(mantissaLength);
    }

    public void Return(PNUMBER value)
    {
        int mantissaLength = value._mant.Length;
        int bucket = FindOrAddBucket(mantissaLength);
        if (bucket < 0 || _counts[bucket] >= RetainedCountLimit(mantissaLength))
        {
            value._poolNext = null;
            return;
        }

        value._sign = 0;
        value._cdigit = 0;
        value._exp = 0;
        Array.Clear(value._mant, 0, value._mant.Length);
        value._poolNext = _heads[bucket];
        _heads[bucket] = value;
        _counts[bucket]++;
    }

    private int FindOrAddBucket(int mantissaLength)
    {
        if (mantissaLength > MaximumMantissaLength)
        {
            return -1;
        }

        int available = -1;
        for (int index = 0; index < _mantissaLengths.Length; index++)
        {
            int registeredLength = _mantissaLengths[index];
            if (registeredLength == mantissaLength)
            {
                return index;
            }

            if (registeredLength == 0 && available < 0)
            {
                available = index;
            }
        }

        if (available >= 0)
        {
            _mantissaLengths[available] = mantissaLength;
        }

        return available;
    }

    private static int RetainedCountLimit(int mantissaLength)
    {
        int approximateBytes = 40 + checked(mantissaLength * sizeof(MANTTYPE));
        int count = BytesPerBucket / approximateBytes;
        return count < 2 ? 2 : count > 64 ? 64 : count;
    }
}
