CREATE TABLE [dbo].[PRS_Checklist] (
    [Id]          INT            IDENTITY (1, 1) NOT NULL,
    [PRSId]       INT            NULL,
    [Tache]       NVARCHAR (200) NULL,
    [Statut]      BIT            NULL,
    [Commentaire] NVARCHAR (500) NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    FOREIGN KEY ([PRSId]) REFERENCES [dbo].[PRS] ([Id]) ON DELETE CASCADE
);

