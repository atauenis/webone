using System;
using System.Collections.Generic;
using System.Text;

namespace WebOne
{
	/// <summary>
	/// Editing rule of an <see cref="EditSet"/>
	/// </summary>
	class EditSetRule
	{
		/// <summary>
		/// Rule's action
		/// </summary>
		public string Action { get; private set; }

		/// <summary>
		/// Action's parameter (single)
		/// </summary>
		public string Value { get; private set; }

		public EditSetRule(string action, string value)
		{
			Action = action;
			Value = value;
		}
	}

	//UNDONE: special rules for AddFind+AddReplace, AddConvert, etc (in future)
	/*class FindReplaceEditSetRule : EditSetRule 
	{

	}

	class ConvertEditSetRule : EditSetRule
	{

	}*/
}
