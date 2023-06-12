using System;
using System.IO;
using System.Linq;
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

		public const string DefaultCASubject = "C=SU, O=MITM Proxy, OU=This is not really secure connection, CN=WebOne Certificate Authority";

		/// <summary>
		/// Create a self-signed SSL certificate and private key, and save them to PEM files.
		/// </summary>
		/// <param name="certFilename">Certificate file name.</param>
		/// <param name="keyFilename">Private Key file name.</param>
		/// <param name="certSubject">Certificate subject.</param>
		/// <param name="certHashAlgorithm">Certificate hash algorithm.</param>
		public static void MakeSelfSignedCert(string certFilename, string keyFilename, string certSubject, HashAlgorithmName certHashAlgorithm)
		{
			// PEM file headers.
			const string CRT_HEADER = "-----BEGIN CERTIFICATE-----\n";
			const string CRT_FOOTER = "\n-----END CERTIFICATE-----";

			const string KEY_HEADER = "-----BEGIN RSA PRIVATE KEY-----\n";
			const string KEY_FOOTER = "\n-----END RSA PRIVATE KEY-----";

			// Append unique ID of certificate in its CN if it's default.
			// This prevents "sec_error_bad_signature" error in Firefox.
			if (certSubject == DefaultCASubject) certSubject += " [" + new Random().NextInt64(100, 999) + "]";

			// Set up a certificate creation request.
			using RSA rsa = RSA.Create();
			CertificateRequest certRequest = new(certSubject, rsa, certHashAlgorithm, RSASignaturePadding.Pkcs1);

			// Configure the certificate as CA.
			certRequest.CertificateExtensions.Add(
				   new X509BasicConstraintsExtension(true, true, 12, true));

			// Configure the certificate for Digital Signature and Key Encipherment.
			certRequest.CertificateExtensions.Add(
				new X509KeyUsageExtension(
					X509KeyUsageFlags.KeyCertSign,
					true));

			// Issue & self-sign the certificate.
			X509Certificate2 certificate;
			switch (certHashAlgorithm.ToString())
			{
				case "SHA1":
				case "MD5":
					byte[] certSerialNumber = new byte[16];
					new Random().NextBytes(certSerialNumber);

					X500DistinguishedName certName = new(certSubject);
					RSASha1AndMd5Pkcs1SignatureGenerator customSignatureGenerator = new(rsa);
					certificate = certRequest.Create(
						certName,
						customSignatureGenerator,
						ConfigFile.SslRootValidAfter,
						ConfigFile.SslRootValidBefore,
						certSerialNumber);
					break;
				case "SHA256":
				case "SHA384":
				case "SHA512":
				default:
					certificate = certRequest.CreateSelfSigned(
						ConfigFile.SslRootValidAfter,
						ConfigFile.SslRootValidBefore);
					break;
			}

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
		/// <param name="certHashAlgorithm">Certificate hash algorithm.</param>
		/// <returns>Signed chain of SSL Certificates.</returns>
		public static X509Certificate2 MakeChainSignedCert(string certSubject, X509Certificate2 issuerCertificate, HashAlgorithmName certHashAlgorithm)
		{
			// Look if it is already issued.
			// Why: https://support.mozilla.org/en-US/kb/Certificate-contains-the-same-serial-number-as-another-certificate
			if (FakeCertificates.ContainsKey(certSubject))
			{
				X509Certificate2 CachedCertificate = FakeCertificates[certSubject];
				//check that it hasn't expired
				if (CachedCertificate.NotAfter > DateTime.Now && CachedCertificate.NotBefore < DateTime.Now)
				{ return CachedCertificate; }
				else
				{ FakeCertificates.Remove(certSubject); }
			}

			// If not, initialize private key generator & set up a certificate creation request.
			using RSA rsa = RSA.Create();
			CertificateRequest certRequest = new(certSubject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
			//CertificateRequest certRequest = new(certSubject, rsa, certHashAlgorithm, RSASignaturePadding.Pkcs1);

			// Generate an unique serial number.
			byte[] certSerialNumber = new byte[16];
			new Random().NextBytes(certSerialNumber);

			// Issue & sign the certificate.
			X509Certificate2 certificate;
			/*switch (certHashAlgorithm.ToString())
			{
				case "SHA1":
				case "MD5":
					X500DistinguishedName certName = new(certSubject);
					RSASha1AndMd5Pkcs1SignatureGenerator customSignatureGenerator = new(rsa);
					certificate = certRequest.Create(
						issuerCertificate.SubjectName,
						customSignatureGenerator,
						DateTimeOffset.Now.AddDays(ConfigFile.SslCertVaildBeforeNow),
						DateTimeOffset.Now.AddDays(ConfigFile.SslCertVaildAfterNow),
						certSerialNumber);
					break;
				case "SHA256":
				case "SHA384":
				case "SHA512":
				default:
					certificate = certRequest.Create(
						issuerCertificate,
						DateTimeOffset.Now.AddDays(ConfigFile.SslCertVaildBeforeNow),
						DateTimeOffset.Now.AddDays(ConfigFile.SslCertVaildAfterNow),
						certSerialNumber
					);
					break;
			}*/
			//strange, but on SHA1/MD5 we're got "sec_error_bad_signature", so use SHA256 temporary.
			certificate = certRequest.Create(
				issuerCertificate,
				DateTimeOffset.Now.AddDays(ConfigFile.SslCertVaildBeforeNow),
				DateTimeOffset.Now.AddDays(ConfigFile.SslCertVaildAfterNow),
				certSerialNumber
			);

			// Export the issued certificate with private key.
			X509Certificate2 certificateWithKey = new(certificate.CopyWithPrivateKey(rsa).Export(X509ContentType.Pkcs12));

			// Save the certificate and return it.
			FakeCertificates.Add(certSubject, certificateWithKey);
			return certificateWithKey;
		}
	}

	/// <summary>
	/// SHA1 and MD5 signature generator for X509 certificates.
	/// </summary>
	sealed class RSASha1AndMd5Pkcs1SignatureGenerator : X509SignatureGenerator
	{
		// Workaround for SHA1 and MD5 ban in .NET 4.7.2 and .NET Core.
		// Should not be called if use SHA256 or other modern ciphers.
		// Ideas used from:
		// https://stackoverflow.com/a/59989889/7600726
		// https://github.com/dotnet/corefx/pull/18344/files/c74f630f38b6f29142c8dc73623fdcb4f7905f87#r112066147
		// https://github.com/dotnet/corefx/blob/5fe5f9aae7b2987adc7082f90712b265bee5eefc/src/System.Security.Cryptography.X509Certificates/tests/CertificateCreation/PrivateKeyAssociationTests.cs#L531-L553
		// https://github.com/dotnet/runtime/blob/89f3a9ef41383bb409b69d1a0f0db910f3ed9a34/src/libraries/System.Security.Cryptography/tests/X509Certificates/CertificateCreation/X509Sha1SignatureGenerators.cs#LL31C38-L31C38

		private readonly X509SignatureGenerator _realRsaGenerator;

		internal RSASha1AndMd5Pkcs1SignatureGenerator(RSA rsa)
		{
			_realRsaGenerator = X509SignatureGenerator.CreateForRSA(rsa, RSASignaturePadding.Pkcs1);
		}

		protected override PublicKey BuildPublicKey() => _realRsaGenerator.PublicKey;

		/// <summary>
		/// Callback for .NET signing functions.
		/// </summary>
		/// <param name="hashAlgorithm">Hashing algorithm name.</param>
		/// <returns>Hashing algorithm ID in some correct format.</returns>
		public override byte[] GetSignatureAlgorithmIdentifier(HashAlgorithmName hashAlgorithm)
		{
			const string SHA1id = "300D06092A864886F70D0101050500";
			const string MD5id = "300D06092A864886F70D0101040500";
			//

			if (hashAlgorithm == HashAlgorithmName.SHA1)
				//return "300D06092A864886F70D0101050500".HexToByteArray();
				return HexToByteArray(SHA1id);
			if (hashAlgorithm == HashAlgorithmName.MD5)
				//The equivalent for RSA-PKCS1-MD5 is 300D06092A864886F70D0101040500.
				return HexToByteArray(MD5id);

			throw new ArgumentOutOfRangeException(nameof(hashAlgorithm), "'" + hashAlgorithm + "' is not a supported algorithm at this moment.");
		}

		/// <summary>
		/// Convert a hex-formatted string to byte array.
		/// </summary>
		/// <param name="hex">A string loogking like "300D06092A864886F70D0101050500".</param>
		/// <returns>A byte array.</returns>
		public static byte[] HexToByteArray(string hex)
		{
			//copypasted from:
			//https://social.msdn.microsoft.com/Forums/en-US/851492fa-9ddb-42d7-8d9a-13d5e12fdc70/convert-from-a-hex-string-to-a-byte-array-in-c?forum=aspgettingstarted
			return Enumerable.Range(0, hex.Length)
							 .Where(x => x % 2 == 0)
							 .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
							 .ToArray();
		}

		/// <summary>
		/// Sign specified <paramref name="data"/> using specified <paramref name="hashAlgorithm"/>.
		/// </summary>
		/// <returns>X.509 signature for specified data.</returns>
		public override byte[] SignData(byte[] data, HashAlgorithmName hashAlgorithm) =>
			_realRsaGenerator.SignData(data, hashAlgorithm);
	}
}
