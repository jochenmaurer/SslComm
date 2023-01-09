using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace PlusSslComm
{
    public class Dispatcher
    {
        private const int _port = 13000;
        private readonly IPAddress _localAddress = IPAddress.Parse("127.0.0.1");
        private const string RelativeCccomGlobal = @"..\Config\CCCommGlobal.cfg";
        private const string FallbackCccomGlobal = @"C:\Program Files\PlusClient\Global\Config\CCCommGlobal.cfg";
        private readonly Dictionary<string, SingleSslThread> _openSslThreads = new Dictionary<string, SingleSslThread>();
        private readonly string _configFilePath = Path.Combine(Path.GetTempPath(), "ApplicationSystems.cfg");
        //private readonly string _configFilePath = Path.Combine(Environment.CurrentDirectory, @"..\Config\ApplicationSystems.cfg");
        private readonly List<string> _startedApplicationSystems = new List<string>();

        public Dictionary<string, ConnectionParameters> ConnectionParametersMap { get; set; }

        public Dispatcher()
        {
            var cccomGlobal = RelativeCccomGlobal;
            if (!File.Exists(cccomGlobal))
            {
                cccomGlobal = FallbackCccomGlobal;
            }

            Console.WriteLine("CCCom config taken from: {0}", cccomGlobal);

            ConnectionParametersMap = new ConfigHandling().GetConnectionParametersMap(cccomGlobal);
        }

        public void Run()
        {
            ReadConfigAndStartApplicationSystems();

            while (true)
            {
                TcpListener server = null;
                try
                {
                    server = new TcpListener(_localAddress, _port);
                    server.Start();

                    var bytes = new byte[256];

                    while (true)
                    {
                        Console.Write("Dispatcher waiting for a connection... ");

                        using (var client = server.AcceptTcpClient())
                        {
                            Console.WriteLine("Connected!");

                            var stream = client.GetStream();

                            int i;

                            while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                            {
                                var incomingMessage = Encoding.ASCII.GetString(bytes, 0, i);

                                Console.Out.WriteLine("Incoming message: {0}", incomingMessage);

                                var applicationSystem = "";
                                var action = ActionMode.None;
                                if (incomingMessage.ToUpper().StartsWith("START"))
                                {
                                    applicationSystem = incomingMessage.ToUpper().Replace("START ", "");
                                    action = ActionMode.Start;
                                }
                                else if (incomingMessage.ToUpper().StartsWith("STOP"))
                                {
                                    applicationSystem = incomingMessage.ToUpper().Replace("STOP ", "");
                                    action = ActionMode.Stop;
                                }

                                switch (action)
                                {
                                    case ActionMode.Start:
                                        StartSsl(applicationSystem);
                                        break;
                                    case ActionMode.Stop:
                                        StopSsl(applicationSystem);
                                        break;
                                }

                                var resultMessage = action == ActionMode.None ? " not recognized" : " success";
                                var msg = Encoding.ASCII.GetBytes(applicationSystem + resultMessage);

                                stream.Write(msg, 0, msg.Length);
                            }
                        }
                    }
                }
                catch (SocketException e)
                {
                    Console.WriteLine("SocketException: {0}", e);
                }
                finally
                {
                    server?.Stop();
                }
            }
        }

        private void StopSsl(string applicationSystem)
        {
            if (_openSslThreads.TryGetValue(applicationSystem, out var singleSslThread))
            {
                RemoveApplicationSystemFromConfig(applicationSystem);
                singleSslThread.Stop();
            }
        }

        private CccomConnectionInformation GetConnectionInformation(string appSystem)
        {
            if (ConnectionParametersMap.TryGetValue(appSystem, out var connectionParameters))
            {
                var result = new CccomConnectionInformation
                {
                    FromAddress = connectionParameters.ProtocolParameters["RemoteHost"].ToString(),
                    FromPort = int.Parse(
                        connectionParameters.ProtocolParameters["RemotePort"].ToString()),

                    ToAddress = connectionParameters.ProtocolParameters["SslRemoteHost"].ToString(),
                    ToPort = int.Parse(
                        connectionParameters.ProtocolParameters["SslRemotePort"].ToString()),

                    Encoding = connectionParameters.ConnectionParameter["Encoding"].ToString()
                };

                return result;
            }

            return null;
        }

        private void StartSsl(string applicationSystem)
        {
            if (_openSslThreads.ContainsKey(applicationSystem))
                return;

            if (applicationSystem.Length > 0 && GetConnectionInformation(applicationSystem) is CccomConnectionInformation connectionInformation)
            {
                var thread = new Thread(() =>
                {
                    var singleSslThread = new SingleSslThread()
                    {
                        LocalAddress = connectionInformation.FromAddress,
                        LocalPort = connectionInformation.FromPort,
                        RemoteAddress = connectionInformation.ToAddress,
                        RemotePort = connectionInformation.ToPort
                    };

                    _openSslThreads.Add(applicationSystem, singleSslThread);

                    singleSslThread.Start();

                    //let the thread of the ssl transfer come up
                    Thread.Sleep(100);
                });

                thread.Start();

                AddApplicationSystemToConfig(applicationSystem);

                Console.WriteLine("Starting single SSL thread from {0}:{1} to {2}:{3}... ", connectionInformation.FromAddress,
                    connectionInformation.FromPort, connectionInformation.ToAddress, connectionInformation.ToPort);
            }
        }

        #region Config



        private void ReadConfigAndStartApplicationSystems()
        {
            if (!File.Exists(_configFilePath))
                return;

            foreach (var line in File.ReadLines(_configFilePath))
            {
                _startedApplicationSystems.Add(line);
                StartSsl(line);
            }
        }

        private void AddApplicationSystemToConfig(string applicationSystem)
        {
            if (!_startedApplicationSystems.Contains(applicationSystem))
            {
                _startedApplicationSystems.Add(applicationSystem);
                PersistApplicationSystemConfig();
            }
        }

        private void RemoveApplicationSystemFromConfig(string applicationSystem)
        {
            if (_startedApplicationSystems.Contains(applicationSystem))
            {
                _startedApplicationSystems.Remove(applicationSystem);
                PersistApplicationSystemConfig();
            }
        }

        private void PersistApplicationSystemConfig()
        {
            if (File.Exists(_configFilePath))
                File.Delete(_configFilePath);

            File.WriteAllLines(_configFilePath, _startedApplicationSystems);
        }

        #endregion
    }

    public enum ActionMode
    {
        Start,
        Stop,
        None
    }
}
