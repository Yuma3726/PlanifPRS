CREATE TABLE [dbo].[ChecklistGroupes] (
    [Id]          INT            IDENTITY (1, 1) NOT NULL,
    [Nom]         NVARCHAR (100) NOT NULL,
    [Description] NVARCHAR (500) NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC)
);

