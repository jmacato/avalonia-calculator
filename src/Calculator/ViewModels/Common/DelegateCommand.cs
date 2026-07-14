// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Windows.Input;


namespace CalculatorApp.ViewModel.Common
{

    public delegate void DelegateCommandHandler(object? parameter);

    // Static helper class to provide the MakeDelegateCommandHandler functionality
    public static class CommandHelpers
    {
        // Generic method to create a command handler with weak reference to the target
        public static DelegateCommandHandler MakeDelegateCommandHandler<T>(T target, Action<T, object?> function) where T : class
        {
            WeakReference weakTarget = new WeakReference(target);

            return parameter =>
            {
                if (weakTarget.Target is T thatTarget)
                {
                    function(thatTarget, parameter);
                }
            };
        }
    }

    // Equivalent to the C++ DelegateCommand class
    public sealed class DelegateCommand : ICommand
    {
        private readonly DelegateCommandHandler _handler;
        public DelegateCommand(DelegateCommandHandler handler)
        {
            _handler = handler;
        }

        // ICommand implementation
        bool ICommand.CanExecute(object? parameter)
        {
            return true;
        }

        void ICommand.Execute(object? parameter)
        {
            _handler(parameter);
        }

        event EventHandler? ICommand.CanExecuteChanged
        {
            add { }
            remove { }
        }
    }
}
