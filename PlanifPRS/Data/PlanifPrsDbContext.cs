using Microsoft.EntityFrameworkCore;
using PlanifPRS.Models;

namespace PlanifPRS.Data
{
    public class PlanifPrsDbContext : DbContext
    {
        public PlanifPrsDbContext(DbContextOptions<PlanifPrsDbContext> options) : base(options) { }

        public DbSet<Prs> Prs { get; set; }
        public DbSet<PrsChecklist> PrsChecklists { get; set; }
        public DbSet<ChecklistStandard> ChecklistsStandards { get; set; }
        public DbSet<ChecklistModele> ChecklistModeles { get; set; }
        public DbSet<ChecklistElementModele> ChecklistElementModeles { get; set; }
        public DbSet<PrsJalon> PrsJalons { get; set; }
        public DbSet<Ligne> Lignes { get; set; }
        public DbSet<PrsFamille> PrsFamilles { get; set; }
        public DbSet<Utilisateur> Utilisateurs { get; set; }
        public DbSet<JalonUtilisateur> JalonUtilisateurs { get; set; }
        public DbSet<Secteur> Secteurs { get; set; }
        public DbSet<PrsFichier> PrsFichiers { get; set; }
        public DbSet<LienDossierPrs> LiensDossierPrs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuration table PRS_Famille
            modelBuilder.Entity<PrsFamille>(entity =>
            {
                entity.ToTable("PRS_Famille");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Libelle)
                      .IsRequired()
                      .HasMaxLength(100);
                entity.Property(e => e.CouleurHex)
                      .HasMaxLength(7);
            });

            // Configuration pour la table Secteur
            modelBuilder.Entity<Secteur>(entity =>
            {
                entity.ToTable("Secteur", "dbo");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.IdTypeSecteur).HasColumnName("idTypeSecteur");
                entity.Property(e => e.Nom).HasColumnName("nom").IsRequired();
                entity.Property(e => e.Description).HasColumnName("description");
                entity.Property(e => e.Activation).HasColumnName("activation");
                entity.Property(e => e.DoNotDelete).HasColumnName("doNotDelete");
                entity.Property(e => e.DateCreated).HasColumnName("dateCreated");
                entity.Property(e => e.DateModified).HasColumnName("dateModified");
                entity.Property(e => e.DateDeleted).HasColumnName("dateDeleted");
            });

            // Configuration pour la table Ligne
            modelBuilder.Entity<Ligne>(entity =>
            {
                entity.ToTable("Lignes", "dbo");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nom).IsRequired().HasMaxLength(255);
                entity.Property(e => e.IdSecteur).HasColumnName("idSecteur");
                entity.Property(e => e.Description).HasMaxLength(100).HasColumnName("description");
                entity.Property(e => e.Activation).HasColumnName("activation");
                entity.Property(e => e.DoNotDelete).HasColumnName("doNotDelete");
                entity.Property(e => e.DateCreated).HasColumnName("dateCreated");
                entity.Property(e => e.DateModified).HasColumnName("dateModified");
                entity.Property(e => e.DateDeleted).HasColumnName("dateDeleted");

                entity.HasOne(l => l.Secteur)
                      .WithMany()
                      .HasForeignKey(l => l.IdSecteur)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configuration PRS_Checklist - MISE À JOUR avec nouvelles propriétés
            modelBuilder.Entity<PrsChecklist>(entity =>
            {
                entity.ToTable("PRS_Checklist");
                entity.HasKey(e => e.Id);

                // Colonnes existantes
                entity.Property(e => e.Tache).HasMaxLength(200);
                entity.Property(e => e.Commentaire).HasMaxLength(500);
                entity.Property(e => e.ValidePar).HasMaxLength(100);
                entity.Property(e => e.CreatedByLogin).HasMaxLength(100);

                // Nouvelles colonnes pour les priorités et échéances
                entity.Property(e => e.Categorie).HasMaxLength(100);
                entity.Property(e => e.SousCategorie).HasMaxLength(100);
                entity.Property(e => e.Libelle).HasMaxLength(255);
                entity.Property(e => e.Priorite).IsRequired().HasDefaultValue(3);
                entity.Property(e => e.Obligatoire).IsRequired().HasDefaultValue(false);
                entity.Property(e => e.EstCoche).IsRequired().HasDefaultValue(false);
                entity.Property(e => e.DateEcheance).HasColumnType("datetime2");
                entity.Property(e => e.DateValidation).HasColumnType("datetime2");
                entity.Property(e => e.DateCreation).IsRequired().HasColumnType("datetime2").HasDefaultValueSql("GETDATE()");

                // Relations
                entity.HasOne(c => c.Prs)
                      .WithMany(p => p.Checklist)
                      .HasForeignKey(c => c.PRSId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(c => c.Famille)
                      .WithMany()
                      .HasForeignKey(c => c.FamilleId)
                      .OnDelete(DeleteBehavior.SetNull)
                      .IsRequired(false);

                entity.HasOne(c => c.ChecklistModeleSource)
                      .WithMany()
                      .HasForeignKey(c => c.ChecklistModeleSourceId)
                      .OnDelete(DeleteBehavior.SetNull)
                      .IsRequired(false);

                entity.HasOne(c => c.PrsSource)
                      .WithMany()
                      .HasForeignKey(c => c.PrsSourceId)
                      .OnDelete(DeleteBehavior.NoAction)
                      .IsRequired(false);

                // Index pour optimisation des nouvelles fonctionnalités
                entity.HasIndex(e => new { e.PRSId, e.Priorite })
                      .HasDatabaseName("IX_PRS_Checklist_PRSId_Priorite");

                entity.HasIndex(e => e.DateEcheance)
                      .HasDatabaseName("IX_PRS_Checklist_DateEcheance")
                      .HasFilter("DateEcheance IS NOT NULL");

                entity.HasIndex(e => new { e.Priorite, e.EstCoche })
                      .HasDatabaseName("IX_PRS_Checklist_Priorite_EstCoche");

                entity.HasIndex(e => new { e.PRSId, e.EstCoche, e.Obligatoire })
                      .HasDatabaseName("IX_PRS_Checklist_PRSId_EstCoche_Obligatoire");
            });

            // Configuration ChecklistModele
            modelBuilder.Entity<ChecklistModele>(entity =>
            {
                entity.ToTable("ChecklistModeles");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nom).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.FamilleEquipement).HasMaxLength(100);
                entity.Property(e => e.CreatedByLogin).HasMaxLength(100);
                entity.Property(e => e.DateCreation).IsRequired().HasColumnType("datetime2").HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.Actif).IsRequired().HasDefaultValue(true);

                // Index pour optimisation
                entity.HasIndex(e => e.FamilleEquipement)
                      .HasDatabaseName("IX_ChecklistModeles_FamilleEquipement");
            });

            // Configuration ChecklistElementModele
            modelBuilder.Entity<ChecklistElementModele>(entity =>
            {
                entity.ToTable("ChecklistElementModeles");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Categorie).IsRequired().HasMaxLength(100);
                entity.Property(e => e.SousCategorie).HasMaxLength(100);
                entity.Property(e => e.Libelle).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Priorite).IsRequired().HasDefaultValue(3);
                entity.Property(e => e.Obligatoire).IsRequired().HasDefaultValue(false);

                entity.HasOne(e => e.ChecklistModele)
                      .WithMany(m => m.Elements)
                      .HasForeignKey(e => e.ChecklistModeleId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Index pour optimisation
                entity.HasIndex(e => new { e.ChecklistModeleId, e.Priorite })
                      .HasDatabaseName("IX_ChecklistElementModeles_ChecklistModeleId_Priorite");
            });

            // Relation PRS → PrsFamille
            modelBuilder.Entity<Prs>()
                .HasOne(p => p.Famille)
                .WithMany()
                .HasForeignKey(p => p.FamilleId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relation PRS → Ligne
            modelBuilder.Entity<Prs>()
                .HasOne(p => p.Ligne)
                .WithMany(l => l.PRSs)
                .HasForeignKey(p => p.LigneId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configuration pour la table PrsFichier
            modelBuilder.Entity<PrsFichier>(entity =>
            {
                entity.ToTable("PRS_Fichiers");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.NomOriginal).IsRequired().HasMaxLength(255);
                entity.Property(e => e.CheminFichier).IsRequired().HasMaxLength(500);
                entity.Property(e => e.TypeMime).HasMaxLength(100);
                entity.Property(e => e.UploadParLogin).HasMaxLength(100);

                entity.HasOne(f => f.Prs)
                      .WithMany(p => p.Fichiers)
                      .HasForeignKey(f => f.PrsId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configuration pour la table LienDossierPrs
            modelBuilder.Entity<LienDossierPrs>(entity =>
            {
                entity.ToTable("PRS_LiensDossierPRS");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Chemin).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.AjouteParLogin).IsRequired().HasMaxLength(100);
                entity.Property(e => e.DateAjout).IsRequired().HasDefaultValueSql("GETDATE()");

                entity.HasOne(l => l.Prs)
                     .WithMany(p => p.LiensDossier)
                     .HasForeignKey(l => l.PrsId)
                     .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.PrsId)
                     .HasDatabaseName("IX_PRS_LiensDossierPRS_PrsId");
            });

            // Clé alternative pour LoginWindows dans Utilisateur
            modelBuilder.Entity<Utilisateur>()
                .HasAlternateKey(u => u.LoginWindows);

            // ChecklistStandard → Utilisateur via LoginWindows
            modelBuilder.Entity<ChecklistStandard>()
                .HasOne(cs => cs.Utilisateur)
                .WithMany()
                .HasForeignKey(cs => cs.CreePar)
                .HasPrincipalKey(u => u.LoginWindows)
                .OnDelete(DeleteBehavior.Restrict);

            // Mapping PRS_Jalons
            modelBuilder.Entity<PrsJalon>(entity =>
            {
                entity.ToTable("PRS_Jalons");
                entity.HasKey(e => e.Id);

                entity.HasOne(j => j.Prs)
                      .WithMany(p => p.Jalons)
                      .HasForeignKey(j => j.PRSId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Mapping JalonUtilisateurs (clé composite)
            modelBuilder.Entity<JalonUtilisateur>(entity =>
            {
                entity.ToTable("JalonUtilisateurs");

                entity.HasKey(ju => new { ju.JalonId, ju.UtilisateurId });

                entity.HasOne(ju => ju.PrsJalon)
                      .WithMany(j => j.JalonUtilisateurs)
                      .HasForeignKey(ju => ju.JalonId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ju => ju.Utilisateur)
                      .WithMany(u => u.JalonUtilisateurs)
                      .HasForeignKey(ju => ju.UtilisateurId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }

        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    if (entry.Entity is PrsChecklist checklistItem && checklistItem.DateCreation == default)
                    {
                        checklistItem.DateCreation = DateTime.Now;
                    }
                    if (entry.Entity is ChecklistModele modele && modele.DateCreation == default)
                    {
                        modele.DateCreation = DateTime.Now;
                    }
                }

                if (entry.Entity is Prs prs)
                {
                    prs.DerniereModification = DateTime.Now;
                }
            }
        }
    }
}