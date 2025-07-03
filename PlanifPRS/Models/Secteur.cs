using System.ComponentModel.DataAnnotations;

namespace PlanifPRS.Models
{
    public class Secteur
    {
        public int Id { get; set; }
        public int IdTypeSecteur { get; set; }

        [Required]
        public string Nom { get; set; }

        public string Description { get; set; }
        public bool Activation { get; set; }
        public bool DoNotDelete { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime? DateModified { get; set; }
        public DateTime? DateDeleted { get; set; }
    }
}