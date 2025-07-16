CREATE TABLE [dbo].[ChecklistUtilisateurs] (
    [Id]    INT            IDENTITY (1, 1) NOT NULL,
    [Login] NVARCHAR (100) NOT NULL,
    [Nom]   NVARCHAR (100) NOT NULL,
    [Email] NVARCHAR (200) NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC)
);

