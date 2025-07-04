using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace PlanifPRS.Models
{
    public class PrsChecklist2
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Prs")]
        public int PRSId { get; set; }

        [ValidateNever]
        public Prs Prs { get; set; }

        // ✅ NOUVELLES PROPRIÉTÉS POUR LES CHECKLISTS
        [MaxLength(255)]
        public string Titre { get; set; }  // Le titre de l'élément de checklist

        [MaxLength(1000)]
        public string? Description { get; set; }  // Description optionnelle

        public bool EstCoche { get; set; } = false;  // Remplace votre Statut bool

        [MaxLength(100)]
        public string? Categorie { get; set; }  // "Produit", "Documentation", "Process", etc.

        public int Ordre { get; set; } = 0;  // Pour trier les éléments

        public DateTime DateCreation { get; set; } = DateTime.Now;

        public DateTime? DateCompletee { get; set; }  // Quand l'élément a été coché

        // ✅ CONSERVATION DE VOS PROPRIÉTÉS EXISTANTES
        [MaxLength(200)]
        public string? Tache { get; set; }  // Votre propriété existante

        public bool? Statut { get; set; } // true=OK, false=NOK ou null - votre propriété existante

        [MaxLength(500)]
        public string? Commentaire { get; set; }  // Votre propriété existante

        public int? FamilleId { get; set; }  // Votre propriété existante

        [ForeignKey("FamilleId")]
        [ValidateNever]
        public PrsFamille? Famille { get; set; }  // Votre propriété existante
    }
}