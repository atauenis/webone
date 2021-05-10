using System;
using System.Collections.Generic;
using System.Text;

namespace WebOne
{
	/// <summary>
	/// A real editing rule in a <see cref="EditSet"/> or base for virtual editing rules.<br/>
	/// See also: <seealso cref="FindReplaceEditSetRule"/>, <seealso cref="ConvertEditSetRule"/>
	/// </summary>
	class EditSetRule
	{
		/// <summary>
		/// Rule's action
		/// </summary>
		public string Action { get; internal set; }

		/// <summary>
		/// Action's parameter (single)
		/// </summary>
		public string Value { get; internal set; }

		public EditSetRule(string action, string value)
		{
			Action = action;
			Value = value;
		}
	}

	//virtual editing rules (generated from multiple webone.conf lines)

	/// <summary>
	/// Content Find&amp;Replace virtual editing rule
	/// </summary>
	class FindReplaceEditSetRule : EditSetRule 
	{
		public string Find { get; internal set; }

		public string Replace { get; internal set; }

		public FindReplaceEditSetRule(string action, string find, string replace) : base (action, null)
		{
			Action = action;
			Find = find;
			Replace = replace;
		}
	}

	/// <summary>
	/// Format converting virtual editing rule
	/// </summary>
	class ConvertEditSetRule : EditSetRule
	{
		public string Converter { get; internal set; }

		public string ConvertDest { get; internal set; }

		public string ConvertArg1 { get; internal set; }

		public string ConvertArg2 { get; internal set; }

		public ConvertEditSetRule(string action, string converter, string dest, string arg1, string arg2) : base(action, null)
		{
			Action = action;
			Converter = converter;
			ConvertDest = dest;
			ConvertArg1 = arg1;
			ConvertArg2 = arg2;
		}

	}
}
