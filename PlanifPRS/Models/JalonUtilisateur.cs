using System.ComponentModel.DataAnnotations;

namespace PlanifPRS.Models
{
    public class JalonUtilisateur
    {
        public int JalonId { get; set; }        // Correspond à la colonne JalonId en base
        public int UtilisateurId { get; set; }

        public PrsJalon PrsJalon { get; set; }
        public Utilisateur Utilisateur { get; set; }
    }
}

