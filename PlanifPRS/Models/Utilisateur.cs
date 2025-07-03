using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlanifPRS.Models
{
    [Table("Utilisateurs")]
    public class Utilisateur
    {
        public int Id { get; set; }

        public string? Matricule { get; set; }
        public string? Nom { get; set; }
        public string? Prenom { get; set; }
        public string? LoginWindows { get; set; }
        public string? Mail { get; set; }
        public string? Service { get; set; }

        public DateTime? DateImport { get; set; }
        public DateTime? DateDeleted { get; set; }

        public string? Droits { get; set; }

        public ICollection<JalonUtilisateur> JalonUtilisateurs { get; set; } = new List<JalonUtilisateur>();
    }
}
