﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;

namespace PlusSslCom
{
    public class Program
    {
        public static int BufferSize = 32767;
        public static int SmallBufferSize = 2048;
        private static string certificate = @"C:\temp\certificate.crt";

        public static string CurrentEncoding { get; set; } = "Windows-1252";

        private static string firstRequest =
            "CD01000256      060                               RD#TEST                           1  Default-PC                                T    [#  0500            M1                                                                                                    \0";
        private static string firstRequestWith =
            "CD01000256      060                               RD#TEST                           1  Default-PC                                T    [#  0500            M1                                                                                                    ";

        public static List<Mapping> Mappings { get; set; } = new List<Mapping>();
        
        public static void Main(string[] args)
        {
            //var with = Encoding.GetEncoding(CurrentEncoding).GetBytes(firstRequest);
            //var without = Encoding.GetEncoding(CurrentEncoding).GetBytes(firstRequestWith);

            Console.WriteLine("Application started! Mode is {0}", args[0]);

            Mappings.Add(new Mapping() { Name = "localtcpproxy", FromPort = 3023, ToAddress = "127.0.0.1", ToPort = 2025, Encoding = CurrentEncoding });
            Mappings.Add(new Mapping() { Name = "localsslproxy", FromPort = 3023, ToAddress = "127.0.0.1", ToPort = 62067, UseSsl = true, Encoding = CurrentEncoding });
            Mappings.Add(new Mapping() { Name = "distriproxy", FromPort = 3023, ToAddress = "172.16.20.101", ToPort = 2025, UseSsl = false, Encoding = CurrentEncoding });
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
                    for (var i = 0; i < 5; i++)
                    {
                        Proxy.SendNormalMessageToServer("127.0.0.1", 3023, firstRequest, CurrentEncoding);
                        Thread.Sleep(1000);
                    }

                    Console.WriteLine("\nHit enter to continue...");
                    Console.Read();
                }

                //Async Tcp proxy. Seems to be a good solution but hung up after the first request. Client is detached.
                if (args[0].ToLower() == "simpleasync")
                {
                    Task.Run(async () =>
                    {
                        var stt = new SimpleTcpAsyncTransaction();
                        await stt.Start(Mappings.First(t => t.Name == "distriproxy"));
                    }).Wait();
                }

                //Tcp proxy, works good but not perfect
                if (args[0].ToLower() == "simple")
                {
                    var stt = new SimpleTcpTransaction();
                    stt.Start(Mappings.First(t => t.Name == "distriproxy"));
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
                //Tcp proxy, works not as good as the simple proxy before. Hung up at the end before the client is started. No NULL byte check
                if (args[0].ToLower() == "simplessl")
                {
                    var stt = new SimpleSslTransaction();
                    stt.Start(Mappings.First(t => t.Name == "distrisslproxy"));
                }
                
                
                //Both do not run
                if (args[0].ToLower() == "sslcom")
                {
                    var proxy = new SslProxy
                    {
                        Mapping = Mappings.First(t => t.Name == "distrisslproxy")
                    };
                    proxy.Run();
                }
                if (args[0].ToLower() == "tcpcom")
                {
                    var proxy = new TcpProxy
                    {
                        Mapping = Mappings.First(t => t.Name == "distriproxy")
                    };
                    proxy.Run();
                }
                    

                if (args[0].ToLower().Contains("proxy"))
                    Proxy.Start(Mappings.First(t => t.Name == args[0].ToLower()));

            }
        }
    }
}