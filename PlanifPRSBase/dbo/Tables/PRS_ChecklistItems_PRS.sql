CREATE TABLE [dbo].[PRS_ChecklistItems_PRS] (
    [Id]                     INT            IDENTITY (1, 1) NOT NULL,
    [PrsChecklistTemplateId] INT            NOT NULL,
    [CategoryName]           NVARCHAR (100) NOT NULL,
    [SubCategoryName]        NVARCHAR (100) NULL,
    [Description]            NVARCHAR (255) NOT NULL,
    [IsRequired]             BIT            DEFAULT ((0)) NOT NULL,
    [IsCompleted]            BIT            DEFAULT ((0)) NOT NULL,
    [CompletedBy]            NVARCHAR (100) NULL,
    [CompletedAt]            DATETIME       NULL,
    [Notes]                  NVARCHAR (MAX) NULL,
    [Order]                  INT            DEFAULT ((0)) NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_PrsChecklistItems_PrsChecklistTemplate] FOREIGN KEY ([PrsChecklistTemplateId]) REFERENCES [dbo].[PRS_ChecklistTemplates] ([Id]) ON DELETE CASCADE
);


GO
CREATE NONCLUSTERED INDEX [IX_PRS_ChecklistItems_PRS_TemplateId]
    ON [dbo].[PRS_ChecklistItems_PRS]([PrsChecklistTemplateId] ASC);

