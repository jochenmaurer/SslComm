using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace PlusSslCom
{
    public class Program
    {
        public static int BufferSize = 32767;
        private static string certificate = @"C:\temp\certificate.crt";

        public static string Encoding { get; set; } = "Windows-1252";

        private static string firstRequest =
            "CD01000256      060                               RD#TEST                           1  Default-PC                                T    [#  0500            M1                                                                                                    \0";

        public static List<Mapping> Mappings { get; set; } = new List<Mapping>();
        
        public static void Main(string[] args)
        {
            Mappings.Add(new Mapping() { Name = "localtcpproxy", FromPort = 3023, ToAddress = "127.0.0.1", ToPort = 2025, Encoding = Encoding });
            Mappings.Add(new Mapping() { Name = "localsslproxy", FromPort = 3023, ToAddress = "127.0.0.1", ToPort = 62067, UseSsl = true, Encoding = Encoding });
            Mappings.Add(new Mapping() { Name = "distriproxy", FromPort = 3023, ToAddress = "172.16.20.101", ToPort = 2025, UseSsl = false, Encoding = Encoding });
            Mappings.Add(new Mapping() { Name = "distrisslproxy", FromPort = 3023, ToAddress = "172.16.20.101", ToPort = 62067, UseSsl = true, Encoding = Encoding });

            if (args.Length > 0)
            {
                if (args[0].ToLower() == "server")
                    TcpServer.RunServer(2025);
                if (args[0].ToLower() == "sslserver")
                    SslTcpServer.RunServer(62067, certificate);


                if (args[0].ToLower() == "sslcom")
                {
                    var proxy = new SslProxy
                    {
                        Mapping = Mappings.First(t => t.Name == "distrisslproxy")
                    };
                    var task = proxy.Run();
                    task.Wait();
                }
                if (args[0].ToLower() == "tcpcom")
                {
                    var proxy = new TcpProxy
                    {
                        Mapping = Mappings.First(t => t.Name == "distriproxy")
                    };
                    var task = proxy.Run();
                    task.Wait();
                }
                    

                if (args[0].ToLower().Contains("proxy"))
                    Proxy.Start(Mappings.First(t => t.Name == args[0].ToLower()));

                if (args[0].ToLower() == "client")
                {
                    for (int i = 0; i < 5; i++)
                    {
                        Proxy.SendNormalMessageToServer("127.0.0.1", 3023, firstRequest, Encoding);
                        Thread.Sleep(1000);
                    }
                    
                    Console.WriteLine("\nHit enter to continue...");
                    Console.Read();
                }
            }
        }
    }
}