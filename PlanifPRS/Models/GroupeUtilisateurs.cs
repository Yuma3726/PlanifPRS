using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlanifPRS.Models
{
    [Table("GroupesUtilisateurs")]
    public class GroupeUtilisateurs
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string NomGroupe { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public DateTime DateCreation { get; set; } = DateTime.Now;

        [MaxLength(100)]
        public string? CreePar { get; set; }

        public bool Actif { get; set; } = true;

        // Changement du nom de la propriété pour éviter le conflit
        public ICollection<GroupeUtilisateur> Membres { get; set; } = new List<GroupeUtilisateur>();
        public ICollection<PrsAffectation> PrsAffectations { get; set; } = new List<PrsAffectation>();

        public ICollection<ChecklistAffectation> ChecklistAffectations { get; set; } = new List<ChecklistAffectation>();

    }
}