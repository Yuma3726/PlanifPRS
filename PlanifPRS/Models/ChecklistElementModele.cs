using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace PlanifPRS.Models
{
    public class ChecklistElementModele
    {
        public int Id { get; set; }

        public int ChecklistModeleId { get; set; }

        [Required, MaxLength(100)]
        public string Categorie { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? SousCategorie { get; set; }

        [Required, MaxLength(255)]
        public string Libelle { get; set; } = string.Empty;

        [Range(1, 5)]
        public int Priorite { get; set; } = 3;

        public bool Obligatoire { get; set; } = false;

        // Délai par défaut en jours depuis le début de la PRS
        public int? DelaiDefautJours { get; set; }

        [ValidateNever]
        public virtual ChecklistModele ChecklistModele { get; set; } = null!;

        // Propriété calculée pour l'affichage
        [NotMapped]
        public string CategorieComplete
        {
            get
            {
                if (!string.IsNullOrEmpty(SousCategorie))
                    return $"{Categorie} - {SousCategorie}";
                return Categorie;
            }
        }

        [NotMapped]
        public string IconeCategorie
        {
            get
            {
                return Categorie?.ToLower() switch
                {
                    "produit" => "fas fa-box",
                    "documentation" => "fas fa-file-alt",
                    "process" => "fas fa-cogs",
                    "matière" => "fas fa-cubes",
                    "production" => "fas fa-industry",
                    _ => "fas fa-check-circle"
                };
            }
        }

        [NotMapped]
        public string CouleurCategorie
        {
            get
            {
                return Categorie?.ToLower() switch
                {
                    "produit" => "#007bff",
                    "documentation" => "#28a745",
                    "process" => "#fd7e14",
                    "matière" => "#6f42c1",
                    "production" => "#dc3545",
                    _ => "#6c757d"
                };
            }
        }

        [NotMapped]
        public string PrioriteLibelle
        {
            get
            {
                return Priorite switch
                {
                    1 => "🔴 Critique",
                    2 => "🟠 Haute",
                    3 => "🔵 Normale",
                    4 => "🟢 Basse",
                    5 => "⚪ Optionnelle",
                    _ => "❓ Non définie"
                };
            }
        }

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
        public string DelaiAffichage => DelaiDefautJours.HasValue
            ? $"{DelaiDefautJours.Value} jour{(DelaiDefautJours.Value > 1 ? "s" : "")}"
            : "Non défini";
    }
}