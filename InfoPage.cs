using System.IO;
using System.Net;

namespace WebOne
{
	/// <summary>
	/// An information page (used by internal status and other pages inside WebOne)
	/// </summary>
	public class InfoPage
	{
		/// <summary>
		/// The information page title
		/// </summary>
		public string Title { get; set; }
		/// <summary>
		/// The information page 1st level header (or null if no title)
		/// </summary>
		public string Header { get; set; }
		/// <summary>
		/// The information page content (HTML)
		/// </summary>
		public string Content { get; set; }

		/// <summary>
		/// Attached binary file (used instead of the page body)
		/// </summary>
		public Stream Attachment { get; set; }

		public string AttachmentContentType
		{
			get { return HttpHeaders["Content-Type"]; }
			set { HttpHeaders["Content-Type"] = value; }
		}

		/// <summary>
		/// Additional HTTP headers, which can be sent to the client
		/// </summary>
		public WebHeaderCollection HttpHeaders { get; set; }

		/// <summary>
		/// HTTP status code ("200 OK" by default)
		/// </summary>
		public int HttpStatusCode { get; set; }

		/// <summary>
		/// Show the WebOne &amp; OS version in the page footer
		/// </summary>
		public bool ShowFooter { get; set; }

		/// <summary>
		/// Add default CSS styles to the page HTML header
		/// </summary>
		public bool AddCss { get; set; }

		/// <summary>
		/// Specify additional HTML headers (before body tag)
		/// </summary>
		public string HtmlHeaders { get; set; }

		/// <summary>
		/// Create an information page
		/// </summary>
		/// <param name="Title">The information page title</param>
		/// <param name="Header">The information page 1st level header (or null if no title)</param>
		/// <param name="Content">The information page content (HTML)</param>
		/// <param name="HttpStatusCode">The information page HTTP status code (200/404/302/500/etc)</param>
		public InfoPage(string Title = null, string Header = null, string Content = "No description is available.", int HttpStatusCode = 200)
		{
			this.HttpStatusCode = HttpStatusCode;
			this.HttpHeaders = new WebHeaderCollection();
			this.HtmlHeaders = "";
			this.Title = Title;
			this.Header = Header;
			this.Content = Content;
			ShowFooter = true;
			AddCss = true;
		}
	}
}
