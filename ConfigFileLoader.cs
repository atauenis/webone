using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WebOne
{
	/// <summary>
	/// Loader of configuration files
	/// </summary>
	static class ConfigFileLoader
	{
		static List<string> LoadedFiles = new List<string>();
		public static List<KeyValuePair<string, string>> RawEntries = new List<KeyValuePair<string, string>>();

		private static LogWriter Log = new LogWriter();

		/// <summary>
		/// Add all lines from this file onto RawEntries
		/// </summary>
		/// <param name="Body">This configuration file body</param>
		/// <param name="FileName">This configuration file name</param>
		public static void LoadConfigFileContent(string[] Body, string FileName)
		{
			Log.WriteLine(true, false, "Using configuration file {0}.", FileName);

			for(int i = 0; i<Body.Length; i++)
			{
				string LineNo = FileName + ", line " + i;
				string Line = Body[i].TrimStart().TrimEnd();

				try
				{

					if (Line.StartsWith(";")) continue; //skip comments
					if (Line.Length == 0) continue; //skip empty lines
					if (Line.StartsWith("[Include:"))
					{
						LoadInclude(Line);
						continue;
					}

					RawEntries.Add(new KeyValuePair<string, string>(Line, LineNo));
				}
				catch(Exception ex)
				{
					Log.WriteLine(true, false, "Error in {0}: {1} Line ignored.", LineNo, ex.Message);
				}
			}
		}

		/// <summary>
		/// Process "[Include:path/filename.ext]" directive.
		/// </summary>
		/// <param name="IncludeString">"[Include:path/filename.ext]"-formatted string</param>
		public static void LoadInclude(string IncludeString)
		{
			if (!(IncludeString.StartsWith("[Include:") && IncludeString.EndsWith("]"))) throw new Exception("Incorrect include section.");

			int Start = "[Include:".Length;
			int Length = IncludeString.Length - 1 - Start;

			string Includable = IncludeString.Substring(Start, Length);

			string DefaultConfigDir = ".";
			switch (Environment.OSVersion.Platform)
			{
				case PlatformID.Win32NT:
					if (Directory.Exists(@"C:\ProgramData\WebOne\")) DefaultConfigDir = @"C:\ProgramData\WebOne\";
					break;
				case PlatformID.Unix:
					if (Directory.Exists(@"/etc/webone.conf.d/")) DefaultConfigDir = @"/etc/webone.conf.d/";
					break;
					//may be rewritten to a separate function, see Program.GetConfigurationFileName() code
			}
			Includable = Includable.Replace("%WOConfigDir%", DefaultConfigDir);

			if (Includable.Contains('*') || Includable.Contains('?')) //it's a file mask
			{
				int MaskStart = Includable.LastIndexOf(Path.DirectorySeparatorChar);
				if (MaskStart < 0) MaskStart = Includable.LastIndexOf(Path.AltDirectorySeparatorChar);
				if (MaskStart > 0) // "/etc/webone.conf.d/*.conf" or even "C:/program files/webone/myconf/*.conf"
					LoadDirectory(Includable.Substring(0, MaskStart), Includable.Substring(MaskStart + 1));
				else // simply "*.conf"
					LoadDirectory(".", Includable);
			}
			else if (File.GetAttributes(Includable).HasFlag(FileAttributes.Directory)) LoadDirectory(Includable); else LoadFile(Includable);
		}

		/// <summary>
		/// Load all entries from all configuration files in the directory.
		/// </summary>
		/// <param name="PathMask">Path to the directory and filename mask.</param>
		public static void LoadDirectory(string Path, string Mask = "*.*")
		{
			if (!Directory.Exists(Path)) throw new DirectoryNotFoundException();

			foreach(string f in Directory.GetFiles(Path, Mask)){
				try { LoadFile(f); }
				catch (Exception e) { throw new Exception(string.Format("Can't load included {0}: {1}", f, e.Message)); };
			}
		}

		/// <summary>
		/// Load all entries from this configuration file.
		/// </summary>
		/// <param name="Path">Path of the configuration file.</param>
		public static void LoadFile(string Path)
		{
			if (!File.Exists(Path)) throw new FileNotFoundException();

			string FullPath = new FileInfo(Path).FullName;
			if (LoadedFiles.Contains(FullPath)) return; //prevent infinite loops
			LoadedFiles.Add(FullPath);

			string ShortFileName = new FileInfo(Path).Name;
			LoadConfigFileContent(File.ReadAllLines(Path), ShortFileName);
		}

		/// <summary>
		/// Process loaded configuration files (RawEntries) and load them to ConfigFile properties
		/// </summary>
		public static void ProcessConfiguration()
		{
			//UNDONE: move parser here from constructor of ConfigFile static class!
		}
	}
}
