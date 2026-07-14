using System;
using System.Collections.Generic;
using Xunit;
using CalculationManager;

namespace CalcEngineTests
{
    // Mock implementation of IResourceProvider for testing
    internal sealed class DefaultResourceProvider : IResourceProvider
    {
        public string GetCEngineString(string id)
        {
            if (DefaultCombinedCalcResource.StringResources.TryGetValue(id, out var res))
                return res;
            return id;
        }
    }
}
