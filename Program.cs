using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace WebOne
{
	public static class Program
	{


		static void Main(string[] args)
		{

			int Port = 80;
			try { Port = Convert.ToInt32(args[0]); } catch { }

			Console.WriteLine("WebOne HTTP Proxy Server {0}.\n(C) 2019 Alexander Tauenis.\nhttps://github.com/atauenis/webone\n\n", Assembly.GetExecutingAssembly().GetName().Version);
			Console.Title = "WebOne @ " + Port;
#if NET40
			Console.WriteLine("Warning: this build is compiled for .NET Framework 4.0 from 2010.\n");
#endif


			/*string host = "https://mozilla.org";
			HTTPC https = new HTTPC();
			Console.WriteLine("try to get...");
			HttpResponse response = https.GET(host, new CookieContainer());
			Console.WriteLine("wait for response...");
			Console.WriteLine("Code=" + response.StatusCode);
			Console.WriteLine("wait for body...");
			var body = response.Content;
			Console.WriteLine("Body length" + body.Length);
			Console.ReadKey();

			if (response.StatusCode == HttpStatusCode.OK)
			{
				//var body = response.Content;
			}*/

			HTTPServer Server = new HTTPServer(Port);

			Console.WriteLine("That's all. Closing.");
		}

		public static string GetInfoString() {
			string OnNet40 = "";
#if NET40
			OnNet40 = " / .NET FW 4.0";
#endif
			return "<hr>WebOne Proxy Server " + Assembly.GetExecutingAssembly().GetName().Version + "<br>on " + Environment.OSVersion.VersionString + OnNet40;
		}
	}
}
