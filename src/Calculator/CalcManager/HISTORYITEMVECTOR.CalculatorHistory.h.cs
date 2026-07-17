// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.Generic;
using CalcEngine;
using WString = string;
using wstring_view = string;

namespace CalculationManager
{
    internal struct HISTORYITEMVECTOR
    {
        public List<(WString, int)> spTokens;
        public List<IExpressionCommand> spCommands;
        public WString expression;
        public WString result;
    };
}
