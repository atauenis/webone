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
        /// List of masks of HTTP request headers on which the Set would not be used [OnHeader]
        /// </summary>
        public List<string> HeaderMasks { get; set; }

        /// <summary>
        /// Flag that indicates that the edits can be performed at time of HTTP Request (before get of response)
        /// </summary>
        public bool IsForRequest { get; private set; }

        /// <summary>
        /// List of edits that would be performed on the need content
        /// </summary>
        public List<EditSetRule> Edits { get; set; }

        /// <summary>
        /// Create a Set of edits from a section from webone.conf
        /// </summary>
        /// <param name="Section">The webone.conf section</param>
        public EditSet(ConfigFileSection Section){
            UrlMasks = new List<string>();
            UrlIgnoreMasks = new List<string>();
            ContentTypeMasks = new List<string>();
            HeaderMasks = new List<string>();
            Edits = new List<EditSetRule>();
            IsForRequest = false;

            bool MayBeForResponse = false; //does this set containing tasks for HTTP response processing?


            if(Section.Mask != null) { UrlMasks.Add(Section.Mask); }

            foreach(var Line in Section.Options)
            {
                if (!Line.HaveKeyValue) continue;
                switch(Line.Key)
                {
                    //detection rules
                    case "OnUrl":
                        UrlMasks.Add(Line.Value);
                        continue;
                    case "OnCode":
                        OnCode = int.Parse(Line.Value);
                        continue;
                    case "IgnoreUrl":
                        UrlIgnoreMasks.Add(Line.Value);
                        continue;
                    case "OnContentType":
                        ContentTypeMasks.Add(Line.Value);
                        continue;
                    case "OnHeader":
                        HeaderMasks.Add(Line.Value);
                        continue;
                    //editing rules
                    case "AddRedirect":
                    case "AddInternalRedirect":
                    case "AddHeader":
                    case "AddResponseHeader":
                    case "AddConvert":
                    case "AddConvertDest":
                    case "AddConvertArg1":
                    case "AddConvertArg2":
                    case "AddFind":
                    case "AddReplace":
                    case "AddHeaderDumping":
                    case "AddRequestDumping":
                    case "AddOutputEncoding":
                    case "AddTranslit":
                        Edits.Add(new EditSetRule(Line.Key, Line.Value));
                        break;
                    default:
                        if (Line.Key.StartsWith("Add"))
                            new LogWriter().WriteLine(true, false, "Warning: unknown editing rule \"{0}\".", Line.Key);
                        else
                            new LogWriter().WriteLine(true, false, "Warning: unknown detection rule \"{0}\".", Line.Key);
                        break;

                    //UNDONE: implement   case "Add*":  for each possible kind of multi-line rules
                }
                if (Line.Key.StartsWith("AddConvert")) MayBeForResponse = true;
                if (Line.Key == "AddContentType") MayBeForResponse = true;
                if (Line.Key == "AddFind") MayBeForResponse = true;
                if (Line.Key == "AddReplace") MayBeForResponse = true;
                if (Line.Key == "AddInternalRedirect") MayBeForResponse = false;
            }

            ProcessComplexRules(Section.Location);

            //check if the edit set can be runned on HTTP-request time
            if (ContentTypeMasks.Count == 0 && !MayBeForResponse) IsForRequest = true;

            if (UrlMasks.Count == 0) UrlMasks.Add(".*");
        }

        /// <summary>
        /// Process multi-line rules (like AddFind+AddReplace) to a virtual rule
        /// </summary>
        /// <param name="EditSetLocation">Edit Set's location (for error message if need)</param>
		private void ProcessComplexRules(string EditSetLocation)
        {
            //process AddFind, AddReplace -> AddFindReplace
            List<string> Finds = new List<string>();
            List<string> Replacions = new List<string>();
            foreach (EditSetRule Rule in Edits)
            {
                switch (Rule.Action)
                {
                    case "AddFind":
                        Finds.Add(Rule.Value);
                        break;
                    case "AddReplace":
                        Replacions.Add(Rule.Value);
                        break;
                }
            }
            if (Finds.Count != Replacions.Count)
                Log.WriteLine(true,false,"Warning: Invalid amount of Find/Replace in {0}.", EditSetLocation);
            else if (Finds.Count > 0)
                for (int i = 0; i < Finds.Count; i++)
                {
                    Edits.Add(new FindReplaceEditSetRule("AddFindReplace", Finds[i], Replacions[i]));
                }

            //UNDONE:
            //AddHeaderFind, AddHeaderReplace -> AddHeaderFindReplace (FindReplaceEditSetRule)
            //AddRequestHeaderFind, AddRequestHeaderReplace -> AddRequestHeaderFindReplace (FindReplaceEditSetRule)
            //then both are need to be implemented in Transit

            //UNDONE: AddConvert, AddConvertDest, AddConvertArg1, AddConvertArg2 -> AddConverting (ConvertEditSetRule)
        }


        /* List of virtual editing rules, not listed at https://github.com/atauenis/webone/wiki/Sets-of-edits
           TODO: Create a wiki article for devs?
          
         * AddFind + AddReplace = AddFindReplace
         * 
         * continue list when implement other?
         */

        //test function
        public override string ToString()
        {
            string Str = "[Edit:"+UrlMasks[0]+"]\n";
            if (UrlMasks.Count > 1) for(int i = 1; i < UrlMasks.Count; i++) Str += "OnUrl=" + UrlMasks[i] + "\n";
            foreach (var imask in UrlIgnoreMasks) Str += "IgnoreUrl=" + "=" + imask + "\n";
            foreach (var ctmask in ContentTypeMasks) Str += "OnContentType=" + "=" + ctmask + "\n";
            foreach (var hmask in HeaderMasks) Str += "OnHeader=" + "=" + hmask + "\n";
            foreach (var edit in Edits) Str += edit.Action + "=" + edit.Value + "\n";
            return Str;
        }
    }

}
