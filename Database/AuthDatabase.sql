/* Run this script against the same SQL Server database configured in DefaultConnection (AirborneDB by default). */
IF OBJECT_ID(N'dbo.AuthUsers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuthUsers
    (
        UserId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuthUsers PRIMARY KEY,
        LoginId NVARCHAR(80) NOT NULL,
        FullName NVARCHAR(150) NOT NULL,
        PasswordHash VARBINARY(32) NOT NULL,
        PasswordSalt VARBINARY(16) NOT NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_AuthUsers_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2(0) NULL
    );
    CREATE UNIQUE INDEX UX_AuthUsers_LoginId ON dbo.AuthUsers(LoginId);
END;
GO
IF OBJECT_ID(N'dbo.RevokedAuthTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RevokedAuthTokens
    (
        Jti NVARCHAR(64) NOT NULL CONSTRAINT PK_RevokedAuthTokens PRIMARY KEY,
        UserId INT NOT NULL,
        RevokedAt DATETIME2(0) NOT NULL CONSTRAINT DF_RevokedAuthTokens_RevokedAt DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_RevokedAuthTokens_UserId ON dbo.RevokedAuthTokens(UserId);
END;
GO
