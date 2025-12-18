using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.Edm.Validation;

namespace DataverseDebugger.Runner.Conversion.Utils
{
    /// <summary>
    /// Factory for creating DataverseContext instances from OData metadata files.
    /// </summary>
    /// <remarks>
    /// This factory is used for offline/testing scenarios where a full CRM connection
    /// is not available. It creates a context from a saved $metadata CSDL file.
    /// </remarks>
    public static class ConversionContextFactory
    {
        /// <summary>
        /// Attempts to create a DataverseContext from an OData metadata file.
        /// </summary>
        /// <param name="metadataPath">The path to the $metadata CSDL file.</param>
        /// <param name="orgUrl">The organization URL (used to extract the host name).</param>
        /// <param name="context">The created context if successful; null otherwise.</param>
        /// <param name="error">The error message if creation failed; null otherwise.</param>
        /// <returns>True if the context was created successfully; false otherwise.</returns>
        public static bool TryCreate(string metadataPath, string orgUrl, out DataverseContext context, out string error)
        {
            context = null;
            error = null;

            if (string.IsNullOrWhiteSpace(metadataPath))
            {
                error = "Metadata path is empty.";
                return false;
            }

            if (!File.Exists(metadataPath))
            {
                error = "Metadata file not found: " + metadataPath;
                return false;
            }

            try
            {
                IEdmModel model;
                using (var stream = File.OpenRead(metadataPath))
                using (var reader = XmlReader.Create(stream))
                {
                    if (!CsdlReader.TryParse(reader, out model, out IEnumerable<EdmError> errors))
                    {
                        error = "Metadata parse failed: " + string.Join("; ", errors.Select(e => e.ErrorMessage));
                        return false;
                    }
                }

                var host = string.Empty;
                if (!string.IsNullOrWhiteSpace(orgUrl) && Uri.TryCreate(orgUrl, UriKind.Absolute, out var orgUri))
                {
                    host = orgUri.Host;
                }

                var operationSnapshotPath = TryGetOperationSnapshotPath(metadataPath);
                var cache = MetadataCache.CreateFromModel(model, operationSnapshotPath);
                context = new DataverseContext
                {
                    Host = host,
                    Model = model,
                    MetadataCache = cache
                };
                return true;
            }
            catch (Exception ex)
            {
                error = "Metadata parse failed: " + ex.Message;
                return false;
            }
        }

        private static string TryGetOperationSnapshotPath(string metadataPath)
        {
            if (string.IsNullOrWhiteSpace(metadataPath))
            {
                return null;
            }

            var directory = Path.GetDirectoryName(metadataPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return null;
            }

            return Path.Combine(directory, "operationparameters.json");
        }
    }
}
