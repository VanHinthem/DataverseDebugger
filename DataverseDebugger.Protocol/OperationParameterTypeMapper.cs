using System.Collections.Generic;
using System.Text;

namespace DataverseDebugger.Protocol
{
    /// <summary>
    /// Provides helpers for translating Dataverse metadata hints into <see cref="OperationParameterType"/> constants.
    /// </summary>
    public static class OperationParameterTypeMapper
    {
        private static readonly Dictionary<string, int> ActionTypeLabelMap = new()
        {
            ["boolean"] = OperationParameterType.Boolean,
            ["bool"] = OperationParameterType.Boolean,
            ["datetime"] = OperationParameterType.DateTime,
            ["date"] = OperationParameterType.DateTime,
            ["dateandtime"] = OperationParameterType.DateTime,
            ["decimal"] = OperationParameterType.Decimal,
            ["double"] = OperationParameterType.Float,
            ["float"] = OperationParameterType.Float,
            ["single"] = OperationParameterType.Float,
            ["integer"] = OperationParameterType.Integer,
            ["int"] = OperationParameterType.Integer,
            ["int32"] = OperationParameterType.Integer,
            ["money"] = OperationParameterType.Money,
            ["string"] = OperationParameterType.String,
            ["memo"] = OperationParameterType.String,
            ["text"] = OperationParameterType.String,
            ["multilinetext"] = OperationParameterType.String,
            ["guid"] = OperationParameterType.Guid,
            ["uniqueidentifier"] = OperationParameterType.Guid,
            ["picklist"] = OperationParameterType.Picklist,
            ["optionset"] = OperationParameterType.Picklist,
            ["optionsetvalue"] = OperationParameterType.Picklist,
            ["entity"] = OperationParameterType.Entity,
            ["record"] = OperationParameterType.Entity,
            ["entitycollection"] = OperationParameterType.EntityCollection,
            ["collection"] = OperationParameterType.EntityCollection,
            ["partylist"] = OperationParameterType.EntityCollection,
            ["entityreference"] = OperationParameterType.EntityReference,
            ["reference"] = OperationParameterType.EntityReference,
            ["lookup"] = OperationParameterType.EntityReference,
            ["customer"] = OperationParameterType.EntityReference,
            ["owner"] = OperationParameterType.EntityReference,
            ["stringarray"] = OperationParameterType.StringArray
        };

        /// <summary>
        /// Maps an assembly-qualified parser type name (as exposed by classic SDK metadata) to a parameter type constant.
        /// </summary>
        /// <param name="parser">The parser type string.</param>
        /// <returns>The corresponding <see cref="OperationParameterType"/> value, or null when unknown.</returns>
        public static int? FromParser(string? parser)
        {
            if (string.IsNullOrWhiteSpace(parser))
            {
                return null;
            }

            var parserValue = parser!;
            var parts = parserValue.Split(',');
            var typeCandidate = parts.Length > 0 ? parts[0] : parserValue;
            var typeName = typeCandidate.Trim();

            return typeName switch
            {
                "System.Boolean" => OperationParameterType.Boolean,
                "System.DateTime" => OperationParameterType.DateTime,
                "System.DateTimeOffset" => OperationParameterType.DateTime,
                "System.Decimal" => OperationParameterType.Decimal,
                "System.Double" => OperationParameterType.Float,
                "System.Single" => OperationParameterType.Float,
                "System.Int32" => OperationParameterType.Integer,
                "System.String" => OperationParameterType.String,
                "System.Guid" => OperationParameterType.Guid,
                "Microsoft.Xrm.Sdk.OptionSetValue" => OperationParameterType.Picklist,
                "Microsoft.Xrm.Sdk.Money" => OperationParameterType.Money,
                "Microsoft.Xrm.Sdk.Entity" => OperationParameterType.Entity,
                "Microsoft.Xrm.Sdk.EntityCollection" => OperationParameterType.EntityCollection,
                "Microsoft.Xrm.Sdk.EntityReference" => OperationParameterType.EntityReference,
                _ => (int?)null
            };
        }

        /// <summary>
        /// Maps the formatted label (from FetchXML/Web API annotations) for an action parameter type to a parameter constant.
        /// </summary>
        /// <param name="label">The formatted type label.</param>
        /// <returns>The corresponding <see cref="OperationParameterType"/> value, or null when unknown.</returns>
        public static int? FromFormattedActionType(string? label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return null;
            }

            var normalized = NormalizeLabel(label!);
            if (normalized.Length == 0)
            {
                return null;
            }

            return ActionTypeLabelMap.TryGetValue(normalized, out var mapped) ? mapped : (int?)null;
        }

        private static string NormalizeLabel(string label)
        {
            var builder = new StringBuilder(label.Length);
            foreach (var ch in label)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(char.ToLowerInvariant(ch));
                }
            }

            return builder.ToString();
        }
    }
}
