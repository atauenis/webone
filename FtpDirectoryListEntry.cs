using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WebOne
{
	/// <summary>
	/// FTP directory list entry (an line of LIST command output)
	/// </summary>
	internal class FtpDirectoryListEntry
	{
		/// <summary>
		/// Access control (e.g. "drwxr-xr-x")
		/// </summary>
		public string Permissions { get; private set; }
		/// <summary>
		/// Inode number
		/// </summary>
		public int Inode { get; private set; }
		/// <summary>
		/// File or directory owner (user name)
		/// </summary>
		public string OwnerUser { get; private set; }
		/// <summary>
		/// File or directory owner (group name)
		/// </summary>
		public string OwnerGroup { get; private set; }
		/// <summary>
		/// File size
		/// </summary>
		public long Size { get; private set; }
		/// <summary>
		/// File or directory change date
		/// </summary>
		public string Date { get; private set; }
		/// <summary>
		/// File or directory name
		/// </summary>
		public string Name { get; private set; }
		/// <summary>
		/// Is the item a directory (true) or a file (false)
		/// </summary>
		public bool Directory { get; private set; }

		public new string ToString()
		{
			if (Directory) return Name + " <DIR>";
			return Name;
		}


		/// <summary>
		/// Mask for LIST entries in UNIX format
		/// </summary>
		private const string UnixPattern =
			@"^([\w-]+)\s+(\d+)\s+(\w+)\s+(\w+)\s+(\d+)\s+" +
			@"(\w+\s+\d+\s+\d+|\w+\s+\d+\s+\d+:\d+)\s+(.+)$";
		/// <summary>
		/// Mask for LIST entries in DOS/WinNT format
		/// </summary>
		private const string DosPattern =
			@"^(\d+-\d+-\d+\s+\d+:\d+(?:AM|PM))\s+(<DIR>|\d+)\s+(.+)$";



		/// <summary>
		/// Decode a LIST result line to <see cref="FtpDirectoryListEntry"/>
		/// </summary>
		/// <param name="Line">Raw line value</param>
		/// <param name="Type">Line syntax (kind of FTP server)</param>
		/// <returns></returns>
		public static FtpDirectoryListEntry ParseLine(string Line, LineType Type)
		{
			switch (Type)
			{
				case LineType.UNIX:
					return ParseUNIX(Line);
				case LineType.DOS:
					return ParseDOS(Line);
				default:
					throw new ArgumentOutOfRangeException("Unknown LIST result line type", nameof(Type));
			}
		}

		/// <summary>
		/// Decode a UNIX-formatted line to <see cref="FtpDirectoryListEntry"/>
		/// </summary>
		/// <param name="Line">Raw line value (e.g. "drwxr-xr-x   3 oscollect 1011         4096 Nov 25 07:55 200 Best Games")</param>
		private static FtpDirectoryListEntry ParseUNIX(string Line)
		{
			//thanks: https://stackoverflow.com/a/40045894/7600726
			FtpDirectoryListEntry entry = new();
			Regex regex = new (UnixPattern);
			Match match = regex.Match(Line);
			if (!match.Success) throw new ArgumentException("The LIST result line is not in UNIX format", nameof(Line));

			entry.Permissions = match.Groups[1].Value;
			entry.Inode = int.Parse(match.Groups[2].Value);
			entry.OwnerUser = match.Groups[3].Value;
			entry.OwnerGroup = match.Groups[4].Value;
			entry.Size = long.Parse(match.Groups[5].Value);
			entry.Date = match.Groups[6].Value;
			entry.Name = match.Groups[7].Value;
			entry.Directory = entry.Permissions.StartsWith("d");
			return entry;
		}

		/// <summary>
		/// Decode a DOS-formatted line to <see cref="FtpDirectoryListEntry"/>
		/// </summary>
		/// <param name="Line">Raw line value (e.g. "06-25-09  02:41PM            144700153 image34.gif")</param>
		private static FtpDirectoryListEntry ParseDOS(string Line)
		{
			//thanks: https://stackoverflow.com/a/39771146/7600726
			FtpDirectoryListEntry entry = new();
			Regex regex = new(DosPattern);
			Match match = regex.Match(Line);
			if (!match.Success) throw new ArgumentException("The LIST result line is not in DOS/WINNT format", nameof(Line));

			entry.Date = match.Groups[1].Value;
			entry.Directory = match.Groups[2].Value == "<DIR>";
			if (!entry.Directory) entry.Size = long.Parse(match.Groups[2].Value);
			else entry.Size = -1;
			entry.Name = match.Groups[3].Value;
			return entry;
		}

		/// <summary>
		/// Test if the line is in UNIX format
		/// </summary>
		/// <param name="Line">Raw line value (e.g. "drwxr-xr-x   3 oscollect 1011         4096 Nov 25 07:55 200 Best Games")</param>
		/// <returns>true if it's UNIX, false if not</returns>
		public static bool IsUnixLine(string Line)
		{
			Regex regex = new Regex(UnixPattern);
			Match match = regex.Match(Line);
			return match.Success;
		}

		/// <summary>
		/// Test if the line is in DOS format
		/// </summary>
		/// <param name="Line">Raw line value (e.g. "drwxr-xr-x   3 oscollect 1011         4096 Nov 25 07:55 200 Best Games")</param>
		/// <returns>true if it's DOS, false if not</returns>
		public static bool IsDosLine(string Line)
		{
			Regex regex = new Regex(DosPattern);
			Match match = regex.Match(Line);
			return match.Success;
		}

		/// <summary>
		/// Kinds of FTP LIST result line syntaxes
		/// </summary>
		public enum LineType
		{
			/// <summary>
			/// <!--Auto-detect-->
			/// </summary>
			Unknown,
			/// <summary>
			/// d--x--x--x    2 ftp      ftp          4096 Mar 07  2002 bin<br/>
			/// -rw-r--r--    1 ftp      ftp        659450 Jun 15 05:07 TEST.TXT<br/>
			/// -rw-r--r--    1 ftp      ftp      101786380 Sep 08  2008 TEST03-05.TXT<br/>
			/// drwxrwxr-x    2 ftp      ftp          4096 May 06 12:24 dropoff<br/>
			/// </summary>
			UNIX,
			/// <summary>
			/// 08-10-11  12:02PM       &lt;DIR&gt;          Version2<br/>
			/// 06-25-09  02:41PM            144700153 image34.gif<br/>
			/// 06-25-09  02:51PM            144700153 updates.txt<br/>
			/// 11-04-10  02:45PM            144700214 digger.tif<br/>
			/// </summary>
			DOS
		}
	}
}

