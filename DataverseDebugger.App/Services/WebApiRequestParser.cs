using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Xml;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.UriParser;

namespace DataverseDebugger.App.Services
{
    /// <summary>
    /// Parsed Web API request information.
    /// </summary>
    internal sealed class ParsedWebApiRequest
    {
        /// <summary>Gets the primary message name.</summary>
        public string MessageName { get; init; } = string.Empty;
        /// <summary>Gets candidate message names for plugin matching.</summary>
        public List<string> MessageCandidates { get; init; } = new List<string>();
        /// <summary>Gets the primary entity logical name.</summary>
        public string? PrimaryEntity { get; init; }
        /// <summary>Gets the OData entity set name.</summary>
        public string? EntitySetName { get; init; }
    }

    /// <summary>
    /// Parses Dataverse Web API URLs into message and entity information.
    /// </summary>
    /// <remarks>
    /// Uses OData EDM model to parse URIs and determine the appropriate
    /// SDK message name (Create, Update, Retrieve, etc.) and target entity.
    /// </remarks>
    internal sealed class WebApiRequestParser
    {
        private readonly object _lock = new object();
        private string? _metadataPath;
        private DateTime _metadataWriteUtc;
        private IEdmModel? _model;

        /// <summary>
        /// Sets the path to the OData metadata file.
        /// </summary>
        public void SetMetadataPath(string? path)
        {
            lock (_lock)
            {
                _metadataPath = path;
                _metadataWriteUtc = DateTime.MinValue;
                _model = null;
            }
        }

        public bool TryParse(string method, string url, out ParsedWebApiRequest parsed, out string? error)
        {
            parsed = new ParsedWebApiRequest();
            error = null;

            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            if (url.IndexOf("/$batch", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                parsed = new ParsedWebApiRequest
                {
                    MessageName = "ExecuteMultiple",
                    MessageCandidates = BuildMessageCandidates("ExecuteMultiple")
                };
                return true;
            }

            if (!TryGetLocalPathWithQuery(url, out var localPathWithQuery))
            {
                return false;
            }

            if (!TryEnsureModel(out var model, out error))
            {
                return false;
            }

            if (!TryGetRelativePath(localPathWithQuery, out var relativePath))
            {
                return false;
            }

            try
            {
                var parser = new ODataUriParser(model, new Uri(relativePath, UriKind.Relative))
                {
                    Resolver = new AlternateKeysODataUriResolver(model)
                };
                var path = parser.ParsePath();
                parsed = BuildParsedRequest(method, path);
                return parsed.MessageCandidates.Count > 0;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private bool TryEnsureModel([NotNullWhen(true)] out IEdmModel? model, out string? error)
        {
            model = null;
            error = null;

            string? path;
            lock (_lock)
            {
                path = _metadataPath;
            }

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                error = "Metadata file not found.";
                return false;
            }

            var lastWriteUtc = File.GetLastWriteTimeUtc(path);
            lock (_lock)
            {
                if (_model != null && _metadataWriteUtc == lastWriteUtc)
                {
                    model = _model;
                    return true;
                }
            }

            try
            {
                using var stream = File.OpenRead(path);
                using var reader = XmlReader.Create(stream);
                if (!CsdlReader.TryParse(reader, out model, out var errors))
                {
                    error = errors?.FirstOrDefault()?.ToString() ?? "Failed to parse metadata.";
                    return false;
                }

                lock (_lock)
                {
                    _model = model;
                    _metadataWriteUtc = lastWriteUtc;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryGetLocalPathWithQuery(string url, out string localPathWithQuery)
        {
            localPathWithQuery = string.Empty;
            if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
            {
                return false;
            }

            if (uri.IsAbsoluteUri)
            {
                localPathWithQuery = uri.LocalPath + uri.Query;
            }
            else
            {
                localPathWithQuery = uri.OriginalString;
                if (!localPathWithQuery.StartsWith("/", StringComparison.Ordinal))
                {
                    localPathWithQuery = "/" + localPathWithQuery;
                }
            }

            return localPathWithQuery.IndexOf("/api/data/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryGetRelativePath(string localPathWithQuery, out string relative)
        {
            relative = string.Empty;
            var apiIndex = localPathWithQuery.IndexOf("/api/data/", StringComparison.OrdinalIgnoreCase);
            if (apiIndex < 0)
            {
                return false;
            }

            var after = localPathWithQuery.Substring(apiIndex + "/api/data/".Length);
            var slashIndex = after.IndexOf('/');
            if (slashIndex < 0 || slashIndex + 1 >= after.Length)
            {
                return false;
            }

            relative = "/" + after.Substring(slashIndex + 1);
            return true;
        }

        private static ParsedWebApiRequest BuildParsedRequest(string method, ODataPath path)
        {
            var methodUpper = method?.ToUpperInvariant() ?? string.Empty;

            string? entitySet = null;
            string? primaryEntity = null;
            if (path.FirstSegment is EntitySetSegment entitySetSegment)
            {
                entitySet = entitySetSegment.Identifier;
                primaryEntity = entitySetSegment.EntitySet?.EntityType()?.Name ?? entitySetSegment.Identifier;
                if (!string.IsNullOrWhiteSpace(primaryEntity) && primaryEntity.Contains("."))
                {
                    primaryEntity = primaryEntity.Substring(primaryEntity.LastIndexOf('.') + 1);
                }
            }

            string? message = null;
            if (path.LastSegment is OperationSegment operationSegment)
            {
                message = operationSegment.Identifier;
            }
            else if (path.LastSegment is OperationImportSegment operationImportSegment)
            {
                message = operationImportSegment.Identifier;
            }
            else
            {
                switch (methodUpper)
                {
                    case "GET":
                        message = path.Any(segment => segment is KeySegment) ? "Retrieve" : "RetrieveMultiple";
                        break;
                    case "POST":
                        message = "Create";
                        break;
                    case "PATCH":
                    case "MERGE":
                    case "PUT":
                        message = "Update";
                        break;
                    case "DELETE":
                        message = "Delete";
                        break;
                }
            }

            var messageCandidates = BuildMessageCandidates(message);
            return new ParsedWebApiRequest
            {
                MessageName = message ?? string.Empty,
                MessageCandidates = messageCandidates,
                PrimaryEntity = primaryEntity,
                EntitySetName = entitySet
            };
        }

        private static List<string> BuildMessageCandidates(string? message)
        {
            var candidates = new List<string>();
            if (string.IsNullOrWhiteSpace(message))
            {
                return candidates;
            }

            candidates.Add(message);
            if (message.Contains("."))
            {
                var shortName = message.Substring(message.LastIndexOf('.') + 1);
                if (!string.IsNullOrWhiteSpace(shortName))
                {
                    candidates.Add(shortName);
                }
            }

            return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
