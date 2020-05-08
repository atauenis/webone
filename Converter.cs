using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static WebOne.Program;

namespace WebOne
{
	/// <summary>
	/// External file format converter utility
	/// </summary>
	public class Converter
	{
		/// <summary>
		/// Command or path to executable file of the converter
		/// </summary>
		public string Executable { get; private set; }

		/// <summary>
		/// Command line arguments of the converter
		/// </summary>
		public string CommandLine { get; private set; }

		/// <summary>
		/// Does the converter returning the result content through STDOUT pipe
		/// </summary>
		public bool UseStdout { get; private set; }

		/// <summary>
		/// Does the converter getting the source content through STDIN pipe
		/// </summary>
		public bool UseStdin { get; private set; }

		/// <summary>
		/// Does the converter downloading the content itself
		/// </summary>
		public bool SelfDownload { get; private set; }

		/// <summary>
		/// Register a converter
		/// </summary>
		/// <param name="Str">Line from webone.conf [Converters] section</param>
		public Converter(string Str)
		{
			if (Str.IndexOf(" ") < 0) throw new Exception("Converter line is invalid");
			//todo: make parsing paying attention to quotes, not simply by space character
			Executable = Str.Substring(0, Str.IndexOf(" "));
			CommandLine = Str.Substring(Str.IndexOf(" ") + 1);

			if (!CommandLine.Contains("%DEST%")) UseStdout = true;
			if (!CommandLine.Contains("%SRC%")) UseStdin = true;
			SelfDownload = CommandLine.Contains("%SRCURL%");
			if (UseStdin && SelfDownload) UseStdin = false;
		}

		/// <summary>
		/// Run a converter (input: Stream; output: Stream)
		/// </summary>
		/// <param name="Log">Log writer for current convertion</param>
		/// <param name="InputStream">Input stream with source content (or <see cref="null"/> if the converter can download itself)</param>
		/// <param name="Args1">First set of converter arguments</param>
		/// <param name="Args2">Second set of converter arguments</param>
		/// <param name="DestinationType">Destination extension</param>
		/// <param name="SrcUrl">Source content URL for converters which can download itself (or <see cref="null"/> for others)</param>
		/// <returns>Stream with converted content</returns>
		public Stream Run(LogWriter Log, Stream InputStream = null, string Args1 = "", string Args2 = "", string DestinationType = "tmp", string SrcUrl = null)
        {
			if (SelfDownload && (InputStream != null || SrcUrl == null))
				throw new InvalidOperationException("The converter " + Executable + " can only download the content self");

			int Rnd = new Random().Next();
			string SourceTmpFile = ConfigFile.TemporaryDirectory + "convert-" + Rnd + ".orig.tmp";
			string DestinationTmpFile = ConfigFile.TemporaryDirectory + "convert-" + Rnd + ".conv." + DestinationType;

			if (!UseStdin && !SelfDownload)
			{
				//the converter input is Temporary File; save it
				FileStream TmpFileStream = File.OpenWrite(SourceTmpFile);
				InputStream.CopyTo(TmpFileStream);
				TmpFileStream.Close();
			}

			//prepare command line
			string ConvCommandLine = ProcessUriMasks(CommandLine, SrcUrl ?? "http://webone.github.io/index.htm")
			.Replace("%SRC%", SourceTmpFile)
			.Replace("%ARG1%", Args1)
			.Replace("%DEST%", DestinationTmpFile)
			.Replace("%ARG2%", Args2)
			.Replace("%DESTEXT%", DestinationType)
			.Replace("%SRCURL%", SrcUrl ?? "http://webone.github.io/index.htm");

			//run the converter
			return RunConverter(Log, ConvCommandLine, InputStream, SourceTmpFile, DestinationTmpFile);
		}

		/// <summary>
		/// Run (really) the converter and get its result
		/// </summary>
		/// <param name="Log">Log writer for current convertion</param>
		/// <param name="ConvCommandLine">Real command line (excluding executable file name)</param>
		/// <param name="InputStream">Input stream with source content (or <see cref="null"/> if the converter can download itself)</param>
		/// <param name="SourceTmpFile">Path to source temporary file (even if it doesn't exists)</param>
		/// <param name="DestinationTmpFile">Path to destination temporary file (even if it doesn't exists)</param>
		/// <returns>Stream with converted content (from stdout or a filestream)</returns>
		private Stream RunConverter(LogWriter Log, string ConvCommandLine, Stream InputStream, string SourceTmpFile, string DestinationTmpFile)
		{
			//run the converter
			ProcessStartInfo ConvProcInfo = new ProcessStartInfo();
			ConvProcInfo.FileName = Executable;
			ConvProcInfo.Arguments = ConvCommandLine;
			ConvProcInfo.StandardOutputEncoding = Encoding.GetEncoding("latin1"); //see https://stackoverflow.com/a/5446177/7600726
			ConvProcInfo.RedirectStandardOutput = true;
			ConvProcInfo.RedirectStandardInput = true;
			ConvProcInfo.UseShellExecute = false;
			Process ConvProc = Process.Start(ConvProcInfo);
			Log.WriteLine(" Converting: {0} {1}...", Executable, ConvCommandLine);
			float ConvCpuLoad = 0;

			if (UseStdout)
			{
				//the converter is outputing to stream
				if (UseStdin)
				{
#if DEBUG
					Log.WriteLine(" Writing stdin...");
#endif
					new Task(() => { try { InputStream.CopyTo(ConvProc.StandardInput.BaseStream); } catch { } }).Start();
				}

#if DEBUG
				Log.WriteLine(" Reading stdout...");
#endif
				//new Task(() => { while (InputStream.CanRead) { } if (!ConvProc.HasExited) ConvProc.Kill(); Console.WriteLine(); }).Start();
				//new Task(() => { while (!ConvProc.HasExited) { if (ClientResponse.StatusCode == 500) { if (!ConvProc.HasExited) { ConvProc.Kill(); } } } }).Start();
				//new Task(() => { while (!ConvProc.HasExited) { CheckIdle(ref ConvCpuLoad, ref ConvProc); } }).Start();
				//SendStream(ConvProc.StandardOutput.BaseStream, DestMime, false);

				new Task(() =>
				{
					if(InputStream != null) new Task(() =>
					{
						while (InputStream.CanRead && !ConvProc.HasExited) { }; if (!ConvProc.HasExited) ConvProc.Kill(); Console.WriteLine();
					}).Start();

					new Task(() =>
					{
						while (!ConvProc.HasExited)
						{
							CheckIdle(ref ConvCpuLoad, ref ConvProc, Log);
						}
					}).Start();

#if DEBUG
					Log.WriteLine(" Waiting for finish of converting...");
#endif
					ConvProc.WaitForExit();
					if(InputStream != null) InputStream.Close();
#if DEBUG
					Log.WriteLine(" Converting end.");
#endif
					if (File.Exists(SourceTmpFile)) File.Delete(SourceTmpFile);
					if (File.Exists(DestinationTmpFile)) File.Delete(DestinationTmpFile);
				}).Start();
				return ConvProc.StandardOutput.BaseStream;
			}
			else
			{
				//the converter is outputing to temporary file
				if (UseStdin)
				{
#if DEBUG
					Log.WriteLine(" Writing stdin...");
#endif
					new Task(() => { try { InputStream.CopyTo(ConvProc.StandardInput.BaseStream); } catch { } }).Start();
				}

#if DEBUG
				Log.WriteLine(" Waiting for converter...");
#endif
				ConvProc.WaitForExit();
				InputStream.Close();
				if (File.Exists(SourceTmpFile)) File.Delete(SourceTmpFile);
				if (File.Exists(DestinationTmpFile)) File.Delete(DestinationTmpFile);
				return File.OpenRead(DestinationTmpFile);
			}
		}

	/// <summary>
	/// Get CPU load for process
	/// </summary>
	/// <param name="process">The process object</param>
	/// <returns>CPU usage in percents</returns>
	private double GetUsage(Process process)
		{
			//thx to: https://stackoverflow.com/a/49064915/7600726
			//see also https://www.mono-project.com/archived/mono_performance_counters/

			if (process.HasExited) return double.MinValue;

			// Preparing variable for application instance name
			string name = "";

			foreach (string instance in new PerformanceCounterCategory("Process").GetInstanceNames())
			{
				if (process.HasExited) return double.MinValue;
				if (instance.StartsWith(process.ProcessName))
				{
					using (PerformanceCounter processId = new PerformanceCounter("Process", "ID Process", instance, true))
					{
						if (process.Id == (int)processId.RawValue)
						{
							name = instance;
							break;
						}
					}
				}
			}

			PerformanceCounter cpu = new PerformanceCounter("Process", "% Processor Time", name, true);

			// Getting first initial values
			cpu.NextValue();

			// Creating delay to get correct values of CPU usage during next query
			Thread.Sleep(500);

			if (process.HasExited) return double.MinValue;
			return Math.Round(cpu.NextValue() / Environment.ProcessorCount, 2);
		}

		/// <summary>
		/// Check process for idle mode and kill it if yes
		/// </summary>
		/// <param name="AverageLoad">Average process load</param>
		/// <param name="Proc">Which process</param>
		private void CheckIdle(ref float AverageLoad, ref Process Proc, LogWriter Log)
		{
			Thread.Sleep(1000);
			AverageLoad = (float)(AverageLoad + GetUsage(Proc)) / 2;

			if (Math.Round(AverageLoad, 2) <= 0 && !Proc.HasExited)
			{
				//the process is counting crows. Fire!
				Proc.Kill();
				Console.WriteLine("\n");
				Log.WriteLine(" Idle process killed.");
			}
		}
	}
}
