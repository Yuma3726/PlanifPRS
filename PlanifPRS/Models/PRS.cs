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
        public string? Titre { get; set; }  // nullable, car tu as un exemple où ça pourrait être null

        [Required, MaxLength(50)]
        public string Equipement { get; set; }  // required, car tu as dit que c'est jamais null

        [MaxLength(100)]
        public string? ReferenceProduit { get; set; }  // nullable string

        public int? Quantite { get; set; }  // nullable int

        [MaxLength(200)]
        public string? BesoinOperateur { get; set; }  // nullable string

        [MaxLength(200)]
        public string? PresenceClient { get; set; }  // nullable string

        public DateTime DateDebut { get; set; }

        public DateTime DateFin { get; set; }

        [MaxLength(50)]
        public string Statut { get; set; } = "En attente";

        public string? InfoDiverses { get; set; }  // nullable string

        public DateTime DateCreation { get; set; } = DateTime.Now;

        public DateTime DerniereModification { get; set; } = DateTime.Now;

        // Relations

        [ValidateNever]
        public List<PrsChecklist> Checklist { get; set; } = new List<PrsChecklist>();

        [ValidateNever]
        public List<PrsJalon> Jalons { get; set; } = new List<PrsJalon>();

        public int? FamilleId { get; set; } // clé étrangère optionnelle

        [ForeignKey("FamilleId")]
        [ValidateNever]
        public PrsFamille? Famille { get; set; }  // nullable navigation property

        public int LigneId { get; set; } // Clé étrangère

        [ValidateNever]
        public Ligne Ligne { get; set; }  // navigation non-nullable (obligatoire)
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

        public bool? Statut { get; set; } // true=OK, false=NOK ou null

        [MaxLength(500)]
        public string? Commentaire { get; set; }  // nullable

        public int? FamilleId { get; set; }

        [ForeignKey("FamilleId")]
        public PrsFamille? Famille { get; set; }  // nullable navigation property
    }
}
