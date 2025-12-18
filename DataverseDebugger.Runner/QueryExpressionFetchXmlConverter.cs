using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseDebugger.Runner
{
    /// <summary>
    /// Converts <see cref="QueryExpression"/> trees to FetchXML strings so the runner can replay queries via Web API.
    /// </summary>
    internal static class QueryExpressionFetchXmlConverter
    {
        /// <summary>
        /// Attempts to convert the provided <see cref="QueryExpression"/> into FetchXML.
        /// </summary>
        public static bool TryConvert(QueryExpression query, out string fetchXml)
        {
            fetchXml = string.Empty;
            if (query == null || string.IsNullOrWhiteSpace(query.EntityName))
            {
                return false;
            }

            try
            {
                var builder = new StringBuilder();
                builder.Append("<fetch version=\"1.0\" mapping=\"logical\"");
                if (query.Distinct)
                {
                    builder.Append(" distinct=\"true\"");
                }

                if (query.TopCount.HasValue && query.TopCount.Value > 0)
                {
                    builder.Append(" top=\"").Append(query.TopCount.Value).Append('\"');
                }

                var count = query.PageInfo?.Count;
                if (count.HasValue && count.Value > 0)
                {
                    builder.Append(" count=\"").Append(count.Value).Append('\"');
                }

                builder.Append('>');
                WriteEntity(builder, query.EntityName, query.ColumnSet, query.Criteria, query.Orders, query.LinkEntities);
                builder.Append("</fetch>");
                fetchXml = builder.ToString();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void WriteEntity(StringBuilder builder, string entityName, ColumnSet columns, FilterExpression criteria, DataCollection<OrderExpression> orders, DataCollection<LinkEntity> links)
        {
            builder.Append("<entity name=\"").Append(entityName).Append('\"');
            builder.Append('>');
            WriteColumns(builder, columns);
            WriteOrders(builder, orders);
            WriteFilter(builder, criteria);
            if (links != null)
            {
                foreach (var link in links)
                {
                    WriteLink(builder, link);
                }
            }
            builder.Append("</entity>");
        }

        private static void WriteColumns(StringBuilder builder, ColumnSet columns)
        {
            var names = columns?.Columns;
            if (columns == null || columns.AllColumns || names == null || names.Count == 0)
            {
                builder.Append("<all-attributes />");
                return;
            }

            foreach (var column in names)
            {
                if (string.IsNullOrWhiteSpace(column))
                {
                    continue;
                }

                builder.Append("<attribute name=\"").Append(column).Append("\" />");
            }
        }

        private static void WriteOrders(StringBuilder builder, DataCollection<OrderExpression> orders)
        {
            if (orders == null)
            {
                return;
            }

            foreach (var order in orders)
            {
                if (string.IsNullOrWhiteSpace(order.AttributeName))
                {
                    continue;
                }

                builder.Append("<order attribute=\"").Append(order.AttributeName).Append('\"');
                if (order.OrderType == OrderType.Descending)
                {
                    builder.Append(" descending=\"true\"");
                }

                builder.Append(" />");
            }
        }

        private static void WriteFilter(StringBuilder builder, FilterExpression filter)
        {
            if (!HasFilterContent(filter))
            {
                return;
            }

            var type = filter.FilterOperator == LogicalOperator.Or ? "or" : "and";
            builder.Append("<filter type=\"").Append(type).Append('\"');
            if (filter.IsQuickFindFilter)
            {
                builder.Append(" isquickfindfields=\"true\"");
            }

            builder.Append('>');
            if (filter.Conditions != null)
            {
                foreach (var condition in filter.Conditions)
                {
                    WriteCondition(builder, condition);
                }
            }

            if (filter.Filters != null)
            {
                foreach (var child in filter.Filters)
                {
                    WriteFilter(builder, child);
                }
            }

            builder.Append("</filter>");
        }

        private static bool HasFilterContent(FilterExpression filter)
        {
            if (filter == null)
            {
                return false;
            }

            if (filter.Conditions != null && filter.Conditions.Count > 0)
            {
                return true;
            }

            if (filter.Filters == null)
            {
                return false;
            }

            return filter.Filters.Any(HasFilterContent);
        }

        private static void WriteCondition(StringBuilder builder, ConditionExpression condition)
        {
            if (condition == null || string.IsNullOrWhiteSpace(condition.AttributeName))
            {
                return;
            }

            var op = MapOperator(condition.Operator);
            builder.Append("<condition attribute=\"").Append(condition.AttributeName).Append("\" operator=\"").Append(op).Append('\"');

            if (!RequiresValues(condition.Operator))
            {
                builder.Append(" />");
                return;
            }

            var values = GetConditionValues(condition)?.ToArray() ?? Array.Empty<string>();
            if (SupportsValueAttribute(condition.Operator) && values.Length <= 1)
            {
                var value = values.Length == 1 ? values[0] : string.Empty;
                builder.Append(" value=\"").Append(EscapeXml(value)).Append("\" />");
                return;
            }

            builder.Append('>');
            foreach (var raw in values)
            {
                builder.Append("<value>").Append(EscapeXml(raw)).Append("</value>");
            }

            builder.Append("</condition>");
        }

        private static IEnumerable<string> GetConditionValues(ConditionExpression condition)
        {
            if (condition.Values == null || condition.Values.Count == 0)
            {
                return Array.Empty<string>();
            }

            return condition.Values
                .OfType<object>()
                .Select(value => FormatValue(value, condition.Operator));
        }

        private static bool SupportsValueAttribute(ConditionOperator op)
        {
            switch (op)
            {
                case ConditionOperator.In:
                case ConditionOperator.NotIn:
                case ConditionOperator.Between:
                case ConditionOperator.NotBetween:
                    return false;
                default:
                    return true;
            }
        }

        private static bool RequiresValues(ConditionOperator op)
        {
            switch (op)
            {
                case ConditionOperator.Null:
                case ConditionOperator.NotNull:
                    return false;
                default:
                    return true;
            }
        }

        private static string MapOperator(ConditionOperator op)
        {
            switch (op)
            {
                case ConditionOperator.Equal:
                    return "eq";
                case ConditionOperator.NotEqual:
                    return "ne";
                case ConditionOperator.GreaterThan:
                    return "gt";
                case ConditionOperator.GreaterEqual:
                    return "ge";
                case ConditionOperator.LessThan:
                    return "lt";
                case ConditionOperator.LessEqual:
                    return "le";
                case ConditionOperator.Like:
                case ConditionOperator.Contains:
                case ConditionOperator.ChildOf:
                    return "like";
                case ConditionOperator.NotLike:
                case ConditionOperator.DoesNotContain:
                    return "not-like";
                case ConditionOperator.BeginsWith:
                    return "begins-with";
                case ConditionOperator.DoesNotBeginWith:
                    return "not-begins-with";
                case ConditionOperator.EndsWith:
                    return "ends-with";
                case ConditionOperator.DoesNotEndWith:
                    return "not-ends-with";
                case ConditionOperator.In:
                    return "in";
                case ConditionOperator.NotIn:
                    return "not-in";
                case ConditionOperator.Null:
                    return "null";
                case ConditionOperator.NotNull:
                    return "not-null";
                case ConditionOperator.On:
                    return "on";
                case ConditionOperator.OnOrAfter:
                    return "on-or-after";
                case ConditionOperator.OnOrBefore:
                    return "on-or-before";
                case ConditionOperator.Between:
                    return "between";
                case ConditionOperator.NotBetween:
                    return "not-between";
                default:
                    throw new NotSupportedException($"Condition operator {op} is not supported for FetchXML conversion.");
            }
        }

        private static void WriteLink(StringBuilder builder, LinkEntity link)
        {
            if (link == null || string.IsNullOrWhiteSpace(link.LinkToEntityName) || string.IsNullOrWhiteSpace(link.LinkFromAttributeName) || string.IsNullOrWhiteSpace(link.LinkToAttributeName))
            {
                return;
            }

            builder.Append("<link-entity name=\"").Append(link.LinkToEntityName).Append("\" from=\"").Append(link.LinkToAttributeName)
                .Append("\" to=\"").Append(link.LinkFromAttributeName).Append('\"');

            if (!string.IsNullOrWhiteSpace(link.EntityAlias))
            {
                builder.Append(" alias=\"").Append(link.EntityAlias).Append('\"');
            }

            if (link.JoinOperator == JoinOperator.LeftOuter)
            {
                builder.Append(" link-type=\"outer\"");
            }

            builder.Append('>');
            WriteColumns(builder, link.Columns);
            WriteOrders(builder, link.Orders);
            WriteFilter(builder, link.LinkCriteria);
            if (link.LinkEntities != null)
            {
                foreach (var child in link.LinkEntities)
                {
                    WriteLink(builder, child);
                }
            }

            builder.Append("</link-entity>");
        }

        private static string FormatValue(object value, ConditionOperator op)
        {
            if (value is AliasedValue aliased && aliased.Value != null)
            {
                value = aliased.Value;
            }

            switch (value)
            {
                case null:
                    return string.Empty;
                case string s:
                    return PrepareLikeValue(s, op);
                case Guid guid:
                    return guid.ToString();
                case EntityReference er:
                    return er.Id.ToString();
                case OptionSetValue osv:
                    return osv.Value.ToString(CultureInfo.InvariantCulture);
                case Money money:
                    return money.Value.ToString(CultureInfo.InvariantCulture);
                case DateTime dateTime:
                    return dateTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss\\Z", CultureInfo.InvariantCulture);
                case bool flag:
                    return flag ? "1" : "0";
                case Enum enumValue:
                    return Convert.ToInt32(enumValue, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                case IConvertible convertible:
                    return PrepareLikeValue(convertible.ToString(CultureInfo.InvariantCulture), op);
                default:
                    return PrepareLikeValue(value.ToString(), op);
            }
        }

        private static string PrepareLikeValue(string value, ConditionOperator op)
        {
            if (value == null)
            {
                return string.Empty;
            }

            switch (op)
            {
                case ConditionOperator.Contains:
                    return "%" + value + "%";
                case ConditionOperator.DoesNotContain:
                    return "%" + value + "%";
                case ConditionOperator.BeginsWith:
                    return value + "%";
                case ConditionOperator.DoesNotBeginWith:
                    return value + "%";
                case ConditionOperator.EndsWith:
                    return "%" + value;
                case ConditionOperator.DoesNotEndWith:
                    return "%" + value;
                default:
                    return value;
            }
        }

        private static string EscapeXml(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }
    }
}
