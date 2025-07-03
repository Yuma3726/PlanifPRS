CREATE TABLE [dbo].[PRS] (
    [Id]                   INT            IDENTITY (1, 1) NOT NULL,
    [Titre]                NVARCHAR (200) NOT NULL,
    [Equipement]           NVARCHAR (50)  NOT NULL,
    [ReferenceProduit]     NVARCHAR (100) NULL,
    [Quantite]             INT            NULL,
    [BesoinOperateur]      NVARCHAR (200) NULL,
    [PresenceClient]       NVARCHAR (200) NULL,
    [DateDebut]            DATETIME       NOT NULL,
    [DateFin]              DATETIME       NOT NULL,
    [Statut]               NVARCHAR (50)  DEFAULT ('En attente') NULL,
    [InfoDiverses]         NVARCHAR (MAX) NULL,
    [DateCreation]         DATETIME       DEFAULT (getdate()) NULL,
    [DerniereModification] DATETIME       DEFAULT (getdate()) NULL,
    [FamilleId]            INT            NULL,
    [LigneId]              INT            NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_PRS_Ligne] FOREIGN KEY ([LigneId]) REFERENCES [dbo].[Lignes] ([Id])
);

