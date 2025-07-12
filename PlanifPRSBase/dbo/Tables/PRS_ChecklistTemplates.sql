CREATE TABLE [dbo].[PRS_ChecklistTemplates] (
    [Id]                     INT      IDENTITY (1, 1) NOT NULL,
    [PrsId]                  INT      NOT NULL,
    [SourceChecklistModelId] INT      NULL,
    [CreatedAt]              DATETIME DEFAULT (getdate()) NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_PrsChecklistTemplates_Prs] FOREIGN KEY ([PrsId]) REFERENCES [dbo].[PRS] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PrsChecklistTemplates_SourceModel] FOREIGN KEY ([SourceChecklistModelId]) REFERENCES [dbo].[PRS_ChecklistModels] ([Id]) ON DELETE SET NULL
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_PRS_ChecklistTemplates_PrsId]
    ON [dbo].[PRS_ChecklistTemplates]([PrsId] ASC);

