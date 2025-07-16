using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlanifPRS.Models
{
    [Table("ChecklistUtilisateurs")]
    public class ChecklistUtilisateur
    {
        public int ChecklistId { get; set; }
        public int UtilisateurId { get; set; }

        [ForeignKey("ChecklistId")]
        public virtual PrsChecklist PrsChecklist { get; set; }

        [ForeignKey("UtilisateurId")]
        public virtual Utilisateur Utilisateur { get; set; }

        public DateTime DateAssignation { get; set; } = DateTime.Now;
    }
}