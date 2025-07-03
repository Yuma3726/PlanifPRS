using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PlanifPRS.Models
{
    public class ChecklistStandard
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Tache { get; set; }

        [MaxLength(100)]
        public string FamilleEquipement { get; set; }

        [MaxLength(100)]
        public string Categorie { get; set; }

        public bool Actif { get; set; } = true;

        [MaxLength(100)]
        public string CreePar { get; set; }

        public Utilisateur Utilisateur { get; set; }
    }


}
