using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace WindowsTimeManager.Core
{
    public enum NetJoinStatus
    {
        NetSetupUnknownStatus = 0,
        NetSetupUnjoined,
        NetSetupWorkgroupName,
        NetSetupDomainName
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEMTIME
    {
        public ushort wYear;
        public ushort wMonth;
        public ushort wDayOfWeek;
        public ushort wDay;
        public ushort wHour;
        public ushort wMinute;
        public ushort wSecond;
        public ushort wMilliseconds;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TOKEN_PRIVILEGES
    {
        public uint PrivilegeCount;
        public LUID Luid;
        public uint Attributes;
    }

    /// <summary>
    /// Safe P/Invoke wrappers for Win32 security tokens, system clock modification,
    /// and Active Directory domain discovery.
    /// </summary>
    public static class NativeMethods
    {
        private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const uint TOKEN_QUERY = 0x0008;
        private const uint SE_PRIVILEGE_ENABLED = 0x00000002;
        private const int ERROR_NOT_ALL_ASSIGNED = 1300;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetSystemTime(ref SYSTEMTIME lpSystemTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetLocalTime(ref SYSTEMTIME lpSystemTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern void GetSystemTime(out SYSTEMTIME lpSystemTime);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, ref IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, ref LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool AdjustTokenPrivileges(
            IntPtr TokenHandle,
            bool DisableAllPrivileges,
            ref TOKEN_PRIVILEGES NewState,
            uint BufferLength,
            IntPtr PreviousState,
            IntPtr ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int NetGetJoinInformation(string server, out IntPtr nameBuffer, out NetJoinStatus bufferType);

        [DllImport("netapi32.dll")]
        public static extern int NetApiBufferFree(IntPtr buffer);

        /// <summary>
        /// Enables the specified privilege in the current process access token.
        /// Strictly validates both the return code and Marshal.GetLastWin32Error()
        /// to ensure ERROR_NOT_ALL_ASSIGNED (1300) is detected and reported.
        /// </summary>
        public static bool EnablePrivilege(string privilegeName, out string errorMessage)
        {
            errorMessage = null;
            IntPtr hToken = IntPtr.Zero;
            try
            {
                if (!OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, ref hToken))
                {
                    int err = Marshal.GetLastWin32Error();
                    errorMessage = string.Format("Failed to open process token (Win32 Error {0}: {1})", err, new Win32Exception(err).Message);
                    return false;
                }

                LUID luid = new LUID();
                if (!LookupPrivilegeValue(null, privilegeName, ref luid))
                {
                    int err = Marshal.GetLastWin32Error();
                    errorMessage = string.Format("Privilege '{0}' not found on system (Win32 Error {1})", privilegeName, err);
                    return false;
                }

                TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES
                {
                    PrivilegeCount = 1,
                    Luid = luid,
                    Attributes = SE_PRIVILEGE_ENABLED
                };

                // Clear previous error state before API call
                Marshal.GetLastWin32Error();

                bool adjusted = AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
                int lastWin32Error = Marshal.GetLastWin32Error();

                if (!adjusted || lastWin32Error != 0)
                {
                    if (lastWin32Error == ERROR_NOT_ALL_ASSIGNED)
                    {
                        errorMessage = string.Format("Process token does not possess privilege '{0}'. Administrative elevation is required.", privilegeName);
                    }
                    else
                    {
                        errorMessage = string.Format("AdjustTokenPrivileges failed (Win32 Error {0}: {1})",
                            lastWin32Error, new Win32Exception(lastWin32Error).Message);
                    }
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
            finally
            {
                if (hToken != IntPtr.Zero)
                    CloseHandle(hToken);
            }
        }

        /// <summary>
        /// Sets the system clock with sub-second (millisecond) precision using SetSystemTime.
        /// </summary>
        public static bool SetSystemClockUtc(DateTime utcTime, out string errorMessage)
        {
            errorMessage = null;

            string privErr;
            if (!EnablePrivilege("SeSystemtimePrivilege", out privErr))
            {
                errorMessage = "Cannot acquire SeSystemtimePrivilege: " + privErr;
                return false;
            }

            SYSTEMTIME st = new SYSTEMTIME
            {
                wYear = (ushort)utcTime.Year,
                wMonth = (ushort)utcTime.Month,
                wDayOfWeek = (ushort)utcTime.DayOfWeek,
                wDay = (ushort)utcTime.Day,
                wHour = (ushort)utcTime.Hour,
                wMinute = (ushort)utcTime.Minute,
                wSecond = (ushort)utcTime.Second,
                wMilliseconds = (ushort)utcTime.Millisecond
            };

            if (!SetSystemTime(ref st))
            {
                int err = Marshal.GetLastWin32Error();
                errorMessage = string.Format("SetSystemTime failed (Win32 Error {0}: {1})", err, new Win32Exception(err).Message);
                return false;
            }

            return true;
        }

        public static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity id = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(id);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks whether the local system is joined to an Active Directory domain.
        /// </summary>
        public static bool IsDomainJoined(out string domainOrWorkgroupName)
        {
            domainOrWorkgroupName = string.Empty;
            IntPtr pDomain = IntPtr.Zero;
            try
            {
                NetJoinStatus status;
                int ret = NetGetJoinInformation(null, out pDomain, out status);
                if (ret == 0 && pDomain != IntPtr.Zero)
                {
                    domainOrWorkgroupName = Marshal.PtrToStringUni(pDomain);
                    return status == NetJoinStatus.NetSetupDomainName;
                }
            }
            catch { }
            finally
            {
                if (pDomain != IntPtr.Zero)
                    NetApiBufferFree(pDomain);
            }
            return false;
        }
    }
}
