using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
                .Include(cm => cm.Elements.OrderBy(e => e.Ordre))
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

                // Créer les nouveaux éléments basés sur le modèle
                var newItems = modele.Elements.Select(element => new PrsChecklist
                {
                    PRSId = prsId,
                    Categorie = element.Categorie,
                    SousCategorie = element.SousCategorie,
                    Libelle = element.Libelle,
                    Tache = element.Libelle, // Compatibilité avec l'ancien système
                    Ordre = element.Ordre,
                    Obligatoire = element.Obligatoire,
                    EstCoche = false,
                    Statut = null,
                    ChecklistModeleSourceId = checklistModeleId,
                    CreatedByLogin = userLogin,
                    DateCreation = DateTime.Now
                }).ToList();

                _context.PrsChecklists.AddRange(newItems);
                await _context.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> CopyChecklistFromPrsAsync(int targetPrsId, int sourcePrsId, string userLogin)
        {
            try
            {
                var sourceChecklist = await _context.PrsChecklists
                    .Where(pc => pc.PRSId == sourcePrsId)
                    .ToListAsync();

                if (!sourceChecklist.Any()) return false;

                // Supprimer les éléments existants de la checklist PRS cible
                var existingItems = await _context.PrsChecklists
                    .Where(pc => pc.PRSId == targetPrsId)
                    .ToListAsync();

                _context.PrsChecklists.RemoveRange(existingItems);

                // Créer les nouveaux éléments basés sur la PRS source
                var newItems = sourceChecklist.Select(sourceItem => new PrsChecklist
                {
                    PRSId = targetPrsId,
                    Categorie = sourceItem.Categorie,
                    SousCategorie = sourceItem.SousCategorie,
                    Libelle = sourceItem.Libelle,
                    Tache = sourceItem.Tache,
                    Ordre = sourceItem.Ordre,
                    Obligatoire = sourceItem.Obligatoire,
                    EstCoche = false, // Réinitialiser l'état
                    Statut = null, // Réinitialiser l'état
                    ChecklistModeleSourceId = sourceItem.ChecklistModeleSourceId,
                    PrsSourceId = sourcePrsId,
                    CreatedByLogin = userLogin,
                    DateCreation = DateTime.Now
                }).ToList();

                _context.PrsChecklists.AddRange(newItems);
                await _context.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> CreateCustomChecklistAsync(int prsId, List<PrsChecklist> elements, string userLogin)
        {
            try
            {
                // Supprimer les éléments existants
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
                    element.EstCoche = false;
                    element.Statut = null;

                    // Si Libelle est vide, utiliser Tache pour compatibilité
                    if (string.IsNullOrEmpty(element.Libelle) && !string.IsNullOrEmpty(element.Tache))
                        element.Libelle = element.Tache;

                    // Si Tache est vide, utiliser Libelle pour compatibilité
                    if (string.IsNullOrEmpty(element.Tache) && !string.IsNullOrEmpty(element.Libelle))
                        element.Tache = element.Libelle;
                }

                _context.PrsChecklists.AddRange(elements);
                await _context.SaveChangesAsync();

                return true;
            }
            catch
            {
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
                item.Statut = estCoche;
                item.Commentaire = commentaire;
                item.ValidePar = estCoche ? userLogin : null;
                item.DateValidation = estCoche ? DateTime.Now : null;

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

        public async Task<List<Prs>> SearchPrsWithChecklistAsync(string searchTerm, int limit = 10)
        {
            return await _context.Prs
                .Include(p => p.Checklist)
                .Where(p => p.Checklist.Any() &&
                           (p.Titre.Contains(searchTerm) ||
                            p.Id.ToString().Contains(searchTerm) ||
                            p.Equipement.Contains(searchTerm)))
                .OrderByDescending(p => p.DateCreation)
                .Take(limit)
                .ToListAsync();
        }

        // Méthodes utilitaires
        public async Task<Dictionary<string, int>> GetChecklistStatsAsync(int prsId)
        {
            var checklist = await _context.PrsChecklists
                .Where(pc => pc.PRSId == prsId)
                .ToListAsync();

            return new Dictionary<string, int>
            {
                ["Total"] = checklist.Count,
                ["Valides"] = checklist.Count(c => c.EstValide),
                ["Obligatoires"] = checklist.Count(c => c.Obligatoire),
                ["ObligatoiresValides"] = checklist.Count(c => c.Obligatoire && c.EstValide),
                ["EnAttente"] = checklist.Count(c => !c.EstValide)
            };
        }

        public async Task<List<string>> GetCategoriesUtiliseesAsync()
        {
            return await _context.ChecklistElementModeles
                .Select(cem => cem.Categorie)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
        }

        public async Task<List<string>> GetSousCategoriesAsync(string categorie)
        {
            return await _context.ChecklistElementModeles
                .Where(cem => cem.Categorie == categorie && !string.IsNullOrEmpty(cem.SousCategorie))
                .Select(cem => cem.SousCategorie)
                .Distinct()
                .OrderBy(sc => sc)
                .ToListAsync();
        }
    }
}