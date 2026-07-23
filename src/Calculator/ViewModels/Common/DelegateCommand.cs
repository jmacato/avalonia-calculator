// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Windows.Input;

namespace CalculatorApp.ViewModel.Common
{
    // Equivalent to the C++ DelegateCommand class
    public sealed class DelegateCommand(DelegateCommandHandler handler) : ICommand
    {
        // ICommand implementation
        bool ICommand.CanExecute(object? parameter)
        {
            return true;
        }

        void ICommand.Execute(object? parameter)
        {
            handler(parameter);
        }

        event EventHandler? ICommand.CanExecuteChanged
        {
            add
            {
            }

            remove
            {
            }
        }
    }
}
