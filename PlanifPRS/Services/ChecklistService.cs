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

        // Méthodes pour les modèles de checklist
        public async Task<List<ChecklistModele>> GetChecklistModelesAsync(bool activeOnly = true)
        {
            var query = _context.ChecklistModeles
                .Include(cm => cm.Elements)
                .AsQueryable();

            if (activeOnly)
                query = query.Where(cm => cm.Actif);

            return await query
                .OrderBy(cm => cm.FamilleEquipement)
                .ThenBy(cm => cm.Nom)
                .ToListAsync();
        }

        public async Task<ChecklistModele?> GetChecklistModeleByIdAsync(int id)
        {
            return await _context.ChecklistModeles
                .Include(cm => cm.Elements.OrderBy(e => e.Priorite))
                .FirstOrDefaultAsync(cm => cm.Id == id);
        }

        public async Task<List<ChecklistModele>> GetChecklistModelesByFamilleAsync(string familleEquipement)
        {
            return await _context.ChecklistModeles
                .Include(cm => cm.Elements)
                .Where(cm => cm.Actif &&
                            (cm.FamilleEquipement == familleEquipement ||
                             cm.FamilleEquipement == "Générique"))
                .OrderBy(cm => cm.FamilleEquipement == "Générique" ? 1 : 0)
                .ThenBy(cm => cm.Nom)
                .ToListAsync();
        }

        public async Task<ChecklistModele> CreateChecklistModeleAsync(ChecklistModele modele)
        {
            _context.ChecklistModeles.Add(modele);
            await _context.SaveChangesAsync();
            return modele;
        }

        public async Task<bool> UpdateChecklistModeleAsync(ChecklistModele modele)
        {
            try
            {
                _context.ChecklistModeles.Update(modele);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteChecklistModeleAsync(int id)
        {
            var modele = await _context.ChecklistModeles.FindAsync(id);
            if (modele == null) return false;

            // Soft delete - marquer comme inactif au lieu de supprimer
            modele.Actif = false;
            await _context.SaveChangesAsync();
            return true;
        }

        // Méthodes pour les checklists PRS
        public async Task<List<PrsChecklist>> GetPrsChecklistAsync(int prsId)
        {
            return await _context.PrsChecklists
                .Include(pc => pc.Famille)
                .Include(pc => pc.ChecklistModeleSource)
                .Where(pc => pc.PRSId == prsId)
                .OrderBy(pc => pc.Ordre)
                .ThenBy(pc => pc.Priorite)
                .ThenBy(pc => pc.DateEcheance)
                .ThenBy(pc => pc.Categorie)
                .ThenBy(pc => pc.SousCategorie)
                .ToListAsync();
        }

        public async Task<bool> ApplyChecklistModeleAsync(int prsId, int checklistModeleId, string userLogin)
        {
            try
            {
                var modele = await GetChecklistModeleByIdAsync(checklistModeleId);
                if (modele == null) return false;

                // Supprimer les éléments existants de la checklist PRS
                var existingItems = await _context.PrsChecklists
                    .Where(pc => pc.PRSId == prsId)
                    .ToListAsync();

                _context.PrsChecklists.RemoveRange(existingItems);

                // Récupérer les infos du PRS pour calculer les échéances
                var prs = await _context.Prs.FindAsync(prsId);
                if (prs == null) return false;

                // Créer les nouveaux éléments basés sur le modèle
                int ordre = 1;
                foreach (var element in modele.Elements.OrderBy(e => e.Priorite))
                {
                    var prsChecklistItem = new PrsChecklist
                    {
                        PRSId = prsId,
                        Categorie = element.Categorie,
                        SousCategorie = element.SousCategorie,
                        Libelle = element.Libelle,
                        Tache = element.Libelle, // Compatibilité avec l'ancien système
                        Ordre = ordre++,
                        Priorite = element.Priorite,
                        Obligatoire = element.Obligatoire,
                        EstCoche = false,
                        Statut = null,
                        DateEcheance = CalculerDateEcheance(prs, element),
                        ChecklistModeleSourceId = checklistModeleId,
                        CreatedByLogin = userLogin,
                        DateCreation = DateTime.Now
                    };

                    _context.PrsChecklists.Add(prsChecklistItem);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                // Log l'erreur si nécessaire
                Console.WriteLine($"Erreur lors de l'application du modèle de checklist: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CopyChecklistFromPrsAsync(int targetPrsId, int sourcePrsId, string userLogin)
        {
            try
            {
                var sourceChecklist = await GetPrsChecklistAsync(sourcePrsId);
                if (!sourceChecklist.Any()) return false;

                // Récupérer les infos des PRS pour recalculer les échéances
                var sourcePrs = await _context.Prs.FindAsync(sourcePrsId);
                var targetPrs = await _context.Prs.FindAsync(targetPrsId);
                if (sourcePrs == null || targetPrs == null) return false;

                // Supprimer les éléments existants
                var existingItems = await _context.PrsChecklists
                    .Where(pc => pc.PRSId == targetPrsId)
                    .ToListAsync();

                _context.PrsChecklists.RemoveRange(existingItems);

                // Copier les éléments
                foreach (var sourceItem in sourceChecklist)
                {
                    var newItem = new PrsChecklist
                    {
                        PRSId = targetPrsId,
                        Categorie = sourceItem.Categorie,
                        SousCategorie = sourceItem.SousCategorie,
                        Libelle = sourceItem.Libelle,
                        Tache = sourceItem.Tache,
                        Ordre = sourceItem.Ordre,
                        Priorite = sourceItem.Priorite,
                        Obligatoire = sourceItem.Obligatoire,
                        EstCoche = false, // Reset du statut
                        Statut = null,
                        Commentaire = null,
                        DateEcheance = RecalculerDateEcheance(sourcePrs, targetPrs, sourceItem.DateEcheance),
                        PrsSourceId = sourcePrsId,
                        CreatedByLogin = userLogin,
                        DateCreation = DateTime.Now
                    };

                    _context.PrsChecklists.Add(newItem);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la copie de la checklist: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateChecklistItemAsync(int itemId, bool estCoche, string? commentaire, string userLogin)
        {
            try
            {
                var item = await _context.PrsChecklists.FindAsync(itemId);
                if (item == null) return false;

                item.EstCoche = estCoche;
                item.Commentaire = commentaire;

                if (estCoche)
                {
                    item.DateValidation = DateTime.Now;
                    item.ValidePar = userLogin;
                }
                else
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

        // AJOUT DES MÉTHODES MANQUANTES
        public async Task<bool> CreateCustomChecklistAsync(int prsId, List<PrsChecklist> elements, string userLogin)
        {
            try
            {
                // Supprimer les éléments existants de la checklist PRS
                var existingItems = await _context.PrsChecklists
                    .Where(pc => pc.PRSId == prsId)
                    .ToListAsync();

                _context.PrsChecklists.RemoveRange(existingItems);

                // Ajouter les nouveaux éléments
                foreach (var element in elements)
                {
                    element.PRSId = prsId;
                    element.CreatedByLogin = userLogin;
                    element.DateCreation = DateTime.Now;
                    _context.PrsChecklists.Add(element);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteChecklistItemAsync(int itemId)
        {
            try
            {
                var item = await _context.PrsChecklists.FindAsync(itemId);
                if (item == null) return false;

                _context.PrsChecklists.Remove(item);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<object> GetChecklistStatsAsync(int prsId)
        {
            var items = await _context.PrsChecklists
                .Where(pc => pc.PRSId == prsId)
                .ToListAsync();

            var total = items.Count;
            var completed = items.Count(i => i.EstCoche || (i.Statut.HasValue && i.Statut.Value));
            var obligatoires = items.Count(i => i.Obligatoire);
            var obligatoiresCompletes = items.Count(i => i.Obligatoire && (i.EstCoche || (i.Statut.HasValue && i.Statut.Value)));

            return new
            {
                Total = total,
                Completed = completed,
                Remaining = total - completed,
                Obligatoires = obligatoires,
                ObligatoiresCompletes = obligatoiresCompletes,
                PourcentageCompletion = total > 0 ? (double)completed / total * 100 : 0,
                PourcentageObligatoires = obligatoires > 0 ? (double)obligatoiresCompletes / obligatoires * 100 : 0
            };
        }

        public async Task<List<string>> GetCategoriesUtiliseesAsync()
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

        public async Task<List<PrsChecklist>> SearchPrsWithChecklistAsync(string searchTerm, int limit = 10)
        {
            return await _context.PrsChecklists
                .Include(pc => pc.Prs)
                .Where(pc => pc.Prs.Titre.Contains(searchTerm) ||
                           pc.Prs.Equipement.Contains(searchTerm) ||
                           pc.Prs.Id.ToString().Contains(searchTerm))
                .GroupBy(pc => pc.PRSId)
                .Select(g => g.First())
                .Take(limit)
                .ToListAsync();
        }

        public async Task<Dictionary<string, int>> GetStatistiquesChecklistAsync()
        {
            var dateActuelle = DateTime.Now.Date;

            var stats = new Dictionary<string, int>
            {
                ["TotalElements"] = await _context.PrsChecklists.CountAsync(),
                ["ElementsCoches"] = await _context.PrsChecklists.CountAsync(pc => pc.EstCoche),
                ["ElementsEnRetard"] = await _context.PrsChecklists.CountAsync(pc => pc.DateEcheance.HasValue && pc.DateEcheance.Value.Date < dateActuelle && !pc.EstCoche),
                ["ElementsEcheanceProche"] = await _context.PrsChecklists.CountAsync(pc => pc.DateEcheance.HasValue && pc.DateEcheance.Value.Date >= dateActuelle && pc.DateEcheance.Value.Date <= dateActuelle.AddDays(7) && !pc.EstCoche),
                ["ElementsCritiques"] = await _context.PrsChecklists.CountAsync(pc => pc.Obligatoire && !pc.EstCoche)
            };

            return stats;
        }
    }
}