// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UnitConversionManager;

public interface IUnitConverterVMCallback
{
    void DisplayCallback(string from, string toValue);
    void SuggestedValueCallback(IList<(string, Unit)> suggestedValues);
    void MaxDigitsReached();
}
