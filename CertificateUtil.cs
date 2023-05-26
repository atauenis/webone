using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using static WebOne.Program;

namespace WebOne
{
	/// <summary>
	/// Utilities for manipulating TLS/SSL Certificates and Keys.
	/// </summary>
	static class CertificateUtil
	{
		// Based on ideas from:
		// https://github.com/wheever/ProxHTTPSProxyMII/blob/master/CertTool.py#L58
		// https://github.com/rwatjen/AzureIoTDPSCertificates/blob/master/src/DPSCertificateTool/CertificateUtil.cs#L46

		const string DefaultCASubject = "C=SU, O=MITM Proxy, OU=This is not really secure connection, CN=WebOne Certificate Authority";

		/// <summary>
		/// Create a self-signed SSL certificate and private key, and save them to PEM files.
		/// </summary>
		/// <param name="certFilename">Certificate file name.</param>
		/// <param name="keyFilename">Private Key file name.</param>
		/// <param name="certSubject">Certificate subject.</param>
		public static void MakeSelfSignedCert(string certFilename, string keyFilename, string certSubject = DefaultCASubject)
		{
			const string CRT_HEADER = "-----BEGIN CERTIFICATE-----\n";
			const string CRT_FOOTER = "\n-----END CERTIFICATE-----";

			const string KEY_HEADER = "-----BEGIN RSA PRIVATE KEY-----\n";
			const string KEY_FOOTER = "\n-----END RSA PRIVATE KEY-----";

			// Set up a certificate creation request.
			using RSA rsa = RSA.Create();
			CertificateRequest certRequest = new(certSubject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

			// Configure the certificate as CA.
			certRequest.CertificateExtensions.Add(
				   new X509BasicConstraintsExtension(true, true, 12, true));

			// Configure the certificate for Digital Signature and Key Encipherment.
			certRequest.CertificateExtensions.Add(
				new X509KeyUsageExtension(
					X509KeyUsageFlags.KeyCertSign,
					true));

			// Issue & sign the certificate.
			// We're just going to create a temporary certificate, that won't be valid for long.
			X509Certificate2 certificate = certRequest.CreateSelfSigned(
				new DateTimeOffset(1970, 01, 01, 00, 00, 00, new TimeSpan(0)),
				new DateTimeOffset(2070, 12, 31, 23, 59, 59, new TimeSpan(0))
			);// (DateTimeOffset.Now, DateTimeOffset.Now.AddDays(7));

			// Export the private key.
			string privateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey(), Base64FormattingOptions.InsertLineBreaks);
			File.WriteAllText(keyFilename, KEY_HEADER + privateKey + KEY_FOOTER);

			// Export the certificate.
			byte[] exportData = certificate.Export(X509ContentType.Cert);
			string crt = Convert.ToBase64String(exportData, Base64FormattingOptions.InsertLineBreaks);
			File.WriteAllText(certFilename, CRT_HEADER + crt + CRT_FOOTER);
		}

		/// <summary>
		/// Issue a chain-signed SSL certificate with private key.
		/// </summary>
		/// <param name="certSubject">Certificate subject (domain name).</param>
		/// <param name="issuerCertificate">Authority's certificate used to sign this certificate.</param>
		/// <returns>Signed chain of SSL Certificates.</returns>
		public static X509Certificate2 MakeChainSignedCert(string certSubject, X509Certificate2 issuerCertificate)
		{
			// Look if it is already issued.
			if (FakeCertificates.ContainsKey(certSubject)) return FakeCertificates[certSubject];
			// Why: https://support.mozilla.org/en-US/kb/Certificate-contains-the-same-serial-number-as-another-certificate

			// If not, initialize private key generator & set up a certificate creation request.
			using RSA rsa = RSA.Create();
			CertificateRequest certRequest = new(certSubject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

			// Generate an unique serial number.
			//byte[] certSerialNumber = Encoding.ASCII.GetBytes(certSubject + " " + Program.Variables["WOVer"]);
			byte[] certSerialNumber = new byte[16];
			new Random().NextBytes(certSerialNumber);

			// Issue & sign the certificate.
			X509Certificate2 certificate = certRequest.Create(
				issuerCertificate,
				DateTimeOffset.Now.AddDays(-7),
				DateTimeOffset.Now.AddDays(7),
				certSerialNumber
			);

			// Export the issued certificate with private key.
			X509Certificate2 certificateWithKey = new(certificate.CopyWithPrivateKey(rsa).Export(X509ContentType.Pkcs12));

			// Save the certificate and return it.
			FakeCertificates.Add(certSubject, certificateWithKey);
			return certificateWithKey;
		}
	}
}
