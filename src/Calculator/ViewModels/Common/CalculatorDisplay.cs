// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using CalcEngine;

namespace CalculatorApp.ViewModel.Common
{
    // Callback interface to be implemented by the CalculatorManager
    public partial class CalculatorDisplay : ICalcDisplay
    {
        //public:
        //    CalculatorDisplay();
        //    void SetCallback(Platform.WeakReference callbackReference);
        //    void SetHistoryCallback(Platform.WeakReference callbackReference);

        //private:
        //    void SetPrimaryDisplay( string& displayString,  bool isError) override;
        //    void SetIsInError(bool isError) override;
        //    void SetExpressionDisplay(
        //         List<(string, int)> const& tokens,
        //         shared_ptr<vector<IExpressionCommand>> const& commands) override;
        //    void SetMemorizedNumbers( vector<string>& memorizedNumbers) override;
        //    void OnHistoryItemAdded( uint addedItemIndex) override;
        //    void SetParenthesisNumber( uint parenthesisCount) override;
        //    void OnNoRightParenAdded() override;
        //    void MaxDigitsReached() override;
        //    void BinaryOperatorReceived() override;
        //    void MemoryItemChanged(uint indexOfMemory) override;
        //    void InputChanged() override;
        WeakReference? m_callbackReference;
        WeakReference? m_historyCallbackReference;
    };
}
