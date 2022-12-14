using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace WebOne
{
	/// <summary>
	/// FTP Client (backend)
	/// </summary>
	internal class FtpClient
	{
		public LogWriter Log;
		
		public string FtpLog, Server, User;

		TcpClient Client = new();
		TcpClient PasvClient;

		/// <summary>
		/// Create a FTP connection client
		/// </summary>
		/// <param name="Server">FTP server</param>
		/// <param name="User">FTP user name</param>
		/// <param name="Pass">FTP user password</param>
		/// <param name="Log">WebOne.log writer</param>
		public FtpClient(string Server, string User = "Anonymous", string Pass = "email@example.com", LogWriter Log = null)
		{
			this.Log = Log;
			this.Server = Server;
			this.User = User;

			Log.WriteLine(">FTP connect to: " + Server);
			try
			{
				Client.Connect(Server, 21); //undone: custom port support
				FtpLog += "\nEstablished a TCP/IP connection.";
				FtpResponse resp = GetServerWelcome();
				FtpLog += "\n" + resp.ToString();

				resp = TransmitCommand("USER " + User);
				FtpLog += "\nUSER => " + resp.ToString();
				if (resp.Code != 331) { Client.Close(); FtpLog += "\nDisconnected by client."; }
				resp = TransmitCommand("PASS " + Pass);
				FtpLog += "\nPASS => " + resp.ToString();
				if (resp.Code == 230)
				{
					FtpLog += "\nSuccessfull.";
				}
				else
				{
					Client.Close();
					FtpLog += "\nDisconnected by client.";
				}
			}
			catch(Exception ex)
			{
				Log.WriteLine(" Connecting error: " + ex.GetType().FullName + " " + ex.Message);
#if DEBUG
				FtpLog += "\nERROR: " + ex.ToString();
#else
				FtpLog += "\nERROR: " + ex.Message;

#endif
			}

			if (Connected) Log.WriteLine(" Success.");
			else Log.WriteLine(" Connect failed.");
		}
		
		/// <summary>
		/// Transmit FTP command and get server response
		/// </summary>
		/// <param name="Command">The FTP command with arguments (if any)</param>
		public FtpResponse TransmitCommand(string Command)
		{
			NetworkStream networkStream = Client.GetStream();
			if (!networkStream.CanWrite || !networkStream.CanRead)
				return new FtpResponse("000 CLIENT ERROR: cannot use NetworkStream");

			byte[] sendBytes = Encoding.ASCII.GetBytes(Command + "\r\n");
			networkStream.Write(sendBytes, 0, sendBytes.Length);

			try
			{
				StreamReader streamReader = new StreamReader(networkStream);
				return new FtpResponse(streamReader.ReadLine());
			}
			catch(IOException ioex)
			{
				Log.WriteLine("!Errror on NetworkStream: " + ioex.Message);
				return new FtpResponse("000 CLIENT ERROR: unexpected close of NetworkStream");
			}
		}

		/// <summary>
		/// Get server welcome message
		/// </summary>
		private FtpResponse GetServerWelcome()
		{
			try
			{
				NetworkStream networkStream = Client.GetStream();
				if (!networkStream.CanWrite || !networkStream.CanRead)
					return new FtpResponse("000 CLIENT ERROR: cannot use NetworkStream");

				byte[] receiveBytes = new byte[Client.ReceiveBufferSize];
				networkStream.ReadTimeout = 10000;
				networkStream.Read(receiveBytes, 0, Client.ReceiveBufferSize);

				return new FtpResponse(Encoding.ASCII.GetString(receiveBytes));
			}
			catch
			{
				// Ignore all irrelevant exceptions
				return new FtpResponse("000 CLIENT ERROR: there is an exception");
			}
		}

		/// <summary>
		/// Open a FTP data connection stream to transfer data in Passive mode
		/// </summary>
		/// <param name="PasvInfo">Result of FTP PASV command like:"227  Entering Passive Mode (89,108,84,132,138,69)"</param>
		/// <returns>NetworkStream of the data connection</returns>
		/// <exception cref="ArgumentException">If the 227 reply is incorrect</exception>
		/// <exception cref="SocketException">If cannot open the data connection or its stream</exception>
		/// <exception cref="IOException">If the data connection is not working</exception>
		public NetworkStream GetPasvDataStream(string PasvInfo)
		{
			System.Text.RegularExpressions.Match PasvMatch = System.Text.RegularExpressions.Regex.Match(PasvInfo, @"\([0-9,]*\)");
			if (!PasvMatch.Success) throw new ArgumentException("PASV 227 reply is not correct", nameof(PasvInfo));
			string PasvData = PasvMatch.Value.Substring(1, PasvMatch.Value.Length - 2);
			string[] PasvParts = PasvData.Split(',');
			if (PasvParts.Count() < 6) throw new ArgumentException("PASV 227 reply contains not full IP", nameof(PasvInfo));

			string PasvIP = string.Format("{0}.{1}.{2}.{3}", PasvParts[0], PasvParts[1], PasvParts[2], PasvParts[3]);
			int PasvPort1 = Convert.ToInt32(PasvParts[4]);
			int PasvPort2 = Convert.ToInt32(PasvParts[5]);
			int PasvPort = (PasvPort1 * 256) + PasvPort2; //(p1 * 256) + p2 = data port

			Log.WriteLine(" Passive connect: " + PasvIP + ":" + PasvPort);
			PasvClient = new TcpClient();
			PasvClient.Connect(PasvIP,PasvPort);
			return PasvClient.GetStream();
		}

		/// <summary>
		/// Is the FTP connection alive
		/// </summary>
		public bool Connected
		{
			get { return Client.Connected; }
		}
	}

	/// <summary>
	/// A FTP server response to a command
	/// </summary>
	public class FtpResponse
	{
		/// <summary>
		/// Result code (e.g. 230)
		/// </summary>
		public int Code { get; private set; }

		/// <summary>
		/// Result string (e.g. "User anonymous logged in")
		/// </summary>
		public string Result { get; private set; }

		/// <summary>
		/// Get string representation of the reply (e.g. "230 User anonymous logged in")
		/// </summary>
		public new string ToString()
		{
			return (Code != 0 ? Code.ToString() : "000") + " " + Result;
		}

		/// <summary>
		/// Create a FTP response representation
		/// </summary>
		/// <param name="Response">Raw response string (e.g. "230 User anonymous logged in")</param>
		public FtpResponse(string Response)
		{
			string response = Response.TrimEnd('\0').TrimEnd('\n');
			if (!int.TryParse(response.Substring(0, 3), out int code)) throw new ArgumentException("Incorrect FTP server response", nameof(Response));
			Code = code;
			Result = response.Substring(3);
		}
	}
}
