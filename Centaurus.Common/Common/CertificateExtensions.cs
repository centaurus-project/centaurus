using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Centaurus
{
    public static class CertificateExtensions
    {
        public static X509Certificate2 GetSertificate(string certFile, string privateKeyFile)
        {
            if (string.IsNullOrWhiteSpace(certFile))
                throw new ArgumentNullException(nameof(certFile));

            if (!File.Exists(certFile))
                throw new FileNotFoundException("Certificate file is not found", certFile);

            var rsa = default(RSA);
            if (!string.IsNullOrWhiteSpace(privateKeyFile))
            {
                if (!File.Exists(privateKeyFile))
                    throw new FileNotFoundException("Private key file is not found", privateKeyFile);
                var rawPk = File.ReadAllText(privateKeyFile).Trim();

                rawPk = rawPk.Substring(rawPk.IndexOf('\n') + 1); //remove BEGIN header
                rawPk = rawPk.Substring(0, rawPk.LastIndexOf('\n')); //remove END footer

                rsa = RSA.Create();
                rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(rawPk), out _);

            }
            var cert = new X509Certificate2(certFile);
            if (rsa == null)
                return cert;
            return cert.CopyWithPrivateKey(rsa);
        }
    }
}
