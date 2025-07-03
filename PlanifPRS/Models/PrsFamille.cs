using System.ComponentModel.DataAnnotations;

namespace PlanifPRS.Models
{
    public class PrsFamille
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Libelle { get; set; }

        [MaxLength(7)]
        public string CouleurHex { get; set; } // exemple : #FF0000
    }


}
