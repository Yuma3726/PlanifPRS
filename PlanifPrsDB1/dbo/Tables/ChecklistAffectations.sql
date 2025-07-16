CREATE TABLE [dbo].[ChecklistAffectations] (
    [Id]               INT            IDENTITY (1, 1) NOT NULL,
    [PrsChecklistId]   INT            NOT NULL,
    [UtilisateurLogin] NVARCHAR (100) NULL,
    [GroupeId]         INT            NULL,
    [DateAffectation]  DATETIME2 (7)  NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    FOREIGN KEY ([PrsChecklistId]) REFERENCES [dbo].[PRS_Checklist] ([Id])
);

