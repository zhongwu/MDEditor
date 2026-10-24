using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using MDEditor.Core.Interfaces;

namespace MDEditor.Core.Services;

/// <summary>
/// <see cref="ITokenStore"/> backed by Windows Credential Manager (generic credentials).
/// Secrets are encrypted per-user by Windows and are visible/revocable by the user under
/// Control Panel > Credential Manager > Windows Credentials as "&lt;prefix&gt;&lt;key&gt;".
/// </summary>
public class CredentialManagerTokenStore : ITokenStore
{
    private const int CRED_TYPE_GENERIC = 1;
    private const int CRED_PERSIST_LOCAL_MACHINE = 2;
    private const int ERROR_NOT_FOUND = 1168;

    private readonly string _targetPrefix;

    public CredentialManagerTokenStore() : this("MDEditor:")
    {
    }

    /// <param name="targetPrefix">Prefix for credential target names (tests use a unique one).</param>
    public CredentialManagerTokenStore(string targetPrefix)
    {
        _targetPrefix = targetPrefix;
    }

    public void Save(string key, string secret)
    {
        var blob = Encoding.UTF8.GetBytes(secret);
        var blobPtr = Marshal.AllocHGlobal(blob.Length);
        try
        {
            Marshal.Copy(blob, 0, blobPtr, blob.Length);
            var credential = new CREDENTIAL
            {
                Type = CRED_TYPE_GENERIC,
                TargetName = _targetPrefix + key,
                CredentialBlobSize = blob.Length,
                CredentialBlob = blobPtr,
                Persist = CRED_PERSIST_LOCAL_MACHINE,
                UserName = key,
            };

            if (!CredWrite(ref credential, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to save credential.");
        }
        finally
        {
            // Don't leave the plaintext sitting in unmanaged memory.
            Marshal.Copy(new byte[blob.Length], 0, blobPtr, blob.Length);
            Marshal.FreeHGlobal(blobPtr);
            Array.Clear(blob);
        }
    }

    public string? Get(string key)
    {
        if (!CredRead(_targetPrefix + key, CRED_TYPE_GENERIC, 0, out var credentialPtr))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ERROR_NOT_FOUND)
                return null;
            throw new Win32Exception(error, "Failed to read credential.");
        }

        try
        {
            var credential = Marshal.PtrToStructure<CREDENTIAL>(credentialPtr);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
                return string.Empty;

            var blob = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, blob, 0, blob.Length);
            return Encoding.UTF8.GetString(blob);
        }
        finally
        {
            CredFree(credentialPtr);
        }
    }

    public void Delete(string key)
    {
        if (!CredDelete(_targetPrefix + key, CRED_TYPE_GENERIC, 0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != ERROR_NOT_FOUND)
                throw new Win32Exception(error, "Failed to delete credential.");
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public int Flags;
        public int Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref CREDENTIAL credential, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, int type, int flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}
