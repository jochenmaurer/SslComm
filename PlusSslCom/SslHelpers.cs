using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace PlusSslCom
{
    public static class SslHelpers
    {
        public static SslStream GetSecuredStream(TcpClient client, string authName)
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
                sslStream.AuthenticateAsClient(authName);
                sslStream.ReadTimeout = 50;
            }
            catch (AuthenticationException e)
            {
                Console.WriteLine("Exception: {0}", e.Message);
                if (e.InnerException != null)
                {
                    Console.WriteLine("Inner exception: {0}", e.InnerException.Message);
                }

                Console.WriteLine("Authentication failed - closing the connection.");
                return null;
            }

            return sslStream;
        }

        private static bool ValidateServerCertificate(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}
