using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace PlusSslComm.Old
{
    public class SslProxy : ProxyBase
    {
        protected override void HandleRequest(Mapping mapping)
        {
            //input
            using (var inputStream = mapping.Source.GetStream())
            {
                ReadBytesFromInputStream(inputStream, out var buffer, out var bytesRead);

                if (IsLoggingEnabled)
                {
                    var data = Encoding.GetEncoding(mapping.Encoding).GetString(buffer, 0, bytesRead);
                    Console.WriteLine("Incoming: {0}", data);
                }

                //output
                if (mapping.TargetSslStream == null)
                    mapping.TargetSslStream = CreateSslStreamToTarget(mapping.Target);

                mapping.TargetSslStream.Write(buffer);
                mapping.TargetSslStream.Flush();

                ReadBytesFromOutputStream(mapping.TargetSslStream, out buffer, out bytesRead);

                if (IsLoggingEnabled)
                {
                    var serverMessage = ReadMessage(buffer, bytesRead, mapping.Encoding);
                    Console.WriteLine("Outgoing: {0}", serverMessage);
                }

                inputStream.Write(buffer, 0, bytesRead);
            }
        }

        private static SslStream CreateSslStreamToTarget(TcpClient client)
        {
            var sslStream = new SslStream(
                client.GetStream(),
                false,
                new RemoteCertificateValidationCallback(ValidateServerCertificate),
                null
            );
            // The server name must match the name on the server certificate.
            try
            {
                sslStream.AuthenticateAsClient("");
            }
            catch (AuthenticationException e)
            {
                Console.WriteLine("Exception: {0}", e.Message);
                if (e.InnerException != null)
                {
                    Console.WriteLine("Inner exception: {0}", e.InnerException.Message);
                }

                Console.WriteLine("Authentication failed - closing the connection.");
            }

            return sslStream;
        }

        // The following method is invoked by the RemoteCertificateValidationDelegate.
        public static bool ValidateServerCertificate(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            //if (sslPolicyErrors == SslPolicyErrors.None) { return true; }

            //if (sslPolicyErrors.ToString() ==
            //    "SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors")
            //    return true;
            //Console.WriteLine("*** SSL Error: " + sslPolicyErrors.ToString());
            //return false;

            return true;
        }
    }
}
