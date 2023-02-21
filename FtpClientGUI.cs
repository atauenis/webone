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
		public InfoPage GetPage(string DestinationUrl = null)
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
					return GetConnectPage(DestinationUrl);
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
#endif
				page.Content += "<p>Navigation: " +
				"<a href='javascript:history.back()'><b>Go back</b></a>. " +
				"<a href='/!ftp/'><b>Reconnect</b></a>. Directory listing: " +
				"<a href='/!ftp/?client=" + ClientID + "&task=listdir&cwd=/'><b>root</b></a>, " +
				"<a href='/!ftp/?client=" + ClientID + "&task=listdir'><b>current</b>.</a> " +
				"</p>";
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
			"<center><input type='hidden' name='client' value='-1'>\n" +
			"<p>Server: <input type='text' size='23' name='server' value=''><br>\n" +
			"or URI: <input type='text' size='23' name='uri' value=''></p>\n" +
			"<p>Username: <input type='text' size='20' name='user' value='anonymous'><br>\n" +
			"Password: <input type='password' size='20' name='pass'value='user@domain.su'></p>\n" +
			"<p><input type='submit' value=\"Let's go!\"></p>\n" +
			"</center></form>";

			Page.Content += Form;

			return Page;
		}

		/// <summary>
		/// Connect to a FTP server, and get a Web-FTP connection status page
		/// </summary>
		public FtpClientPage GetConnectPage(string DestinationUrl = null)
		{
			string Server = RequestArguments["server"];
			string User = RequestArguments["user"] ?? "anonymous";
			string Pass = RequestArguments["pass"] ?? "email@example.com";
			string FtpUri = RequestArguments["uri"];
			if (!string.IsNullOrEmpty(DestinationUrl)) FtpUri = DestinationUrl;
			
			if(string.IsNullOrEmpty (Server) && string.IsNullOrEmpty(FtpUri))
			{
				Page.Content = 
				"<h2>Empty connection data</h2>\n" +
				"<p>You need to specify the remote server first.</p>" +
				"<p><a href='/!ftp/'><b>Go back.</b></a></p>"; 
				return Page;
			}

			//prepare destination Web-FTP page URL
			int NewClientId = new Random().Next();
			string WebFtpUrl = "/!ftp/?client=" + NewClientId;
			if (string.IsNullOrEmpty(FtpUri))
			{
				WebFtpUrl += "&task=listdir";
			}
			else
			{
				if (!FtpUri.StartsWith("ftp://"))
				{
					Page.Content =
					"<h2>Malformed Universal Resource Identificator</h2>\n" +
					"<p>URIs (locations) are accepted only in the following format:\n"+
					"<pre>ftp://ftp.microsoft.com/MISC1/DESKAPPS/DOSWORD/KB/Q81/4/46.TXT</pre>\n" +
					"Also the Identificator can contain FTP user name, password and port.</p>"+
					"<p><a href='/!ftp/'><b>Go back.</b></a></p>";
					return Page;
				}
				const string PathCleanupMask = "(;type=.*)";
				string RequestedPath = new Regex(PathCleanupMask).Replace(FtpUri, "");
				RequestedPath = RequestedPath.Replace("ftp://", "", true, null).Replace("http://", "", true, null);

				if (!RequestedPath.Contains('/')) RequestedPath += "/"; //set root directory

				Server = RequestedPath.Substring(0, RequestedPath.IndexOf("/"));
				RequestedPath = RequestedPath.Substring(RequestedPath.IndexOf("/"));

				if (RequestedPath.EndsWith("/"))
					WebFtpUrl += "&cwd=" + RequestedPath + "&task=listdir";
				else
					WebFtpUrl += "&name=" + RequestedPath + "&task=retr";
			}

			//Let's go! :)
			FtpClient NewClient = new(Server, User, Pass, Log);
			if (NewClient.Connected)
			{
				FtpTransitManager.Backends.Add(NewClientId, NewClient);
				Page.Header = "File Transfer: " + FtpTransitManager.Backends[NewClientId].Server;
				Page.Content = "<h2>Connecting to the server</h2>\n";
				Page.Content += "<pre>" + NewClient.FtpLog + "</pre>\n";
				Page.Content += "<p>Okay, <a href='" + WebFtpUrl + "'><b>click here</b></a>.</p>";
				Page.HttpStatusCode = 302;
				Page.HttpHeaders.Add("Location", WebFtpUrl);
				return Page;
			}
			else
			{
				Page.Title = "WebOne cannot open FTP connection";
				Page.Header = "File Transfer: " + Server;
				Page.Content = "<h2>The client could not connect to the server.</h2>\n"+
				"<pre>" + NewClient.FtpLog + "</pre>\n"+
				"<p>Return to <a href='/!ftp/'><b>connection page</b></a> and try again.</p>";
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

			Page.Title = Backend.Server + " - FTP";
			Page.Header = "File Transfer: " + Backend.Server;
			Page.Content = "";

			if(!Backend.Connected)
			{
				Page.Content = "<h2>Connection lost</h2>";
				Page.Content += "<p>The FTP connection to the server is closed.</p>";
				Page.Content += "<p><a href='/!ftp/'><b>Go to start</b></a> and try again.</p>";
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
					if (cmd.Code != 257)
					{
						Page.Content += "<p><b>&quot;Print Working Directory&quot; command has returned an unexpected result:</b> " + cmd.ToString() + "</p>";
						Page.Content += "<p>Return to <a href='/!ftp/'><b>start page</b></a> and try to connect again.</p>";
						return Page;
					}

					Match PWDregex = Regex.Match(cmd.Result, @"""(.*)""");
					if (PWDregex.Success)
					{
						Page.Content += "<h2>";
						Page.Content += "<b><a href='/!ftp/?client=" + ClientID + "&task=listdir&cwd=" + Uri.EscapeDataString("/") + "'>Server root</a></b>";

						Backend.WorkdirPath = "";
						foreach (var dir in PWDregex.Groups[1].Value.Split("/"))
						{
							Backend.WorkdirPath += dir + "/";
							if(dir != "")
							Page.Content += " &raquo; <b><a href='/!ftp/?client=" + ClientID + "&task=listdir&cwd=" + Uri.EscapeDataString(Backend.WorkdirPath) + "'>" + dir + "</a></b>";
						}
						Page.Content += "</h2>\n";
					}
					else
					{
						Page.Content += "<h2>" + cmd.Result + "</h2>\n";
						Backend.WorkdirPath = "./";
					}					

					cmd = Backend.TransmitCommand("OPTS UTF8 ON");
					if (cmd.Code == 200) {/*we have UTF-8 support*/}

					cmd = Backend.TransmitCommand("TYPE A");
					if (cmd.Code != 200)
					{
						Page.Content += "<p><b>Cannot set ASCII mode:</b> " + cmd.ToString() + "</p>";
						Page.Content += "<p>Return to <a href='/!ftp/'><b>start page</b></a> and try to connect again.</p>";
						return Page;
					}

					cmd = Backend.TransmitCommand("PASV");
					if (cmd.Code != 227)
					{
						Page.Content += "<p><b>Cannot prepare data connection:</b> " + cmd.ToString() + "</p>";
						Page.Content += "<p>Return to <a href='/!ftp/'><b>start page</b></a> and try to connect again.</p>";
						return Page;
					}

					System.Net.Sockets.NetworkStream datastream = null;
					try
					{
						datastream = Backend.GetPasvDataStream(cmd.Result);
					}
					catch
					{
						Page.Content += "<p><b>Cannot establish data connection:</b> " + cmd.ToString() + "</p>";
						Page.Content += "<p>Return to <a href='/!ftp/'><b>start page</b></a> and try to connect again.</p>";
						return Page;
					}

					cmd = Backend.TransmitCommand("LIST");
					if (cmd.Code != 150)
					{
						Page.Content += "<p><b>Directory listing is inaccessible:</b> " + cmd.ToString() + "</p>";
						Page.Content += "<p>Return to <a href='/!ftp/'><b>start page</b></a> and try to connect again.</p>";
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
					if (Backend.WorkdirPath != "//")
					{
						Page.Content += "<tr>";
						Page.Content += "<td>";
						Page.Content += "[<a href='/!ftp/?client=" + ClientID + "&task=listdir&cwd=" + Uri.EscapeDataString(Backend.WorkdirPath + "..") + "'>..</a>]";
						Page.Content += "</td>";
						Page.Content += "<td>";
						Page.Content += "</td>";
						Page.Content += "<td>";
						Page.Content += "</td>";
						Page.Content += "<td>";
						Page.Content += "</td>";
						Page.Content += "</tr>\n";
					}
					foreach (var Item in FileListTable)
					{
						string FileName = Item.Name;

						Page.Content += "<tr>";
						Page.Content += "<td>";
						if (Item.Directory && !Item.SymLink)
						{
							Page.Content += "[<a href='/!ftp/?client=" + ClientID + "&task=listdir&cwd=" + Uri.EscapeDataString(Backend.WorkdirPath + FileName) + "'>" + FileName + "</a>]";
						}
						else if (Item.Directory && Item.SymLink)
						{
							Page.Content += "<i>[" + FileName + "]</i>";
						}
						else if(!Item.Directory && Item.SymLink)
						{
							Page.Content += "<i>" + FileName + "</i>";
						}
						else
						{
							Page.Content += "<a href='/!ftp/?client=" + ClientID + "&task=retr&name=" + Uri.EscapeDataString(Backend.WorkdirPath + FileName) + "' target='_blank'>" + FileName + "</a>";
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
						Page.Content += "<p><a href='/!ftp/?client=" + ClientID + "&task=listdir'>Click here</a> to see directory listing.</p>";
						return Page;
					}

					cmd = Backend.TransmitCommand("TYPE I");
					if (cmd.Code != 200)
					{
						Page.Content += "<p><b>Cannot set BINARY mode:</b> " + cmd.ToString() + "</p>";
						Page.Content += "<p>Return to <a href='/!ftp/'><b>start page</b></a> and try to connect again.</p>";
						return Page;
					}

					cmd = Backend.TransmitCommand("PASV");
					if (cmd.Code != 227)
					{
						Page.Content += "<p><b>Cannot prepare data connection:</b> " + cmd.ToString() + "</p>";
						Page.Content += "<p>Return to <a href='/!ftp/'><b>start page</b></a> and try to connect again.</p>";
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
				case "close":
					Backend.Close();
					Page.Content = "<h2>Disconnected from the server</h2>\n" +
					"<p>The file transfer session has been ended.</p>\n" +
					"<p>Return to <a href='/!ftp/'><b>connection page</b></a>.</p>";
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
