using shrNet;
using shrServices;

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SfcOpServer
{
    public partial class GameServer : GamespyGame
    {
        private readonly string _root;

        private readonly ConcurrentDictionary<int, Client27000> _clients;
        private readonly ConcurrentDictionary<int, Client27001> _launchers;

        // servers

        private readonly IrcService _ircService;

        private readonly DuplexListener _listener27000;
        private readonly DuplexListener _listener27001;

        // clients

        private readonly IrcClientStream _client6667;

        public GameServer(IPAddress localIP, int localPort, string root, string[] motd, string[] logo) : base(localIP, localPort)
        {
            root = Path.GetFullPath(root).Replace('\\', '/');

            if (!root.EndsWith('/'))
                root += '/';

            _root = root;

            _clients = new();
            _launchers = new();

            // servers

            _ircService = new(localIP, 6667, motd, logo);

            _listener27000 = new(localIP, localPort, ProcessClientAsync);
            _listener27001 = new(localIP, localPort + 1, ProcessLauncherAsync);

            // clients

            _client6667 = _ircService.CreateInternalClient();
        }

        public void Start()
        {
            InitializeData();
            InitializeCampaign();

            _ = Task.Factory.StartNew(StartAsync, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private async Task StartAsync()
        {
            Console.Write("CAMPAIGN: Starting...\n");

            Task ircServiceTask = _ircService.StartAsync();

            Task campaignTask = ProcessCampaignAsync();
            Task advertiseTask = AdvertiseAsync();

            Task listener2700Task = _listener27000.StartAsync();
            Task listener2701Task = _listener27001.StartAsync();

            Console.Write("CAMPAIGN: Started\n\n");

            await Task.WhenAny(ircServiceTask, campaignTask, advertiseTask, listener2700Task, listener2701Task);

            Dispose();
        }

        protected override void TryDispose()
        {
            base.TryDispose();

            // clients

            _ircService.CloseInternalClient(_client6667);

            // servers

            _ircService.Dispose();

            _listener27000.Dispose();
            _listener27001.Dispose();

            // data

            DisposeStack();
        }

        // clients

        private async Task ProcessClientAsync(Socket socket)
        {
            Client27000 client = null;

            try
            {
                DuplexSocket.Initialize(socket);

                client = new(socket);

                _clients.TryAdd(client.Id, client);

                await client.StartAsync();
            }
            catch (Exception)
            { }
            finally
            {
                if (client != null)
                {
                    if (_clients.ContainsKey(client.Id))
                        _logouts.Enqueue(client.Id);

                    client.Dispose();
                }
                else
                    socket.Dispose();
            }
        }

        // launchers

        private async Task ProcessLauncherAsync(Socket socket)
        {
            Client27001 launcher = null;

            try
            {
                DuplexSocket.Initialize(socket);

                launcher = new(socket);

#if DEBUG
                int address = BitConverter.ToInt32([192, 168, 1, 64]); // winXP 63, win7 69
#else
                int address = GetEndPointAddress(socket.RemoteEndPoint);
#endif

                if (_launchers.TryAdd(address, launcher))
                {
                    launcher.Address = address;

                    Console.WriteLine("LAUNCHER: " + address + " was added");

                    await launcher.StartAsync();
                }
                else
                    Console.WriteLine("LAUNCHER: " + address + " already exists!");
            }
            catch (Exception)
            { }
            finally
            {
                if (launcher != null)
                {
                    int address = launcher.Address;

                    if (_launchers.TryRemove(address, out _))
                    {
                        launcher.Address = 0;

                        // unlink attempt

                        if (_clients.TryGetValue(launcher.ClientId, out Client27000 client))
                        {
                            Contract.Assert(client.LauncherId != 0);

                            client.LauncherId = 0;
                            launcher.ClientId = 0;

                            Console.WriteLine("LINK: client " + client.Id + " closed its link to launcher " + address);
                        }

                        Console.WriteLine("LAUNCHER: " + address + " was removed");
                    }
                    else
                        Console.WriteLine("LAUNCHER: " + address + " doesn't exist!");

                    launcher.Dispose();
                }
                else
                    socket.Dispose();
            }
        }

        private static int GetEndPointAddress(EndPoint ep)
        {
            Contract.Assert(ep != null && ep.AddressFamily == AddressFamily.InterNetwork);

            // 0-1 AddressFamily
            // 2-3 Port
            // 4-7 Address 

            return Unsafe.ReadUnaligned<int>(ref MemoryMarshal.GetReference(ep.Serialize().Buffer[4..].Span));
        }

        // byte pool

        public static void Rent(int size, out byte[] b, out MemoryStream m, out BinaryWriter w, out BinaryReader r)
        {
            b = ArrayPool<byte>.Shared.Rent(size);

            m = new MemoryStream(b);
            w = new BinaryWriter(m, Encoding.UTF8, true);
            r = new BinaryReader(m, Encoding.UTF8, true);
        }

        public static void Return(byte[] b, MemoryStream m, BinaryWriter w, BinaryReader r)
        {
            r.Dispose();
            w.Dispose();
            m.Dispose();

            ArrayPool<byte>.Shared.Return(b);
        }
    }
}
