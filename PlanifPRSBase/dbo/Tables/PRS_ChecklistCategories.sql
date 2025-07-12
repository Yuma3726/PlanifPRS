CREATE TABLE [dbo].[PRS_ChecklistCategories] (
    [Id]               INT            IDENTITY (1, 1) NOT NULL,
    [Name]             NVARCHAR (100) NOT NULL,
    [Order]            INT            DEFAULT ((0)) NOT NULL,
    [ChecklistModelId] INT            NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ChecklistCategories_ChecklistModel] FOREIGN KEY ([ChecklistModelId]) REFERENCES [dbo].[PRS_ChecklistModels] ([Id]) ON DELETE CASCADE
);

