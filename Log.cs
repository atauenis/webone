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
		public static void WriteLine(string LogMessage, bool Display = true, bool Write = true)
		{
			if (Display)
				Console.WriteLine(LogMessage);
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

					string StartMsg = string.Format("{0}\tWebOne {1} ({2}{3}, Runtime {4}) log started.",
						DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"),
						System.Reflection.Assembly.GetExecutingAssembly().GetName().Version,
						Environment.OSVersion.Platform,
						Environment.Is64BitOperatingSystem ? "-64" : "-32",
						Environment.Version);
					LogStreamWriter.WriteLine(StartMsg);
					Console.WriteLine("Using event log file {0}.", LogFileName);
				}
				catch (Exception ex)
				{
					Console.WriteLine("Cannot use log file {0}: {1}", LogFileName, ex.Message);
				}
			}
			else Console.WriteLine("Not using log file.");
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
			WriteLine(true, format, arg);
		}

		/// <summary>
		/// Write the text representation of the specified array of objects to
		/// server log agent using the specified format information.
		/// </summary>
		/// <param name="display">Shall the representation be displayed in the console window.</param>
		/// <param name="format">A composite format string.</param>
		/// <param name="arg">An array of objects to write using format.</param>
		public void WriteLine(bool display, string format, params object[] arg)
		{
			string str;
			string timestamp;

			if (FirstTime)
			{
				timestamp = string.Format("{0}+0", BeginTime.ToString("HH:mm:ss.fff")); FirstTime = false; 
			}
			else
			{ 
				timestamp = GetTime(BeginTime); 
			}
			
			if(timestamp.Length < 20) //15:55:58.283+7600010 = 20 character long
			{
				str = string.Format("{0}+0\t\t{1}", BeginTime.ToString("HH:mm:ss.fff"), string.Format(format, arg));
				LogAgent.WriteLine(str, display, false);
				str = string.Format("{0}+0\t{1}", BeginTime.ToString("HH:mm:ss.fff"), string.Format(format, arg));
				LogAgent.WriteLine(str, false, true);
			}
			else
			{
				str = string.Format("{0}\t{1}", GetTime(BeginTime), string.Format(format, arg));
				LogAgent.WriteLine(str, display);
			}
		}
	}
}
