using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlanifPRS.Data;
using PlanifPRS.Models;
using PlanifPRS.Services;

namespace PlanifPRS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChecklistsController : ControllerBase
    {
        private readonly ChecklistService _checklistService;
        private readonly PlanifPrsDbContext _context;

        public ChecklistsController(ChecklistService checklistService, PlanifPrsDbContext context)
        {
            _checklistService = checklistService;
            _context = context;
        }

        private string GetCurrentUserLogin()
        {
            return User?.Identity?.Name ?? "Anonymous";
        }

        [HttpGet("modeles")]
        public async Task<IActionResult> GetChecklistModeles([FromQuery] string? familleEquipement = null, [FromQuery] bool activeOnly = true)
        {
            try
            {
                List<ChecklistModele> modeles;

                if (!string.IsNullOrEmpty(familleEquipement))
                {
                    modeles = await _checklistService.GetChecklistModelesByFamilleAsync(familleEquipement);
                }
                else
                {
                    modeles = await _checklistService.GetChecklistModelesAsync(activeOnly);
                }

                var result = modeles.Select(m => new
                {
                    m.Id,
                    m.Nom,
                    m.Description,
                    m.FamilleEquipement,
                    FamilleAffichage = m.FamilleAffichage,
                    NombreElements = m.NombreElements,
                    NombreElementsObligatoires = m.NombreElementsObligatoires,
                    m.DateCreation,
                    m.CreatedByLogin,
                    m.Actif
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Erreur lors de la récupération des modèles: {ex.Message}" });
            }
        }

        [HttpGet("modeles/{id}")]
        public async Task<IActionResult> GetChecklistModele(int id)
        {
            try
            {
                var modele = await _checklistService.GetChecklistModeleByIdAsync(id);
                if (modele == null)
                    return NotFound(new { message = "Modèle de checklist non trouvé" });

                var result = new
                {
                    modele.Id,
                    modele.Nom,
                    modele.Description,
                    modele.FamilleEquipement,
                    modele.DateCreation,
                    modele.CreatedByLogin,
                    modele.Actif,
                    Elements = modele.Elements.OrderBy(e => e.Priorite).Select(e => new
                    {
                        e.Id,
                        e.Categorie,
                        e.SousCategorie,
                        e.Libelle,
                        e.Priorite,
                        e.Obligatoire,
                        CategorieComplete = e.CategorieComplete,
                        IconeCategorie = e.IconeCategorie,
                        CouleurCategorie = e.CouleurCategorie
                    }).ToList()
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Erreur lors de la récupération du modèle: {ex.Message}" });
            }
        }

        [HttpPost("modeles/{id}/apply/{prsId}")]
        public async Task<IActionResult> ApplyChecklistModele(int id, int prsId)
        {
            try
            {
                var userLogin = GetCurrentUserLogin();
                var success = await _checklistService.ApplyChecklistModeleAsync(prsId, id, userLogin);

                if (!success)
                    return BadRequest(new { message = "Erreur lors de l'application du modèle de checklist" });

                var checklist = await _checklistService.GetPrsChecklistAsync(prsId);
                return Ok(new
                {
                    message = "Modèle de checklist appliqué avec succès",
                    checklist = checklist.Select(c => new
                    {
                        c.Id,
                        c.Categorie,
                        c.SousCategorie,
                        c.Libelle,
                        c.Tache,
                        c.Ordre,
                        c.Priorite,
                        c.Obligatoire,
                        c.EstCoche,
                        c.Statut,
                        c.Commentaire,
                        LibelleAffichage = c.LibelleAffichage,
                        StatutAffichage = c.StatutAffichage,
                        CssClass = c.CssClass
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Erreur lors de l'application du modèle: {ex.Message}" });
            }
        }

        [HttpGet("prs/{prsId}")]
        public async Task<IActionResult> GetPrsChecklist(int prsId)
        {
            try
            {
                var checklist = await _checklistService.GetPrsChecklistAsync(prsId);
                var stats = await _checklistService.GetChecklistStatsAsync(prsId);

                var result = new
                {
                    PrsId = prsId,
                    Stats = stats,
                    Elements = checklist.Select(c => new
                    {
                        c.Id,
                        c.Categorie,
                        c.SousCategorie,
                        c.Libelle,
                        c.Tache,
                        c.Ordre,
                        c.Priorite,
                        c.Obligatoire,
                        c.EstCoche,
                        c.Statut,
                        c.Commentaire,
                        c.DateValidation,
                        c.ValidePar,
                        LibelleAffichage = c.LibelleAffichage,
                        StatutAffichage = c.StatutAffichage,
                        CssClass = c.CssClass
                    }).ToList()
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Erreur lors de la récupération de la checklist: {ex.Message}" });
            }
        }

        [HttpPost("prs/{prsId}/copy/{sourcePrsId}")]
        public async Task<IActionResult> CopyChecklistFromPrs(int prsId, int sourcePrsId)
        {
            try
            {
                var userLogin = GetCurrentUserLogin();
                var success = await _checklistService.CopyChecklistFromPrsAsync(prsId, sourcePrsId, userLogin);

                if (!success)
                    return BadRequest(new { message = "Erreur lors de la copie de la checklist" });

                var checklist = await _checklistService.GetPrsChecklistAsync(prsId);
                return Ok(new
                {
                    message = "Checklist copiée avec succès",
                    checklist = checklist.Select(c => new
                    {
                        c.Id,
                        c.Categorie,
                        c.SousCategorie,
                        c.Libelle,
                        c.Tache,
                        c.Ordre,
                        c.Priorite,
                        c.Obligatoire,
                        c.EstCoche,
                        c.Statut,
                        c.Commentaire,
                        LibelleAffichage = c.LibelleAffichage,
                        StatutAffichage = c.StatutAffichage,
                        CssClass = c.CssClass
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Erreur lors de la copie: {ex.Message}" });
            }
        }

        [HttpPost("prs/{prsId}/custom")]
        public async Task<IActionResult> CreateCustomChecklist(int prsId, [FromBody] List<PrsChecklistCreateDto> elements)
        {
            try
            {
                var userLogin = GetCurrentUserLogin();

                var checklistElements = elements.Select((e, index) => new PrsChecklist
                {
                    Categorie = e.Categorie,
                    SousCategorie = e.SousCategorie,
                    Libelle = e.Libelle,
                    Tache = e.Libelle, // Compatibilité
                    Ordre = e.Ordre > 0 ? e.Ordre : index + 1,
                    Priorite = e.Priorite > 0 ? e.Priorite : 3,
                    Obligatoire = e.Obligatoire
                }).ToList();

                var success = await _checklistService.CreateCustomChecklistAsync(prsId, checklistElements, userLogin);

                if (!success)
                    return BadRequest(new { message = "Erreur lors de la création de la checklist personnalisée" });

                var checklist = await _checklistService.GetPrsChecklistAsync(prsId);
                return Ok(new
                {
                    message = "Checklist personnalisée créée avec succès",
                    checklist = checklist.Select(c => new
                    {
                        c.Id,
                        c.Categorie,
                        c.SousCategorie,
                        c.Libelle,
                        c.Tache,
                        c.Ordre,
                        c.Priorite,
                        c.Obligatoire,
                        c.EstCoche,
                        c.Statut,
                        c.Commentaire,
                        LibelleAffichage = c.LibelleAffichage,
                        StatutAffichage = c.StatutAffichage,
                        CssClass = c.CssClass
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Erreur lors de la création: {ex.Message}" });
            }
        }

        [HttpPut("items/{itemId}")]
        public async Task<IActionResult> UpdateChecklistItem(int itemId, [FromBody] ChecklistItemUpdateDto dto)
        {
            try
            {
                var userLogin = GetCurrentUserLogin();
                var success = await _checklistService.UpdateChecklistItemAsync(itemId, dto.EstCoche, dto.Commentaire, userLogin);

                if (!success)
                    return BadRequest(new { message = "Erreur lors de la mise à jour de l'élément" });

                // Récupérer l'élément mis à jour
                var item = await _context.PrsChecklists.FindAsync(itemId);
                if (item == null)
                    return NotFound();

                return Ok(new
                {
                    message = "Élément mis à jour avec succès",
                    item = new
                    {
                        item.Id,
                        item.EstCoche,
                        item.Statut,
                        item.Commentaire,
                        item.DateValidation,
                        item.ValidePar,
                        StatutAffichage = item.StatutAffichage,
                        CssClass = item.CssClass
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Erreur lors de la mise à jour: {ex.Message}" });
            }
        }

        [HttpDelete("items/{itemId}")]
        public async Task<IActionResult> DeleteChecklistItem(int itemId)
        {
            try
            {
                var success = await _checklistService.DeleteChecklistItemAsync(itemId);

                if (!success)
                    return BadRequest(new { message = "Erreur lors de la suppression de l'élément" });

                return Ok(new { message = "Élément supprimé avec succès" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Erreur lors de la suppression: {ex.Message}" });
            }
        }

        [HttpGet("search-prs")]
        public async Task<IActionResult> SearchPrsWithChecklist([FromQuery] string searchTerm, [FromQuery] int limit = 10)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length < 2)
                    return Ok(new List<object>());

                var prsList = await _checklistService.SearchPrsWithChecklistAsync(searchTerm, limit);

                var result = prsList.Select(p => new
                {
                    p.Id,
                    p.Prs.Titre,
                    p.Prs.Equipement,
                    p.Prs.ReferenceProduit,
                    p.Prs.DateCreation,
                    p.Prs.CreatedByLogin,
                    NombreElementsChecklist = prsList.Count(x => x.PRSId == p.PRSId),
                    PourcentageCompletionChecklist = 0,
                    StatutChecklist = "En cours"
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Erreur lors de la recherche: {ex.Message}" });
            }
        }

        [HttpGet("prs/{prsId}/export")]
        public async Task<IActionResult> ExportPrsChecklist(int prsId)
        {
            try
            {
                var checklist = await _checklistService.GetPrsChecklistAsync(prsId);
                var stats = await _checklistService.GetChecklistStatsAsync(prsId);

                // Récupérer les infos PRS
                var prs = await _context.Prs.FindAsync(prsId);
                if (prs == null)
                    return NotFound(new { message = "PRS non trouvé" });

                var exportData = new
                {
                    PrsId = prsId,
                    DateExport = DateTime.Now,
                    Titre = prs.Titre,
                    Equipement = prs.Equipement,
                    ReferenceProduit = prs.ReferenceProduit,
                    Stats = stats,
                    NombreElementsChecklist = checklist.Count,
                    PourcentageCompletionChecklist = prs.PourcentageCompletionChecklist,
                    Elements = checklist.OrderBy(c => c.Ordre).Select(c => new
                    {
                        c.Categorie,
                        c.SousCategorie,
                        LibelleAffichage = c.LibelleAffichage,
                        c.Ordre,
                        c.Priorite,
                        PrioriteLibelle = c.PrioriteLibelle,
                        c.Obligatoire,
                        StatutAffichage = c.StatutAffichage,
                        c.Commentaire,
                        c.DateValidation,
                        c.ValidePar,
                        c.DateEcheance
                    }).ToList()
                };

                return Ok(exportData);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Erreur lors de l'export: {ex.Message}" });
            }
        }

        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var categories = await _checklistService.GetCategoriesUtiliseesAsync();
                return Ok(categories);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Erreur lors de la récupération des catégories: {ex.Message}" });
            }
        }

        [HttpGet("categories/{categorie}/sous-categories")]
        public async Task<IActionResult> GetSousCategories(string categorie)
        {
            try
            {
                var sousCategories = await _checklistService.GetSousCategoriesAsync(categorie);
                return Ok(sousCategories);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Erreur lors de la récupération des sous-catégories: {ex.Message}" });
            }
        }

        [HttpGet("prs/{prsId}/stats")]
        public async Task<IActionResult> GetChecklistStats(int prsId)
        {
            try
            {
                var stats = await _checklistService.GetChecklistStatsAsync(prsId);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Erreur lors de la récupération des statistiques: {ex.Message}" });
            }
        }
    }

    // DTOs pour l'API
    public class PrsChecklistCreateDto
    {
        public string Categorie { get; set; } = string.Empty;
        public string? SousCategorie { get; set; }
        public string Libelle { get; set; } = string.Empty;
        public int Ordre { get; set; }
        public int Priorite { get; set; } = 3;
        public bool Obligatoire { get; set; }
    }

    public class ChecklistItemUpdateDto
    {
        public bool EstCoche { get; set; }
        public string? Commentaire { get; set; }
    }
}