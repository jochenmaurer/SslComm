using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PlusSslComm.Old;

namespace PlusSslComm
{
    public class Program
    {
        public static int BufferSize = 32817;
        public static int SmallBufferSize = 2048;
        public static int MediumBufferSize = 2048 * 4;
        private static string certificate = @"C:\temp\certificate.crt";

        public static string CurrentEncoding { get; set; } = "Windows-1252";

        public static string FirstRequest =
            "CD01000256      060                               RD#TEST                           1  Default-PC                                T    [#  0500            M1                                                                                                    \0";
        public static string FirstRequestWithAddress =
            "{SSL:DACOS4_MAIN_SSL};CD01000256      060                               RD#TEST                           1  Default-PC                                T    [#  0500            M1                                                                                                    \0";
        public static string firstRequestWith =
            "CD01000256      060                               RD#TEST                           1  Default-PC                                T    [#  0500            M1                                                                                                    ";
        public static string s00S40RDRequest =
            "CD01                                              RDS00S24                        RD1  Default-PC                    USE001IN    T      J 0500                                                                                                                  \0";
    
        public static List<Mapping> Mappings { get; set; } = new List<Mapping>();

        [STAThread]
        public static void Main(string[] args)
        {
            //TestMatch();
            //var with = Encoding.GetEncoding(CurrentEncoding).GetBytes(FirstRequest);
            //var without = Encoding.GetEncoding(CurrentEncoding).GetBytes(firstRequestWith);

            Console.WriteLine("Application started! Mode is {0}", args[0]);

            Mappings.Add(new Mapping() { Name = "localtcpproxy", FromPort = 3023, ToAddress = "127.0.0.1", ToPort = 2067, Encoding = CurrentEncoding });
            Mappings.Add(new Mapping() { Name = "localsslproxy", FromPort = 3023, ToAddress = "127.0.0.1", ToPort = 62067, UseSsl = true, Encoding = CurrentEncoding });
            Mappings.Add(new Mapping() { Name = "distriproxy", FromPort = 3023, ToAddress = "172.16.20.101", ToPort = 2067, UseSsl = false, Encoding = CurrentEncoding });
            Mappings.Add(new Mapping() { Name = "distrisslproxy", FromPort = 3023, ToAddress = "172.16.20.101", ToPort = 62067, UseSsl = true, Encoding = CurrentEncoding });

            if (args.Length > 0)
            {
                //Simple cases
                if (args[0].ToLower() == "server")
                    TcpServer.RunServer(2025);
                if (args[0].ToLower() == "sslserver")
                    SslTcpServer.RunServer(62067, certificate);
                if (args[0].ToLower() == "client")
                {
                 Client("127.0.0.1", 3023, FirstRequest);  
                }
                if (args[0].ToLower() == "clienta")
                {
                 Client("127.0.0.1", 3023, FirstRequestWithAddress);  
                }
                if (args[0].ToLower() == "tandem")
                {
                 Client("172.16.20.101", 2067, FirstRequest);  
                }

                
                //Tcp proxy with cyclic remote read - works good
                if (args[0].ToLower() == "cycle")
                {
                    var stt = new SimpleTcpCycle();
                    stt.Start(Mappings.First(t => t.Name == "distriproxy"));
                }
                //Ssl proxy with cyclic remote read
                if (args[0].ToLower() == "sslcycle")
                {
                    var stt = new SimpleSslCycle();
                    stt.Start(Mappings.First(t => t.Name == "distrisslproxy"));
                }
                //Ssl proxy with thread to receive remote messages
                if (args[0].ToLower() == "sslthread")
                {
                    var stt = new SslThread();
                    stt.Start(Mappings.First(t => t.Name == "distrisslproxy"));
                }
                //Ssl proxy with thread to receive remote messages. Can use multiple remotes
                if (args[0].ToLower() == "multisslthread")
                {
                    var stt = new MultiSslThreads();
                    var mapping = Mappings.First(t => t.Name == "distrisslproxy");
                    stt.Start(mapping.FromAddress, mapping.FromPort);
                }
            }
        }

        private static void TestMatch()
        {
            var telConnection = "DACOS4_MAIN_1";
            var regex = new Regex("(_[0-9]*$)");
            var match = regex.Match(telConnection);
            if (match.Success && match.Groups.Count > 0)
                telConnection = telConnection.Remove(telConnection.Length - match.Value.Length, match.Value.Length);

            Console.WriteLine(telConnection);
        }

        private static void Client(string ip, int port, string request)
        {
            var rounds = new Dictionary<int, long>();
            var start = DateTime.Now.Ticks;
            for (var i = 0; i < 5; i++)
            {
                var innerStart = DateTime.Now.Ticks;
                Proxy.SendNormalMessageToServer(ip, port, request, CurrentEncoding, 5);
                rounds.Add(i, DateTime.Now.Ticks - innerStart);
            }

            var end = DateTime.Now.Ticks;

            foreach (var round in rounds)
            {
                Console.WriteLine("Round {0}: duration {1}", round.Key, round.Value);
            }

            Console.WriteLine("Total duration {0}", end - start);

            Console.WriteLine("\nHit enter to continue...");
            Console.Read();
        }
    }
}

////Async Tcp proxy. Seems to be a good solution but hung up after the first request. Client is detached.
//if (args[0].ToLower() == "simpleasync")
//{
//    Task.Run(async () =>
//    {
//        var stt = new SimpleTcpAsyncTransaction();
//        await stt.Start(Mappings.First(t => t.Name == "distriproxy"));
//    }).Wait();
//}

////Tcp proxy, works good but not perfect
//if (args[0].ToLower() == "simple")
//{
//    var stt = new SimpleTcpTransaction();
//    stt.Start(Mappings.First(t => t.Name == "distriproxy"));
//}

////Both do not run
//if (args[0].ToLower() == "sslcom")
//{
//    var proxy = new SslProxy
//    {
//        Mapping = Mappings.First(t => t.Name == "distrisslproxy")
//    };
//    proxy.Run();
//}
//if (args[0].ToLower() == "tcpcom")
//{
//    var proxy = new TcpProxy
//    {
//        Mapping = Mappings.First(t => t.Name == "distriproxy")
//    };
//    proxy.Run();
//}
                    

//if (args[0].ToLower().Contains("proxy"))
//    Proxy.Start(Mappings.First(t => t.Name == args[0].ToLower()));

//if (args[0].ToLower() == "ssl2")
//{
//    var tunnel = new SshTunnel2();
//    tunnel.Open(Mappings.First(t => t.Name == "distrisslproxy"));
//}

//if (args[0].ToLower() == "sar")
//{
//    SslSendAndReceive.Run(Mappings.First(t => t.Name == "distrisslproxy"));
//}

    
////Tcp proxy, works not as good as the simple proxy before. Hung up at the end before the client is started. No NULL byte check
//if (args[0].ToLower() == "simplessl")
//{
//    var stt = new SimpleSslTransaction();
//    stt.Start(Mappings.First(t => t.Name == "distrisslproxy"));
//}