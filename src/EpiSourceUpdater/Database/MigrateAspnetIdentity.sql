--If you are migrate from Microsoft.AspNetCore.Identity.EntityFrameworkCore 2.0 to 5.0

BEGIN TRANSACTION;
GO

ALTER TABLE [dbo].[AspNetUsers] ADD [ConcurrencyStamp] nvarchar(max) NULL;
GO

ALTER TABLE [dbo].[AspNetUsers] ADD [LockoutEnd] datetimeoffset NULL;
GO

ALTER TABLE [dbo].[AspNetUsers] ADD [NormalizedEmail] nvarchar(256) NULL;
GO

ALTER TABLE [dbo].[AspNetUsers] ADD [NormalizedUserName] nvarchar(256) NULL;
GO

ALTER TABLE [dbo].[AspNetRoles] ADD [ConcurrencyStamp] nvarchar(max) NULL;
GO

ALTER TABLE [dbo].[AspNetRoles] ADD [NormalizedName] nvarchar(256) NULL;
GO

UPDATE [dbo].[AspNetUsers] SET [NormalizedEmail] = UPPER([Email]), [NormalizedUserName] = UPPER([UserName]) WHERE [NormalizedEmail] IS NULL
GO

UPDATE [dbo].[AspNetRoles] SET [NormalizedName] = UPPER(Name) WHERE [NormalizedName] IS NULL
GO

CREATE TABLE [dbo].[AspNetRoleClaims] (
    [Id]         INT            IDENTITY (1, 1) NOT NULL,
    [ClaimType]  NVARCHAR (MAX) NULL,
    [ClaimValue] NVARCHAR (MAX) NULL,
    [RoleId]     NVARCHAR (128) NOT NULL,
    CONSTRAINT [PK_AspNetRoleClaims]
 PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId]
 FOREIGN KEY ([RoleId])
  REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE
)
GO


COMMIT;
GO