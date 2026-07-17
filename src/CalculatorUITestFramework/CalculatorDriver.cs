// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.VisualStudio.TestTools.UnitTesting;

using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

using System;
using System.Threading;

namespace CalculatorUITestFramework
{
    public sealed class CalculatorDriver : IDisposable
    {
        private const string defaultAppId = "Microsoft.WindowsCalculator.Dev_8wekyb3d8bbwe!App";
        private const int NotStarted = 0;
        private const int Starting = 1;
        private const int Started = 2;
        private const int Stopping = 3;

        private static readonly CalculatorDriver instance = new();
        private WinAppDriverLocalServer server;
        private WindowsDriver<WindowsElement> calculatorSession;
        private int lifecycleState;

        public static CalculatorDriver Instance => instance;

        public WindowsDriver<WindowsElement> CalculatorSession
        {
            get
            {
                if (Volatile.Read(ref lifecycleState) != Started)
                {
                    throw new InvalidOperationException("The Calculator UI automation session has not been started.");
                }

                return Volatile.Read(ref calculatorSession);
            }
        }

        private CalculatorDriver()
        {
        }

        public void SetupCalculatorSession(TestContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            int previousState = Interlocked.CompareExchange(ref lifecycleState, Starting, NotStarted);
            if (previousState == Started)
            {
                return;
            }

            if (previousState != NotStarted)
            {
                throw new InvalidOperationException("The Calculator UI automation session is changing state.");
            }

            try
            {
                server = new WinAppDriverLocalServer();
                var options = new AppiumOptions();

                if (context.Properties.TryGetValue("AppId", out var configuredAppId) && configuredAppId is string appId)
                {
                    options.AddAdditionalCapability("app", appId);
                }
                else
                {
                    options.AddAdditionalCapability("app", defaultAppId);
                }

                options.AddAdditionalCapability("deviceName", "WindowsPC");
                calculatorSession = new WindowsDriver<WindowsElement>(WinAppDriverLocalServer.ServiceUrl, options);
                calculatorSession.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
                Assert.IsNotNull(calculatorSession);

                Volatile.Write(ref lifecycleState, Started);
            }
            catch
            {
                Interlocked.Exchange(ref calculatorSession, null)?.Dispose();
                Interlocked.Exchange(ref server, null)?.Dispose();
                Volatile.Write(ref lifecycleState, NotStarted);
                throw;
            }
        }

        public void TearDownCalculatorSession()
        {
            int previousState = Interlocked.CompareExchange(ref lifecycleState, Stopping, Started);
            if (previousState == NotStarted || previousState == Stopping)
            {
                return;
            }

            if (previousState != Started)
            {
                throw new InvalidOperationException("The Calculator UI automation session is still starting.");
            }

            try
            {
                calculatorSession?.Quit();
            }
            finally
            {
                calculatorSession?.Dispose();
                server?.Dispose();
                Volatile.Write(ref calculatorSession, null);
                Volatile.Write(ref server, null);
                Volatile.Write(ref lifecycleState, NotStarted);
            }
        }

        public void Dispose()
        {
            TearDownCalculatorSession();
        }
    }
}
