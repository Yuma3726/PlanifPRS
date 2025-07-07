using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace PlanifPRS.Models
{
    public class Prs
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(200)]
        public string? Titre { get; set; }

        [Required, MaxLength(50)]
        public string Equipement { get; set; }

        [MaxLength(100)]
        public string? ReferenceProduit { get; set; }

        public int? Quantite { get; set; }

        [MaxLength(200)]
        public string? BesoinOperateur { get; set; }

        [MaxLength(200)]
        public string? PresenceClient { get; set; }

        public DateTime DateDebut { get; set; }

        public DateTime DateFin { get; set; }

        [MaxLength(50)]
        public string Statut { get; set; } = "En attente";

        public string? InfoDiverses { get; set; }

        public DateTime DateCreation { get; set; } = DateTime.Now;

        public DateTime DerniereModification { get; set; } = DateTime.Now;

        // Propriété pour la couleur PRS
        [MaxLength(7)]
        public string? CouleurPRS { get; set; }

        // Nouvelle propriété pour stocker le login du créateur
        [MaxLength(100)]
        public string? CreatedByLogin { get; set; }

        // Relations
        [ValidateNever]
        public List<PrsChecklist> Checklist { get; set; } = new List<PrsChecklist>();

        [ValidateNever]
        public List<PrsJalon> Jalons { get; set; } = new List<PrsJalon>();

        public int? FamilleId { get; set; }

        [ForeignKey("FamilleId")]
        [ValidateNever]
        public PrsFamille? Famille { get; set; }

        public int LigneId { get; set; }

        [ValidateNever]
        public Ligne Ligne { get; set; }
    }

    public class PrsChecklist
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Prs")]
        public int PRSId { get; set; }

        public Prs Prs { get; set; }

        [MaxLength(200)]
        public string Tache { get; set; }

        public bool? Statut { get; set; }

        [MaxLength(500)]
        public string? Commentaire { get; set; }

        public int? FamilleId { get; set; }

        [ForeignKey("FamilleId")]
        public PrsFamille? Famille { get; set; }
    }
}