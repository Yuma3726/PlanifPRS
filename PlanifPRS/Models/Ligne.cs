using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PlanifPRS.Models
{
    public class Ligne
    {
        public int Id { get; set; }

        [Required]
        [StringLength(255)] // adapte la taille selon la DB
        public string Nom { get; set; }

        // Ajout de la clé étrangère vers Secteur
        public int IdSecteur { get; set; }

        [StringLength(100)] // selon la taille dans SQL (varchar(100))
        public string Description { get; set; }

        public bool? Activation { get; set; } // nullable bool

        public bool? DoNotDelete { get; set; } // nullable bool

        public DateTime? DateCreated { get; set; }

        public DateTime? DateModified { get; set; }

        public DateTime? DateDeleted { get; set; }

        // Navigation properties
        public Secteur Secteur { get; set; } // Relation vers Secteur
        public ICollection<Prs> PRSs { get; set; }

        public Ligne()
        {
            PRSs = new HashSet<Prs>();
        }
    }
}