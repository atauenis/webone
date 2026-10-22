using System;
using System.Collections.Generic;
using System.Text;

namespace WebOne
{
	/// <summary>
	/// Configuration file section's option (list entry).
	/// </summary>
	class ConfigFileOption : ConfigFileEntry
	{
		/// <summary>
		/// The option's key (if any).
		/// </summary>
		public string Key { get; private set; }

		/// <summary>
		/// The option's value or raw line content.
		/// </summary>
		public string Value { get; private set; }

		/// <summary>
		/// The option's source line string.
		/// </summary>
		public string RawString { get; private set; }

		/// <summary>
		/// Does the option have a key&amp;value pair (<i>true</i>) or it is a simple list line (<i>false</i>).
		/// </summary>
		public bool HaveKeyValue => !(String.IsNullOrEmpty(Key));

		/// <summary>
		/// The option's all values
		/// </summary>
		public string[] Values { get; private set; }

		/// <summary>
		/// Construct this section's option
		/// </summary>
		/// <param name="RawString">The line raw string.</param>
		/// <param name="Location">The line's location (file name, line number).</param>
		public ConfigFileOption(string RawString, string Location)
		{
			this.RawString = RawString;
			this.Location = Location;

			if (RawString.Contains("="))
			{
				int SplitPosition = RawString.IndexOf('=');
				Key = RawString.Substring(0, SplitPosition);
				Value = RawString.Substring(SplitPosition + 1);

				List<string> values = new();

				//if the value uses character escaping
				if (Value.StartsWith('\"') || Value.StartsWith('\\'))
				{
					StringBuilder sb = new();
					bool Escape = false;
					bool StringStarted = (Value[0] == '\"');
					bool PartsGapStarted = false;
					for (int i = 1; i < Value.Length; i++)
					{
						if (!PartsGapStarted)
						{
							//value
							if (!Escape && Value[i] == '\\') { Escape = true; continue; }
							if (Escape)
							{
								//process escaped characters
								Escape = false;
								switch (Value[i])
								{
									case '\'': sb.Append('\''); continue;
									case '\"': sb.Append('\"'); continue;
									case '\\': sb.Append('\\'); continue;
									case '0': sb.Append('\0'); continue;
									case 'a': sb.Append('\a'); continue;
									case 'b': sb.Append('\b'); continue;
									case 'f': sb.Append('\f'); continue;
									case 'n': sb.Append('\n'); continue;
									case 'r': sb.Append('\r'); continue;
									case 't': sb.Append('\t'); continue;
									case 'v': sb.Append('\v'); continue;
									default: throw new Exception("Unknown escape sequence: " + Value[i]);
										// Unicode is not supported at this moment
								}
							}
							else
							{
								//process regular characters or end mark
								if (Value[i] == '\"' && StringStarted)
								{
									//process string end mark
									values.Add(sb.ToString());

									sb.Clear();
									PartsGapStarted = true;
									StringStarted = false;
								}
								else
								{
									//process regular characters 
									sb.Append(Value[i]);
								}
							}
						}
						else
						{
							//gap between multiple values
							bool EndReached = false;
							switch (Value[i])
							{
								case ' ':
								case '\t':
								case ',':
									continue;
								case '\"':
									PartsGapStarted = false;
									StringStarted = true;
									break;
								case ';':
									EndReached = true;
									break;
								default: throw new Exception("Garbage in the middle gap: " + Value[i]);
							}
							if (EndReached) break;
						}
					}

					if (values.Count == 0)
					{
						values.Add(sb.ToString());
					}
					Values = values.ToArray();

					Value = "";
					foreach (string val in values) { Value += val; }
				}
			}
			else { Key = null; Value = RawString; }
		}

		public override string ToString() { return RawString; }
	}

	/// <summary>
	/// Configuration file section.
	/// </summary>
	class ConfigFileSection : ConfigFileEntry
	{
		/// <summary>
		/// The section's title.
		/// </summary>
		public string Title { get; private set; }

		/// <summary>
		/// The section's kind (for masked sections) or its title.
		/// </summary>
		public string Kind { get; private set; }

		/// <summary>
		/// The section's mask (for masked sections) or <i>null</i>.
		/// </summary>
		public string Mask { get; private set; }

		/// <summary>
		/// The section's options (or list entries).
		/// </summary>
		public List<ConfigFileOption> Options { get; private set; }

		/// <summary>
		/// Construct a section.
		/// </summary>
		/// <param name="RawString">Section header.</param>
		/// <param name="Location">Section header location (file name, line number).</param>
		public ConfigFileSection(string RawString, string Location)
		{
			if (!(RawString.StartsWith('[') && RawString.EndsWith(']'))) throw new Exception("Invalid section title.");
			Title = RawString.Substring(1, RawString.Length - 2);
			this.Location = Location;
			Options = new List<ConfigFileOption>();

			if (Title.Contains(":"))
			{
				Kind = Title.Substring(0, Title.IndexOf(':'));
				Mask = Title.Substring(Title.IndexOf(':') + 1);
			}
			else
			{
				Kind = Title;
				Mask = null;
			}
		}

		public override string ToString()
		{
			return "[" + Title + "]";
		}
	}

	/// <summary>
	/// Any configuration file entry (section/option/list line).
	/// </summary>
	abstract class ConfigFileEntry
	{
		/// <summary>
		/// The line's location (file name, line number).
		/// </summary>
		public string Location { get; protected set; }
	}
}
