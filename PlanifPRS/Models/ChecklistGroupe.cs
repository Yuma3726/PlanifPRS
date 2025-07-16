using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlanifPRS.Models
{
    [Table("ChecklistGroupes")]
    public class ChecklistGroupe
    {
        public int ChecklistId { get; set; }
        public int GroupeId { get; set; }

        [ForeignKey("ChecklistId")]
        public virtual PrsChecklist PrsChecklist { get; set; }

        // CORRECTION : Référence vers GroupeUtilisateurs (avec un S)
        [ForeignKey("GroupeId")]
        public virtual GroupeUtilisateurs GroupeUtilisateur { get; set; }

        public DateTime DateAssignation { get; set; } = DateTime.Now;
    }
}