CREATE TABLE [dbo].[ChecklistAffectations] (
    [Id]              INT           IDENTITY (1, 1) NOT NULL,
    [ChecklistId]     INT           NOT NULL,
    [UtilisateurId]   INT           NULL,
    [GroupeId]        INT           NULL,
    [TypeAffectation] VARCHAR (20)  NOT NULL,
    [DateAffectation] DATETIME2 (7) DEFAULT (getdate()) NOT NULL,
    [AffectePar]      VARCHAR (50)  NOT NULL,
    CONSTRAINT [PK_ChecklistAffectations] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ChecklistAffectations_Checklist] FOREIGN KEY ([ChecklistId]) REFERENCES [dbo].[PRS_Checklist] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ChecklistAffectations_Groupe] FOREIGN KEY ([GroupeId]) REFERENCES [dbo].[GroupesUtilisateurs] ([Id]),
    CONSTRAINT [FK_ChecklistAffectations_Utilisateur] FOREIGN KEY ([UtilisateurId]) REFERENCES [dbo].[Utilisateurs] ([Id])
);

