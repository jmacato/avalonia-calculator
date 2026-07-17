// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;

namespace CalculatorUITestFramework
{
    public sealed class WinAppDriverLocalServer : IDisposable
    {
        private const int WinAppDriverDefaultPort = 4723;
        private static readonly IPAddress WinAppDriverDefaultIp = IPAddress.Loopback;
        private static readonly Uri serviceUrl = new UriBuilder
        {
            Scheme = "http",
            Host = WinAppDriverDefaultIp.ToString(),
            Port = WinAppDriverDefaultPort,
        }.Uri;

        private readonly Process winAppDriverProcess;
        private int disposeState;

        public static Uri ServiceUrl => serviceUrl;

        public WinAppDriverLocalServer()
        {
            var driverRelativePath = @"Windows Application Driver\winappdriver.exe";
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), driverRelativePath);
            if (!File.Exists(path))
            {
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), driverRelativePath);
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "Could not find winappdriver.exe in program files. Make sure WinAppDriver is installed: https://aka.ms/winappdriver",
                    path);
            }

            var process = new Process();
            try
            {
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.FileName = path;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.OutputDataReceived += OnProcessOutputDataReceived;
                process.ErrorDataReceived += OnProcessErrorDataReceived;
                if (!process.Start())
                {
                    throw new InvalidOperationException("WinAppDriver process failed to start.");
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var pingUri = new Uri(ServiceUrl, "/status");
                using var httpClient = new HttpClient();
                using HttpResponseMessage response = httpClient
                    .GetAsync(pingUri)
                    .WaitAsync(TimeSpan.FromSeconds(10))
                    .GetAwaiter()
                    .GetResult();
                response.EnsureSuccessStatusCode();
                winAppDriverProcess = process;
            }
            catch
            {
                process.Dispose();
                throw;
            }
        }

        private void OnProcessOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            var data = e.Data?.Replace("\0", string.Empty, System.StringComparison.Ordinal);
            if (!string.IsNullOrEmpty(data))
            {
                Console.WriteLine(PrependWinAppDriverToEachLine(data));
            }
        }

        private void OnProcessErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            var data = e.Data?.Replace("\0", string.Empty, System.StringComparison.Ordinal);
            if (!string.IsNullOrEmpty(data))
            {
                Console.Error.WriteLine(PrependWinAppDriverToEachLine(data));
            }
        }

        private static string PrependWinAppDriverToEachLine(string data)
        {
            return string.Join("\r\n", data
                .ReplaceLineEndings("\n")
                .Split("\n")
                .Select(line => "WinAppDriver> " + line));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!disposing || Interlocked.Exchange(ref disposeState, 1) != 0)
            {
                return;
            }

            winAppDriverProcess.OutputDataReceived -= OnProcessOutputDataReceived;
            winAppDriverProcess.ErrorDataReceived -= OnProcessErrorDataReceived;
            try
            {
                if (!winAppDriverProcess.HasExited)
                {
                    winAppDriverProcess.Kill();
                    winAppDriverProcess.WaitForExit();
                }
            }
            catch (InvalidOperationException)
            {
                // The process exited between checking HasExited and stopping it.
            }
            finally
            {
                winAppDriverProcess.Dispose();
            }
        }
    }
}
