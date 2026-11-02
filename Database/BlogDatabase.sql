/* Run this script in the existing AirborneDB database. */
IF DB_ID(N'AirborneDB') IS NULL
BEGIN
    THROW 50001, 'AirborneDB does not exist. Create/use the existing AirborneDB database first.', 1;
END;
GO
USE AirborneDB;
GO

IF OBJECT_ID(N'dbo.BlogMaster', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BlogMaster
    (
        BlogId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BlogMaster PRIMARY KEY,
        Title NVARCHAR(200) NOT NULL,
        Slug NVARCHAR(220) NOT NULL,
        Category NVARCHAR(100) NOT NULL CONSTRAINT DF_BlogMaster_Category DEFAULT N'',
        Excerpt NVARCHAR(500) NOT NULL CONSTRAINT DF_BlogMaster_Excerpt DEFAULT N'',
        Content NVARCHAR(MAX) NOT NULL,
        ImageUrl NVARCHAR(1000) NULL,
        Author NVARCHAR(150) NOT NULL CONSTRAINT DF_BlogMaster_Author DEFAULT N'Airborne Flight Desk',
        IsPublished BIT NOT NULL CONSTRAINT DF_BlogMaster_IsPublished DEFAULT 0,
        PublishedAt DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_BlogMaster_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NULL,
        CONSTRAINT UQ_BlogMaster_Slug UNIQUE (Slug)
    );
END;
GO

CREATE OR ALTER PROCEDURE dbo.GetPublishedBlogs
AS
BEGIN
    SET NOCOUNT ON;
    SELECT BlogId, Title, Slug, Category, Excerpt, Content, ImageUrl, Author,
           IsPublished, PublishedAt, CreatedAt, UpdatedAt
    FROM dbo.BlogMaster
    WHERE IsPublished = 1
      AND (PublishedAt IS NULL OR PublishedAt <= SYSUTCDATETIME())
    ORDER BY COALESCE(PublishedAt, CreatedAt) DESC, BlogId DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.GetPublishedBlogBySlug
    @Slug NVARCHAR(220)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT BlogId, Title, Slug, Category, Excerpt, Content, ImageUrl, Author,
           IsPublished, PublishedAt, CreatedAt, UpdatedAt
    FROM dbo.BlogMaster
    WHERE Slug = @Slug
      AND IsPublished = 1
      AND (PublishedAt IS NULL OR PublishedAt <= SYSUTCDATETIME());
END;
GO

CREATE OR ALTER PROCEDURE dbo.GetAllBlogs
AS
BEGIN
    SET NOCOUNT ON;
    SELECT BlogId, Title, Slug, Category, Excerpt, Content, ImageUrl, Author,
           IsPublished, PublishedAt, CreatedAt, UpdatedAt
    FROM dbo.BlogMaster
    ORDER BY CreatedAt DESC, BlogId DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.CreateBlog
    @Title NVARCHAR(200),
    @Slug NVARCHAR(220),
    @Category NVARCHAR(100),
    @Excerpt NVARCHAR(500),
    @Content NVARCHAR(MAX),
    @ImageUrl NVARCHAR(1000),
    @Author NVARCHAR(150),
    @IsPublished BIT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @NewId INT;
    INSERT INTO dbo.BlogMaster
        (Title, Slug, Category, Excerpt, Content, ImageUrl, Author, IsPublished, PublishedAt)
    VALUES
        (@Title, @Slug, @Category, @Excerpt, @Content, @ImageUrl, @Author,
         @IsPublished, CASE WHEN @IsPublished = 1 THEN SYSUTCDATETIME() ELSE NULL END);
    SET @NewId = CONVERT(INT, SCOPE_IDENTITY());

    SELECT BlogId, Title, Slug, Category, Excerpt, Content, ImageUrl, Author,
           IsPublished, PublishedAt, CreatedAt, UpdatedAt
    FROM dbo.BlogMaster WHERE BlogId = @NewId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.UpdateBlog
    @BlogId INT,
    @Title NVARCHAR(200),
    @Slug NVARCHAR(220),
    @Category NVARCHAR(100),
    @Excerpt NVARCHAR(500),
    @Content NVARCHAR(MAX),
    @ImageUrl NVARCHAR(1000),
    @Author NVARCHAR(150),
    @IsPublished BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.BlogMaster
    SET Title = @Title,
        Slug = @Slug,
        Category = @Category,
        Excerpt = @Excerpt,
        Content = @Content,
        ImageUrl = @ImageUrl,
        Author = @Author,
        IsPublished = @IsPublished,
        PublishedAt = CASE
            WHEN @IsPublished = 0 THEN NULL
            WHEN PublishedAt IS NULL THEN SYSUTCDATETIME()
            ELSE PublishedAt
        END,
        UpdatedAt = SYSUTCDATETIME()
    WHERE BlogId = @BlogId;

    IF @@ROWCOUNT = 0 RETURN;

    SELECT BlogId, Title, Slug, Category, Excerpt, Content, ImageUrl, Author,
           IsPublished, PublishedAt, CreatedAt, UpdatedAt
    FROM dbo.BlogMaster WHERE BlogId = @BlogId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.DeleteBlog
    @BlogId INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.BlogMaster WHERE BlogId = @BlogId;
    SELECT @@ROWCOUNT AS AffectedRows;
END;
GO
