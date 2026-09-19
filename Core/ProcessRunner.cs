using System;
using System.Diagnostics;
using System.Text;

namespace WindowsTimeManager.Core
{
    public class ProcessResult
    {
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public string StdOut { get; set; }
        public string StdErr { get; set; }
        public bool TimedOut { get; set; }
        public long ExecutionTimeMs { get; set; }
        public string ErrorMessage { get; set; }

        public string CombinedOutput
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                if (!string.IsNullOrEmpty(StdOut)) sb.AppendLine(StdOut.Trim());
                if (!string.IsNullOrEmpty(StdErr)) sb.AppendLine(StdErr.Trim());
                if (!string.IsNullOrEmpty(ErrorMessage)) sb.AppendLine(ErrorMessage.Trim());
                return sb.ToString().Trim();
            }
        }
    }

    /// <summary>
    /// Safe execution of external native Windows executables (w32tm.exe, sc.exe, net.exe).
    /// Eliminates false-success states by strictly validating process termination,
    /// capturing exit codes, reading standard output/error, and enforcing timeouts.
    /// </summary>
    public static class ProcessRunner
    {
        public static ProcessResult Execute(string fileName, string arguments, int timeoutMs = 10000)
        {
            ProcessResult result = new ProcessResult
            {
                Success = false,
                ExitCode = -1,
                StdOut = string.Empty,
                StdErr = string.Empty,
                TimedOut = false
            };

            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(fileName, arguments)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using (Process p = new Process { StartInfo = psi })
                {
                    StringBuilder stdoutBuilder = new StringBuilder();
                    StringBuilder stderrBuilder = new StringBuilder();

                    p.OutputDataReceived += (s, e) => { if (e.Data != null) stdoutBuilder.AppendLine(e.Data); };
                    p.ErrorDataReceived += (s, e) => { if (e.Data != null) stderrBuilder.AppendLine(e.Data); };

                    if (!p.Start())
                    {
                        result.ErrorMessage = "Failed to launch process: " + fileName;
                        return result;
                    }

                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();

                    if (!p.WaitForExit(timeoutMs))
                    {
                        result.TimedOut = true;
                        try { p.Kill(); } catch { }
                        result.ErrorMessage = string.Format("Process timed out after {0} ms: {1} {2}", timeoutMs, fileName, arguments);
                        return result;
                    }

                    // Wait for async reader threads to finish flushing
                    p.WaitForExit();

                    sw.Stop();
                    result.ExecutionTimeMs = sw.ElapsedMilliseconds;
                    result.ExitCode = p.ExitCode;
                    result.StdOut = stdoutBuilder.ToString();
                    result.StdErr = stderrBuilder.ToString();

                    if (p.ExitCode == 0)
                    {
                        result.Success = true;
                    }
                    else
                    {
                        result.ErrorMessage = string.Format("Command returned non-zero exit code: {0}. {1}",
                            p.ExitCode, result.CombinedOutput);
                    }

                    return result;
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                result.ExecutionTimeMs = sw.ElapsedMilliseconds;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }
    }
}
