using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Infrastructure.Annotations;
using HSEQRCode.Domain.Entities;

namespace HSEQRCode.Data
{
    /// <summary>
    /// Entity Framework 6.2.0 DbContext for the HSE Dynamic Asset Inspection &amp; QR Tracking System.
    /// </summary>
    public class HseDbContext : DbContext
    {
        static HseDbContext()
        {
            Database.SetInitializer<HseDbContext>(null);
        }

        public HseDbContext()
            : base("name=HseDbConnection")
        {
            Configuration.LazyLoadingEnabled = false;
            Configuration.ProxyCreationEnabled = false;
        }

        public HseDbContext(string nameOrConnectionString)
            : base(nameOrConnectionString)
        {
            Configuration.LazyLoadingEnabled = false;
            Configuration.ProxyCreationEnabled = false;
        }

        public HseDbContext(DbConnection existingConnection, bool contextOwnsConnection)
            : base(existingConnection, contextOwnsConnection)
        {
            Configuration.LazyLoadingEnabled = false;
            Configuration.ProxyCreationEnabled = false;
        }

        public DbSet<AppUser> AppUsers { get; set; }

        public DbSet<DataSource> DataSources { get; set; }

        public DbSet<CustomField> CustomFields { get; set; }

        public DbSet<AssetIndicator> AssetIndicators { get; set; }

        public DbSet<FormPersonnel> FormPersonnel { get; set; }

        public DbSet<InspectionRecord> InspectionRecords { get; set; }

        public DbSet<InspectionRecordValue> InspectionRecordValues { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            ConfigureAppUser(modelBuilder);
            ConfigureDataSource(modelBuilder);
            ConfigureCustomField(modelBuilder);
            ConfigureAssetIndicator(modelBuilder);
            ConfigureFormPersonnel(modelBuilder);
            ConfigureInspectionRecord(modelBuilder);
            ConfigureInspectionRecordValue(modelBuilder);
        }

        private static void ConfigureAppUser(DbModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<AppUser>();
            entity.ToTable("AppUsers", "dbo");
            entity.HasKey(u => u.Id);

            entity.Property(u => u.Username)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new IndexAttribute("IX_AppUsers_Username") { IsUnique = true }));

            entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(256);
            entity.Property(u => u.FullName).IsRequired().HasMaxLength(200);
        }

        private static void ConfigureDataSource(DbModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<DataSource>();
            entity.ToTable("DataSources", "dbo");
            entity.HasKey(d => d.Id);

            entity.Property(d => d.Name).IsRequired().HasMaxLength(200);

            entity.Property(d => d.ViewName)
                .IsRequired()
                .HasMaxLength(256)
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new IndexAttribute("IX_DataSources_ViewName") { IsUnique = true }));

            entity.Property(d => d.UniqueKeyColumn).IsRequired().HasMaxLength(128);
            entity.Property(d => d.EavAssetIdColumn).HasMaxLength(128);
            entity.Property(d => d.EavFieldIdColumn).HasMaxLength(128);
            entity.Property(d => d.EavFieldNameColumn).HasMaxLength(128);
            entity.Property(d => d.EavFieldTypeColumn).HasMaxLength(128);
            entity.Property(d => d.EavDisplayOrderColumn).HasMaxLength(128);
            entity.Property(d => d.IndicatorSeparator).HasMaxLength(10);
            entity.Property(d => d.IndicatorPrefix).HasMaxLength(50);
        }

        private static void ConfigureCustomField(DbModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<CustomField>();
            entity.ToTable("CustomFields", "dbo");
            entity.HasKey(f => f.Id);

            entity.Property(f => f.Name).IsRequired().HasMaxLength(128);
            entity.Property(f => f.Label).IsRequired().HasMaxLength(200);
            entity.Property(f => f.BooleanTrueLabel).HasMaxLength(100);
            entity.Property(f => f.BooleanFalseLabel).HasMaxLength(100);
            entity.Property(f => f.OptionsViewName).HasMaxLength(256);
            entity.Property(f => f.OptionsValueColumn).HasMaxLength(128);
            entity.Property(f => f.OptionsDisplayColumn).HasMaxLength(128);

            entity.Property(f => f.DataSourceId)
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new IndexAttribute("IX_CustomFields_DataSourceId_DisplayOrder", 1)));

            entity.Property(f => f.DisplayOrder)
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new IndexAttribute("IX_CustomFields_DataSourceId_DisplayOrder", 2)));

            entity.HasRequired(f => f.DataSource)
                .WithMany(d => d.CustomFields)
                .HasForeignKey(f => f.DataSourceId)
                .WillCascadeOnDelete(true);
        }

        private static void ConfigureAssetIndicator(DbModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<AssetIndicator>();
            entity.ToTable("AssetIndicators", "dbo");
            entity.HasKey(a => a.Id);

            entity.Property(a => a.DataSourceId)
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new IndexAttribute("IX_AssetIndicators_DataSourceId_AssetKey", 1)));

            entity.Property(a => a.AssetKey)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new IndexAttribute("IX_AssetIndicators_DataSourceId_AssetKey", 2)));

            entity.Property(a => a.Indicator)
                .IsRequired()
                .HasMaxLength(250)
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new IndexAttribute("IX_AssetIndicators_Indicator") { IsUnique = true }));

            entity.Property(a => a.QrPayloadUrl).HasMaxLength(500);

            entity.HasRequired(a => a.DataSource)
                .WithMany(d => d.AssetIndicators)
                .HasForeignKey(a => a.DataSourceId)
                .WillCascadeOnDelete(true);
        }

        private static void ConfigureFormPersonnel(DbModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<FormPersonnel>();
            entity.ToTable("FormPersonnel", "dbo");
            entity.HasKey(p => p.Id);

            entity.Property(p => p.DataSourceId)
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new IndexAttribute("IX_FormPersonnel_DataSourceId_Username", 1) { IsUnique = true }));

            entity.Property(p => p.Username)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new IndexAttribute("IX_FormPersonnel_DataSourceId_Username", 2) { IsUnique = true }));

            entity.Property(p => p.FullName).IsRequired().HasMaxLength(200);

            entity.HasRequired(p => p.DataSource)
                .WithMany(d => d.FormPersonnel)
                .HasForeignKey(p => p.DataSourceId)
                .WillCascadeOnDelete(true);
        }

        private static void ConfigureInspectionRecord(DbModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<InspectionRecord>();
            entity.ToTable("InspectionRecords", "dbo");
            entity.HasKey(r => r.Id);

            entity.Property(r => r.AssetKey).IsRequired().HasMaxLength(200);
            entity.Property(r => r.InspectorUsername).IsRequired().HasMaxLength(100);
            entity.Property(r => r.InspectorFullName).IsRequired().HasMaxLength(200);
            entity.Property(r => r.Notes).HasMaxLength(2000);

            entity.Property(r => r.AssetIndicatorId)
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new IndexAttribute("IX_InspectionRecords_AssetIndicatorId_InspectedAtUtc", 1)));

            entity.Property(r => r.InspectedAtUtc)
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new IndexAttribute("IX_InspectionRecords_AssetIndicatorId_InspectedAtUtc", 2)));

            entity.HasRequired(r => r.DataSource)
                .WithMany(d => d.InspectionRecords)
                .HasForeignKey(r => r.DataSourceId)
                .WillCascadeOnDelete(false);

            entity.HasRequired(r => r.AssetIndicator)
                .WithMany(a => a.InspectionRecords)
                .HasForeignKey(r => r.AssetIndicatorId)
                .WillCascadeOnDelete(false);
        }

        private static void ConfigureInspectionRecordValue(DbModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<InspectionRecordValue>();
            entity.ToTable("InspectionRecordValues", "dbo");
            entity.HasKey(v => v.Id);

            entity.Property(v => v.NumberValue).HasPrecision(18, 4);
            entity.Property(v => v.StoredFileName).HasMaxLength(260);
            entity.Property(v => v.OriginalFileName).HasMaxLength(260);
            entity.Property(v => v.ContentType).HasMaxLength(150);

            entity.Property(v => v.InspectionRecordId)
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new IndexAttribute("IX_InspectionRecordValues_InspectionRecordId_CustomFieldId", 1)));

            entity.Property(v => v.CustomFieldId)
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new IndexAttribute("IX_InspectionRecordValues_InspectionRecordId_CustomFieldId", 2)));

            entity.HasRequired(v => v.InspectionRecord)
                .WithMany(r => r.Values)
                .HasForeignKey(v => v.InspectionRecordId)
                .WillCascadeOnDelete(true);

            entity.HasRequired(v => v.CustomField)
                .WithMany(f => f.InspectionRecordValues)
                .HasForeignKey(v => v.CustomFieldId)
                .WillCascadeOnDelete(false);
        }
    }
}
