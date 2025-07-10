using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PlanifPRS.Data;
using PlanifPRS.Models;
using Microsoft.Extensions.Logging;

namespace PlanifPRS.Services
{
    public class LienDossierPrsService
    {
        private readonly PlanifPrsDbContext _context;
        private readonly ILogger<LienDossierPrsService> _logger;

        public LienDossierPrsService(PlanifPrsDbContext context, ILogger<LienDossierPrsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Récupère tous les liens de dossiers pour une PRS donnée
        /// </summary>
        public async Task<List<LienDossierPrs>> GetLiensByPrsIdAsync(int prsId)
        {
            try
            {
                return await _context.LiensDossierPrs
                    .Where(l => l.PrsId == prsId)
                    .OrderByDescending(l => l.DateAjout)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erreur lors de la récupération des liens de dossiers pour PRS ID {prsId}: {ex.Message}", ex);
                return new List<LienDossierPrs>();
            }
        }

        /// <summary>
        /// Ajoute un nouveau lien de dossier pour une PRS
        /// </summary>
        public async Task<LienDossierPrs?> AddLienAsync(int prsId, string chemin, string? description, string userLogin)
        {
            if (string.IsNullOrEmpty(chemin))
            {
                _logger.LogWarning("Tentative d'ajout d'un lien de dossier avec un chemin vide");
                return null;
            }

            try
            {
                var lien = new LienDossierPrs
                {
                    PrsId = prsId,
                    Chemin = chemin,
                    Description = description,
                    DateAjout = DateTime.Now,
                    AjouteParLogin = userLogin
                };

                _context.LiensDossierPrs.Add(lien);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Lien de dossier ajouté avec succès: ID={lien.Id}, PrsId={prsId}, Chemin={chemin}");

                return lien;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erreur lors de l'ajout du lien de dossier pour PRS ID {prsId}: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// Ajoute plusieurs liens de dossiers pour une PRS
        /// </summary>
        public async Task<List<LienDossierPrs>> AddMultipleLinksAsync(int prsId, List<LienDossierPrs> liens)
        {
            if (liens == null || !liens.Any())
                return new List<LienDossierPrs>();

            try
            {
                foreach (var lien in liens)
                {
                    lien.PrsId = prsId;
                    if (lien.DateAjout == default)
                        lien.DateAjout = DateTime.Now;

                    _context.LiensDossierPrs.Add(lien);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"{liens.Count} liens de dossiers ajoutés pour la PRS ID {prsId}");

                return liens;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erreur lors de l'ajout multiple de liens de dossiers pour PRS ID {prsId}: {ex.Message}", ex);
                return new List<LienDossierPrs>();
            }
        }

        /// <summary>
        /// Supprime un lien de dossier par son ID
        /// </summary>
        public async Task<bool> DeleteLienAsync(int id)
        {
            try
            {
                var lien = await _context.LiensDossierPrs.FindAsync(id);

                if (lien == null)
                {
                    _logger.LogWarning($"Tentative de suppression d'un lien de dossier inexistant: ID {id}");
                    return false;
                }

                _context.LiensDossierPrs.Remove(lien);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Lien de dossier supprimé: ID={id}, PrsId={lien.PrsId}, Chemin={lien.Chemin}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erreur lors de la suppression du lien de dossier ID {id}: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Vérifie si un lien existe déjà pour un chemin et une PRS donnés
        /// </summary>
        public async Task<bool> ExistsAsync(int prsId, string chemin)
        {
            if (string.IsNullOrEmpty(chemin))
                return false;

            try
            {
                return await _context.LiensDossierPrs
                    .AnyAsync(l => l.PrsId == prsId && l.Chemin == chemin);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erreur lors de la vérification d'existence d'un lien de dossier: {ex.Message}", ex);
                return false;
            }
        }
    }
}