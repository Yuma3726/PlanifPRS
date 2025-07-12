CREATE TABLE [dbo].[PRS_ChecklistSubCategories] (
    [Id]                  INT            IDENTITY (1, 1) NOT NULL,
    [Name]                NVARCHAR (100) NOT NULL,
    [Order]               INT            DEFAULT ((0)) NOT NULL,
    [ChecklistCategoryId] INT            NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ChecklistSubCategories_ChecklistCategory] FOREIGN KEY ([ChecklistCategoryId]) REFERENCES [dbo].[PRS_ChecklistCategories] ([Id]) ON DELETE CASCADE
);

