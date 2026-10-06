using System;
using System.Collections.Generic;
using static WebOne.Program;

namespace WebOne
{
	/// <summary>
	/// Set of edits for particular pages
	/// </summary>
	class EditSet
	{
		/// <summary>
		/// List of masks of URLs on which the Set whould be used [title and OnUrl]
		/// </summary>
		public List<string> UrlMasks { get; set; }

		/// <summary>
		/// List of masks of URLs on which the Set would not be used [IgnoreUrl]
		/// </summary>
		public List<string> UrlIgnoreMasks { get; set; }

		/// <summary>
		/// List of masks of MIME Content-Types on which the Set would be used [OnContentType]
		/// </summary>
		public List<string> ContentTypeMasks { get; set; }

		/// <summary>
		/// Mask (exact) of HTTP status code where the Set would be used [OnCode]
		/// </summary>
		public int? OnCode { get; set; }

		/// <summary>
		/// Flag indicating that the edit set should be used only on plain HTTP requests
		/// </summary>
		public bool HttpOnly { get; set; }

		/// <summary>
		/// Flag indicating that the edit set should be used only on HTTPS requests
		/// </summary>
		public bool HttpsOnly { get; set; }

		/// <summary>
		/// List of masks of HTTP request headers on which the Set would not be used [OnHeader]
		/// </summary>
		public List<string> HeaderMasks { get; set; }

		/// <summary>
		/// List of users for which the Set would be used
		/// </summary>
		public List<string> Users { get; set; }

		/// <summary>
		/// Flag that indicates that the edits can be performed at time of HTTP Request (before get of response)
		/// </summary>
		public bool IsForRequest { get; private set; }

		/// <summary>
		/// Limitation by proxy server operating system [OnHostOS]<br/>
		/// True if OS is good, False if is bad.
		/// </summary>
		public bool CorrectHostOS { get; private set; }

		/// <summary>
		/// List of edits that would be performed on the need content
		/// </summary>
		public List<EditSetRule> Edits { get; set; }

		/// <summary>
		/// Create a Set of edits from a section from webone.conf
		/// </summary>
		/// <param name="Section">The webone.conf section</param>
		public EditSet(ConfigFileSection Section)
		{
			UrlMasks = new List<string>();
			UrlIgnoreMasks = new List<string>();
			ContentTypeMasks = new List<string>();
			HeaderMasks = new List<string>();
			Users = new List<string>();
			CorrectHostOS = true;
			Edits = new List<EditSetRule>();
			IsForRequest = false;

			bool MayBeForResponse = false; //does this set containing tasks for HTTP response processing?


			if (Section.Mask != null)
			{
				Program.CheckRegExp(Section.Mask, Section.Location);
				UrlMasks.Add(Section.Mask);
			}

			foreach (var Line in Section.Options)
			{
				if (!Line.HaveKeyValue) continue;
				switch (Line.Key)
				{
					// Condition rules
					case EditSetConditionRules.OnUrl:
						CheckRegExp(Line);
						UrlMasks.Add(Line.Value);
						continue;
					case EditSetConditionRules.OnCode:
						OnCode = int.Parse(Line.Value);
						continue;
					case EditSetConditionRules.IgnoreUrl:
						CheckRegExp(Line);
						UrlIgnoreMasks.Add(Line.Value);
						continue;
					case EditSetConditionRules.OnContentType:
						CheckRegExp(Line);
						ContentTypeMasks.Add(Line.Value);
						continue;
					case EditSetConditionRules.OnHeader:
						CheckRegExp(Line);
						HeaderMasks.Add(Line.Value);
						continue;
					case EditSetConditionRules.OnHostOS:
						switch (Line.Value.ToLower())
						{
							case "windows":
								CorrectHostOS = OperatingSystem.IsWindows();
								continue;
							case "linux":
								CorrectHostOS = OperatingSystem.IsLinux();
								continue;
							case "macos":
								CorrectHostOS = OperatingSystem.IsMacOS();
								continue;
							default:
								new LogWriter().WriteLine(true, false, "Warning: unknown host OS \"{0}\", edit set disabled.", Line.Value);
								CorrectHostOS = false;
								continue;
						}
					case EditSetConditionRules.OnHttpOnly:
						HttpOnly = ToBoolean(Line.Value);
						continue;
					case EditSetConditionRules.OnHttpsOnly:
						HttpsOnly = ToBoolean(Line.Value);
						continue;
					case EditSetConditionRules.OnUser:
						foreach (string user in (Line.Values ?? new string[1] { Line.Value }))
						{
							Users.Add(user);
							bool userFound = false;
							foreach (string registeredUser in ConfigFile.Authenticate)
							{
								if (registeredUser.StartsWith(user + ":")) userFound = true;
							}
							if (!userFound) new LogWriter().WriteLine(true, false, "Warning: unknown user \"{1}\" at {0}.", Line.Location, user);
						}
						continue;
					/*case EditSetConditionRules.OnVariable:
					case EditSetConditionRules.OnVariableNot:
						continue;*/
					// Action rules (can contain regular expressions)
					case EditSetActionRules.AddRedirect:
					case EditSetActionRules.AddInternalRedirect:
					case EditSetActionRules.AddFind:
					case EditSetActionRules.AddReplace:
						CheckRegExp(Line);
						Edits.Add(new EditSetRule(Line.Key, Line.Value));
						break;
					// Action rules (no value verification)
					case EditSetActionRules.AddConvert:
					case EditSetActionRules.AddConvertDest:
					case EditSetActionRules.AddConvertArg1:
					case EditSetActionRules.AddConvertArg2:
					case EditSetActionRules.AddRequestHeaderFind:
					case EditSetActionRules.AddRequestHeaderReplace:
					case EditSetActionRules.AddResponseHeaderFind:
					case EditSetActionRules.AddResponseHeaderReplace:
					case EditSetActionRules.AddHeaderDumping:
					case EditSetActionRules.AddRequestDumping:
					case EditSetActionRules.AddDumping:
					case EditSetActionRules.AddOutputEncoding:
					case EditSetActionRules.AddTranslit:
					case EditSetActionRules.AddDebugPrint:
						Edits.Add(new EditSetRule(Line.Key, Line.Values ?? new string[1] { Line.Value }));
						break;
					// Action rules (with value verification)
					case EditSetActionRules.AddHeader:
					case EditSetActionRules.AddRequestHeader:
					case EditSetActionRules.AddResponseHeader:
						if (Line.Value.Contains(": "))
							Edits.Add(new EditSetRule(Line.Key, Line.Values ?? new string[1] { Line.Value }));
						else
							new LogWriter().WriteLine(true, false, "Warning: Incorrect HTTP header at {0}. Line ignored.", Line.Location);
						break;
					case EditSetActionRules.AddRequestHttpVersion:
						//same as [Server]/RemoteHttpVersion option
						if (System.Text.RegularExpressions.Regex.IsMatch(Line.Value, @"[\d][\.][\d]"))
						{ Edits.Add(new EditSetRule("AddRequestHttpVersion", "=" + Line.Value)); }
						else if (System.Text.RegularExpressions.Regex.IsMatch(Line.Value, @"([=><a])[u0-3][t\.][o0-9]"))
						{ Edits.Add(new EditSetRule("AddRequestHttpVersion", Line.Value)); }
						else
						{ new LogWriter().WriteLine(true, false, "Warning: Incorrect HTTP version at {0}. Line ignored.", Line.Location); }
						break;
					case EditSetActionRules.AddResponseHttpVersion:
						if (Version.TryParse(Line.Value, out Version ver))
							Edits.Add(new EditSetRule(Line.Key, Line.Value));
						else
							new LogWriter().WriteLine(true, false, "Warning: Incorrect HTTP version at {0}. Line ignored.", Line.Location);
						break;
					case EditSetActionRules.AddVariable:
						switch (Line.Values.Length)
						{
							//see HttpTransit.AddVariable(string[]) for details
							case 2:
							case 3:
								Edits.Add(new EditSetRule(Line.Key, Line.Values));
								break;
							case 4:
								if (int.TryParse(Line.Values[3], out int i) && i >= 0)
									Edits.Add(new EditSetRule(Line.Key, Line.Values));
								else
									new LogWriter().WriteLine(true, false, "Warning: invalid group number [4th argument] at {0}. Line ignored.", Line.Location);
								break;
							default:
								new LogWriter().WriteLine(true, false, "Warning: bad count of arguments of AddVariable at {0}. Line ignored.", Line.Location);
								break;
						}
						break;
					default:
						if (Line.Key.StartsWith("Add"))
							new LogWriter().WriteLine(true, false, "Warning: unknown edit action \"{0}\" at {1}. Line ignored.", Line.Key, Line.Location);
						else
							new LogWriter().WriteLine(true, false, "Warning: unknown edit set condition \"{0}\" at {1}. Line ignored.", Line.Key, Line.Location);
						break;
				}
				if (Line.Key.StartsWith("AddConvert")) MayBeForResponse = true;
				if (Line.Key == EditSetActionRules.AddFind) MayBeForResponse = true;
				if (Line.Key == EditSetActionRules.AddReplace) MayBeForResponse = true;
				if (Line.Key == EditSetActionRules.AddInternalRedirect) MayBeForResponse = false;
			}

			ProcessComplexRules(Section.Location);

			//check if the edit set can be runned on HTTP-request time
			if (ContentTypeMasks.Count == 0 && !MayBeForResponse) IsForRequest = true;

			if (UrlMasks.Count == 0) UrlMasks.Add(".*");
		}

		/// <summary>
		/// Process all multi-line rules (like AddFind+AddReplace) to virtual rules (e.g. AddFindReplace)
		/// </summary>
		/// <param name="EditSetLocation">Edit Set's location (for error message if need)</param>
		private void ProcessComplexRules(string EditSetLocation)
		{
			/* List of virtual editing rules, not listed at https://github.com/atauenis/webone/wiki/Sets-of-edits

             * AddFind + AddReplace = AddFindReplace                                             (FindReplaceEditSetRule)
             * AddConvert + AddConvertDest + AddConvertArg1 + AddConvertArg2 = AddConverting     (ConvertEditSetRule)
             * AddHeaderFind + AddHeaderReplace = AddRequestHeaderFindReplace                    (FindReplaceEditSetRule)
             * AddResponseHeaderFind + AddResponseHeaderReplace = AddResponseHeaderFindReplace   (FindReplaceEditSetRule)
             */

			//load all original lines
			List<string> Finds = new();
			List<string> Replacions = new();
			List<string> RequestHeaderFinds = new();
			List<string> RequestHeaderReplacions = new();
			List<string> ResponseHeaderFinds = new();
			List<string> ResponseHeaderReplacions = new();
			string Converter = null;
			string ConvertDest = "";
			string ConvertArg1 = "";
			string ConvertArg2 = "";
			foreach (EditSetRule Rule in Edits)
			{
				switch (Rule.Action)
				{
					case EditSetActionRules.AddFind:
						Finds.Add(Rule.Value);
						break;
					case EditSetActionRules.AddReplace:
						Replacions.Add(Rule.Value);
						break;
					case EditSetActionRules.AddRequestHeaderFind:
						RequestHeaderFinds.Add(Rule.Value);
						break;
					case EditSetActionRules.AddRequestHeaderReplace:
						RequestHeaderReplacions.Add(Rule.Value);
						break;
					case EditSetActionRules.AddResponseHeaderFind:
						ResponseHeaderFinds.Add(Rule.Value);
						break;
					case EditSetActionRules.AddResponseHeaderReplace:
						ResponseHeaderReplacions.Add(Rule.Value);
						break;
					case EditSetActionRules.AddConvert:
						Converter = Rule.Value;
						break;
					case EditSetActionRules.AddConvertDest:
						ConvertDest = Rule.Value;
						break;
					case EditSetActionRules.AddConvertArg1:
						ConvertArg1 = Rule.Value;
						break;
					case EditSetActionRules.AddConvertArg2:
						ConvertArg2 = Rule.Value;
						break;
				}
			}

			//process AddFind, AddReplace -> AddFindReplace
			if (Finds.Count != Replacions.Count)
				Log.WriteLine(true, false, "Warning: Invalid amount of finds/replaces in {0}.", EditSetLocation);
			else if (Finds.Count > 0)
				for (int i = 0; i < Finds.Count; i++)
				{
					Edits.Add(new FindReplaceEditSetRule(EditSetActionRules.AddFindReplace, Finds[i], Replacions[i]));
				}

			//process AddHeaderFind, AddHeaderReplace -> AddRequestHeaderFindReplace
			if (RequestHeaderFinds.Count != RequestHeaderReplacions.Count)
				Log.WriteLine(true, false, "Warning: Invalid amount of request header finds/replaces in {0}.", EditSetLocation);
			else if (RequestHeaderFinds.Count > 0)
				for (int i = 0; i < RequestHeaderFinds.Count; i++)
				{
					Edits.Add(new FindReplaceEditSetRule(EditSetActionRules.AddRequestHeaderFindReplace, RequestHeaderFinds[i], RequestHeaderReplacions[i]));
				}

			//process AddResponseHeaderFind, AddResponseHeaderReplace -> AddResponseHeaderFindReplace
			if (ResponseHeaderFinds.Count != ResponseHeaderReplacions.Count)
				Log.WriteLine(true, false, "Warning: Invalid amount of response header finds/replaces in {0}.", EditSetLocation);
			else if (ResponseHeaderFinds.Count > 0)
				for (int i = 0; i < ResponseHeaderFinds.Count; i++)
				{
					Edits.Add(new FindReplaceEditSetRule(EditSetActionRules.AddResponseHeaderFindReplace, ResponseHeaderFinds[i], ResponseHeaderReplacions[i]));
				}

			//process AddConvert, AddConvertDest, AddConvertArg1, AddConvertArg2 -> AddConverting
			if (!string.IsNullOrEmpty(Converter))
			{
				//check converter presence and warn if there is no such
				bool CorrectConverter = false;
				foreach (var conv in ConfigFile.Converters)
				{
					if (conv.Executable == Converter)
					{
						Edits.Add(new ConvertEditSetRule(EditSetActionRules.AddConverting, Converter, ConvertDest, ConvertArg1, ConvertArg2));
						CorrectConverter = true;
						break;
					}
				}
				if (!CorrectConverter) Log.WriteLine(true, false, @"Warning: Converter ""{1}"" in Edit Set at {0} is not present in lists of converters.", EditSetLocation, Converter);

			}
			else if (ConvertDest != "" || ConvertArg1 != "" || ConvertArg2 != "")
			{
				Log.WriteLine(true, false, "Warning: Please add AddConvert rule to Edit Set starting at {0} to use converting.", EditSetLocation);
			}
		}

		/// <summary>
		/// Test validness of a Regular Expression pattern in a rule line
		/// </summary>
		/// <param name="RegExpLine">Rule line</param>
		/// <returns><see cref="True"/> if RegExp is valid or <see cref="False"/> if it is invalid</returns>
		private bool CheckRegExp(ConfigFileOption RegExpLine) { return Program.CheckRegExp(RegExpLine.Value, RegExpLine.Location); }

		//test function
		public override string ToString()
		{
			string Str = "[Edit:" + UrlMasks[0] + "]\n";
			if (UrlMasks.Count > 1) for (int i = 1; i < UrlMasks.Count; i++) Str += "OnUrl=" + UrlMasks[i] + "\n";
			foreach (var imask in UrlIgnoreMasks) Str += "IgnoreUrl=" + "=" + imask + "\n";
			foreach (var ctmask in ContentTypeMasks) Str += "OnContentType=" + "=" + ctmask + "\n";
			foreach (var hmask in HeaderMasks) Str += "OnHeader=" + "=" + hmask + "\n";
			foreach (var edit in Edits) Str += edit.Action + "=" + edit.Value + "\n";
			return Str;
		}
	}

	// Condition and action rules of edit sets
	// See https://github.com/atauenis/webone/wiki/Sets-of-edits

	/// <summary>
	/// Names of condition rules for traffic edit sets
	/// </summary>
	static class EditSetConditionRules
	{
		public const string OnUrl = "OnUrl";
		public const string OnCode = "OnCode";
		public const string IgnoreUrl = "IgnoreUrl";
		public const string OnContentType = "OnContentType";
		public const string OnHeader = "OnHeader";
		public const string OnHostOS = "OnHostOS";
		public const string OnHttpOnly = "OnHttpOnly";
		public const string OnHttpsOnly = "OnHttpsOnly";
		public const string OnUser = "OnUser";
	}

	/// <summary>
	/// Names of action rules for traffic edit sets
	/// </summary>
	static class EditSetActionRules
	{
		public const string AddRedirect = "AddRedirect";
		public const string AddInternalRedirect = "AddInternalRedirect";
		public const string AddFind = "AddFind"; //configfile only
		public const string AddReplace = "AddReplace"; //configfile only
		public const string AddFindReplace = "AddFindReplace";
		public const string AddConvert = "AddConvert"; //configfile only
		public const string AddConvertDest = "AddConvertDest"; //configfile only
		public const string AddConvertArg1 = "AddConvertArg1"; //configfile only
		public const string AddConvertArg2 = "AddConvertArg2"; //configfile only
		public const string AddConverting = "AddConverting";
		public const string AddRequestHeaderFind = "AddRequestHeaderFind"; //configfile only
		public const string AddRequestHeaderReplace = "AddRequestHeaderReplace"; //configfile only
		public const string AddRequestHeaderFindReplace = "AddRequestHeaderFindReplace";
		public const string AddResponseHeaderFind = "AddResponseHeaderFind"; //configfile only
		public const string AddResponseHeaderReplace = "AddResponseHeaderReplace"; //configfile only
		public const string AddResponseHeaderFindReplace = "AddResponseHeaderFindReplace";
		public const string AddHeaderDumping = "AddHeaderDumping"; //equal to AddDumping
		public const string AddRequestDumping = "AddRequestDumping"; //equal to AddDumping
		public const string AddDumping = "AddDumping";
		public const string AddOutputEncoding = "AddOutputEncoding";
		public const string AddTranslit = "AddTranslit";
		public const string AddDebugPrint = "AddDebugPrint";
		public const string AddHeader = "AddHeader";
		public const string AddRequestHeader = "AddRequestHeader";
		public const string AddResponseHeader = "AddResponseHeader";
		public const string AddRequestHttpVersion = "AddRequestHttpVersion";
		public const string AddResponseHttpVersion = "AddResponseHttpVersion";
		public const string AddVariable = "AddVariable";
	}
}
