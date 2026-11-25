/*
===============================================================================
  HSE Dynamic Asset Inspection & QR Tracking System
  01_Schema.sql - Idempotent Application Schema (dbo)
===============================================================================
*/

SET NOCOUNT ON;
GO

-- 1. dbo.AppUsers
IF OBJECT_ID(N'dbo.AppUsers', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AppUsers] (
        [Id]           INT            IDENTITY(1, 1) NOT NULL,
        [Username]     NVARCHAR(100)  NOT NULL,
        [PasswordHash] NVARCHAR(256)  NOT NULL,
        [FullName]     NVARCHAR(200)  NOT NULL,
        [IsAdmin]      BIT            NOT NULL CONSTRAINT [DF_AppUsers_IsAdmin] DEFAULT (0),
        [IsActive]     BIT            NOT NULL CONSTRAINT [DF_AppUsers_IsActive] DEFAULT (1),
        [CreatedAtUtc] DATETIME2(7)   NOT NULL CONSTRAINT [DF_AppUsers_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_AppUsers] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_AppUsers_Username' AND object_id = OBJECT_ID(N'dbo.AppUsers')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_AppUsers_Username]
        ON [dbo].[AppUsers] ([Username] ASC);
END
GO

-- 2. dbo.DataSources
IF OBJECT_ID(N'dbo.DataSources', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DataSources] (
        [Id]                     INT            IDENTITY(1, 1) NOT NULL,
        [Name]                   NVARCHAR(200)  NOT NULL,
        [ViewName]               NVARCHAR(256)  NOT NULL,
        [UniqueKeyColumn]        NVARCHAR(128)  NOT NULL,
        [IsEav]                  BIT            NOT NULL CONSTRAINT [DF_DataSources_IsEav] DEFAULT (0),
        [EavAssetIdColumn]       NVARCHAR(128)  NULL,
        [EavFieldIdColumn]       NVARCHAR(128)  NULL,
        [EavFieldNameColumn]     NVARCHAR(128)  NULL,
        [EavFieldTypeColumn]     NVARCHAR(128)  NULL,
        [EavDisplayOrderColumn]  NVARCHAR(128)  NULL,
        [IndicatorColumnsJson]   NVARCHAR(MAX)  NULL,
        [IndicatorSeparator]     NVARCHAR(10)   NULL,
        [IndicatorPrefix]        NVARCHAR(50)   NULL,
        [PublicBaseFieldsJson]   NVARCHAR(MAX)  NULL,
        [LabelCaptionFieldsJson] NVARCHAR(MAX)  NULL,
        [IsActive]               BIT            NOT NULL CONSTRAINT [DF_DataSources_IsActive] DEFAULT (1),
        [CreatedAtUtc]           DATETIME2(7)   NOT NULL CONSTRAINT [DF_DataSources_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_DataSources] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DataSources_ViewName' AND object_id = OBJECT_ID(N'dbo.DataSources')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_DataSources_ViewName]
        ON [dbo].[DataSources] ([ViewName] ASC);
END
GO

-- 3. dbo.CustomFields
IF OBJECT_ID(N'dbo.CustomFields', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CustomFields] (
        [Id]                   INT            IDENTITY(1, 1) NOT NULL,
        [DataSourceId]         INT            NOT NULL,
        [Name]                 NVARCHAR(128)  NOT NULL,
        [Label]                NVARCHAR(200)  NOT NULL,
        [FieldType]            INT            NOT NULL,
        [IsRequired]           BIT            NOT NULL CONSTRAINT [DF_CustomFields_IsRequired] DEFAULT (0),
        [DisplayOrder]         INT            NOT NULL CONSTRAINT [DF_CustomFields_DisplayOrder] DEFAULT (0),
        [IsPublicVisible]      BIT            NOT NULL CONSTRAINT [DF_CustomFields_IsPublicVisible] DEFAULT (1),
        [BooleanTrueLabel]     NVARCHAR(100)  NULL,
        [BooleanFalseLabel]    NVARCHAR(100)  NULL,
        [OptionsSourceType]    INT            NOT NULL CONSTRAINT [DF_CustomFields_OptionsSourceType] DEFAULT (0),
        [StaticOptionsJson]    NVARCHAR(MAX)  NULL,
        [OptionsViewName]      NVARCHAR(256)  NULL,
        [OptionsValueColumn]   NVARCHAR(128)  NULL,
        [OptionsDisplayColumn] NVARCHAR(128)  NULL,
        [IsGregorianDate]      BIT            NOT NULL CONSTRAINT [DF_CustomFields_IsGregorianDate] DEFAULT (0),
        [IsDeleted]            BIT            NOT NULL CONSTRAINT [DF_CustomFields_IsDeleted] DEFAULT (0),
        CONSTRAINT [PK_CustomFields] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_CustomFields_DataSources_DataSourceId] FOREIGN KEY ([DataSourceId])
            REFERENCES [dbo].[DataSources] ([Id]) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_CustomFields_DataSourceId_DisplayOrder' AND object_id = OBJECT_ID(N'dbo.CustomFields')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_CustomFields_DataSourceId_DisplayOrder]
        ON [dbo].[CustomFields] ([DataSourceId] ASC, [DisplayOrder] ASC);
END
GO

-- 4. dbo.AssetIndicators
IF OBJECT_ID(N'dbo.AssetIndicators', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AssetIndicators] (
        [Id]                   INT            IDENTITY(1, 1) NOT NULL,
        [DataSourceId]         INT            NOT NULL,
        [AssetKey]             NVARCHAR(200)  NOT NULL,
        [Indicator]            NVARCHAR(250)  NOT NULL,
        [QrPayloadUrl]         NVARCHAR(500)  NULL,
        [CachedBaseFieldsJson] NVARCHAR(MAX)  NULL,
        [CreatedAtUtc]         DATETIME2(7)   NOT NULL CONSTRAINT [DF_AssetIndicators_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_AssetIndicators] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_AssetIndicators_DataSources_DataSourceId] FOREIGN KEY ([DataSourceId])
            REFERENCES [dbo].[DataSources] ([Id]) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_AssetIndicators_Indicator' AND object_id = OBJECT_ID(N'dbo.AssetIndicators')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_AssetIndicators_Indicator]
        ON [dbo].[AssetIndicators] ([Indicator] ASC);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_AssetIndicators_DataSourceId_AssetKey' AND object_id = OBJECT_ID(N'dbo.AssetIndicators')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AssetIndicators_DataSourceId_AssetKey]
        ON [dbo].[AssetIndicators] ([DataSourceId] ASC, [AssetKey] ASC);
END
GO

-- 5. dbo.FormPersonnel
IF OBJECT_ID(N'dbo.FormPersonnel', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[FormPersonnel] (
        [Id]            INT            IDENTITY(1, 1) NOT NULL,
        [DataSourceId]  INT            NOT NULL,
        [Username]      NVARCHAR(100)  NOT NULL,
        [FullName]      NVARCHAR(200)  NOT NULL,
        [IsActive]      BIT            NOT NULL CONSTRAINT [DF_FormPersonnel_IsActive] DEFAULT (1),
        [AssignedAtUtc] DATETIME2(7)   NOT NULL CONSTRAINT [DF_FormPersonnel_AssignedAtUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_FormPersonnel] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_FormPersonnel_DataSources_DataSourceId] FOREIGN KEY ([DataSourceId])
            REFERENCES [dbo].[DataSources] ([Id]) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_FormPersonnel_DataSourceId_Username' AND object_id = OBJECT_ID(N'dbo.FormPersonnel')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_FormPersonnel_DataSourceId_Username]
        ON [dbo].[FormPersonnel] ([DataSourceId] ASC, [Username] ASC);
END
GO

-- 6. dbo.InspectionRecords
IF OBJECT_ID(N'dbo.InspectionRecords', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[InspectionRecords] (
        [Id]                INT             IDENTITY(1, 1) NOT NULL,
        [DataSourceId]      INT             NOT NULL,
        [AssetIndicatorId]  INT             NOT NULL,
        [AssetKey]          NVARCHAR(200)   NOT NULL,
        [InspectorUsername] NVARCHAR(100)   NOT NULL,
        [InspectorFullName] NVARCHAR(200)   NOT NULL,
        [InspectedAtUtc]    DATETIME2(7)    NOT NULL CONSTRAINT [DF_InspectionRecords_InspectedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [Notes]             NVARCHAR(2000)  NULL,
        CONSTRAINT [PK_InspectionRecords] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_InspectionRecords_DataSources_DataSourceId] FOREIGN KEY ([DataSourceId])
            REFERENCES [dbo].[DataSources] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InspectionRecords_AssetIndicators_AssetIndicatorId] FOREIGN KEY ([AssetIndicatorId])
            REFERENCES [dbo].[AssetIndicators] ([Id]) ON DELETE NO ACTION
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_InspectionRecords_AssetIndicatorId_InspectedAtUtc' AND object_id = OBJECT_ID(N'dbo.InspectionRecords')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_InspectionRecords_AssetIndicatorId_InspectedAtUtc]
        ON [dbo].[InspectionRecords] ([AssetIndicatorId] ASC, [InspectedAtUtc] DESC);
END
GO

-- 7. dbo.InspectionRecordValues
IF OBJECT_ID(N'dbo.InspectionRecordValues', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[InspectionRecordValues] (
        [Id]                 INT             IDENTITY(1, 1) NOT NULL,
        [InspectionRecordId] INT             NOT NULL,
        [CustomFieldId]      INT             NOT NULL,
        [TextValue]          NVARCHAR(MAX)   NULL,
        [NumberValue]        DECIMAL(18, 4)  NULL,
        [DateValue]          DATETIME2(7)    NULL,
        [BooleanValue]       BIT             NULL,
        [SelectedValuesJson] NVARCHAR(MAX)   NULL,
        [StoredFileName]     NVARCHAR(260)   NULL,
        [OriginalFileName]   NVARCHAR(260)   NULL,
        [ContentType]        NVARCHAR(150)   NULL,
        CONSTRAINT [PK_InspectionRecordValues] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_InspectionRecordValues_InspectionRecords_InspectionRecordId] FOREIGN KEY ([InspectionRecordId])
            REFERENCES [dbo].[InspectionRecords] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_InspectionRecordValues_CustomFields_CustomFieldId] FOREIGN KEY ([CustomFieldId])
            REFERENCES [dbo].[CustomFields] ([Id]) ON DELETE NO ACTION
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_InspectionRecordValues_InspectionRecordId_CustomFieldId' AND object_id = OBJECT_ID(N'dbo.InspectionRecordValues')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_InspectionRecordValues_InspectionRecordId_CustomFieldId]
        ON [dbo].[InspectionRecordValues] ([InspectionRecordId] ASC, [CustomFieldId] ASC);
END
GO
