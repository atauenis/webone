using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace WebOne
{
	class Program
	{
		static void Main(string[] args)
		{
			int Port = 80;
			Console.WriteLine("WebOne HTTP Proxy Server.\n(C) 2015 Alexander Tauenis.\n");
			Console.Title = "WebOne @ " + Port;

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
	}
}
