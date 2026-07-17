// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Windows.Input;

namespace CalculatorApp.ViewModel.Common
{
    // Equivalent to the C++ DelegateCommand class
    public sealed class DelegateCommand : ICommand
    {
        private readonly DelegateCommandHandler _handler;
        public DelegateCommand(DelegateCommandHandler handler)
        {
            _handler = handler;
        }

        // ICommand implementation
        bool ICommand.CanExecute(object parameter)
        {
            return true;
        }

        void ICommand.Execute(object parameter)
        {
            _handler?.Invoke(parameter);
        }

        event EventHandler ICommand.CanExecuteChanged
        {
            add { }
            remove { }
        }
    }
}
