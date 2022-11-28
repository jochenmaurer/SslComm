using System;
using System.Threading;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace PlusSslCom
{
    public class SshTunnel2
    {
        public void Open(Mapping mapping)
        {
            using (var client = new SshClient(mapping.ToAddress, "pg1.p00", ""))
            {
                client.Connect();

                var port = new ForwardedPortLocal(mapping.FromAddress, (uint)mapping.FromPort, mapping.ToAddress, (uint)mapping.ToPort);
                client.AddForwardedPort(port);

                Console.WriteLine("Port forwarding from {0}:{1} to {2}:{3} prepared!", port.BoundHost, port.BoundPort, port.Host, port.Port);

                port.Exception += delegate (object sender, ExceptionEventArgs e)
                {
                    Console.WriteLine(e.Exception.ToString());
                };
                port.Start();

                while (true)
                {
                    // ... hold the port open ... //
                    Thread.Sleep(100);
                }

                port.Stop();
                client.Disconnect();
            }
        }
    }
}
