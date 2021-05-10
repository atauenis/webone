using System;
using System.Collections.Generic;
using System.Text;

namespace WebOne
{
	/// <summary>
	/// A real editing rule in a <see cref="EditSet"/> or base for virtual editing rules.<br/>
	/// See also: <seealso cref="FindReplaceEditSetRule"/>
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

	/*class ConvertEditSetRule : EditSetRule
	{

	}*/
}
