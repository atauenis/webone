using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

		//undone: add cleanup of unused or closed backends
	}
}
