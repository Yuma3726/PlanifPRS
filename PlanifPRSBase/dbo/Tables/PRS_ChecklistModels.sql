CREATE TABLE [dbo].[PRS_ChecklistModels] (
    [Id]          INT            IDENTITY (1, 1) NOT NULL,
    [Name]        NVARCHAR (100) NOT NULL,
    [Description] NVARCHAR (500) NULL,
    [IsDefault]   BIT            DEFAULT ((0)) NOT NULL,
    [CreatedAt]   DATETIME       DEFAULT (getdate()) NOT NULL,
    [UpdatedAt]   DATETIME       DEFAULT (getdate()) NOT NULL,
    [CreatedBy]   NVARCHAR (100) NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC)
);

