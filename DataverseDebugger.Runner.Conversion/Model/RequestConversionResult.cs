using System.Collections.Generic;
using Microsoft.Xrm.Sdk;

namespace DataverseDebugger.Runner.Conversion.Model
{
    /// <summary>
    /// Represents the result of converting a Web API request to an SDK OrganizationRequest.
    /// </summary>
    /// <remarks>
    /// This class holds both the original Web API request and the converted OrganizationRequest,
    /// or an error message if conversion failed. It also provides storage for custom data
    /// needed during response conversion (e.g., inner batch request results).
    /// </remarks>
    public class RequestConversionResult
    {
        /// <summary>
        /// Gets or sets the original Web API request that was converted.
        /// </summary>
        public WebApiRequest SrcRequest { get; internal set; }

        /// <summary>
        /// Gets or sets the error message if conversion failed; null if successful.
        /// </summary>
        public string ConvertFailureMessage { get; internal set; }

        /// <summary>
        /// Gets or sets the converted OrganizationRequest; null if conversion failed.
        /// </summary>
        public OrganizationRequest ConvertedRequest { get; internal set; }

        /// <summary>
        /// Gets detailed diagnostic messages captured during request conversion.
        /// </summary>
        public List<string> DebugLog { get; } = new List<string>();

        /// <summary>
        /// Gets a dictionary for storing custom data during the conversion process.
        /// </summary>
        /// <remarks>
        /// Used internally to pass additional context between request and response conversion,
        /// such as inner conversions for batch requests.
        /// </remarks>
        internal Dictionary<string, object> CustomData { get; } = new Dictionary<string, object>();

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestConversionResult"/> class.
        /// </summary>
        internal RequestConversionResult()
        {
        }
    }
}
