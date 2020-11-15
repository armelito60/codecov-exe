using System.Diagnostics;
using System.Text;
using System.Threading;
using Codecov.Exceptions;
using Serilog;

namespace Codecov.Terminal
{
    internal class Terminal : ITerminal
    {
        public virtual bool Exits => true;

        public virtual string Run(string command, params string[] commandArguments)
        {
            try
            {
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = command.Trim(),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    foreach (var arg in commandArguments)
                    {
                        process.StartInfo.ArgumentList.Add(arg);
                    }

                    var output = new StringBuilder();
                    var error = new StringBuilder();

                    using (var outputWaitHandle = new AutoResetEvent(false))
                    using (var errorWaitHandle = new AutoResetEvent(false))
                    {
                        process.OutputDataReceived += (sender, e) =>
                        {
                            if (e.Data == null)
                            {
                                // ReSharper disable AccessToDisposedClosure
                                outputWaitHandle.Set(); // ReSharper restore AccessToDisposedClosure
                            }
                            else
                            {
                                output.AppendLine(e.Data);
                            }
                        };

                        process.ErrorDataReceived += (sender, e) =>
                        {
                            if (e.Data == null)
                            {
                                // ReSharper disable AccessToDisposedClosure
                                errorWaitHandle.Set(); // ReSharper restore AccessToDisposedClosure
                            }
                            else
                            {
                                error.AppendLine(e.Data);
                            }
                        };

                        process.Start();

                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        const int timeout = 300000; // 5 mins
                        if (!process.WaitForExit(timeout) || !outputWaitHandle.WaitOne(timeout) || !errorWaitHandle.WaitOne(timeout))
                        {
                            throw new TerminalException("Terminal process timed out.");
                        }

                        var errorAsString = error.ToString().Trim();
                        if (!string.IsNullOrWhiteSpace(errorAsString))
                        {
                            throw new TerminalException(errorAsString);
                        }

                        return output.ToString().Trim();
                    }
                }
            }
            catch (TerminalException ex)
            {
                Log.Verbose(ex, "Exception while running the terminal.");
                return string.Empty;
            }
        }

        public string RunScript(string script)
            => Run(script, string.Empty);
    }
}
