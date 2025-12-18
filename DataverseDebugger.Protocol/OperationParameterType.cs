namespace DataverseDebugger.Protocol
{
    /// <summary>
    /// Defines shared operation parameter type values that mirror Dataverse Custom API parameter types.
    /// </summary>
    public static class OperationParameterType
    {
        public const int Boolean = 0;
        public const int DateTime = 1;
        public const int Decimal = 2;
        public const int Entity = 3;
        public const int EntityCollection = 4;
        public const int EntityReference = 5;
        public const int Float = 6;
        public const int Integer = 7;
        public const int Money = 8;
        public const int Picklist = 9;
        public const int String = 10;
        public const int StringArray = 11;
        public const int Guid = 12;
    }
}
