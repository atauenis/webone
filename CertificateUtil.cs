using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using static WebOne.Program;

namespace WebOne
{
	/// <summary>
	/// Utilities for manipulating TLS/SSL Certificates and their Keys.
	/// </summary>
	static class CertificateUtil
	{
		// Based on ideas from:
		// https://github.com/wheever/ProxHTTPSProxyMII/blob/master/CertTool.py#L58
		// https://github.com/rwatjen/AzureIoTDPSCertificates/blob/master/src/DPSCertificateTool/CertificateUtil.cs#L46
		// https://blog.rassie.dk/2018/04/creating-an-x-509-certificate-chain-in-c/

		public const string DefaultCASubject = "CN=WebOne Certificate Authority %number%,OU=This is not really secure connection,O=MITM Proxy,C=SU";

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
			var RandomNumber = new Random().NextInt64(100, 999);
			if (certSubject == DefaultCASubject) certSubject = certSubject.Replace("%number%", RandomNumber.ToString());
			X500DistinguishedName certName = new(certSubject);

			// Set up a certificate creation request.
			using RSA rsa = RSA.Create();
			CertificateRequest certRequest = new(certName, rsa, certHashAlgorithm, RSASignaturePadding.Pkcs1);

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
			byte[] certSerialNumber = BitConverter.GetBytes(RandomNumber);
			//byte[] certSerialNumber = new byte[16];
			//new Random().NextBytes(certSerialNumber);

			RsaPkcs1SignatureGenerator customSignatureGenerator = new(rsa);
			certificate = certRequest.Create(
				certName,
				customSignatureGenerator,
				ConfigFile.SslRootValidAfter,
				ConfigFile.SslRootValidBefore,
				certSerialNumber);
			/*
			//try .NET Signature Generator without MD5 / SHA1 support
			certificate = certRequest.CreateSelfSigned(
				ConfigFile.SslRootValidAfter,
				ConfigFile.SslRootValidBefore);
			*/

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

			// Look certificate in disk cache or create using external utility
			if (!string.IsNullOrWhiteSpace(ConfigFile.SslSiteCerts) || !string.IsNullOrWhiteSpace(ConfigFile.SslSiteCertGenerator))
			{
				Dictionary<string, string> CertAndKeyFileNameDic = new();
				CertAndKeyFileNameDic.Add("Subject", certSubject);
				string CertAndKeyName = ExpandMaskedVariables(ConfigFile.SslSiteCerts, CertAndKeyFileNameDic);
				string CertCRTFile = CertAndKeyName + ".crt";
				string CertKEYFile = CertAndKeyName + ".key";

				// Look in disk certificate cache
				if (!string.IsNullOrWhiteSpace(ConfigFile.SslSiteCerts))
				{
					if (File.Exists(CertCRTFile) && File.Exists(CertKEYFile))
					{
#if DEBUG
						Log.WriteLine(" Cached certificate: {0} & .key", CertCRTFile);
#endif
						return X509Certificate2.CreateFromPemFile(CertCRTFile, CertKEYFile);
					}
				}

				// Run external generator if available
				if (!string.IsNullOrWhiteSpace(ConfigFile.SslSiteCertGenerator))
				{
					string SslUtilApp = ConfigFile.SslSiteCertGenerator.Substring(0, ConfigFile.SslSiteCertGenerator.IndexOf(" "));
					string SslUtilArgs = ConfigFile.SslSiteCertGenerator.Substring(ConfigFile.SslSiteCertGenerator.IndexOf(" "));
					SslUtilArgs = ExpandMaskedVariables(SslUtilArgs, CertAndKeyFileNameDic);

#if DEBUG
					Log.WriteLine(" External certificate utility: {0} {1}", SslUtilApp, SslUtilArgs);
#endif
					if (!string.IsNullOrWhiteSpace(SslUtilApp) && !string.IsNullOrWhiteSpace(SslUtilArgs))
					{
						ProcessStartInfo SslUtilStartInfo = new();
						SslUtilStartInfo.FileName = SslUtilApp;
						SslUtilStartInfo.Arguments = SslUtilArgs;
						Process SslUtilProc = Process.Start(SslUtilStartInfo);
						SslUtilProc.WaitForExit();

						if (File.Exists(CertCRTFile) && File.Exists(CertKEYFile))
						{
#if DEBUG
							Log.WriteLine(" External certificate: {0} & .key", CertCRTFile);
#endif
							return X509Certificate2.CreateFromPemFile(CertCRTFile, CertKEYFile);
						}
						else
						{
							Log.WriteLine(" No certificate and/or private key file were produced: {0} (.key)!", CertCRTFile);
						}
					}

					// Look in disk certificate cache (again)
					if (string.IsNullOrWhiteSpace(ConfigFile.SslSiteCerts))
					{
						if (File.Exists(CertCRTFile) && File.Exists(CertKEYFile))
						{
#if DEBUG
							Log.WriteLine("External certificate: {0} & .key", CertCRTFile);
#endif
							return X509Certificate2.CreateFromPemFile(CertCRTFile, CertKEYFile);
						}
					}
				}
			}

			// If not found, initialize private key generator & set up a certificate creation request.
			using RSA rsa = RSA.Create();

			// Generate an unique serial number.
			byte[] certSerialNumber = new byte[16];
			new Random().NextBytes(certSerialNumber);

			// Issue & sign the certificate.
			X509Certificate2 certificate;
			// set up a certificate creation request.
			CertificateRequest certRequestAny = new(certSubject, rsa, certHashAlgorithm, RSASignaturePadding.Pkcs1);
			RsaPkcs1SignatureGenerator customSignatureGenerator = new(RootCertificate.GetRSAPrivateKey());
			SubjectAlternativeNameBuilder sanBuilder = new();
			sanBuilder.AddDnsName(certSubject.Replace("CN=",""));
			X509Extension sanExtension = sanBuilder.Build();
			certRequestAny.CertificateExtensions.Add(sanExtension);
			certificate = certRequestAny.Create(
				issuerCertificate.IssuerName,
				customSignatureGenerator,
				DateTimeOffset.Now.AddDays(ConfigFile.SslCertVaildBeforeNow),
				DateTimeOffset.Now.AddDays(ConfigFile.SslCertVaildAfterNow),
				certSerialNumber);
			/*
			// set up a certificate creation request.
			// using .NET signature generator & SHA256 only.
			CertificateRequest certRequestSha256 = new(certSubject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
			certificate = certRequestSha256.Create(
				issuerCertificate,
				DateTimeOffset.Now.AddDays(ConfigFile.SslCertVaildBeforeNow),
				DateTimeOffset.Now.AddDays(ConfigFile.SslCertVaildAfterNow),
				certSerialNumber
			);
			*/

			// Export the issued certificate with private key.
			X509Certificate2 certificateWithKey = new(certificate.CopyWithPrivateKey(rsa).Export(X509ContentType.Pkcs12));
			/*
			//save to file for debug purposes
			const string CRT_HEADER = "-----BEGIN CERTIFICATE-----\n";
			const string CRT_FOOTER = "\n-----END CERTIFICATE-----";

			// Export the certificate.
			byte[] exportData = certificate.Export(X509ContentType.Cert);
			string crt = Convert.ToBase64String(exportData, Base64FormattingOptions.InsertLineBreaks);
			File.WriteAllText(certSubject + ".crt", CRT_HEADER + crt + CRT_FOOTER);
			*/

			// Save the certificate and return it.
			if (!FakeCertificates.ContainsKey(certSubject))
				FakeCertificates.Add(certSubject, certificateWithKey);
			return certificateWithKey;
		}
	}

	/// <summary>
	/// RSA-MD5, RSA-SHA1, RSA-SHA256, RSA-SHA512 signature generator for X509 certificates.
	/// </summary>
	sealed class RsaPkcs1SignatureGenerator : X509SignatureGenerator
	{
		// Workaround for SHA1 and MD5 ban in .NET 4.7.2 and .NET Core.
		// Ideas used from:
		// https://stackoverflow.com/a/59989889/7600726
		// https://github.com/dotnet/corefx/pull/18344/files/c74f630f38b6f29142c8dc73623fdcb4f7905f87#r112066147
		// https://github.com/dotnet/corefx/blob/5fe5f9aae7b2987adc7082f90712b265bee5eefc/src/System.Security.Cryptography.X509Certificates/tests/CertificateCreation/PrivateKeyAssociationTests.cs#L531-L553
		// https://github.com/dotnet/runtime/blob/89f3a9ef41383bb409b69d1a0f0db910f3ed9a34/src/libraries/System.Security.Cryptography/tests/X509Certificates/CertificateCreation/X509Sha1SignatureGenerators.cs#LL31C38-L31C38

		private readonly X509SignatureGenerator _realRsaGenerator;

		internal RsaPkcs1SignatureGenerator(RSA rsa)
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
			/*
			 * https://bugzilla.mozilla.org/show_bug.cgi?id=1064636#c28
				300d06092a864886f70d0101020500  :md2WithRSAEncryption           1
				300b06092a864886f70d01010b      :sha256WithRSAEncryption        2
				300b06092a864886f70d010105      :sha1WithRSAEncryption          1
				300d06092a864886f70d01010c0500  :sha384WithRSAEncryption        20
				300a06082a8648ce3d040303        :ecdsa-with-SHA384              20
				300a06082a8648ce3d040302        :ecdsa-with-SHA256              97
				300d06092a864886f70d0101040500  :md5WithRSAEncryption           6512
				300d06092a864886f70d01010d0500  :sha512WithRSAEncryption        7715
				300d06092a864886f70d01010b0500  :sha256WithRSAEncryption        483338
				300d06092a864886f70d0101050500  :sha1WithRSAEncryption          4498605
			 */
			const string MD5id = "300D06092A864886F70D0101040500";
			const string SHA1id = "300D06092A864886F70D0101050500";
			const string SHA256id = "300D06092A864886F70D01010B0500";
			const string SHA384id = "300D06092A864886F70D01010C0500"; //?
			const string SHA512id = "300D06092A864886F70D01010D0500";

			if (hashAlgorithm == HashAlgorithmName.MD5)
				return HexToByteArray(MD5id);
			if (hashAlgorithm == HashAlgorithmName.SHA1)
				return HexToByteArray(SHA1id);
			if (hashAlgorithm == HashAlgorithmName.SHA256)
				return HexToByteArray(SHA256id);
			if (hashAlgorithm == HashAlgorithmName.SHA384)
				return HexToByteArray(SHA384id);
			if (hashAlgorithm == HashAlgorithmName.SHA512)
				return HexToByteArray(SHA512id);

			throw new ArgumentOutOfRangeException(nameof(hashAlgorithm), "'" + hashAlgorithm + "' is not a supported algorithm at this moment.");
		}

		/// <summary>
		/// Convert a hex-formatted string to byte array.
		/// </summary>
		/// <param name="hex">A string looking like "300D06092A864886F70D0101050500".</param>
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
