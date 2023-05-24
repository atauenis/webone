using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace WebOne
{
	/// <summary>
	/// Utilities for TLS/SSL Certificates and Keys
	/// </summary>
	static class CertificateUtil
	{
		/// <summary>
		/// Create a SSL Certificate and Private Key in PEM format
		/// </summary>
		/// <param name="certFilename">Certificate file name</param>
		/// <param name="keyFilename">Private Key file name</param>
		public static void MakeCert(string certFilename, string keyFilename)
		{
			const string CRT_HEADER = "-----BEGIN CERTIFICATE-----\n";
			const string CRT_FOOTER = "\n-----END CERTIFICATE-----";

			const string KEY_HEADER = "-----BEGIN RSA PRIVATE KEY-----\n";
			const string KEY_FOOTER = "\n-----END RSA PRIVATE KEY-----";

			using RSA rsa = RSA.Create();
			CertificateRequest certRequest = new("C=SU, O=MITM Proxy, OU=This is not really secure connection, CN=WebOne Certificate Authority", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

			// We're just going to create a temporary certificate, that won't be valid for long
			X509Certificate2 certificate = certRequest.CreateSelfSigned(
			new DateTimeOffset(1970, 01, 01, 00, 00, 00, new TimeSpan(0)),
			new DateTimeOffset(2070, 12, 31, 23, 59, 59, new TimeSpan(0))
			);// (DateTimeOffset.Now, DateTimeOffset.Now.AddDays(7));

			// Export the private key
			string privateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey(), Base64FormattingOptions.InsertLineBreaks);
			File.WriteAllText(keyFilename, KEY_HEADER + privateKey + KEY_FOOTER);

			// Export the certificate
			byte[] exportData = certificate.Export(X509ContentType.Cert);
			string crt = Convert.ToBase64String(exportData, Base64FormattingOptions.InsertLineBreaks);
			File.WriteAllText(certFilename, CRT_HEADER + crt + CRT_FOOTER);
		}
	}
}
