// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// #pragma  once

// #include  "CalcManager/CalculatorResource.h"

using CalculationManager;

namespace CalculatorApp.ViewModel.Common
{
    public partial class EngineResourceProvider : IResourceProvider
    {
        // public:
        //     EngineResourceProvider();
        //     virtual string GetCEngineString(string_view id) override;

        // private:
        Windows.ApplicationModel.Resources.ResourceLoader m_resLoader;
    };
}
