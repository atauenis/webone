using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static WebOne.Program;

namespace WebOne
{
    /// <summary>
    /// Set of edits for particular pages
    /// </summary>
    class EditSet
    {
        /// <summary>
        /// List of masks of URLs on which the Set whould be used
        /// </summary>
        public List<string> UrlMasks { get; set; }

        /// <summary>
        /// List of masks of URLs on which the Set would not be used
        /// </summary>
        public List<string> UrlIgnoreMasks { get; set; }

        /// <summary>
        /// List of masks of MIME Content-Types on which the Set would be used
        /// </summary>
        public List<string> ContentTypeMasks { get; set; }

        /// <summary>
        /// Flag that indicates that the edits can be performed at time of HTTP Request (before get of response)
        /// </summary>
        public bool IsForRequest { get; set; }

        /// <summary>
        /// List of edits that would be performed on the need content
        /// </summary>
        public List<KeyValuePair<string, string>> Edits { get; set; }


        /// <summary>
        /// Create a Set of edits from a raw INI-like source
        /// </summary>
        /// <param name="RawEdits">INI-like list of edits (from webone.conf)</param>
        public EditSet(List<string> RawEdits)
        {
            UrlMasks = new List<string>();
            UrlIgnoreMasks = new List<string>();
            ContentTypeMasks = new List<string>();
            Edits = new List<KeyValuePair<string, string>>();
            IsForRequest = false;

            bool MayBeForResponse = false; //does this set containing tasks for HTTP response processing?

            foreach(string EditRule in RawEdits)
            {
                int BeginValue = EditRule.IndexOf("=");
                if (BeginValue < 1) continue; //ignore bad lines
                string ParamName = EditRule.Substring(0, BeginValue);
                string ParamValue = EditRule.Substring(BeginValue + 1);

                switch (ParamName)
                {
                    case "OnUrl":
                        UrlMasks.Add(ParamValue);
                        continue;
                    case "IgnoreUrl":
                        UrlIgnoreMasks.Add(ParamValue);
                        continue;
                    case "OnContentType":
                        ContentTypeMasks.Add(ParamValue);
                        continue;
                    default:
                        if (ParamName.StartsWith("Add"))
                            Edits.Add(new KeyValuePair<string, string>(ParamName, ParamValue));
                        else
                            Console.WriteLine("Warning: unknown mask \"{0}\" will be ignored.", ParamName);

                        if (ParamName.StartsWith("AddConvert")) MayBeForResponse = true;
                        if (ParamName == "AddContentType") MayBeForResponse = true;
                        if (ParamName == "AddFind") MayBeForResponse = true;
                        if (ParamName == "AddReplace") MayBeForResponse = true;
                        if (ParamName == "AddInternalRedirect") MayBeForResponse = false;

                        continue;
                }
            }

            //check if the edit set can be runned on HTTP-request time
            if (ContentTypeMasks.Count == 0 && !MayBeForResponse) IsForRequest = true;

            if (UrlMasks.Count == 0) UrlMasks.Add(".*");
        }
    }

}
