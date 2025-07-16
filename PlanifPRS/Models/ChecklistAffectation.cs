using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlanifPRS.Models
{
    [Table("ChecklistAffectations")]
    public class ChecklistAffectation
    {
        public int Id { get; set; }
        public int ChecklistId { get; set; }
        public int? UtilisateurId { get; set; }
        public int? GroupeId { get; set; }

        [MaxLength(20)]
        public string TypeAffectation { get; set; } = "";

        public DateTime DateAffectation { get; set; } = DateTime.Now;

        [MaxLength(100)]
        public string AffectePar { get; set; } = "";

        // Relations
        public PrsChecklist? Checklist { get; set; }
        public Utilisateur? Utilisateur { get; set; }
        public GroupeUtilisateurs? Groupe { get; set; }
    }
}