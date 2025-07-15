using System.ComponentModel.DataAnnotations;

namespace PlanifPRS.Models
{
    public class ChecklistAffectation
    {
        public int Id { get; set; }

        public int ChecklistId { get; set; }

        public int? UtilisateurId { get; set; }

        public int? GroupeId { get; set; }

        [Required, MaxLength(50)]
        public string TypeAffectation { get; set; } // "Utilisateur" ou "Groupe"

        public DateTime DateAffectation { get; set; } = DateTime.Now;

        [MaxLength(100)]
        public string? AffectePar { get; set; }

        public PrsChecklist Checklist { get; set; }
        public Utilisateur? Utilisateur { get; set; }
        public GroupeUtilisateurs? Groupe { get; set; }
    }
}