using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CredsSetup.Impersonation
{
    public sealed class UserProfileLoader : IDisposable
    {
        private readonly SafeTokenHandle _token;

        [StructLayout(LayoutKind.Sequential)]
        private struct ProfileInfo
        {
            public int dwSize;
            public int dwFlags;
            public string lpUserName;
            public string lpProfilePath;
            public string lpDefaultPath;
            public string lpServerName;
            public string lpPolicyPath;
            public IntPtr hProfile;
        }

        [DllImport("userenv.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool LoadUserProfile(IntPtr hToken, ref ProfileInfo lpProfileInfo);

        [DllImport("Userenv.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool UnloadUserProfile(IntPtr hToken, IntPtr lpProfileInfo);

        private ProfileInfo _profileInfo = new ProfileInfo();

        public UserProfileLoader(string userName, SafeTokenHandle token)
        {
            _token = token;
            _profileInfo.dwSize = Marshal.SizeOf(_profileInfo);
            _profileInfo.lpUserName = userName;
            _profileInfo.dwFlags = 1;

            bool loadSuccess = LoadUserProfile(_token.DangerousGetHandle(), ref _profileInfo);

            if (!loadSuccess || _profileInfo.hProfile == IntPtr.Zero)
            {
                Debug.WriteLine("LoadUserProfile() failed with error code: " + Marshal.GetLastWin32Error());
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        public void Dispose()
        {
            if (!UnloadUserProfile(_token.DangerousGetHandle(), _profileInfo.hProfile))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
    }
}