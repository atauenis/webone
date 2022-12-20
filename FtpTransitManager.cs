using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebOne
{
	/// <summary>
	/// Manager for FTP backends
	/// </summary>
	internal static class FtpTransitManager
	{
		/// <summary>
		/// Available FTP backends (running FTP clients)
		/// </summary>
		public static Dictionary<int, FtpClient> Backends = new();

		/// <summary>
		/// Period of checks for abandoned FTP backends (seconds); default = 300 sec (5 minutes)
		/// </summary>
		public static int IdleTimeout = 60 * 5;

		static FtpTransitManager()
		{
			new Task(() =>
			{
				//clean up unused & unuseful backends in background
				while (true)
				{
					Thread.Sleep(1000 * (IdleTimeout + 1) );

					foreach (int BackendId in Backends.Keys.ToList())
					{
						if (!Backends.ContainsKey(BackendId)) continue;
						
						if (!Backends[BackendId].Connected)
						{
							Backends[BackendId].Log.WriteLine(" Remove closed FTP connection to {0}.", Backends[BackendId].Server);
							Backends.Remove(BackendId);
							continue;
						}

						if(DateTime.Now.Subtract(Backends[BackendId].LastUsed).TotalSeconds > IdleTimeout)
						{
							Backends[BackendId].Log.WriteLine(" Close abandoned FTP connection to {0}.", Backends[BackendId].Server);
							Backends[BackendId].Close();
							Backends.Remove(BackendId);
							continue;
						}
					}
				}
			}).Start();
		}
	}
}
