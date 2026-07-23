// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalculatorApp.ViewModel.Common
{
    // Static helper class to provide the MakeDelegateCommandHandler functionality
    public static class CommandHelpers
    {
        // Generic method to create a command handler with weak reference to the target
        public static DelegateCommandHandler MakeDelegateCommandHandler<T>(T target, Action<T, object?> function)
            where T : class
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
}
