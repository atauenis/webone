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
		/// Construct this section's option
		/// </summary>
		/// <param name="RawString">The line raw string.</param>
		/// <param name="Location">The line's location (file name, line number).</param>
		public ConfigFileOption(string RawString, string Location)
		{
			this.RawString = RawString;
			this.Location = Location;

			if(RawString.Contains("=")){
				int SplitPosition = RawString.IndexOf('=');
				Key = RawString.Substring(0, SplitPosition);
				Value = RawString.Substring(SplitPosition + 1);
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
		public ConfigFileSection(string RawString, string Location){
			if (!(RawString.StartsWith('[') && RawString.EndsWith(']'))) throw new Exception("Invalid section title.");
			Title = RawString.Substring(1, RawString.Length - 2);
			this.Location = Location;
			Options = new List<ConfigFileOption>();

			if(Title.Contains(":"))
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
