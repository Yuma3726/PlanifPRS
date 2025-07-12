using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.Linq;

namespace PlanifPRS.Models
{
    public class ChecklistModele
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Nom { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string? FamilleEquipement { get; set; }

        public DateTime DateCreation { get; set; } = DateTime.Now;

        [MaxLength(100)]
        public string CreatedByLogin { get; set; } = string.Empty;

        public bool Actif { get; set; } = true;

        [ValidateNever]
        public virtual ICollection<ChecklistElementModele> Elements { get; set; } = new List<ChecklistElementModele>();

        // Propriétés calculées
        [NotMapped]
        public int NombreElements => Elements?.Count ?? 0;

        [NotMapped]
        public int NombreElementsObligatoires => Elements?.Count(e => e.Obligatoire) ?? 0;

        [NotMapped]
        public string FamilleAffichage => !string.IsNullOrEmpty(FamilleEquipement) ? FamilleEquipement : "Générique";
    }
}