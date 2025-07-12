using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.Linq;

namespace PlanifPRS.Models
{
    public class Prs
    {
        public Prs()
        {
            Checklist = new List<PrsChecklist>();
            Jalons = new List<PrsJalon>();
            Fichiers = new List<PrsFichier>();
            LiensDossier = new List<LienDossierPrs>();
        }

        [Key]
        public int Id { get; set; }

        [MaxLength(200)]
        public string? Titre { get; set; }

        [Required, MaxLength(50)]
        public string Equipement { get; set; }

        [MaxLength(100)]
        public string? ReferenceProduit { get; set; }

        public int? Quantite { get; set; }

        [MaxLength(200)]
        public string? BesoinOperateur { get; set; }

        [MaxLength(200)]
        public string? PresenceClient { get; set; }

        public DateTime DateDebut { get; set; }

        public DateTime DateFin { get; set; }

        [MaxLength(50)]
        public string Statut { get; set; } = "En attente";

        public string? InfoDiverses { get; set; }

        public DateTime DateCreation { get; set; } = DateTime.Now;

        public DateTime DerniereModification { get; set; } = DateTime.Now;

        [MaxLength(7)]
        public string? CouleurPRS { get; set; }

        [MaxLength(100)]
        public string? CreatedByLogin { get; set; }

        // Relations
        [ValidateNever]
        public List<PrsChecklist> Checklist { get; set; }

        [ValidateNever]
        public List<PrsJalon> Jalons { get; set; }

        [ValidateNever]
        public List<PrsFichier> Fichiers { get; set; }

        [ValidateNever]
        public List<LienDossierPrs> LiensDossier { get; set; }

        public int? FamilleId { get; set; }

        [ForeignKey("FamilleId")]
        [ValidateNever]
        public PrsFamille? Famille { get; set; }

        public int LigneId { get; set; }

        [ValidateNever]
        public Ligne Ligne { get; set; }

        // Propriétés calculées pour la checklist avec support des priorités
        [NotMapped]
        public bool AChecklist => Checklist != null && Checklist.Any();

        [NotMapped]
        public int NombreElementsChecklist => Checklist?.Count ?? 0;

        [NotMapped]
        public int NombreElementsValides => Checklist?.Count(c => c.EstCoche) ?? 0;

        [NotMapped]
        public int NombreElementsObligatoires => Checklist?.Count(c => c.Obligatoire) ?? 0;

        [NotMapped]
        public int NombreElementsObligatoiresValides => Checklist?.Count(c => c.Obligatoire && c.EstCoche) ?? 0;

        [NotMapped]
        public int NombreElementsCritiques => Checklist?.Count(c => c.Priorite == 1) ?? 0;

        [NotMapped]
        public int NombreElementsCritiquesValides => Checklist?.Count(c => c.Priorite == 1 && c.EstCoche) ?? 0;

        [NotMapped]
        public int NombreElementsEnRetard => Checklist?.Count(c => c.EstEnRetard) ?? 0;

        [NotMapped]
        public int NombreElementsEcheanceProche => Checklist?.Count(c => !c.EstCoche && c.DateEcheance.HasValue && c.JoursRestants <= 3 && c.JoursRestants >= 0) ?? 0;

        [NotMapped]
        public double PourcentageCompletionChecklist
        {
            get
            {
                if (NombreElementsChecklist == 0) return 100;
                return Math.Round((double)NombreElementsValides / NombreElementsChecklist * 100, 1);
            }
        }

        [NotMapped]
        public double PourcentageCompletionObligatoire
        {
            get
            {
                if (NombreElementsObligatoires == 0) return 100;
                return Math.Round((double)NombreElementsObligatoiresValides / NombreElementsObligatoires * 100, 1);
            }
        }

        [NotMapped]
        public double PourcentageCompletionCritique
        {
            get
            {
                if (NombreElementsCritiques == 0) return 100;
                return Math.Round((double)NombreElementsCritiquesValides / NombreElementsCritiques * 100, 1);
            }
        }

        [NotMapped]
        public string StatutChecklist
        {
            get
            {
                if (!AChecklist) return "Aucune checklist";
                if (NombreElementsEnRetard > 0) return "Éléments en retard";
                if (PourcentageCompletionCritique < 100) return "Éléments critiques manquants";
                if (PourcentageCompletionObligatoire < 100) return "Éléments obligatoires manquants";
                if (NombreElementsEcheanceProche > 0) return "Échéances proches";
                if (PourcentageCompletionChecklist == 100) return "Complète";
                return "En cours";
            }
        }

        [NotMapped]
        public string CouleurStatutChecklist
        {
            get
            {
                if (!AChecklist) return "#6c757d"; // secondary
                if (NombreElementsEnRetard > 0) return "#dc3545"; // danger
                if (PourcentageCompletionCritique < 100) return "#dc3545"; // danger
                if (PourcentageCompletionObligatoire < 100) return "#fd7e14"; // orange
                if (NombreElementsEcheanceProche > 0) return "#ffc107"; // warning
                if (PourcentageCompletionChecklist == 100) return "#28a745"; // success
                return "#007bff"; // primary
            }
        }

        [NotMapped]
        public string IconeStatutChecklist
        {
            get
            {
                if (!AChecklist) return "fas fa-minus-circle";
                if (NombreElementsEnRetard > 0) return "fas fa-exclamation-triangle";
                if (PourcentageCompletionCritique < 100) return "fas fa-skull";
                if (PourcentageCompletionObligatoire < 100) return "fas fa-exclamation-circle";
                if (NombreElementsEcheanceProche > 0) return "fas fa-clock";
                if (PourcentageCompletionChecklist == 100) return "fas fa-check-circle";
                return "fas fa-tasks";
            }
        }

        // Propriétés pour les statistiques par priorité
        [NotMapped]
        public Dictionary<int, int> StatistiquesPriorite
        {
            get
            {
                if (Checklist == null) return new Dictionary<int, int>();

                return Checklist
                    .Where(c => !c.EstCoche)
                    .GroupBy(c => c.Priorite)
                    .ToDictionary(g => g.Key, g => g.Count());
            }
        }

        [NotMapped]
        public Dictionary<string, int> StatistiquesCategorie
        {
            get
            {
                if (Checklist == null) return new Dictionary<string, int>();

                return Checklist
                    .Where(c => !c.EstCoche && !string.IsNullOrEmpty(c.Categorie))
                    .GroupBy(c => c.Categorie!)
                    .ToDictionary(g => g.Key, g => g.Count());
            }
        }

        // Propriétés pour les alertes
        [NotMapped]
        public List<PrsChecklist> ElementsEnRetard => Checklist?.Where(c => c.EstEnRetard).OrderBy(c => c.DateEcheance).ToList() ?? new List<PrsChecklist>();

        [NotMapped]
        public List<PrsChecklist> ElementsEcheanceProche => Checklist?.Where(c => !c.EstCoche && c.DateEcheance.HasValue && c.JoursRestants <= 7 && c.JoursRestants >= 0).OrderBy(c => c.DateEcheance).ToList() ?? new List<PrsChecklist>();

        [NotMapped]
        public List<PrsChecklist> ElementsCritiquesNonValides => Checklist?.Where(c => c.Priorite == 1 && !c.EstCoche).OrderBy(c => c.DateEcheance).ToList() ?? new List<PrsChecklist>();

        [NotMapped]
        public List<PrsChecklist> ElementsObligatoiresNonValides => Checklist?.Where(c => c.Obligatoire && !c.EstCoche).OrderBy(c => c.Priorite).ThenBy(c => c.DateEcheance).ToList() ?? new List<PrsChecklist>();

        // Méthodes utilitaires pour les priorités
        [NotMapped]
        public bool APrioriteCritique => Checklist?.Any(c => c.Priorite == 1) ?? false;

        [NotMapped]
        public bool APrioriteHaute => Checklist?.Any(c => c.Priorite == 2) ?? false;

        [NotMapped]
        public bool AElementsEnRetard => NombreElementsEnRetard > 0;

        [NotMapped]
        public bool AElementsEcheanceProche => NombreElementsEcheanceProche > 0;

        [NotMapped]
        public bool EstBloquePourElements => NombreElementsEnRetard > 0 || PourcentageCompletionCritique < 100;

        // Propriétés pour l'affichage des badges
        [NotMapped]
        public string BadgeStatut
        {
            get
            {
                var cssClass = "badge ";
                cssClass += StatutChecklist switch
                {
                    "Aucune checklist" => "badge-secondary",
                    "Éléments en retard" => "badge-danger",
                    "Éléments critiques manquants" => "badge-danger",
                    "Éléments obligatoires manquants" => "badge-warning",
                    "Échéances proches" => "badge-warning",
                    "Complète" => "badge-success",
                    _ => "badge-primary"
                };
                return cssClass;
            }
        }

        [NotMapped]
        public string TexteBadgeCompletion
        {
            get
            {
                if (!AChecklist) return "Pas de checklist";
                return $"{NombreElementsValides}/{NombreElementsChecklist} ({PourcentageCompletionChecklist:F0}%)";
            }
        }

        [NotMapped]
        public string TexteBadgeObligatoire
        {
            get
            {
                if (NombreElementsObligatoires == 0) return "Aucun obligatoire";
                return $"{NombreElementsObligatoiresValides}/{NombreElementsObligatoires} obligatoires";
            }
        }

        [NotMapped]
        public string TexteBadgeCritique
        {
            get
            {
                if (NombreElementsCritiques == 0) return "Aucun critique";
                return $"{NombreElementsCritiquesValides}/{NombreElementsCritiques} critiques";
            }
        }

        // Méthode pour obtenir un résumé complet de la checklist
        [NotMapped]
        public object ResumeChecklist
        {
            get
            {
                return new
                {
                    AChecklist,
                    NombreElementsChecklist,
                    NombreElementsValides,
                    NombreElementsObligatoires,
                    NombreElementsObligatoiresValides,
                    NombreElementsCritiques,
                    NombreElementsCritiquesValides,
                    NombreElementsEnRetard,
                    NombreElementsEcheanceProche,
                    PourcentageCompletionChecklist,
                    PourcentageCompletionObligatoire,
                    PourcentageCompletionCritique,
                    StatutChecklist,
                    CouleurStatutChecklist,
                    IconeStatutChecklist,
                    EstBloquePourElements,
                    StatistiquesPriorite,
                    StatistiquesCategorie
                };
            }
        }

        // Méthode pour obtenir les éléments par priorité
        public List<PrsChecklist> GetElementsParPriorite(int priorite)
        {
            return Checklist?.Where(c => c.Priorite == priorite).OrderBy(c => c.Categorie).ThenBy(c => c.Libelle).ToList() ?? new List<PrsChecklist>();
        }

        // Méthode pour obtenir les éléments par catégorie
        public List<PrsChecklist> GetElementsParCategorie(string categorie)
        {
            return Checklist?.Where(c => c.Categorie == categorie).OrderBy(c => c.Priorite).ThenBy(c => c.Libelle).ToList() ?? new List<PrsChecklist>();
        }

        // Méthode pour vérifier si le PRS peut être validé
        public bool PeutEtreValide()
        {
            return !EstBloquePourElements && PourcentageCompletionObligatoire >= 100;
        }

        // Méthode pour obtenir la prochaine échéance critique
        public DateTime? ProchaineEcheanceCritique()
        {
            return Checklist?
                .Where(c => !c.EstCoche && c.DateEcheance.HasValue && (c.Priorite == 1 || c.Obligatoire))
                .OrderBy(c => c.DateEcheance)
                .FirstOrDefault()?.DateEcheance;
        }

        // Propriété pour l'affichage de la prochaine échéance
        [NotMapped]
        public string ProchaineEcheanceAffichage
        {
            get
            {
                var prochaine = ProchaineEcheanceCritique();
                if (!prochaine.HasValue) return "Aucune échéance critique";

                var jours = (prochaine.Value.Date - DateTime.Now.Date).Days;
                if (jours < 0) return $"En retard de {Math.Abs(jours)} jour{(Math.Abs(jours) > 1 ? "s" : "")}";
                if (jours == 0) return "Échéance aujourd'hui";
                if (jours == 1) return "Échéance demain";
                return $"Dans {jours} jour{(jours > 1 ? "s" : "")}";
            }
        }

        // Méthode pour obtenir une liste d'alertes pour ce PRS
        public List<string> GetAlertes()
        {
            var alertes = new List<string>();

            if (NombreElementsEnRetard > 0)
                alertes.Add($"{NombreElementsEnRetard} élément{(NombreElementsEnRetard > 1 ? "s" : "")} en retard");

            if (PourcentageCompletionCritique < 100)
                alertes.Add($"{NombreElementsCritiques - NombreElementsCritiquesValides} élément{(NombreElementsCritiques - NombreElementsCritiquesValides > 1 ? "s" : "")} critique{(NombreElementsCritiques - NombreElementsCritiquesValides > 1 ? "s" : "")} non validé{(NombreElementsCritiques - NombreElementsCritiquesValides > 1 ? "s" : "")}");

            if (NombreElementsEcheanceProche > 0)
                alertes.Add($"{NombreElementsEcheanceProche} élément{(NombreElementsEcheanceProche > 1 ? "s" : "")} avec échéance proche");

            return alertes;
        }

        // Méthodes de compatibilité avec l'ancien système
        [NotMapped]
        public int NombreElementsValides => NombreElementsValides;

        [NotMapped]
        public bool EstValide => EstCoche || (Statut.HasValue && Statut.Value);
    }
}