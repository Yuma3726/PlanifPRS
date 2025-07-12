using Microsoft.EntityFrameworkCore;
using PlanifPRS.Data;
using PlanifPRS.Models;

namespace PlanifPRS.Services
{
    public class ChecklistService
    {
        private readonly PlanifPrsDbContext _context;

        public ChecklistService(PlanifPrsDbContext context)
        {
            _context = context;
        }

        // Récupérer tous les modèles de checklist
        public async Task<List<ChecklistModele>> GetChecklistModelesAsync(bool activeOnly = true)
        {
            var query = _context.ChecklistModeles
                .Include(m => m.Elements)
                .AsQueryable();

            if (activeOnly)
                query = query.Where(m => m.Actif);

            return await query.OrderBy(m => m.Nom).ToListAsync();
        }

        // Récupérer un modèle par ID
        public async Task<ChecklistModele?> GetChecklistModeleByIdAsync(int id)
        {
            return await _context.ChecklistModeles
                .Include(m => m.Elements.OrderBy(e => e.Priorite).ThenBy(e => e.Categorie))
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        // Récupérer les modèles par famille d'équipement
        public async Task<List<ChecklistModele>> GetChecklistModelesByFamilleAsync(string familleEquipement)
        {
            return await _context.ChecklistModeles
                .Include(m => m.Elements)
                .Where(m => m.FamilleEquipement == familleEquipement && m.Actif)
                .OrderBy(m => m.Nom)
                .ToListAsync();
        }

        // Appliquer un modèle de checklist à un PRS
        public async Task<bool> ApplyChecklistModeleAsync(int prsId, int modeleId, string userLogin)
        {
            try
            {
                var prs = await _context.Prs.FirstOrDefaultAsync(p => p.Id == prsId);
                if (prs == null) return false;

                var modele = await _context.ChecklistModeles
                    .Include(m => m.Elements)
                    .FirstOrDefaultAsync(m => m.Id == modeleId && m.Actif);
                if (modele == null) return false;

                // Supprimer les éléments existants
                var existingItems = await _context.PrsChecklists
                    .Where(c => c.PRSId == prsId)
                    .ToListAsync();
                _context.PrsChecklists.RemoveRange(existingItems);

                // Créer les nouveaux éléments basés sur le modèle
                foreach (var element in modele.Elements)
                {
                    var checklistItem = new PrsChecklist
                    {
                        PRSId = prsId,
                        FamilleId = null,
                        Categorie = element.Categorie,
                        SousCategorie = element.SousCategorie,
                        Libelle = element.Libelle,
                        Tache = element.Libelle, // Compatibilité
                        Priorite = element.Priorite,
                        Obligatoire = element.Obligatoire,
                        EstCoche = false,
                        Statut = false, // Compatibilité
                        DateEcheance = CalculerDateEcheance(prs, element),
                        DateValidation = null,
                        ValidePar = null,
                        CreatedByLogin = userLogin,
                        DateCreation = DateTime.Now,
                        ChecklistModeleSourceId = modeleId,
                        PrsSourceId = null,
                        Commentaire = null
                    };

                    _context.PrsChecklists.Add(checklistItem);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Récupérer la checklist d'un PRS
        public async Task<List<PrsChecklist>> GetPrsChecklistAsync(int prsId)
        {
            return await _context.PrsChecklists
                .Include(c => c.Prs)
                .Where(c => c.PRSId == prsId)
                .OrderBy(c => c.Priorite)
                .ThenBy(c => c.Categorie)
                .ThenBy(c => c.Libelle)
                .ToListAsync();
        }

        // Copier une checklist d'un PRS vers un autre
        public async Task<bool> CopyChecklistFromPrsAsync(int targetPrsId, int sourcePrsId, string userLogin)
        {
            try
            {
                var sourcePrs = await _context.Prs.FirstOrDefaultAsync(p => p.Id == sourcePrsId);
                var targetPrs = await _context.Prs.FirstOrDefaultAsync(p => p.Id == targetPrsId);

                if (sourcePrs == null || targetPrs == null) return false;

                var sourceChecklist = await _context.PrsChecklists
                    .Where(c => c.PRSId == sourcePrsId)
                    .ToListAsync();

                if (!sourceChecklist.Any()) return false;

                // Supprimer les éléments existants du PRS cible
                var existingItems = await _context.PrsChecklists
                    .Where(c => c.PRSId == targetPrsId)
                    .ToListAsync();
                _context.PrsChecklists.RemoveRange(existingItems);

                // Copier les éléments
                foreach (var sourceItem in sourceChecklist)
                {
                    var newItem = new PrsChecklist
                    {
                        PRSId = targetPrsId,
                        FamilleId = sourceItem.FamilleId,
                        Categorie = sourceItem.Categorie,
                        SousCategorie = sourceItem.SousCategorie,
                        Libelle = sourceItem.Libelle,
                        Tache = sourceItem.Tache, // Compatibilité
                        Priorite = sourceItem.Priorite,
                        Obligatoire = sourceItem.Obligatoire,
                        EstCoche = false,
                        Statut = false, // Compatibilité
                        DateEcheance = RecalculerDateEcheance(sourcePrs, targetPrs, sourceItem.DateEcheance),
                        DateValidation = null,
                        ValidePar = null,
                        CreatedByLogin = userLogin,
                        DateCreation = DateTime.Now,
                        ChecklistModeleSourceId = sourceItem.ChecklistModeleSourceId,
                        PrsSourceId = sourcePrsId,
                        Commentaire = null
                    };

                    _context.PrsChecklists.Add(newItem);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Créer une checklist personnalisée
        public async Task<bool> CreateCustomChecklistAsync(int prsId, IEnumerable<dynamic> elements, string userLogin)
        {
            try
            {
                var prs = await _context.Prs.FirstOrDefaultAsync(p => p.Id == prsId);
                if (prs == null) return false;

                // Supprimer les éléments existants
                var existingItems = await _context.PrsChecklists
                    .Where(c => c.PRSId == prsId)
                    .ToListAsync();
                _context.PrsChecklists.RemoveRange(existingItems);

                // Créer les nouveaux éléments
                foreach (dynamic element in elements)
                {
                    var libelle = element.libelle ?? "Élément personnalisé";
                    var checklistItem = new PrsChecklist
                    {
                        PRSId = prsId,
                        FamilleId = null,
                        Categorie = element.categorie ?? "Général",
                        SousCategorie = element.sousCategorie,
                        Libelle = libelle,
                        Tache = libelle, // Compatibilité
                        Priorite = element.priorite ?? 3,
                        Obligatoire = element.obligatoire ?? false,
                        EstCoche = false,
                        Statut = false, // Compatibilité
                        DateEcheance = element.delaiDefautJours != null ?
                            prs.DateCreation.AddDays((int)element.delaiDefautJours) : null,
                        DateValidation = null,
                        ValidePar = null,
                        CreatedByLogin = userLogin,
                        DateCreation = DateTime.Now,
                        ChecklistModeleSourceId = null,
                        PrsSourceId = null,
                        Commentaire = null
                    };

                    _context.PrsChecklists.Add(checklistItem);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Mettre à jour un élément de checklist
        public async Task<bool> UpdateChecklistItemAsync(int itemId, bool estCoche, string? commentaire, string userLogin)
        {
            try
            {
                var item = await _context.PrsChecklists.FirstOrDefaultAsync(c => c.Id == itemId);
                if (item == null) return false;

                item.EstCoche = estCoche;
                item.Statut = estCoche; // Compatibilité
                item.Commentaire = commentaire;

                if (estCoche && !item.DateValidation.HasValue)
                {
                    item.DateValidation = DateTime.Now;
                    item.ValidePar = userLogin;
                }
                else if (!estCoche)
                {
                    item.DateValidation = null;
                    item.ValidePar = null;
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Mettre à jour la date d'échéance d'un élément
        public async Task<bool> UpdateChecklistItemDateEcheanceAsync(int itemId, DateTime? nouvelleEcheance, string userLogin)
        {
            try
            {
                var item = await _context.PrsChecklists.FirstOrDefaultAsync(c => c.Id == itemId);
                if (item == null) return false;

                item.DateEcheance = nouvelleEcheance;
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Rechercher des PRS avec checklist (version unique)
        public async Task<List<dynamic>> SearchPrsWithChecklistAsync(string searchTerm, int limit = 10)
        {
            var results = await _context.Prs
                .Where(p => p.Titre!.Contains(searchTerm) || p.Equipement.Contains(searchTerm))
                .Where(p => _context.PrsChecklists.Any(c => c.PRSId == p.Id))
                .Take(limit)
                .Select(p => new
                {
                    Id = p.Id,
                    Reference = p.Equipement,
                    Titre = p.Titre,
                    NombreElements = _context.PrsChecklists.Count(c => c.PRSId == p.Id),
                    NombreCoches = _context.PrsChecklists.Count(c => c.PRSId == p.Id && c.EstCoche)
                })
                .ToListAsync();

            return results.Cast<dynamic>().ToList();
        }

        // Récupérer les checklists en retard
        public async Task<List<PrsChecklist>> GetChecklistsEnRetardAsync()
        {
            var dateActuelle = DateTime.Now.Date;

            return await _context.PrsChecklists
                .Include(pc => pc.Prs)
                .Where(pc => pc.DateEcheance.HasValue &&
                           pc.DateEcheance.Value.Date < dateActuelle &&
                           !pc.EstCoche)
                .OrderBy(pc => pc.DateEcheance)
                .ThenBy(pc => pc.Priorite)
                .ToListAsync();
        }

        // Récupérer les checklists avec échéance proche
        public async Task<List<PrsChecklist>> GetChecklistsEcheanceProche(int nombreJours = 7)
        {
            var dateActuelle = DateTime.Now.Date;
            var dateLimite = dateActuelle.AddDays(nombreJours);

            return await _context.PrsChecklists
                .Include(pc => pc.Prs)
                .Where(pc => pc.DateEcheance.HasValue &&
                           pc.DateEcheance.Value.Date >= dateActuelle &&
                           pc.DateEcheance.Value.Date <= dateLimite &&
                           !pc.EstCoche)
                .OrderBy(pc => pc.DateEcheance)
                .ThenBy(pc => pc.Priorite)
                .ToListAsync();
        }

        // Obtenir des statistiques globales (version unique)
        public async Task<dynamic> GetStatistiquesChecklistAsync()
        {
            var total = await _context.PrsChecklists.CountAsync();
            var coches = await _context.PrsChecklists.CountAsync(c => c.EstCoche);
            var enRetard = await _context.PrsChecklists.CountAsync(c => !c.EstCoche && c.DateEcheance.HasValue && c.DateEcheance.Value < DateTime.Now);
            var critiques = await _context.PrsChecklists.CountAsync(c => !c.EstCoche && c.Priorite == 1);

            var statistiquesPriorite = await _context.PrsChecklists
                .Where(c => !c.EstCoche)
                .GroupBy(c => c.Priorite)
                .Select(g => new { Priorite = g.Key, Nombre = g.Count() })
                .ToListAsync();

            var statistiquesCategorie = await _context.PrsChecklists
                .Where(c => !c.EstCoche && !string.IsNullOrEmpty(c.Categorie))
                .GroupBy(c => c.Categorie!)
                .Select(g => new { Categorie = g.Key, Nombre = g.Count() })
                .ToListAsync();

            return new
            {
                Total = total,
                Coches = coches,
                EnRetard = enRetard,
                Critiques = critiques,
                PourcentageCompletion = total > 0 ? (int)(coches * 100.0 / total) : 0,
                StatistiquesPriorite = statistiquesPriorite,
                StatistiquesCategorie = statistiquesCategorie
            };
        }

        // Récupérer les modèles populaires
        public async Task<List<ChecklistModele>> GetModelesPopulairesAsync(int limit = 5)
        {
            var modelesUtilises = await _context.PrsChecklists
                .Where(c => c.ChecklistModeleSourceId.HasValue)
                .GroupBy(c => c.ChecklistModeleSourceId!.Value)
                .Select(g => new { ModeleId = g.Key, NombreUtilisations = g.Count() })
                .OrderByDescending(x => x.NombreUtilisations)
                .Take(limit)
                .ToListAsync();

            var modeles = new List<ChecklistModele>();
            foreach (var utilisation in modelesUtilises)
            {
                var modele = await _context.ChecklistModeles
                    .Include(m => m.Elements)
                    .FirstOrDefaultAsync(m => m.Id == utilisation.ModeleId);
                if (modele != null)
                {
                    modeles.Add(modele);
                }
            }

            return modeles;
        }

        // Dupliquer un modèle de checklist
        public async Task<bool> DuplicateChecklistModeleAsync(int sourceModeleId, string newName, string userLogin)
        {
            try
            {
                var sourceModele = await _context.ChecklistModeles
                    .Include(m => m.Elements)
                    .FirstOrDefaultAsync(m => m.Id == sourceModeleId);

                if (sourceModele == null) return false;

                var newModele = new ChecklistModele
                {
                    Nom = newName,
                    Description = sourceModele.Description,
                    FamilleEquipement = sourceModele.FamilleEquipement,
                    DateCreation = DateTime.Now,
                    CreatedByLogin = userLogin,
                    Actif = true,
                    Elements = new List<ChecklistElementModele>()
                };

                foreach (var element in sourceModele.Elements)
                {
                    var newElement = new ChecklistElementModele
                    {
                        Categorie = element.Categorie,
                        SousCategorie = element.SousCategorie,
                        Libelle = element.Libelle,
                        Priorite = element.Priorite,
                        Obligatoire = element.Obligatoire,
                        DelaiDefautJours = element.DelaiDefautJours
                    };
                    newModele.Elements.Add(newElement);
                }

                _context.ChecklistModeles.Add(newModele);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Recherche filtrée des checklists
        public async Task<List<PrsChecklist>> GetChecklistsByCriteriaAsync(
            int? priorite = null,
            bool? enRetard = null,
            bool? estCoche = null,
            string? categorie = null,
            int? prsId = null)
        {
            var query = _context.PrsChecklists
                .Include(c => c.Prs)
                .AsQueryable();

            if (priorite.HasValue)
                query = query.Where(c => c.Priorite == priorite.Value);

            if (estCoche.HasValue)
                query = query.Where(c => c.EstCoche == estCoche.Value);

            if (!string.IsNullOrEmpty(categorie))
                query = query.Where(c => c.Categorie == categorie);

            if (prsId.HasValue)
                query = query.Where(c => c.PRSId == prsId.Value);

            if (enRetard.HasValue && enRetard.Value)
            {
                var aujourdhui = DateTime.Now.Date;
                query = query.Where(c => !c.EstCoche && c.DateEcheance.HasValue && c.DateEcheance.Value.Date < aujourdhui);
            }

            return await query
                .OrderBy(c => c.Priorite)
                .ThenBy(c => c.DateEcheance)
                .ToListAsync();
        }

        // Autres méthodes existantes préservées pour compatibilité
        public async Task<List<string>> GetCategoriesAsync()
        {
            return await _context.PrsChecklists
                .Where(pc => !string.IsNullOrEmpty(pc.Categorie))
                .Select(pc => pc.Categorie!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
        }

        public async Task<List<string>> GetSousCategoriesAsync(string categorie)
        {
            return await _context.PrsChecklists
                .Where(pc => pc.Categorie == categorie && !string.IsNullOrEmpty(pc.SousCategorie))
                .Select(pc => pc.SousCategorie!)
                .Distinct()
                .OrderBy(sc => sc)
                .ToListAsync();
        }

        // Méthodes privées pour calculer les dates d'échéance
        private DateTime? CalculerDateEcheance(Prs prs, ChecklistElementModele element)
        {
            if (!element.DelaiDefautJours.HasValue) return null;

            DateTime dateDebut;
            if (prs.DateDebut != default(DateTime))
                dateDebut = prs.DateDebut;
            else
                dateDebut = prs.DateCreation;

            return dateDebut.AddDays(element.DelaiDefautJours.Value);
        }

        private DateTime? RecalculerDateEcheance(Prs sourcePrs, Prs targetPrs, DateTime? sourceEcheance)
        {
            if (!sourceEcheance.HasValue) return null;

            DateTime sourceDebut = sourcePrs.DateDebut != default(DateTime) ? sourcePrs.DateDebut : sourcePrs.DateCreation;
            DateTime targetDebut = targetPrs.DateDebut != default(DateTime) ? targetPrs.DateDebut : targetPrs.DateCreation;

            var ecartJours = (sourceEcheance.Value - sourceDebut).Days;
            return targetDebut.AddDays(ecartJours);
        }
    }
}