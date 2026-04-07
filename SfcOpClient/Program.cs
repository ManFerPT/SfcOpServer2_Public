#pragma warning disable IDE0031, IDE0057, IDE0079
#pragma warning disable CA1416

using shrGF;
using shrNet;
using shrQ3;

//using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;

namespace SfcOpClient
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            string directoryName;

#if DEBUG
            directoryName = "D:\\Games\\Starfleet Command 2 Orion Pirates\\";
#else
            directoryName = AppContext.BaseDirectory;
#endif

            static void ShowError(string msg)
            {
                Console.Write($"ERROR: {msg}\r\n\r\n");
            }

            string t;
            int i;

            // tries to load the gf

            GFFile gf = new();

            t = Path.Combine(directoryName, "SfcOpClient.gf");

            if (!gf.Load(t))
            {
                if (File.Exists(t))
                    ShowError("'SfcOpClient.gf' is invalid!");
                else
                {
                    ShowError("'SfcOpClient.gf' was missing!\r\nCreating the file...");
                    CreateCfg(t);
                }

                goto somethingWentWrong;
            }

            // ... tries to get the gamePath

            if (!gf.TryGetValue(string.Empty, "Launch", out string fileName, out _))
                goto somethingWentWrong;

            if (!fileName.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase) || Path.IsPathFullyQualified(fileName))
                goto somethingWentWrong;

            string gamePath = Path.Combine(directoryName, fileName);

            if (!File.Exists(gamePath))
                goto somethingWentWrong;

            directoryName = Path.GetDirectoryName(gamePath);
            fileName = Path.GetFileName(gamePath);

            // checks if we need to create or update any setup files

            if (UpdateSetupFiles(directoryName, fileName) != 0)
            {
                ShowError("One of the setup files was missing!\r\nCreating new files...");

                goto somethingWentWrong;
            }

            // ... tries to get the remoteEP

            if (!gf.TryGetValue("Meta", "Ip", out string remoteIP, out _))
                goto somethingWentWrong;

            if (!gf.TryGetValue("Meta", "Port", out int remotePort))
                goto somethingWentWrong;

            t = $"{remoteIP}:{remotePort}";

            if (!IPEndPoint.TryParse(t, out IPEndPoint remoteEP))
                goto somethingWentWrong;

            // ... tries to set the resolution

            if (!gf.TryGetValue("3D", "Width", out int viewportWidth))
                goto somethingWentWrong;

            if (!gf.TryGetValue("3D", "Height", out int viewportHeight))
                goto somethingWentWrong;

            t = Path.Combine(directoryName, "Assets/Sprites/sprites.q3");

            if (!clsRes.SetResolution(t, viewportWidth, viewportHeight, null, 0.0))
                goto somethingWentWrong;

            UpdateDgVoodoo(directoryName, viewportWidth, viewportHeight);

            // ... tries to get the processor affinity

            if (!gf.TryGetValue("Cpu", "Affinity", out int processorAffinity))
                goto somethingWentWrong;

            i = (1 << Environment.ProcessorCount) - 1; // logical cores bit mask

            if (processorAffinity >= i || processorAffinity < 0)
                processorAffinity = 0;

            // displays the settings

            Console.Write($"Game filename : {gamePath}\r\nMeta address  : {remoteIP}:{remotePort}\r\n3D resolution : {viewportWidth}x{viewportHeight}\r\nCpu affinity  : {processorAffinity}\r\n\r\n");

        tryLaunchClient:

            gf.Clear();

            if (!TrySetMeta(gf, directoryName, remoteIP))
                goto somethingWentWrong;

            //if (!TrySetRegistry(gamePath, directoryName, fileName))
            //    goto somethingWentWrong;

            if (!TryLaunchClient(directoryName, remoteEP, out Client27001 client))
                goto somethingWentWrong;

        tryLaunchGame:

            Process process = null;

            TryDeleteTemporaryFiles(directoryName);

            try
            {
                // tries to kill any existing process

                Process[] processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(fileName));

                for (i = 0; i < processes.Length; i++)
                    TryKill(processes[i]);

                // starts a new process

                process = new()
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        FileName = gamePath,
                        UseShellExecute = true,
                        WorkingDirectory = directoryName
                    }
                };

                process.Start();

                if (processorAffinity > 0)
                {
                    process.PriorityClass = ProcessPriorityClass.BelowNormal;
                    process.ProcessorAffinity = processorAffinity;

                    ProcessThreadCollection threads = process.Threads;

                    for (i = 0; i < threads.Count; i++)
                    {
                        try
                        {
                            ProcessThread thread = threads[i];

                            thread.IdealProcessor = 0;
                            thread.ProcessorAffinity = processorAffinity;
                            thread.PriorityLevel = ThreadPriorityLevel.BelowNormal;
                        }
                        catch (Exception)
                        { }
                    }
                }

                Console.Write("The client started the game\n");
            }
            catch (Exception)
            {
                Console.Write("An error occurred while starting the game\n");

                if (process != null)
                {
                    TryKill(process);

                    process.Dispose();

                    process = null;
                }
            }

            while (true)
            {
                t = Console.ReadLine();

                if (string.IsNullOrEmpty(t))
                    break;

                if (t.Equals("r", StringComparison.Ordinal))
                {
                    process?.Dispose();

                    goto tryLaunchGame;
                }

                if (t.Equals("n", StringComparison.Ordinal))
                {
                    client.Dispose();
                    process?.Dispose();

                    goto tryLaunchClient;
                }

                Console.Write($"Commands:\r\n    r -> restarts the game\r\n    n -> restarts everything\r\n    ENTER -> quits\r\n");
            }

            return;

        somethingWentWrong:

            Console.Write("Press ENTER to exit.\r\n");

            Console.ReadLine();
        }

        private static void CreateCfg(string filename)
        {
            string contents = $@"
Launch = ""StarFleetOP.exe""

[Meta]
Ip = ""192.168.1.64""
Port = 27001

[3D]
Width = 1280
Height = 720

[Cpu]
Affinity = 0 // bitmask, set to 1 in windows 10
"[2..];

            File.WriteAllText(filename, contents);
        }

        private static int UpdateSetupFiles(string directoryName, string fileName)
        {
            int filesMissing = 0;

            static int Update(string path, string contents)
            {
                int filesMissing = 0;

                if (File.Exists(path))
                {
                    string text = File.ReadAllText(path);

                    if (text.Equals(contents, StringComparison.Ordinal))
                        goto skipUpdate;
                }
                else
                    filesMissing++;

                File.WriteAllText(path, contents);

            skipUpdate:

                return filesMissing;
            }

            string contents;

            // add firewall rules

            contents = $@"
set dplPath=C:\windows\syswow64
set sfcPath={directoryName}

netsh advfirewall firewall add rule name=""Microsoft DirectPlay Helper"" dir=in action=allow program=""%dplPath%\dplaysvr.exe"" profile=any
netsh advfirewall firewall add rule name=""Microsoft DirectPlay Helper"" dir=out action=allow program=""%dplPath%\dplaysvr.exe"" profile=any

netsh advfirewall firewall add rule name=""StarFleetOP"" dir=in action=allow program=""%sfcPath%\{fileName}"" profile=any
netsh advfirewall firewall add rule name=""StarFleetOP"" dir=out action=allow program=""%sfcPath%\{fileName}"" profile=any

netsh advfirewall firewall add rule name=""SfcOpClient"" dir=in action=allow program=""%sfcPath%\SfcOpClient.exe"" profile=any
netsh advfirewall firewall add rule name=""SfcOpClient"" dir=out action=allow program=""%sfcPath%\SfcOpClient.exe"" profile=any

netsh advfirewall firewall add rule name=""SfcOpServer"" dir=in action=allow program=""%sfcPath%\SfcOpServer.exe"" profile=any
netsh advfirewall firewall add rule name=""SfcOpServer"" dir=out action=allow program=""%sfcPath%\SfcOpServer.exe"" profile=any
"[2..];

            filesMissing += Update(Path.Combine(directoryName, "Add firewall rules (run as admin).bat"), contents);

            // delete firewall rules

            contents = @"
netsh advfirewall firewall delete rule name=""Microsoft DirectPlay Helper""
netsh advfirewall firewall delete rule name=""StarFleetOP""
netsh advfirewall firewall delete rule name=""SfcOpClient""
netsh advfirewall firewall delete rule name=""SfcOpServer""
"[2..];

            filesMissing += Update(Path.Combine(directoryName, "Delete firewall rules (run as admin).bat"), contents);

            // add registry entries

            contents = $@"
Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\GameSpy]
[HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\GameSpy\CDKeys]
""StarfleetCommand2""=hex:0d,1e,01,da,d5,33,51,9e,c7,e9,cc,7f,94,43,0e,20,c4,e0,de,54,a3,e8,05,c6

[HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Taldren]
[HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Taldren\Starfleet Command Orion Pirates]
""Directory""=""{directoryName}""
"[2..];

            filesMissing += Update(Path.Combine(directoryName, "Add registry entries (right-click and merge).reg"), contents);

            // delete registry entries

            contents = @"
Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\GameSpy]
[-HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Taldren]
"[2..];

            filesMissing += Update(Path.Combine(directoryName, "Delete registry entries (right-click and merge).reg"), contents);

            // returns the number of files missing

            return filesMissing;
        }

        private static void UpdateDgVoodoo(string directoryName, int viewportWidth, int viewportHeight)
        {
            string path = Path.Combine(directoryName, "dgVoodoo.conf");

            if (File.Exists(path))
            {
                string contents = File.ReadAllText(path);

                // tries to find the key we want to update

                const string key = "\r\nDesktopResolution";

                int i = contents.IndexOf(key, StringComparison.OrdinalIgnoreCase);

                if (i >= 0)
                {
                    i += key.Length;

                    Contract.Assert(contents.IndexOf(key, i, StringComparison.OrdinalIgnoreCase) < 0);

                    int j = contents.IndexOf('=', i);

                    if (j >= 0)
                    {
                        int k = contents.IndexOf("\r\n", j);

                        if (k >= 0)
                        {
                            string t = contents.Substring(j, k - j);

                            // checks if we need to update anything

                            string value = $"= {viewportWidth}x{viewportHeight}";

                            if (!t.Equals(value, StringComparison.Ordinal))
                            {
                                t = $"{contents[..j]}{value}{contents[k..]}";

                                File.WriteAllText(path, t);
                            }
                        }
                    }
                }
            }
        }

        private static bool TrySetMeta(GFFile gf, string directoryName, string remoteIP)
        {
            bool r = false;

            if (gf.Load(Path.Combine(directoryName, "sfc.ini")))
            {
                gf.AddOrUpdate("Gamespy", "motd", remoteIP + ":27002");
                gf.AddOrUpdate("Gamespy", "master", remoteIP);
                gf.AddOrUpdate("Gamespy", "key", remoteIP);
                gf.AddOrUpdate("Gamespy", "gpsp", remoteIP);
                gf.AddOrUpdate("Gamespy", "gpcm", remoteIP);

                gf.AddOrUpdate("Meta", "WONMotd", remoteIP + ":27002");

                gf.Save();
                gf.Clear();

                if (gf.Load(Path.Combine(directoryName, "Assets/Settings/Local/Multiplayer/ServerSetup.gf")))
                {
                    gf.AddOrUpdate("WONDirectoryServer", "ServerPath", "/StarFleetCommand2/Game/Release", true);
                    gf.AddOrUpdate("WONDirectoryServer/Addresses", "0", remoteIP + ":15101", true);

                    gf.Save();

                    r = true;
                }
            }

            gf.Clear();

            return r;
        }

        /*
            private static bool TrySetRegistry(string gameFilename, string directoryName, string fileName)
            {
                try
                {
                    using RegistryKey software = Registry.LocalMachine.CreateSubKey("SOFTWARE");
                    using RegistryKey software_WOW6432Node = software.CreateSubKey("WOW6432Node");

                    // cd key

                    using RegistryKey software_WOW6432Node_GameSpy = software_WOW6432Node.CreateSubKey("GameSpy");
                    using RegistryKey software_WOW6432Node_GameSpy_CDKeys = software_WOW6432Node_GameSpy.CreateSubKey("CDKeys");

                    software_WOW6432Node_GameSpy_CDKeys.SetValue("StarfleetCommand2", new byte[] { 0x0d, 0x1e, 0x01, 0xda, 0xd5, 0x33, 0x51, 0x9e, 0xc7, 0xe9, 0xcc, 0x7f, 0x94, 0x43, 0x0e, 0x20, 0xc4, 0xe0, 0xde, 0x54, 0xa3, 0xe8, 0x05, 0xc6 });

                    // game directory

                    using RegistryKey software_WOW6432Node_Taldren = software_WOW6432Node.CreateSubKey("Taldren");
                    using RegistryKey software_WOW6432Node_Taldren_StarfleetCommandOrionPirates = software_WOW6432Node_Taldren.CreateSubKey("Starfleet Command Orion Pirates");

                    software_WOW6432Node_Taldren_StarfleetCommandOrionPirates.SetValue("Directory", directoryName);

                    // windows app path

                    using RegistryKey software_WOW6432Node_Microsoft = software_WOW6432Node.CreateSubKey("Microsoft");
                    using RegistryKey software_WOW6432Node_Microsoft_Windows = software_WOW6432Node_Microsoft.CreateSubKey("Windows");
                    using RegistryKey software_WOW6432Node_Microsoft_Windows_CurrentVersion = software_WOW6432Node_Microsoft_Windows.CreateSubKey("CurrentVersion");
                    using RegistryKey software_WOW6432Node_Microsoft_Windows_CurrentVersion_AppPaths = software_WOW6432Node_Microsoft_Windows_CurrentVersion.CreateSubKey("App Paths");
                    using RegistryKey software_WOW6432Node_Microsoft_Windows_CurrentVersion_AppPaths_Executable = software_WOW6432Node_Microsoft_Windows_CurrentVersion_AppPaths.CreateSubKey(fileName);

                    software_WOW6432Node_Microsoft_Windows_CurrentVersion_AppPaths_Executable.SetValue("Path", directoryName);
                    software_WOW6432Node_Microsoft_Windows_CurrentVersion_AppPaths_Executable.SetValue(string.Empty, gameFilename);

                    return true;
                }
                catch (Exception)
                { }

                return false;
            }
        */

        private static bool TryLaunchClient(string directoryName, IPEndPoint remoteEP, out Client27001 client)
        {
            client = null;

            try
            {
                client = new(directoryName, remoteEP);

                client.StartAsync().FireAndForget();
            }
            catch (Exception)
            {
                if (client != null)
                {
                    client.Dispose();

                    client = null;
                }
            }

            return client != null;
        }

        private static void TryDeleteTemporaryFiles(string directoryName)
        {
            string filename = Path.Combine(directoryName, "_wonStarFleetCommand2motd.txt");

            if (File.Exists(filename))
                File.Delete(filename);

            filename = Path.Combine(directoryName, "_wonsysmotd.txt");

            if (File.Exists(filename))
                File.Delete(filename);

            filename = Path.Combine(directoryName, Client27001.ScriptPath);

            if (File.Exists(filename))
                File.Delete(filename);
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch (Exception)
            { }
        }
    }
}
