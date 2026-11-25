namespace HSEQRCode.Domain.Enums
{
    /// <summary>
    /// Field data types supported by admin-defined custom inspection fields.
    /// </summary>
    public enum CustomFieldType
    {
        Text = 1,
        Number = 2,
        Date = 3,
        Boolean = 4,
        Attachment = 5,
        Select = 6,
        MultiSelect = 7
    }
}
