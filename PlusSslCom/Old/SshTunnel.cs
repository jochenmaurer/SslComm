using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Renci.SshNet;

namespace PlusSslCom
{
    class SshTunnel : IDisposable
    {
        private SshClient client;
        private ForwardedPortRemote port;

        public SshTunnel(ConnectionInfo connectionInfo, Mapping mapping)
        {
            try
            {
                client = new SshClient(connectionInfo);

                // using 0 for the client port to dynamically allocate it
                port = new ForwardedPortRemote(IPAddress.Parse(mapping.FromAddress), (uint)mapping.FromPort, IPAddress.Parse(mapping.ToAddress), 
                    (uint)mapping.ToPort);

                client.Connect();
                client.AddForwardedPort(port);
                port.Start();

                // HACK to get the dynamically allocated client port
                var listener = (TcpListener)typeof(ForwardedPortLocal).GetField("_listener", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(port);
                if (listener != null) LocalPort = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public int LocalPort { get; }

        public void Dispose()
        {
            port?.Dispose();
            client?.Dispose();
        }
    }
}
