using Microsoft.VisualBasic.CompilerServices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using static WebOne.Program;

namespace WebOne
{
	/// <summary>
	/// Agent for message logging and displaying
	/// </summary>
	static class LogAgent
	{
		static StreamWriter LogStreamWriter = null;
		static bool LogStreamWriterReady = true;

		/// <summary>
		/// Write a message to server log.
		/// </summary>
		/// <param name="LogMessage">The message string.</param>
		/// <param name="Display">Shall the message be displayed.</param>
		/// <param name="Write">Shall the message be written.</param>
		/// <param name="DisplayText">The message string (which should be displayed if <paramref name="Display"/> is true).</param>
		public static void WriteLine(string LogMessage, bool Display = true, bool Write = true, string DisplayText = "")
		{
			if (Display && !DaemonMode)
				if (DisplayText == "") Console.WriteLine(LogMessage);
				else Console.WriteLine(DisplayText);
			if (Write && LogStreamWriter != null)
			{
				new Task(() =>
				{
					while (!LogStreamWriterReady) { }
					LogStreamWriterReady = false;
					LogStreamWriter.WriteLine(LogMessage);
					LogStreamWriterReady = true;
				}).Start();
				return;
			}
		}

		/// <summary>
		/// Open a server log file and begin logging.
		/// </summary>
		/// <param name="LogFileName">Path to the log file.</param>
		/// <param name="Append">Append the log file or overwrite?</param>
		public static void OpenLogFile(string LogFileName = null, bool Append = false)
		{
			if (LogFileName != null)
			{
				try
				{
					LogStreamWriter = new StreamWriter(LogFileName, Append) { AutoFlush = true };

					string CommandLineArgs = string.Empty;
					for (int i = 1; i < Environment.GetCommandLineArgs().Length; i++) { CommandLineArgs += " " + Environment.GetCommandLineArgs()[i]; };

					string StartMsg = string.Format("{0}\tWebOne {1}{5} ({2}{3}, Runtime {4}) log started.",
						DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"),
						System.Reflection.Assembly.GetExecutingAssembly().GetName().Version,
						Environment.OSVersion.Platform,
						Environment.Is64BitOperatingSystem ? "-64" : "-32",
						Environment.Version,
						CommandLineArgs);
					LogStreamWriter.WriteLine(StartMsg);
					Console.WriteLine("Using event log file {0}.", LogFileName);
				}
				catch (Exception ex)
				{
					Console.WriteLine("Cannot use log file {0}: {1}", LogFileName, ex.Message);
				}
			}
			else
			{
				LogStreamWriter = null;
				Console.WriteLine("Not using log file.");
			}
		}

		/// <summary>
		/// Does messages logging into a log file.
		/// </summary>
		public static bool IsLoggingEnabled
		{
			get
			{
				return LogStreamWriter != null;
			}
		}
	}

	/// <summary>
	/// Log message writer for log agent
	/// </summary>
	public class LogWriter
	{
		public DateTime BeginTime = DateTime.Now;

		bool FirstTime = true;

		/// <summary>
		/// Write the text representation of the specified array of objects to
		/// server log agent using the specified format information.
		/// </summary>
		/// <param name="format">A composite format string.</param>
		/// <param name="arg">An array of objects to write using format.</param>
		/// <seealso cref="Console.WriteLine(string, object)"/>
		public void WriteLine(string format, params object[] arg)
		{
			WriteLine(true, true, format, arg);
		}

		/// <summary>
		/// Write the text representation of the specified array of objects to
		/// server log agent using the specified format information.
		/// </summary>
		/// <param name="display">Shall the representation be displayed in the console window.</param>
		/// <param name="format">A composite format string.</param>
		/// <param name="arg">An array of objects to write using format.</param>
		public void WriteLine(bool display, bool displayTimestamp, string format, params object[] arg)
		{
			string str;
			string timestamp;
			string message = string.Format(format, arg);

			if (FirstTime)
			{
				timestamp = string.Format("{0}+0", BeginTime.ToString("dd.MM.yyyy HH:mm:ss.fff")); FirstTime = false; 
			}
			else
			{ 
				timestamp = GetTime(BeginTime); 
			}

			if(displayTimestamp) //e.g. ">GET http://example.com/ (127.0.0.1)"
			{
				if (timestamp.Length < 20) //23.02.2021 15:55:58.283+7600010 = 31 character long
				{
					str = string.Format("{0}+0\t\t{1}", BeginTime.ToString("dd.MM.yyyy HH:mm:ss.fff"), message);
					LogAgent.WriteLine(str, display, false);
					str = string.Format("{0}+0\t{1}", BeginTime.ToString("dd.MM.yyyy HH:mm:ss.fff"), message);
					LogAgent.WriteLine(str, false, true);
				}
				else
				{
					str = string.Format("{0}\t{1}", GetTime(BeginTime), message);
					LogAgent.WriteLine(str, true, display);
				}
			}

			else //e.g. "Starting server..."
			{
				str = string.Format("{0}+0\t{1}", BeginTime.ToString("dd.MM.yyyy HH:mm:ss.fff"), message);

				LogAgent.WriteLine(str, display, true, message);
				return;
			}
		}
	}
}
