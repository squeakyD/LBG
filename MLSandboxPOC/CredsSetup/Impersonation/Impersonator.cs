using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace CredsSetup.Impersonation
{

    /// <summary>
    /// Impersonation of a user. Allows to execute code under another user context.
    /// Please note that the account that instantiates the Impersonator class
    /// needs to have the 'Act as part of operating system' privilege set.
    /// </summary>
    /// <remarks>	
    /// This class is based on the information in the Microsoft knowledge base
    /// article http://support.microsoft.com/default.aspx?scid=kb;en-us;Q306158
    ///
    /// </remarks>
    /// 
    class Impersonator
    { 
        #region P/Invoke.

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LogonUser(String lpszUsername, String lpszDomain, String lpszPassword,
            int dwLogonType, int dwLogonProvider, out SafeTokenHandle phToken);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int DuplicateToken(SafeTokenHandle hToken, int impersonationLevel, out SafeTokenHandle hNewToken);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool RevertToSelf();

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern bool CloseHandle(IntPtr handle);

        private enum LogonType
        {
            LOGON32_LOGON_INTERACTIVE = 2,  // This parameter causes LogonUser to create a primary token.
            LOGON32_LOGON_NETWORK = 3,
            LOGON32_LOGON_BATCH = 4,
            LOGON32_LOGON_SERVICE = 5,
            LOGON32_LOGON_UNLOCK = 7,
            LOGON32_LOGON_NETWORK_CLEARTEXT = 8, // Win2K or higher
            LOGON32_LOGON_NEW_CREDENTIALS = 9 // Win2K or higher
        };

        private enum LogonProvider
        {
            LOGON32_PROVIDER_DEFAULT = 0,
            LOGON32_PROVIDER_WINNT35 = 1,
            LOGON32_PROVIDER_WINNT40 = 2,
            LOGON32_PROVIDER_WINNT50 = 3
        };

        #endregion

        /// <summary>
        /// Does the actual impersonation.
        /// </summary>
        /// <param name="userName">The name of the user to act as.</param>
        /// <param name="domainName">The domain name of the user to act as.</param>
        /// <param name="password">The password of the user to act as.</param>
        public static void RunAsUser(string userName, string domain, string password, Action action)
        {
            if (!RevertToSelf())
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            SafeTokenHandle safeTokenHandle;

            // Get the user token for the specified user, domain, and password using the unmanaged LogonUser method.

            if (!LogonUser(userName, domain, password,
                //(int)LogonType.LOGON32_LOGON_INTERACTIVE,
                (int)LogonType.LOGON32_LOGON_SERVICE,
                (int)LogonProvider.LOGON32_PROVIDER_DEFAULT,
                out safeTokenHandle))
            {
                int ret = Marshal.GetLastWin32Error();
                Debug.WriteLine("LogonUser failed with error code : {0}", ret);
                throw new System.ComponentModel.Win32Exception(ret);
            }

            using (safeTokenHandle)
            {
                SafeTokenHandle safeTokenDuplicate;

                // Get primary token
                if (DuplicateToken(safeTokenHandle, 2, out safeTokenDuplicate) == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                using (safeTokenDuplicate)
                {
                    // Check the identity.
                    Debug.WriteLine("Before impersonation: " + WindowsIdentity.GetCurrent().Name);

                    using (new UserProfileLoader(userName, safeTokenDuplicate))
                    {
                        using (WindowsImpersonationContext impersonatedUser = WindowsIdentity.Impersonate(safeTokenDuplicate.DangerousGetHandle()))
                        {
                            Debug.WriteLine("After impersonation: " + WindowsIdentity.GetCurrent().Name);

                            action();
                        }

                        // Check the identity.
                        Debug.WriteLine("After closing the context: " + WindowsIdentity.GetCurrent().Name);
                    }
                }
            }
        }

    }
}
