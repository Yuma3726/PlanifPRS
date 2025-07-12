CREATE TABLE [dbo].[PRS_ChecklistItems] (
    [Id]                     INT            IDENTITY (1, 1) NOT NULL,
    [Description]            NVARCHAR (255) NOT NULL,
    [IsRequired]             BIT            DEFAULT ((0)) NOT NULL,
    [Order]                  INT            DEFAULT ((0)) NOT NULL,
    [ChecklistSubCategoryId] INT            NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ChecklistItems_ChecklistSubCategory] FOREIGN KEY ([ChecklistSubCategoryId]) REFERENCES [dbo].[PRS_ChecklistSubCategories] ([Id]) ON DELETE CASCADE
);

