using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PlusSslComm.Old;

namespace PlusSslComm
{
    public class Program
    {
        public static int BufferSize = 32768;
        public static int SmallBufferSize = 1024;
        public static int MediumBufferSize = 2048 * 4;
        private static string certificate = @"C:\temp\certificate.crt";

        public static string CurrentEncoding { get; set; } = "Windows-1252";

        public static string FirstRequest =
            "CD01000256      060                               RD#TEST                           1  Default-PC                                T    [#  0500            M1                                                                                                    \0";

        public static string FirstRequestWithAddress =
            "DACOS4_MAIN_SSL                         CD01000256      060                               RD#TEST                           1  Default-PC                                T    [#  0500            M1                                                                                                    \0";

        public static string firstRequestWith =
            "CD01000256      060                               RD#TEST                           1  Default-PC                                T    [#  0500            M1                                                                                                    ";

        public static string s00S40RDRequest =
            "DACOS4_MAIN_SSL                         CD01                                              RDS00S24                        RD1  Default-PC                    USE001IN    T      J 0500                                                                                                                  \0";

        //public static List<Mapping> Mappings { get; set; } = new List<Mapping>();

        public static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }

        [STAThread]
        public static void Main(string[] args)
        {
            //var es = Base64Decode("cGx1c25nLXBsYXllcjpwZXJMQUJscnN0ZXI2MQ==");
            //ExamineBytes(FirstRequest, firstRequestWith);
            PreventMultipleStart(out var abort);

#if DEBUG
#else
            if (abort)
                return;
#endif
            var mode = "dispatcher";

            if (args.Length > 0)
            {
                mode = args[0];
            }

            Console.WriteLine("PlusSslComm 2023.1-beta01 started! Mode is {0}", mode);


            //Simple cases
            if (mode.ToLower() == "client")
            {
                Client("127.0.0.1", 3023, FirstRequest);
            }

            if (mode.ToLower() == "clienta")
            {
                Client("127.0.0.1", 3023, FirstRequestWithAddress);
            }

            if (mode.ToLower() == "tandem")
            {
                Client("172.16.20.101", 2067, FirstRequest);
            }

            if (mode.ToLower() == "triggerdispatcherd4")
            {
                Client("127.0.0.1", 13000, "START DACOS4_MAIN_SSL");
            }

            if (mode.ToLower() == "triggerdispatchertir")
            {
                Client("127.0.0.1", 13000, "PE_T05033_SSL");
            }

            if (mode.ToLower() == "dispatcher")
            {
                //start listener on fixed port to receive input from updater
                //if a application system was transferred, start a multisslthread
                var dispatcher = new Dispatcher();
                dispatcher.Run();
            }

            if (mode.ToLower() == "singlesslthread")
            {
                var singleSslThread = new SingleSslThread()
                {
                    LocalAddress = "127.0.0.1",
                    LocalPort = 3023,
                    RemoteAddress = "172.16.20.101",
                    RemotePort = 62067
                };
                singleSslThread.Start();
            }
        }

        private static void Client(string ip, int port, string request)
        {
            Proxy.SendNormalMessageToServer(ip, port, request, CurrentEncoding);
        }

        public static void PreventMultipleStart(out bool abort)
        {
            var currentProcess = Process.GetCurrentProcess();
            var processes = Process.GetProcessesByName(currentProcess.ProcessName);
            if (processes.Length == 1)
            {
                //only the current instance is running, so abort = false
                abort = false;
                return;
            }

            foreach (var p in processes)
            {
                if (p.Id != currentProcess.Id &&
                    p.MainModule?.ModuleName == currentProcess.MainModule?.ModuleName && 
                    p.MainModule?.FileVersionInfo.FileVersion != currentProcess.MainModule?.FileVersionInfo.FileVersion)
                {
                    //if a older version is found, close it
                    p.Kill();
                    abort = false;
                    return;
                }
            }

            //an instance of the same version is already running. No need to start another one
            abort = true;
        }
    }
}



//Ssl proxy with thread to receive remote messages. Can use multiple remotes
//if (mode.ToLower() == "multisslthread")
//{
//    var stt = new MultiSslThreads()
//    {
//        MessageContainsApplicationSystem = true
//    };
//    var mapping = Mappings.First(t => t.Name == "distrisslproxy");
//    stt.Start(mapping.FromAddress, mapping.FromPort);
//}

//TestMatch();
//var with = Encoding.GetEncoding(CurrentEncoding).GetBytes(FirstRequest);
//var without = Encoding.GetEncoding(CurrentEncoding).GetBytes(firstRequestWith);

//Mappings.Add(new Mapping()
//{
//    Name = "localtcpproxy", FromPort = 3023, ToAddress = "127.0.0.1", ToPort = 2067,
//    Encoding = CurrentEncoding
//});
//Mappings.Add(new Mapping()
//{
//    Name = "localsslproxy", FromPort = 3023, ToAddress = "127.0.0.1", ToPort = 62067, UseSsl = true,
//    Encoding = CurrentEncoding
//});
//Mappings.Add(new Mapping()
//{
//    Name = "distriproxy", FromPort = 3023, ToAddress = "172.16.20.101", ToPort = 2067, UseSsl = false,
//    Encoding = CurrentEncoding
//});
//Mappings.Add(new Mapping()
//{
//    Name = "distrisslproxy", FromPort = 3023, ToAddress = "172.16.20.101", ToPort = 62067, UseSsl = true,
//    Encoding = CurrentEncoding
//});

//private static void ExamineBytes(string firstRequest, string second)
//{
//    var a = Encoding.GetEncoding("Windows-1252").GetBytes(firstRequest);
//    var s = Encoding.GetEncoding("Windows-1252").GetBytes(second);

//    var b = a.Length.Equals(s.Length);
//}

//private static void TestMatch()
//{
//    var telConnection = "DACOS4_MAIN_1";
//    var regex = new Regex("(_[0-9]*$)");
//    var match = regex.Match(telConnection);
//    if (match.Success && match.Groups.Count > 0)
//        telConnection = telConnection.Remove(telConnection.Length - match.Value.Length, match.Value.Length);

//    Console.WriteLine(telConnection);
//}

////Tcp proxy with cyclic remote read - works good
//if (mode.ToLower() == "cycle")
//{
//    var stt = new SimpleTcpCycle();
//    stt.Start(Mappings.First(t => t.Name == "distriproxy"));
//}
////Ssl proxy with cyclic remote read
//if (mode.ToLower() == "sslcycle")
//{
//    var stt = new SimpleSslCycle();
//    stt.Start(Mappings.First(t => t.Name == "distrisslproxy"));
//}
////Ssl proxy with thread to receive remote messages
//if (mode.ToLower() == "sslthread")
//{
//    var stt = new SslThread();
//    stt.Start(Mappings.First(t => t.Name == "distrisslproxy"));
//}

//if (mode.ToLower() == "server")
//    TcpServer.RunServer(2025);
//if (mode.ToLower() == "sslserver")
//    SslTcpServer.RunServer(62067, certificate);


////Async Tcp proxy. Seems to be a good solution but hung up after the first request. Client is detached.
//if (mode.ToLower() == "simpleasync")
//{
//    Task.Run(async () =>
//    {
//        var stt = new SimpleTcpAsyncTransaction();
//        await stt.Start(Mappings.First(t => t.Name == "distriproxy"));
//    }).Wait();
//}

////Tcp proxy, works good but not perfect
//if (mode.ToLower() == "simple")
//{
//    var stt = new SimpleTcpTransaction();
//    stt.Start(Mappings.First(t => t.Name == "distriproxy"));
//}

////Both do not run
//if (mode.ToLower() == "sslcom")
//{
//    var proxy = new SslProxy
//    {
//        Mapping = Mappings.First(t => t.Name == "distrisslproxy")
//    };
//    proxy.Run();
//}
//if (mode.ToLower() == "tcpcom")
//{
//    var proxy = new TcpProxy
//    {
//        Mapping = Mappings.First(t => t.Name == "distriproxy")
//    };
//    proxy.Run();
//}


//if (mode.ToLower().Contains("proxy"))
//    Proxy.Start(Mappings.First(t => t.Name == mode.ToLower()));

//if (mode.ToLower() == "ssl2")
//{
//    var tunnel = new SshTunnel2();
//    tunnel.Open(Mappings.First(t => t.Name == "distrisslproxy"));
//}

//if (mode.ToLower() == "sar")
//{
//    SslSendAndReceive.Run(Mappings.First(t => t.Name == "distrisslproxy"));
//}


////Tcp proxy, works not as good as the simple proxy before. Hung up at the end before the client is started. No NULL byte check
//if (mode.ToLower() == "simplessl")
//{
//    var stt = new SimpleSslTransaction();
//    stt.Start(Mappings.First(t => t.Name == "distrisslproxy"));
//}