using System;
using System.Collections.Generic;


namespace PlanifPRS.Models
{
    public class PrsJalon
    {
        public int Id { get; set; }
        public int? PRSId { get; set; }  // si la FK est nullable
        public string? NomJalon { get; set; }  // nullable si possible
        public DateTime? DatePrevue { get; set; }  // Date peut être null en base ?
        public bool EstValide { get; set; }

        public ICollection<JalonUtilisateur>? JalonUtilisateurs { get; set; }
        public Prs? Prs { get; set; }
    }
}
