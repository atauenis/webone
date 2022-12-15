using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static WebOne.Program;

namespace WebOne
{
	/// <summary>
	/// Web-based FTP client HTML interface
	/// </summary>
	internal class FtpClientGUI
	{
		LogWriter Log = new();
		HttpListenerRequest ClientRequest;
		NameValueCollection RequestArguments;
		FtpClientPage Page = new();

		int ClientID = 0;

		/// <summary>
		/// Create an instance of Web-FTP client GUI
		/// </summary>
		/// <param name="ClientRequest">Request from HttpListener</param>
		public FtpClientGUI(HttpListenerRequest ClientRequest)
		{
			this.ClientRequest = ClientRequest;
			RequestArguments = System.Web.HttpUtility.ParseQueryString(ClientRequest.Url.Query);
		}

		/// <summary>
		/// Get the HTML page of current Web-FTP state
		/// </summary>
		/// <returns>WebOne internal page with Web-FTP response</returns>
		public InfoPage GetPage()
		{
			try
			{
				int.TryParse(RequestArguments["client"], out ClientID);
				if (ClientID == 0) //new client - welcome him/her
				{
					return GetWelcomePage();
				}
				if (ClientID == -1) //new connection
				{
					return GetConnectPage();
				}
				if (ClientID != 0) //work with backend
				{
					return GetResultPage();
				}
				throw new Exception("Something went wrong");
			}
			catch(Exception ex)
			{
				FtpClientPage page = new();
				page.Header = "File Transfer Error";
#if DEBUG
				page.Content = ex.ToString().Replace("\n","<br>");
#else
				Log.WriteLine(" FTP Client Error: " + ex.GetType().Name + " - " + ex.Message);
				page.Content = "<big>An error occured in FTP Client.</big>";
				page.Content += "<p>" + ex.Message.Replace("\n","<br>") + "</p>";
				page.Content += "<p>Navigation: "+
				"<a href='javascript:history.back()'><b>Go back</b></a>. " +
				"<a href='/!ftp/'><b>Reconnect</b></a>. Directory listing: " +
				"<a href='/!ftp/?client=" + ClientID + "&task=listdir&cwd=/'><b>root</b></a>, " +
				"<a href='/!ftp/?client=" + ClientID + "&task=listdir'><b>current</b>.</a> "+
				"</p>";
#endif
				return page;
			}
		}

		/// <summary>
		/// Get Web-FTP welcome page
		/// </summary>
		public FtpClientPage GetWelcomePage()
		{
			FtpClientPage Page = new FtpClientPage();
			Log.WriteLine("<Web-FTP: welcome page.");

			Page.Header = "File Transfer Protocol client";
			Page.Content = "<p>Welcome to space of computer files, directories and servers. Here you'll be able to download something to your PC from FTP servers without quitting a web browser.</p>";

			string Form =
			"<form action='/!ftp/' method='GET' name='Connect'>\n" +
			"<input type='hidden' name='client' value='-1'>\n" +
			"<p>Server: <input type='text' size='20' name='server' value='old-dos.ru'></p>\n" +
			"<p>Username: <input type='text' size='20' name='user' value='oscollect'><br>\n" +
			"Password: <input type='password' size='20' name='pass'value='oscollect'></p>\n" +
			"<p><input type='submit' value=\"Let's go!\"></p>\n" +
			"</form>";

			Page.Content += Form;

			return Page;
		}

		/// <summary>
		/// Connect to a FTP server, and get a Web-FTP connection status page
		/// </summary>
		public FtpClientPage GetConnectPage()
		{
			string Server = RequestArguments["server"];
			string User = RequestArguments["user"];
			string Pass = RequestArguments["pass"];

			if(string.IsNullOrEmpty (Server) || string.IsNullOrEmpty(User) || string.IsNullOrEmpty(Pass))
			{
				Page.Content = 
				"<h2>Error - Incorrect Use</h2>" +
				"<p>Need to fill the connection request form first.</p>";
				return Page;
			}

			Page.Header = "File Transfer: " + Server;
			int NewClientId = new Random().Next();
			FtpClient NewClient = new(Server, User, Pass, Log);

			if (NewClient.Connected)
			{
				FtpTransitManager.Backends.Add(NewClientId, NewClient);
				Page.Content = "<h2>Connecting to the server</h2>\n";
				Page.Content += "<pre>" + NewClient.FtpLog + "</pre>\n";
				Page.Content += "<p>Okay, <a href='/!ftp/?client=" + NewClientId + "&task=listdir'><b>click here</b></a>.</p>";
				return Page;
			}
			else
			{
				Page.Content = "<h2>Sorry, an error occured while connecting.</h2>\n"+
				"<pre>" + NewClient.FtpLog + "</pre>\n"+
				"<p><a href='/!ftp/'>Go back</a> and try again.</p>";
				return Page;
			}

		}

		/// <summary>
		/// Send a FTP server command, and get Web-FTP result page
		/// </summary>
		public FtpClientPage GetResultPage()
		{
			if (!FtpTransitManager.Backends.ContainsKey(ClientID)) //check for incorrect backend id
			{
				Log.WriteLine(" Web-FTP: unknown client ID.");
				return GetWelcomePage();
			}

			FtpClient Backend = FtpTransitManager.Backends[ClientID];

			Page.Header = "File Transfer: " + Backend.Server;
			Page.Content = "";

			if(!Backend.Connected)
			{
				Page.Content = "<h2>Connection lost</h2>";
				Page.Content += "<p>The FTP connection to the server is closed.</p>";
				Page.Content += "<p><a href='/!ftp/'>Go to start</a> and try again.</p>";
				return Page;
			}

			FtpResponse cmd;

			//Send a FTP command and process its response
			switch (RequestArguments["task"])
			{
				case "listdir":
					//Get directory listing
					if (!string.IsNullOrEmpty(RequestArguments["cwd"]))
					{
						//Change current directory if need
						cmd = Backend.TransmitCommand("CWD " + RequestArguments["cwd"]);
						if(cmd.Code != 250)
						{
							Page.Content += "<p><b>Cannot change working directory:</b> " + cmd.ToString() + "</p>";
						}
					}

					//Working with current directory
					cmd = Backend.TransmitCommand("PWD");
					Page.Content += "<h2>" + cmd.Result + "</h2>";
					if (cmd.Code != 257)
					{
						Page.Content += "<p><b>&quot;Print Working Directory&quot; command has returned an unexpected result:</b> " + cmd.ToString() + "</p>";
						return Page;
					}

					cmd = Backend.TransmitCommand("OPTS UTF8 ON");
					if (cmd.Code == 200) {/*we have UTF-8 support*/}

					cmd = Backend.TransmitCommand("TYPE A");
					if (cmd.Code != 200)
					{
						Page.Content += "<p><b>Cannot set ASCII mode:</b> " + cmd.ToString() + "</p>";
						return Page;
					}

					cmd = Backend.TransmitCommand("PASV");
					if (cmd.Code != 227)
					{
						Page.Content += "<p><b>Cannot prepare data connection:</b> " + cmd.ToString() + "</p>";
						return Page;
					}
					System.Net.Sockets.NetworkStream datastream = Backend.GetPasvDataStream(cmd.Result);

					cmd = Backend.TransmitCommand("LIST");
					if (cmd.Code != 150)
					{
						Page.Content += "<p><b>Directory listing is inaccessible:</b> " + cmd.ToString() + "</p>";
						return Page;
					}
					StreamReader sr = new StreamReader(datastream);
					string FileList = sr.ReadToEnd();

					// Close data connection and get "226  Transfer complete"
					Backend.CloseDataConnection();
					cmd = Backend.Flush();

					//Decode the directory listing
					List<FtpDirectoryListEntry> FileListTable = new List<FtpDirectoryListEntry>();
					FtpDirectoryListEntry.LineType lineType = FtpDirectoryListEntry.LineType.Unknown;
					foreach(string fileListLine in FileList.Split("\n"))
					{
						if (string.IsNullOrWhiteSpace(fileListLine)) continue;
						if(lineType == FtpDirectoryListEntry.LineType.Unknown)
						{
							if(FtpDirectoryListEntry.IsUnixLine(fileListLine)) lineType = FtpDirectoryListEntry.LineType.UNIX;
							if(FtpDirectoryListEntry.IsDosLine(fileListLine)) lineType = FtpDirectoryListEntry.LineType.DOS;
						}
						FtpDirectoryListEntry Line = FtpDirectoryListEntry.ParseLine(fileListLine.Trim('\r', '\n'), lineType);

						if (Line.Name == "." || Line.Name == "..") continue;
						FileListTable.Add(Line);
					}

					//Format the directory listing
					Page.Content += "<table>\n";
					Page.Content += "<tr>";
					Page.Content += "<td>";
					Page.Content += "[<a href='/!ftp/?client=" + ClientID + "&task=listdir&cwd=..'>..</a>]";
					Page.Content += "</td>";
					Page.Content += "<td>";
					Page.Content += "</td>";
					Page.Content += "<td>";
					Page.Content += "</td>";
					Page.Content += "<td>";
					Page.Content += "</td>";
					Page.Content += "</tr>\n";
					foreach (var Item in FileListTable)
					{
						bool IsDir = Item.Directory;
						string FileName = Item.Name;

						Page.Content += "<tr>";
						Page.Content += "<td>";
						if (IsDir)
						{
							Page.Content += "[<a href='/!ftp/?client=" + ClientID + "&task=listdir&cwd=" + Uri.EscapeDataString(FileName) + "'>" + FileName + "</a>]";
						}
						else
						{
							Page.Content += "<a href='/!ftp/?client=" + ClientID + "&task=retr&name=" + Uri.EscapeDataString(FileName) + "' target='_blank'>" + FileName + "</a>";
						}
						Page.Content += "</td>";
						Page.Content += "<td>";
						Page.Content += Item.Size;
						Page.Content += "</td>";
						Page.Content += "<td>";
						Page.Content += Item.Date;
						Page.Content += "</td>";
						Page.Content += "</tr>\n";
					}
					Page.Content += "\n</table>";

					return Page;
				case "retr":
					//Download a file
					string filename = RequestArguments["name"];
					if (string.IsNullOrEmpty(filename))
					{
						Page.Content += "<h2>Nothing to download</h2>";
						return Page;
					}

					cmd = Backend.TransmitCommand("TYPE I");
					if (cmd.Code != 200)
					{
						Page.Content += "<p><b>Cannot set BINARY mode:</b> " + cmd.ToString() + "</p>";
						return Page;
					}

					cmd = Backend.TransmitCommand("PASV");
					if (cmd.Code != 227)
					{
						Page.Content += "<p><b>Cannot prepare data connection:</b> " + cmd.ToString() + "</p>";
						return Page;
					}
					System.Net.Sockets.NetworkStream datastream2 = Backend.GetPasvDataStream(cmd.Result);

					cmd = Backend.TransmitCommand("RETR " + filename);
					Page.Attachment = datastream2;

					if (filename.ToLower().EndsWith(".txt"))
					{
						Page.AttachmentContentType = "text/plain";
						Page.HttpHeaders.Add("Content-Disposition", "inline; filename=\"" + filename + "\"");
					}
					else
					{
						Page.AttachmentContentType = "application/octet-stream";
						Page.HttpHeaders.Add("Content-Disposition", "attachment; filename=\"" + filename + "\"");
					}

					// Close data connection and get "226  Transfer complete" when its time became
					new Task(() =>
					{
						while (datastream2.CanWrite) {}
						Backend.CloseDataConnection();
						cmd = Backend.Flush();
						Log.WriteLine(" Close data stream.");
						return;
					}).Start();
					return Page;
				default:
					Page.Content = "<h2>No or unknown task</h2>";
					Page.Content += "<p>The specified <i>task</i> argument is not recognized by WebOne.</p>";
					Page.Content += "<p><a href='/!ftp/?client=" + ClientID + "&task=listdir'>Click here</a> to see directory listing.</p>";
					return Page;
			}
		}
	}

	/// <summary>
	/// FTP Web-GUI HTML page
	/// </summary>
	internal class FtpClientPage : InfoPage
	{
		/// <summary>
		/// Create an FTP Web-GUI information page
		/// </summary>
		/// <param name="Title">The information page title</param>
		/// <param name="Header1">The information page 1st level header (or null if no title)</param>
		/// <param name="Content">The information page content (HTML)</param>
		public FtpClientPage(string Title = "WebOne: FTP client", string Header = "File Transfer Protocol client", string Content = "This is a FTP Web-GUI page.")
		{
			this.Title = Title;
			this.Header = Header;
			this.Content = Content;
		}
	}
}
