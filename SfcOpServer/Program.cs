using shrGF;
using shrNet;
using shrServices;
using shrWire;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;

namespace SfcOpServer
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Contract.Requires(args != null);

            // gets the current list of IPs

            string hostName = Dns.GetHostName();
            IPHostEntry hostEntry = Dns.GetHostEntry(hostName);
            IPAddress[] hostAddressList = hostEntry.AddressList;
            List<string> addressList = [];

            string data;

            Console.Write("Address list:\r\n\r\n");

            for (int i = 0; i < hostAddressList.Length; i++)
            {
                if (hostAddressList[i].AddressFamily == AddressFamily.InterNetwork)
                {
                    data = hostAddressList[i].ToString();

                    Console.Write($"{addressList.Count}. {data}\r\n");

                    addressList.Add(data);
                }
            }

            int addressIndex;

            Console.Write("\r\nLocal address: ");

            while (true)
            {
                data = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(data))
                    return;

                if (int.TryParse(data, NumberStyles.Integer, CultureInfo.InvariantCulture, out addressIndex) && addressIndex >= 0 && addressIndex < addressList.Count)
                    break;

                Console.Write("Please enter a valid option, or press ENTER to exit: ");
            }

            Console.WriteLine();

            data = addressList[addressIndex];

            IPAddress privateIP = IPAddress.Parse(data);
            IPAddress publicIP = IPAddress.Parse(data);

            // starts the services

#if DEBUG
            string appDirectory = "D:\\Games\\Starfleet Command 2 Orion Pirates\\";
#else
            string appDirectory = AppContext.BaseDirectory;
#endif

            if (!Directory.Exists(appDirectory))
            {
                Console.Write("ERROR: directory not found!\r\n");

                return;
            }

            AssemblyName app = Assembly.GetEntryAssembly().GetName();

            string appName = app.Name + " " + app.Version.ToString();

            string[] motd =
            [
                appName + " (C) D4v1ks 2020-2026",
                "Server Credits: D4v1ks (programmer), Adam (backer), TarMinyatur (playtester)",
            ];

            string[] logo =
            [
                string.Empty,
                " __________________          _-_",
                " \\________________|)____.---'---`---.____",
                "              ||    \\----._________.----/",
                "              ||     / ,'   `---'",
                "           ___||_,--'  -._",
                "          /___          ||(-",
                "              `---._____-'"
            ];

            // ... http

            DuplexService80.Initialize(appName, motd);

            DuplexService80 service80 = new(privateIP);

            service80.Start();

            // ... won

            DuplexService15101.Initialize(publicIP);

            DuplexService15101 service15101 = new(privateIP);
            DuplexService15300 service15300 = new(privateIP);

            service15101.Start();
            service15300.Start();

            // ... gamespy

            DuplexService28900 service28900 = new(privateIP);
            DuplexService29900 service29900 = new(privateIP);
            DuplexService29901 service29901 = new(privateIP);

            service28900.Start();
            service29900.Start();
            service29901.Start();

            // selects a server

#if DEBUG
            Console.Write("You want to (r)un the new server or (d)ebug a stock server? ");

            data = Console.ReadLine();
#else
            Console.Write("Starting the new server...");

            data = "r";
#endif

            Console.WriteLine();

            // tries to launch the server

            GameServer server = null;

            if (data.Equals("r", StringComparison.Ordinal))
            {
                server = new GameServer(privateIP, 27000, appDirectory, motd, logo);

                server.Start();
            }

#if DEBUG
            else if (data.Equals("d", StringComparison.Ordinal))
            {
                // makes sure no stock server is running in the background

                KillProcess("ServerPlatform");

                // makes sure the stock server is configured with the settings we need

                GFFile gf = new();

                // ... ServerSetup.gf

                gf.Load(appDirectory + "Assets/Settings/Dedicated/Standard/ServerSetup.gf");

                if (gf.TryGetValue("CentralSwitchSetup", "CentralSwitchPort", out int port) && port != 27001)
                {
                    gf.AddOrUpdate("CentralSwitchSetup", "CentralSwitchPort", 27001);

                    gf.Save();
                }

                gf.Clear();

                // ... Chat.gf

                gf.Load(appDirectory + "Assets/Settings/Dedicated/Chat.gf");

                if (gf.TryGetValue("Server", "NickName", out string nick, out bool quotes) && !nick.Equals("A1", StringComparison.Ordinal))
                {
                    gf.AddOrUpdate("Server", "NickName", "A1", true);
                    gf.AddOrUpdate("Server", "Name", "A1", true);
                    gf.AddOrUpdate("Server", "VerboseName", "A1", true);

                    gf.Save();
                }

                gf.Clear();

                // Tries to delete any existing 'Met_Common.ini' file

                try
                {
                    File.Delete(appDirectory + "Assets/Scripts/Met_Common.ini");
                }
                catch (Exception)
                { }

                Thread.Sleep(500);

                // launches the stock server as a separated process

                ProcessStartInfo startInfo = new()
                {
                    WorkingDirectory = appDirectory,
                    FileName = appDirectory + "/ServerPlatform.exe",
                    UseShellExecute = true
                };

                Process.Start(startInfo);

                // it is assumed here that the stock server has at least run once,
                // using the SFC Launcher, to set the ServerPlatform.exe ip addresses

                WireServer mitm = new(privateIP);

                mitm.StartAsync().FireAndForget();
            }
#endif

            else
            {
                Console.Write("Invalid option!\r\n");

                goto closeServer;
            }

            // clears the entire garbage collector (why not ?)

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
            GC.WaitForPendingFinalizers();

            // waits for input

            Console.Write("Press ENTER, at any time, to exit...\r\n\r\n");

        tryReadLine:

            data = Console.ReadLine();

            if (data.Equals("r", StringComparison.Ordinal))
            {
                server.ReloadValidatedClientFiles();

                goto tryReadLine;
            }

        closeServer:

            Console.Write("Closing the server...\r\n");

            // closes the server

            server?.Dispose();

            // ... http

            service80.Dispose();

            // ... won

            service15101.Dispose();
            service15300.Dispose();

            // ... gamespy

            service28900.Dispose();
            service29900.Dispose();
            service29901.Dispose();

            // waits a little for everything to finish

            Thread.Sleep(1_000);

            Console.Write("Done.\r\n");
        }

#if DEBUG
        private static void KillProcess(string processName)
        {
            Process[] processes;

            try
            {
                processes = Process.GetProcessesByName(processName);
            }
            catch (Exception)
            {
                processes = null;
            }

            if (processes != null)
            {
                for (int i = 0; i < processes.Length; i++)
                {
                    try
                    {
                        processes[i].Kill();
                    }
                    catch (Exception)
                    { }
                }
            }
        }
#endif

    }
}
