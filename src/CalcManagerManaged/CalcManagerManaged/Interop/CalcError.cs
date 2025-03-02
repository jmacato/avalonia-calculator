namespace CalcManagerManaged.Interop;

/// <summary>
/// Error codes that match the CalcEngine_CWrapper.h error codes from native layer
/// </summary>
public enum CalcError
{
    /// <summary>
    /// Success status code
    /// </summary>
    Success = 0,
    
    /// <summary>
    /// Invalid parameter passed to function
    /// </summary>
    InvalidParam = unchecked((int)0x80070057),
    
    /// <summary>
    /// Out of memory error
    /// </summary>
    OutOfMemory = unchecked((int)0x8007000E),
    
    /// <summary>
    /// Operation involved division by zero
    /// </summary>
    DivideByZero = unchecked((int)0x80000000),
    
    /// <summary>
    /// Operation input was outside valid domain
    /// </summary>
    Domain = unchecked((int)0x80000001),
    
    /// <summary>
    /// Result is mathematically undefined
    /// </summary>
    Undefined = unchecked((int)0x80000002),
    
    /// <summary>
    /// Positive infinity result
    /// </summary>
    PositiveInfinity = unchecked((int)0x80000003),
    
    /// <summary>
    /// Negative infinity result
    /// </summary>
    NegativeInfinity = unchecked((int)0x80000004),
    
    /// <summary>
    /// Input is within domain but beyond calculation range
    /// </summary>
    InvalidRange = unchecked((int)0x80000006),
    
    /// <summary>
    /// Overflow during calculation
    /// </summary>
    Overflow = unchecked((int)0x80000008),
    
    /// <summary>
    /// No result could be computed
    /// </summary>
    NoResult = unchecked((int)0x80000009)
}