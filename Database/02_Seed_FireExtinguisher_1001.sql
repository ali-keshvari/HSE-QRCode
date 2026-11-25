/*
===============================================================================
  HSE Dynamic Asset Inspection & QR Tracking System
  02_Seed_FireExtinguisher_1001.sql - Idempotent gen.* Schema, Views & Seed Data
===============================================================================
*/

SET NOCOUNT ON;
GO

-- 1. Create [gen] schema if not exists
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'gen')
BEGIN
    EXEC(N'CREATE SCHEMA [gen]');
END
GO

-- 2. Create gen.AssetCategories
IF OBJECT_ID(N'gen.AssetCategories', N'U') IS NULL
BEGIN
    CREATE TABLE [gen].[AssetCategories] (
        [CategoryId]   INT           NOT NULL,
        [CategoryName] NVARCHAR(200) NOT NULL,
        [IsActive]     BIT           NOT NULL CONSTRAINT [DF_gen_AssetCategories_IsActive] DEFAULT (1),
        CONSTRAINT [PK_gen_AssetCategories] PRIMARY KEY CLUSTERED ([CategoryId] ASC)
    );
END
GO

-- 3. Create gen.ManagedAssets
IF OBJECT_ID(N'gen.ManagedAssets', N'U') IS NULL
BEGIN
    CREATE TABLE [gen].[ManagedAssets] (
        [AssetId]      INT           IDENTITY(1, 1) NOT NULL,
        [CategoryId]   INT           NOT NULL,
        [AssetKey]     NVARCHAR(200) NOT NULL,
        [IsActive]     BIT           NOT NULL CONSTRAINT [DF_gen_ManagedAssets_IsActive] DEFAULT (1),
        [CreatedAtUtc] DATETIME2(7)  NOT NULL CONSTRAINT [DF_gen_ManagedAssets_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_gen_ManagedAssets] PRIMARY KEY CLUSTERED ([AssetId] ASC),
        CONSTRAINT [FK_gen_ManagedAssets_AssetCategories] FOREIGN KEY ([CategoryId])
            REFERENCES [gen].[AssetCategories] ([CategoryId])
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_gen_ManagedAssets_CategoryId_AssetKey' AND object_id = OBJECT_ID(N'gen.ManagedAssets')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_gen_ManagedAssets_CategoryId_AssetKey]
        ON [gen].[ManagedAssets] ([CategoryId] ASC, [AssetKey] ASC);
END
GO

-- 4. Create gen.CategoryFields
IF OBJECT_ID(N'gen.CategoryFields', N'U') IS NULL
BEGIN
    CREATE TABLE [gen].[CategoryFields] (
        [CategoryFieldId] INT           IDENTITY(1, 1) NOT NULL,
        [CategoryId]      INT           NOT NULL,
        [FieldName]       NVARCHAR(200) NOT NULL,
        [FieldType]       INT           NOT NULL, -- 1=Text, 2=Date, 3=Number, 4=Boolean, 5=Attachment
        [IsRequired]      BIT           NOT NULL CONSTRAINT [DF_gen_CategoryFields_IsRequired] DEFAULT (0),
        [DisplayOrder]    INT           NOT NULL CONSTRAINT [DF_gen_CategoryFields_DisplayOrder] DEFAULT (0),
        CONSTRAINT [PK_gen_CategoryFields] PRIMARY KEY CLUSTERED ([CategoryFieldId] ASC),
        CONSTRAINT [FK_gen_CategoryFields_AssetCategories] FOREIGN KEY ([CategoryId])
            REFERENCES [gen].[AssetCategories] ([CategoryId])
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_gen_CategoryFields_CategoryId_FieldName' AND object_id = OBJECT_ID(N'gen.CategoryFields')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_gen_CategoryFields_CategoryId_FieldName]
        ON [gen].[CategoryFields] ([CategoryId] ASC, [FieldName] ASC);
END
GO

-- 5. Create gen.AssetFieldValues
IF OBJECT_ID(N'gen.AssetFieldValues', N'U') IS NULL
BEGIN
    CREATE TABLE [gen].[AssetFieldValues] (
        [AssetFieldValueId] INT            IDENTITY(1, 1) NOT NULL,
        [AssetId]           INT            NOT NULL,
        [CategoryFieldId]   INT            NOT NULL,
        [TextValue]         NVARCHAR(MAX)  NULL,
        [NumberValue]       DECIMAL(18, 4) NULL,
        [DateValue]         DATETIME2(7)   NULL,
        [BooleanValue]      BIT            NULL,
        [IsGregorian]       BIT            NOT NULL CONSTRAINT [DF_gen_AssetFieldValues_IsGregorian] DEFAULT (0),
        [StoredFileName]    NVARCHAR(260)  NULL,
        [OriginalFileName]  NVARCHAR(260)  NULL,
        [ContentType]       NVARCHAR(150)  NULL,
        CONSTRAINT [PK_gen_AssetFieldValues] PRIMARY KEY CLUSTERED ([AssetFieldValueId] ASC),
        CONSTRAINT [FK_gen_AssetFieldValues_ManagedAssets] FOREIGN KEY ([AssetId])
            REFERENCES [gen].[ManagedAssets] ([AssetId]) ON DELETE CASCADE,
        CONSTRAINT [FK_gen_AssetFieldValues_CategoryFields] FOREIGN KEY ([CategoryFieldId])
            REFERENCES [gen].[CategoryFields] ([CategoryFieldId])
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_gen_AssetFieldValues_AssetId_CategoryFieldId' AND object_id = OBJECT_ID(N'gen.AssetFieldValues')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_gen_AssetFieldValues_AssetId_CategoryFieldId]
        ON [gen].[AssetFieldValues] ([AssetId] ASC, [CategoryFieldId] ASC);
END
GO

-- 6. Create/Recreate Base EAV View: gen.vw_FireExtinguishers_1001
IF OBJECT_ID(N'gen.vw_FireExtinguishers_1001', N'V') IS NOT NULL
BEGIN
    DROP VIEW [gen].[vw_FireExtinguishers_1001];
END
GO

CREATE VIEW [gen].[vw_FireExtinguishers_1001]
AS
SELECT
    a.AssetId,
    a.AssetKey,
    c.CategoryId,
    c.CategoryName,
    cf.CategoryFieldId,
    cf.FieldName,
    cf.FieldType,
    cf.IsRequired,
    cf.DisplayOrder,
    v.TextValue,
    v.NumberValue,
    v.DateValue,
    v.BooleanValue,
    ISNULL(v.IsGregorian, 0) AS IsGregorian,
    v.StoredFileName,
    v.OriginalFileName,
    v.ContentType
FROM [gen].[ManagedAssets] AS a
INNER JOIN [gen].[AssetCategories] AS c
    ON a.CategoryId = c.CategoryId
INNER JOIN [gen].[CategoryFields] AS cf
    ON cf.CategoryId = c.CategoryId
LEFT JOIN [gen].[AssetFieldValues] AS v
    ON v.AssetId = a.AssetId
   AND v.CategoryFieldId = cf.CategoryFieldId
WHERE c.CategoryId = 1001
  AND a.IsActive = 1;
GO

-- 7. Create/Recreate Lookup View: gen.vw_FireExtinguisherStatuses (for Select custom fields)
IF OBJECT_ID(N'gen.vw_FireExtinguisherStatuses', N'V') IS NOT NULL
BEGIN
    DROP VIEW [gen].[vw_FireExtinguisherStatuses];
END
GO

CREATE VIEW [gen].[vw_FireExtinguisherStatuses]
AS
SELECT N'OK'             AS [StatusCode], N'سالم و آماده به کار'    AS [StatusTitle]
UNION ALL
SELECT N'NEED_RECHARGE'  AS [StatusCode], N'نیازمند شارژ مجدد'       AS [StatusTitle]
UNION ALL
SELECT N'LOW_PRESSURE'   AS [StatusCode], N'افت فشار مانومتر'        AS [StatusTitle]
UNION ALL
SELECT N'DAMAGED'        AS [StatusCode], N'آسیب دیده / خارج از رده' AS [StatusTitle];
GO

-- 8. Create/Recreate Lookup View: gen.vw_InspectionTags (for MultiSelect custom fields)
IF OBJECT_ID(N'gen.vw_InspectionTags', N'V') IS NOT NULL
BEGIN
    DROP VIEW [gen].[vw_InspectionTags];
END
GO

CREATE VIEW [gen].[vw_InspectionTags]
AS
SELECT N'SEAL_INTACT'    AS [TagCode], N'پلمپ سالم است'            AS [TagTitle]
UNION ALL
SELECT N'HOSE_CLEAN'     AS [TagCode], N'شلنگ و نازل بدون ترک'     AS [TagTitle]
UNION ALL
SELECT N'GAUGE_GREEN'    AS [TagCode], N'عقربه فشار در محدوده سبز' AS [TagTitle]
UNION ALL
SELECT N'SIGN_VISIBLE'   AS [TagCode], N'تابلو و برچسب خوانا است'  AS [TagTitle]
UNION ALL
SELECT N'ACCESS_CLEAR'   AS [TagCode], N'مسیر دسترسی بدون مانع'    AS [TagTitle];
GO

-- 9. Seed Category 1001 ("کپسول آتشنشانی")
IF NOT EXISTS (SELECT 1 FROM [gen].[AssetCategories] WHERE [CategoryId] = 1001)
BEGIN
    INSERT INTO [gen].[AssetCategories] ([CategoryId], [CategoryName], [IsActive])
    VALUES (1001, N'کپسول آتشنشانی', 1);
END
GO

-- 10. Seed CategoryFields for Category 1001
DECLARE @Fields TABLE (
    [FieldName]    NVARCHAR(200),
    [FieldType]    INT,
    [IsRequired]   BIT,
    [DisplayOrder] INT
);

INSERT INTO @Fields ([FieldName], [FieldType], [IsRequired], [DisplayOrder])
VALUES
    (N'شماره سریال',     1, 1, 1), -- Text
    (N'نوع کپسول',       1, 1, 2), -- Text
    (N'وزن (کیلوگرم)',   3, 1, 3), -- Number
    (N'محل استقرار',     1, 1, 4), -- Text
    (N'تاریخ شارژ بعدی', 2, 1, 5), -- Date
    (N'فعال',            4, 1, 6); -- Boolean

INSERT INTO [gen].[CategoryFields] ([CategoryId], [FieldName], [FieldType], [IsRequired], [DisplayOrder])
SELECT 1001, f.[FieldName], f.[FieldType], f.[IsRequired], f.[DisplayOrder]
FROM @Fields AS f
WHERE NOT EXISTS (
    SELECT 1 FROM [gen].[CategoryFields] AS cf
    WHERE cf.[CategoryId] = 1001 AND cf.[FieldName] = f.[FieldName]
);
GO

-- 11. Seed 5 Sample Fire Extinguishers (FE-1001-01 .. FE-1001-05)
DECLARE @SampleAssets TABLE (
    [AssetKey]     NVARCHAR(200),
    [SerialNo]     NVARCHAR(200),
    [TypeName]     NVARCHAR(200),
    [WeightKg]     DECIMAL(18, 4),
    [Location]     NVARCHAR(200),
    [NextRecharge] DATETIME2(7),
    [IsReady]      BIT
);

INSERT INTO @SampleAssets ([AssetKey], [SerialNo], [TypeName], [WeightKg], [Location], [NextRecharge], [IsReady])
VALUES
    (N'FE-1001-01', N'SN-1404-001', N'پودر و گاز',     6.0000,  N'ساختمان اداری - طبقه اول',    '2027-03-15', 1),
    (N'FE-1001-02', N'SN-1404-002', N'دی اکسید کربن',  4.5000,  N'اتاق سرور مرکزی',             '2027-04-20', 1),
    (N'FE-1001-03', N'SN-1404-003', N'پودر و گاز',     12.0000, N'انبار مواد اولیه - درب شمالی', '2027-02-10', 1),
    (N'FE-1001-04', N'SN-1404-004', N'فوم (کف)',       9.0000,  N'کارگاه رنگ و رزین',           '2027-01-25', 1),
    (N'FE-1001-05', N'SN-1404-005', N'دی اکسید کربن',  6.0000,  N'پست برق فشار قوی',           '2027-05-05', 1);

INSERT INTO [gen].[ManagedAssets] ([CategoryId], [AssetKey], [IsActive])
SELECT 1001, s.[AssetKey], 1
FROM @SampleAssets AS s
WHERE NOT EXISTS (
    SELECT 1 FROM [gen].[ManagedAssets] AS ma
    WHERE ma.[CategoryId] = 1001 AND ma.[AssetKey] = s.[AssetKey]
);

DECLARE @F_Serial   INT = (SELECT [CategoryFieldId] FROM [gen].[CategoryFields] WHERE [CategoryId] = 1001 AND [FieldName] = N'شماره سریال');
DECLARE @F_Type     INT = (SELECT [CategoryFieldId] FROM [gen].[CategoryFields] WHERE [CategoryId] = 1001 AND [FieldName] = N'نوع کپسول');
DECLARE @F_Weight   INT = (SELECT [CategoryFieldId] FROM [gen].[CategoryFields] WHERE [CategoryId] = 1001 AND [FieldName] = N'وزن (کیلوگرم)');
DECLARE @F_Location INT = (SELECT [CategoryFieldId] FROM [gen].[CategoryFields] WHERE [CategoryId] = 1001 AND [FieldName] = N'محل استقرار');
DECLARE @F_Date     INT = (SELECT [CategoryFieldId] FROM [gen].[CategoryFields] WHERE [CategoryId] = 1001 AND [FieldName] = N'تاریخ شارژ بعدی');
DECLARE @F_Active   INT = (SELECT [CategoryFieldId] FROM [gen].[CategoryFields] WHERE [CategoryId] = 1001 AND [FieldName] = N'فعال');

-- Insert SerialNo
INSERT INTO [gen].[AssetFieldValues] ([AssetId], [CategoryFieldId], [TextValue])
SELECT ma.[AssetId], @F_Serial, s.[SerialNo]
FROM @SampleAssets AS s
INNER JOIN [gen].[ManagedAssets] AS ma ON ma.[CategoryId] = 1001 AND ma.[AssetKey] = s.[AssetKey]
WHERE NOT EXISTS (
    SELECT 1 FROM [gen].[AssetFieldValues] AS v WHERE v.[AssetId] = ma.[AssetId] AND v.[CategoryFieldId] = @F_Serial
);

-- Insert TypeName
INSERT INTO [gen].[AssetFieldValues] ([AssetId], [CategoryFieldId], [TextValue])
SELECT ma.[AssetId], @F_Type, s.[TypeName]
FROM @SampleAssets AS s
INNER JOIN [gen].[ManagedAssets] AS ma ON ma.[CategoryId] = 1001 AND ma.[AssetKey] = s.[AssetKey]
WHERE NOT EXISTS (
    SELECT 1 FROM [gen].[AssetFieldValues] AS v WHERE v.[AssetId] = ma.[AssetId] AND v.[CategoryFieldId] = @F_Type
);

-- Insert WeightKg
INSERT INTO [gen].[AssetFieldValues] ([AssetId], [CategoryFieldId], [NumberValue])
SELECT ma.[AssetId], @F_Weight, s.[WeightKg]
FROM @SampleAssets AS s
INNER JOIN [gen].[ManagedAssets] AS ma ON ma.[CategoryId] = 1001 AND ma.[AssetKey] = s.[AssetKey]
WHERE NOT EXISTS (
    SELECT 1 FROM [gen].[AssetFieldValues] AS v WHERE v.[AssetId] = ma.[AssetId] AND v.[CategoryFieldId] = @F_Weight
);

-- Insert Location
INSERT INTO [gen].[AssetFieldValues] ([AssetId], [CategoryFieldId], [TextValue])
SELECT ma.[AssetId], @F_Location, s.[Location]
FROM @SampleAssets AS s
INNER JOIN [gen].[ManagedAssets] AS ma ON ma.[CategoryId] = 1001 AND ma.[AssetKey] = s.[AssetKey]
WHERE NOT EXISTS (
    SELECT 1 FROM [gen].[AssetFieldValues] AS v WHERE v.[AssetId] = ma.[AssetId] AND v.[CategoryFieldId] = @F_Location
);

-- Insert NextRecharge Date (Jalali display by default: IsGregorian = 0)
INSERT INTO [gen].[AssetFieldValues] ([AssetId], [CategoryFieldId], [DateValue], [IsGregorian])
SELECT ma.[AssetId], @F_Date, s.[NextRecharge], 0
FROM @SampleAssets AS s
INNER JOIN [gen].[ManagedAssets] AS ma ON ma.[CategoryId] = 1001 AND ma.[AssetKey] = s.[AssetKey]
WHERE NOT EXISTS (
    SELECT 1 FROM [gen].[AssetFieldValues] AS v WHERE v.[AssetId] = ma.[AssetId] AND v.[CategoryFieldId] = @F_Date
);

-- Insert Active Boolean
INSERT INTO [gen].[AssetFieldValues] ([AssetId], [CategoryFieldId], [BooleanValue])
SELECT ma.[AssetId], @F_Active, s.[IsReady]
FROM @SampleAssets AS s
INNER JOIN [gen].[ManagedAssets] AS ma ON ma.[CategoryId] = 1001 AND ma.[AssetKey] = s.[AssetKey]
WHERE NOT EXISTS (
    SELECT 1 FROM [gen].[AssetFieldValues] AS v WHERE v.[AssetId] = ma.[AssetId] AND v.[CategoryFieldId] = @F_Active
);
GO

-- 12. Seed Default Users in dbo.AppUsers (only if dbo.AppUsers exists)
-- PasswordHash format for seeded users: "SHA256:<lowercase-hex-sha256-of-utf8-password>"
--   admin      / Admin@123
--   inspector1 / Inspector@123
IF OBJECT_ID(N'dbo.AppUsers', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [dbo].[AppUsers] WHERE [Username] = N'admin')
    BEGIN
        INSERT INTO [dbo].[AppUsers] ([Username], [PasswordHash], [FullName], [IsAdmin], [IsActive])
        VALUES (
            N'admin',
            N'SHA256:e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7',
            N'مدیر سیستم',
            1,
            1
        );
    END

    IF NOT EXISTS (SELECT 1 FROM [dbo].[AppUsers] WHERE [Username] = N'inspector1')
    BEGIN
        INSERT INTO [dbo].[AppUsers] ([Username], [PasswordHash], [FullName], [IsAdmin], [IsActive])
        VALUES (
            N'inspector1',
            N'SHA256:066af044722d973a82b3e1a9757a9f15a06142863b8b58ba2d32c923a7b7b270',
            N'بازرس ایمنی ۱',
            0,
            1
        );
    END
END
GO
