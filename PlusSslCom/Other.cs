namespace PlusSslCom
{
    class Other
    {
        

        //public static int Main(string[] args)
        //{
        //    Mappings.Add(new Mapping(){FromPort = 5555, ToAddress = IPAddress.Parse("1.1.1.1"), ToPort = 4711});
        //    foreach (var mapping in Mappings)
        //    {
        //        var server = new TcpListener(IPAddress.Any, mapping.FromPort);
        //        server.Start();

        //    }




        //    return 0;
        //}

        //public static int Main(string[] args)
        //{
        //    string certificate = null;
        //    if (args == null || args.Length < 1)
        //    {
        //        DisplayUsage();
        //    }
        //    certificate = args[0];
        //    SslTcpServer.RunServer(certificate);
        //    return 0;
        //}

        //private static void DisplayUsage()
        //{
        //    Console.WriteLine("To start the server specify:");
        //    Console.WriteLine("serverSync certificateFile.cer");
        //    Environment.Exit(1);
        //}


        //private static void DisplayUsage()
        //{
        //    Console.WriteLine("To start the client specify:");
        //    Console.WriteLine("clientSync machineName [serverName]");
        //    Environment.Exit(1);
        //}
        //public static int Main(string[] args)
        //{
        //    string serverCertificateName = null;
        //    string machineName = null;
        //    if (args == null || args.Length < 1)
        //    {
        //        DisplayUsage();
        //    }
        //    // User can specify the machine name and server name.
        //    // Server name must match the name on the server's certificate.
        //    machineName = args[0];
        //    if (args.Length < 2)
        //    {
        //        serverCertificateName = machineName;
        //    }
        //    else
        //    {
        //        serverCertificateName = args[1];
        //    }
        //    SslTcpClient.RunClient(machineName, serverCertificateName);
        //    return 0;
        //}
    }
}
