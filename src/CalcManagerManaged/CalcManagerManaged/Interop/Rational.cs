using System;
using System.Runtime.InteropServices;

namespace CalcManagerManaged.Interop
{
    /// <summary>
    /// Represents a rational number with arbitrary precision
    /// </summary>
    public class Rational : IDisposable
    {
        private IntPtr _handle;
        private bool _disposed;
        
        // Default precision for standard calculator mode
        public const int DefaultPrecision = 32;
        
        // Default radix (decimal)
        public const uint DefaultRadix = 10;

        /// <summary>
        /// Creates a rational number from an integer
        /// </summary>
        public Rational(int value)
        {
            _handle = NativeMethods.Calc_CreateRationalFromInt32(value);
            if (_handle == IntPtr.Zero)
                throw new OutOfMemoryException("Failed to create rational number");
        }

        /// <summary>
        /// Creates a rational number from a string
        /// </summary>
        public Rational(string value, uint radix = DefaultRadix, int precision = DefaultPrecision)
        {
            _handle = NativeMethods.Calc_CreateRationalFromString(value, radix, precision);
            if (_handle == IntPtr.Zero)
                throw new OutOfMemoryException("Failed to create rational number");
        }

        /// <summary>
        /// Internal constructor from native handle
        /// </summary>
        internal Rational(IntPtr handle, bool transferOwnership = true)
        {
            _handle = handle;
            if (_handle == IntPtr.Zero)
                throw new ArgumentNullException(nameof(handle));
            
            // If we're not transferring ownership, we need to clone the rational
            if (!transferOwnership)
            {
                var clonedHandle = NativeMethods.Calc_RationalAdd(_handle, NativeMethods.Calc_CreateRationalFromInt32(0), DefaultPrecision);
                if (clonedHandle == IntPtr.Zero)
                    throw new OutOfMemoryException("Failed to clone rational number");
                _handle = clonedHandle;
            }
        }

        /// <summary>
        /// Gets the native handle for this rational number
        /// </summary>
        internal IntPtr Handle => _handle;

        /// <summary>
        /// Converts this rational number to a string
        /// </summary>
        public string ToString(uint radix = DefaultRadix, CalcNumberFormat format = CalcNumberFormat.Float, 
            int precision = DefaultPrecision)
        {
            CheckDisposed();
            
            var strPtr = NativeMethods.Calc_RationalToString(_handle, radix, format, precision);
            if (strPtr == IntPtr.Zero)
                return string.Empty;
            
            try
            {
                return Marshal.PtrToStringAnsi(strPtr);
            }
            finally
            {
                NativeMethods.Calc_FreeString(strPtr);
            }
        }

        /// <summary>
        /// Converts this rational number to a 32-bit integer
        /// </summary>
        public int ToInt32(uint radix = DefaultRadix, int precision = DefaultPrecision)
        {
            CheckDisposed();
            
            int value = 0;
            var result = NativeMethods.Calc_RationalToInt32(_handle, radix, precision, ref value);
            if (result != CalcError.Success)
                throw new InvalidOperationException($"Failed to convert rational to int32: {result}");
                
            return value;
        }

        /// <summary>
        /// Converts this rational number to a 64-bit unsigned integer
        /// </summary>
        public ulong ToUInt64(uint radix = DefaultRadix, int precision = DefaultPrecision)
        {
            CheckDisposed();
            
            ulong value = 0;
            var result = NativeMethods.Calc_RationalToUInt64(_handle, radix, precision, ref value);
            if (result != CalcError.Success)
                throw new InvalidOperationException($"Failed to convert rational to uint64: {result}");
                
            return value;
        }

        /// <summary>
        /// Adds two rational numbers
        /// </summary>
        public static Rational operator +(Rational a, Rational b)
        {
            a.CheckDisposed();
            b.CheckDisposed();
            
            var resultHandle = NativeMethods.Calc_RationalAdd(a._handle, b._handle, DefaultPrecision);
            if (resultHandle == IntPtr.Zero)
                throw new OutOfMemoryException("Failed to add rational numbers");
                
            return new Rational(resultHandle);
        }

        /// <summary>
        /// Subtracts two rational numbers
        /// </summary>
        public static Rational operator -(Rational a, Rational b)
        {
            a.CheckDisposed();
            b.CheckDisposed();
            
            var resultHandle = NativeMethods.Calc_RationalSubtract(a._handle, b._handle, DefaultPrecision);
            if (resultHandle == IntPtr.Zero)
                throw new OutOfMemoryException("Failed to subtract rational numbers");
                
            return new Rational(resultHandle);
        }

        /// <summary>
        /// Multiplies two rational numbers
        /// </summary>
        public static Rational operator *(Rational a, Rational b)
        {
            a.CheckDisposed();
            b.CheckDisposed();
            
            var resultHandle = NativeMethods.Calc_RationalMultiply(a._handle, b._handle, DefaultPrecision);
            if (resultHandle == IntPtr.Zero)
                throw new OutOfMemoryException("Failed to multiply rational numbers");
                
            return new Rational(resultHandle);
        }

        /// <summary>
        /// Divides two rational numbers
        /// </summary>
        public static Rational operator /(Rational a, Rational b)
        {
            a.CheckDisposed();
            b.CheckDisposed();
            
            var resultHandle = NativeMethods.Calc_RationalDivide(a._handle, b._handle, DefaultPrecision);
            if (resultHandle == IntPtr.Zero)
                throw new OutOfMemoryException("Failed to divide rational numbers");
                
            return new Rational(resultHandle);
        }

        /// <summary>
        /// Computes the modulo of two rational numbers
        /// </summary>
        public static Rational operator %(Rational a, Rational b)
        {
            a.CheckDisposed();
            b.CheckDisposed();
            
            var resultHandle = NativeMethods.Calc_RationalMod(a._handle, b._handle);
            if (resultHandle == IntPtr.Zero)
                throw new OutOfMemoryException("Failed to compute modulo of rational numbers");
                
            return new Rational(resultHandle);
        }

        /// <summary>
        /// Negates a rational number
        /// </summary>
        public static Rational operator -(Rational a)
        {
            a.CheckDisposed();
            
            var resultHandle = NativeMethods.Calc_RationalNegate(a._handle);
            if (resultHandle == IntPtr.Zero)
                throw new OutOfMemoryException("Failed to negate rational number");
                
            return new Rational(resultHandle);
        }

        /// <summary>
        /// Compares two rational numbers for equality
        /// </summary>
        public static bool operator ==(Rational a, Rational b)
        {
            if (ReferenceEquals(a, null) && ReferenceEquals(b, null))
                return true;
                
            if (ReferenceEquals(a, null) || ReferenceEquals(b, null))
                return false;
                
            a.CheckDisposed();
            b.CheckDisposed();
            
            bool result = false;
            var calcResult = NativeMethods.Calc_RationalEquals(a._handle, b._handle, DefaultPrecision, ref result);
            if (calcResult != CalcError.Success)
                throw new InvalidOperationException($"Failed to compare rational numbers: {calcResult}");
                
            return result;
        }

        /// <summary>
        /// Compares two rational numbers for inequality
        /// </summary>
        public static bool operator !=(Rational a, Rational b)
        {
            return !(a == b);
        }

        /// <summary>
        /// Compares two rational numbers
        /// </summary>
        public static bool operator <(Rational a, Rational b)
        {
            a.CheckDisposed();
            b.CheckDisposed();
            
            int result = 0;
            var calcResult = NativeMethods.Calc_RationalCompare(a._handle, b._handle, DefaultPrecision, ref result);
            if (calcResult != CalcError.Success)
                throw new InvalidOperationException($"Failed to compare rational numbers: {calcResult}");
                
            return result < 0;
        }

        /// <summary>
        /// Compares two rational numbers
        /// </summary>
        public static bool operator >(Rational a, Rational b)
        {
            a.CheckDisposed();
            b.CheckDisposed();
            
            int result = 0;
            var calcResult = NativeMethods.Calc_RationalCompare(a._handle, b._handle, DefaultPrecision, ref result);
            if (calcResult != CalcError.Success)
                throw new InvalidOperationException($"Failed to compare rational numbers: {calcResult}");
                
            return result > 0;
        }

        /// <summary>
        /// Compares two rational numbers
        /// </summary>
        public static bool operator <=(Rational a, Rational b)
        {
            a.CheckDisposed();
            b.CheckDisposed();
            
            int result = 0;
            var calcResult = NativeMethods.Calc_RationalCompare(a._handle, b._handle, DefaultPrecision, ref result);
            if (calcResult != CalcError.Success)
                throw new InvalidOperationException($"Failed to compare rational numbers: {calcResult}");
                
            return result <= 0;
        }

        /// <summary>
        /// Compares two rational numbers
        /// </summary>
        public static bool operator >=(Rational a, Rational b)
        {
            a.CheckDisposed();
            b.CheckDisposed();
            
            int result = 0;
            var calcResult = NativeMethods.Calc_RationalCompare(a._handle, b._handle, DefaultPrecision, ref result);
            if (calcResult != CalcError.Success)
                throw new InvalidOperationException($"Failed to compare rational numbers: {calcResult}");
                
            return result >= 0;
        }

        /// <summary>
        /// Calculates the sine of a rational number
        /// </summary>
        public Rational Sin(CalcAngleType angleType = CalcAngleType.Radians, uint radix = DefaultRadix, int precision = DefaultPrecision)
        {
            CheckDisposed();
            
            var resultHandle = NativeMethods.Calc_RationalSin(_handle, angleType, radix, precision);
            if (resultHandle == IntPtr.Zero)
                throw new OutOfMemoryException("Failed to calculate sine");
                
            return new Rational(resultHandle);
        }

        /// <summary>
        /// Calculates the cosine of a rational number
        /// </summary>
        public Rational Cos(CalcAngleType angleType = CalcAngleType.Radians, uint radix = DefaultRadix, int precision = DefaultPrecision)
        {
            CheckDisposed();
            
            var resultHandle = NativeMethods.Calc_RationalCos(_handle, angleType, radix, precision);
            if (resultHandle == IntPtr.Zero)
                throw new OutOfMemoryException("Failed to calculate cosine");
                
            return new Rational(resultHandle);
        }

        /// <summary>
        /// Calculates the tangent of a rational number
        /// </summary>
        public Rational Tan(CalcAngleType angleType = CalcAngleType.Radians, uint radix = DefaultRadix, int precision = DefaultPrecision)
        {
            CheckDisposed();
            
            var resultHandle = NativeMethods.Calc_RationalTan(_handle, angleType, radix, precision);
            if (resultHandle == IntPtr.Zero)
                throw new OutOfMemoryException("Failed to calculate tangent");
                
            return new Rational(resultHandle);
        }

        /// <summary>
        /// Calculates the square root of a rational number
        /// </summary>
        public Rational Sqrt(uint radix = DefaultRadix, int precision = DefaultPrecision)
        {
            CheckDisposed();
            
            var resultHandle = NativeMethods.Calc_RationalSqrt(_handle, radix, precision);
            if (resultHandle == IntPtr.Zero)
                throw new OutOfMemoryException("Failed to calculate square root");
                
            return new Rational(resultHandle);
        }

        /// <summary>
        /// Raises a rational number to a power
        /// </summary>
        public Rational Pow(Rational exponent, uint radix = DefaultRadix, int precision = DefaultPrecision)
        {
            CheckDisposed();
            exponent.CheckDisposed();
            
            var resultHandle = NativeMethods.Calc_RationalPow(_handle, exponent._handle, radix, precision);
            if (resultHandle == IntPtr.Zero)
                throw new OutOfMemoryException("Failed to calculate power");
                
            return new Rational(resultHandle);
        }

        /// <summary>
        /// Calculates the nth root of a rational number
        /// </summary>
        public Rational Root(Rational root, uint radix = DefaultRadix, int precision = DefaultPrecision)
        {
            CheckDisposed();
            root.CheckDisposed();
            
            var resultHandle = NativeMethods.Calc_RationalRoot(_handle, root._handle, radix, precision);
            if (resultHandle == IntPtr.Zero)
                throw new OutOfMemoryException("Failed to calculate root");
                
            return new Rational(resultHandle);
        }

        /// <summary>
        /// Calculates the factorial of a rational number
        /// </summary>
        public Rational Factorial(uint radix = DefaultRadix, int precision = DefaultPrecision)
        {
            CheckDisposed();
            
            var resultHandle = NativeMethods.Calc_RationalFact(_handle, radix, precision);
            if (resultHandle == IntPtr.Zero)
                throw new OutOfMemoryException("Failed to calculate factorial");
                
            return new Rational(resultHandle);
        }

        /// <summary>
        /// Calculates the natural logarithm of a rational number
        /// </summary>
        public Rational Ln(int precision = DefaultPrecision)
        {
            CheckDisposed();
            
            var resultHandle = NativeMethods.Calc_RationalLn(_handle, precision);
            if (resultHandle == IntPtr.Zero)
                throw new OutOfMemoryException("Failed to calculate natural logarithm");
                
            return new Rational(resultHandle);
        }

        /// <summary>
        /// Calculates the base-10 logarithm of a rational number
        /// </summary>
        public Rational Log10(int precision = DefaultPrecision)
        {
            CheckDisposed();
            
            var resultHandle = NativeMethods.Calc_RationalLog10(_handle, precision);
            if (resultHandle == IntPtr.Zero)
                throw new OutOfMemoryException("Failed to calculate base-10 logarithm");
                
            return new Rational(resultHandle);
        }

        /// <summary>
        /// Calculates e raised to the power of a rational number
        /// </summary>
        public Rational Exp(uint radix = DefaultRadix, int precision = DefaultPrecision)
        {
            CheckDisposed();
            
            var resultHandle = NativeMethods.Calc_RationalExp(_handle, radix, precision);
            if (resultHandle == IntPtr.Zero)
                throw new OutOfMemoryException("Failed to calculate exponential");
                
            return new Rational(resultHandle);
        }

        /// <summary>
        /// Converts this rational number to string representation
        /// </summary>
        public override string ToString()
        {
            return ToString(DefaultRadix, CalcNumberFormat.Float, DefaultPrecision);
        }

        /// <summary>
        /// Compares this rational number with another object for equality
        /// </summary>
        public override bool Equals(object obj)
        {
            if (!(obj is Rational other))
                return false;
                
            return this == other;
        }

        /// <summary>
        /// Gets a hash code for this rational number
        /// </summary>
        public override int GetHashCode()
        {
            return _handle.GetHashCode();
        }

        /// <summary>
        /// Checks if this rational number has been disposed
        /// </summary>
        private void CheckDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Rational));
        }

        /// <summary>
        /// Disposes this rational number
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes this rational number
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && _handle != IntPtr.Zero)
            {
                NativeMethods.Calc_DestroyRational(_handle);
                _handle = IntPtr.Zero;
            }
            
            _disposed = true;
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~Rational()
        {
            Dispose(false);
        }
    }
}