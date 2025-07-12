using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace PlanifPRS.Models
{
    public class PrsChecklist
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Prs")]
        public int PRSId { get; set; }

        // Propriétés existantes (compatibilité)
        [MaxLength(200)]
        public string? Tache { get; set; }

        public bool? Statut { get; set; }

        [MaxLength(500)]
        public string? Commentaire { get; set; }

        public int? FamilleId { get; set; }

        // Nouvelles propriétés pour l'extension
        [MaxLength(100)]
        public string? Categorie { get; set; }

        [MaxLength(100)]
        public string? SousCategorie { get; set; }

        [MaxLength(255)]
        public string? Libelle { get; set; }

        public int Ordre { get; set; } = 0;

        [Range(1, 5)]
        public int Priorite { get; set; } = 3;

        public bool Obligatoire { get; set; } = false;

        public bool EstCoche { get; set; } = false;

        // Gestion des dates
        public DateTime? DateEcheance { get; set; }

        public DateTime? DateValidation { get; set; }

        [MaxLength(100)]
        public string? ValidePar { get; set; }

        [MaxLength(100)]
        public string? CreatedByLogin { get; set; }

        public DateTime DateCreation { get; set; } = DateTime.Now;

        // Sources possibles
        public int? ChecklistModeleSourceId { get; set; }
        public int? PrsSourceId { get; set; }

        // Navigation properties
        [ValidateNever]
        public virtual Prs Prs { get; set; } = null!;

        [ValidateNever]
        public virtual PrsFamille? Famille { get; set; }

        [ValidateNever]
        public virtual ChecklistModele? ChecklistModeleSource { get; set; }

        [ValidateNever]
        public virtual Prs? PrsSource { get; set; }

        // Propriétés calculées pour l'affichage
        [NotMapped]
        public string LibelleAffichage => !string.IsNullOrEmpty(Libelle) ? Libelle : (Tache ?? "");

        [NotMapped]
        public bool EstValide => EstCoche || (Statut.HasValue && Statut.Value);

        [NotMapped]
        public bool EstEnRetard => DateEcheance.HasValue && DateTime.Now > DateEcheance.Value && !EstValide;

        [NotMapped]
        public int JoursRestants => DateEcheance.HasValue ? (DateEcheance.Value.Date - DateTime.Now.Date).Days : 0;

        [NotMapped]
        public string StatutAffichage
        {
            get
            {
                if (EstCoche) return "✅ Validé";
                if (Statut.HasValue && Statut.Value) return "✅ OK";
                if (Statut.HasValue && !Statut.Value) return "❌ NOK";
                if (EstEnRetard) return "⚠️ En retard";
                return "⏳ En attente";
            }
        }

        [NotMapped]
        public string CssClass
        {
            get
            {
                if (EstCoche || (Statut.HasValue && Statut.Value)) return "table-success";
                if (EstEnRetard) return "table-danger";
                if (DateEcheance.HasValue && JoursRestants <= 3 && JoursRestants >= 0) return "table-warning";
                return "";
            }
        }

        // Nouvelles propriétés calculées pour les priorités
        [NotMapped]
        public string CategorieComplete => string.IsNullOrEmpty(SousCategorie) ? (Categorie ?? "Général") : $"{Categorie} - {SousCategorie}";

        [NotMapped]
        public string IconeCategorie => (Categorie?.ToLower()) switch
        {
            "produit" => "fas fa-box",
            "documentation" => "fas fa-file-alt",
            "process" => "fas fa-cogs",
            "matière" => "fas fa-industry",
            "production" => "fas fa-tools",
            "qualité" => "fas fa-check-circle",
            "sécurité" => "fas fa-shield-alt",
            "environnement" => "fas fa-leaf",
            _ => "fas fa-tasks"
        };

        [NotMapped]
        public string CouleurCategorie => (Categorie?.ToLower()) switch
        {
            "produit" => "primary",
            "documentation" => "info",
            "process" => "warning",
            "matière" => "secondary",
            "production" => "success",
            "qualité" => "success",
            "sécurité" => "danger",
            "environnement" => "success",
            _ => "light"
        };

        [NotMapped]
        public string PrioriteLibelle => Priorite switch
        {
            1 => "Critique",
            2 => "Haute",
            3 => "Normale",
            4 => "Basse",
            5 => "Optionnelle",
            _ => "Non définie"
        };

        [NotMapped]
        public string CouleurPriorite => Priorite switch
        {
            1 => "danger",
            2 => "warning",
            3 => "primary",
            4 => "info",
            5 => "secondary",
            _ => "light"
        };

        [NotMapped]
        public string DelaiAffichage
        {
            get
            {
                if (!DateEcheance.HasValue) return "Pas d'échéance";

                var jours = (DateEcheance.Value.Date - DateTime.Now.Date).Days;

                if (jours < 0) return $"En retard de {Math.Abs(jours)} jour{(Math.Abs(jours) > 1 ? "s" : "")}";
                if (jours == 0) return "Échéance aujourd'hui";
                if (jours == 1) return "Échéance demain";

                return $"Dans {jours} jour{(jours > 1 ? "s" : "")}";
            }
        }
    }
}