using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using PlusSslComm.Old;

namespace PlusSslComm
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
            );// The server name must match the name on the server certificate.
            try
            {
                //sslStream.ReadTimeout = 50;

                sslStream.AuthenticateAsClient("TEST Server");
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

        public static PlusSslStream GetSecuredPlusSslStream(TcpClient client, string authName)
        {
            var sslStream = new PlusSslStream(
                client.GetStream(),
                false,
                new RemoteCertificateValidationCallback(ValidateServerCertificate),
                null
            );
            // The server name must match the name on the server certificate.
            try
            {
                //sslStream.ReadTimeout = 50;

                sslStream.AuthenticateAsClient("TEST Server");
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
            if (sslPolicyErrors == SslPolicyErrors.None) { return true; }

            if (sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors)
                return true;
            
            Console.WriteLine("*** SSL Error: " + sslPolicyErrors.ToString());
            return false;
        }
    }
}
