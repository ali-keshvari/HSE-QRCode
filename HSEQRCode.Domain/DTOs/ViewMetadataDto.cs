using System.Collections.Generic;

namespace HSEQRCode.Domain.DTOs
{
    /// <summary>
    /// Metadata describing a SQL Server view and its columns.
    /// </summary>
    public class ViewMetadataDto
    {
        public ViewMetadataDto()
        {
            Columns = new List<ViewColumnDto>();
        }

        public string SchemaName { get; set; }

        public string ViewName { get; set; }

        public string FullViewName { get; set; }

        public List<ViewColumnDto> Columns { get; set; }
    }

    /// <summary>
    /// Column metadata for a SQL Server view.
    /// </summary>
    public class ViewColumnDto
    {
        public string ColumnName { get; set; }

        public string DataType { get; set; }

        public bool IsNullable { get; set; }

        public int MaxLength { get; set; }

        public int ColumnOrder { get; set; }
    }
}
