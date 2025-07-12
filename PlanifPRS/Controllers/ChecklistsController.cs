using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlanifPRS.Data;
using PlanifPRS.Models;
using PlanifPRS.Services;
using System.Text.Json;

namespace PlanifPRS.Controllers.Api
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
                    Elements = modele.Elements.OrderBy(e => e.Ordre).ThenBy(e => e.Priorite).Select(e => new
                    {
                        e.Id,
                        e.Categorie,
                        e.SousCategorie,
                        e.Libelle,
                        e.Priorite,
                        e.Obligatoire,
                        e.DelaiDefautJours,
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
                        c.Priorite,
                        c.Obligatoire,
                        c.EstCoche,
                        c.DateEcheance,
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

                var result = new
                {
                    elements = checklist.Select(c => new
                    {
                        c.Id,
                        c.Categorie,
                        c.SousCategorie,
                        c.Libelle,
                        c.Priorite,
                        c.Obligatoire,
                        c.EstCoche,
                        c.Statut,
                        c.Commentaire,
                        c.DateEcheance,
                        c.DateValidation,
                        c.ValidePar,
                        delaiDefautJours = c.DateEcheance.HasValue && c.DateCreation != default ?
                            (c.DateEcheance.Value - c.DateCreation).Days : (int?)null,
                        StatutAffichage = c.StatutAffichage,
                        CssClass = c.CssClass,
                        PrioriteLibelle = c.PrioriteLibelle
                    }).ToList()
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Erreur lors de la récupération de la checklist: {ex.Message}" });
            }
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            try
            {
                var stats = await _checklistService.GetChecklistStatsAsync();
                var checklistsEnRetard = await _checklistService.GetChecklistsEnRetardAsync();
                var checklistsEcheanceProche = await _checklistService.GetChecklistsEcheanceProche();

                return Ok(new
                {
                    stats,
                    checklistsEnRetard = checklistsEnRetard.Take(10).Select(c => new
                    {
                        c.Id,
                        PrsId = c.PRSId,
                        PrsTitre = c.Prs?.Titre,
                        c.Libelle,
                        c.DateEcheance,
                        c.Priorite,
                        JoursRetard = c.DateEcheance.HasValue ? (DateTime.Now.Date - c.DateEcheance.Value.Date).Days : 0
                    }),
                    checklistsEcheanceProche = checklistsEcheanceProche.Take(10).Select(c => new
                    {
                        c.Id,
                        PrsId = c.PRSId,
                        PrsTitre = c.Prs?.Titre,
                        c.Libelle,
                        c.DateEcheance,
                        c.Priorite,
                        JoursRestants = c.DateEcheance.HasValue ? (c.DateEcheance.Value.Date - DateTime.Now.Date).Days : 0
                    })
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Erreur lors de la récupération du tableau de bord: {ex.Message}" });
            }
        }

        [HttpPost("prs/{prsId}/items")]
        public async Task<IActionResult> CreateChecklistItem(int prsId, [FromBody] PrsChecklist item)
        {
            try
            {
                item.PRSId = prsId;
                item.CreatedByLogin = GetCurrentUserLogin();
                item.DateCreation = DateTime.Now;

                var elements = new List<PrsChecklist> { item };
                var success = await _checklistService.CreateCustomChecklistAsync(prsId, elements, GetCurrentUserLogin());

                if (!success)
                    return BadRequest(new { message = "Erreur lors de la création de l'élément" });

                return Ok(new { message = "Élément créé avec succès", item });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Erreur lors de la création: {ex.Message}" });
            }
        }

        [HttpPut("items/{id}")]
        public async Task<IActionResult> UpdateChecklistItem(int id, [FromBody] PrsChecklist item)
        {
            try
            {
                if (item.EstCoche && string.IsNullOrEmpty(item.ValidePar))
                {
                    item.ValidePar = GetCurrentUserLogin();
                }

                var success = await _checklistService.UpdateChecklistItemAsync(id, item);

                if (!success)
                    return NotFound(new { message = "Élément de checklist non trouvé" });

                return Ok(new { message = "Élément mis à jour avec succès" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Erreur lors de la mise à jour: {ex.Message}" });
            }
        }

        [HttpDelete("items/{id}")]
        public async Task<IActionResult> DeleteChecklistItem(int id)
        {
            try
            {
                var success = await _checklistService.DeleteChecklistItemAsync(id);

                if (!success)
                    return NotFound(new { message = "Élément de checklist non trouvé" });

                return Ok(new { message = "Élément supprimé avec succès" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Erreur lors de la suppression: {ex.Message}" });
            }
        }

        [HttpGet("search-prs")]
        public async Task<IActionResult> SearchPrs([FromQuery] string searchTerm, [FromQuery] int limit = 10)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length < 2)
                {
                    return Ok(new List<object>());
                }

                var prsList = await _context.Prs
                    .Where(p => p.Titre.Contains(searchTerm) ||
                               p.Equipement.Contains(searchTerm) ||
                               p.ReferenceProduit.Contains(searchTerm) ||
                               p.Id.ToString().Contains(searchTerm))
                    .Include(p => p.Checklist)
                    .Take(limit)
                    .Select(p => new
                    {
                        p.Id,
                        p.Titre,
                        p.Equipement,
                        p.ReferenceProduit,
                        DateCreation = p.DateCreation.ToString("yyyy-MM-dd"),
                        NombreElementsChecklist = p.Checklist.Count,
                        PourcentageCompletion = p.Checklist.Any() ?
                            (int)Math.Round((double)p.Checklist.Count(c => c.EstCoche) / p.Checklist.Count * 100) : 0,
                        StatutChecklist = p.Checklist.Any() ?
                            (p.Checklist.All(c => c.EstCoche) ? "Complète" : "En cours") : "Aucune"
                    })
                    .ToListAsync();

                return Ok(prsList);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Erreur lors de la recherche: {ex.Message}" });
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

        [HttpGet("sous-categories")]
        public async Task<IActionResult> GetSousCategories([FromQuery] string categorie)
        {
            try
            {
                if (string.IsNullOrEmpty(categorie))
                {
                    return BadRequest(new { message = "La catégorie est requise" });
                }

                var sousCategories = await _checklistService.GetSousCategoriesAsync(categorie);
                return Ok(sousCategories);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Erreur lors de la récupération des sous-catégories: {ex.Message}" });
            }
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            try
            {
                var stats = await _checklistService.GetChecklistStatsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Erreur lors de la récupération des statistiques: {ex.Message}" });
            }
        }

        [HttpPost("prs/{sourcePrsId}/copy-to/{targetPrsId}")]
        public async Task<IActionResult> CopyChecklistFromPrs(int sourcePrsId, int targetPrsId)
        {
            try
            {
                var userLogin = GetCurrentUserLogin();
                var success = await _checklistService.CopyChecklistFromPrsAsync(targetPrsId, sourcePrsId, userLogin);

                if (!success)
                    return BadRequest(new { message = "Erreur lors de la copie de la checklist" });

                var checklist = await _checklistService.GetPrsChecklistAsync(targetPrsId);
                return Ok(new
                {
                    message = "Checklist copiée avec succès",
                    checklist = checklist.Select(c => new
                    {
                        c.Id,
                        c.Categorie,
                        c.SousCategorie,
                        c.Libelle,
                        c.Priorite,
                        c.Obligatoire,
                        c.EstCoche,
                        c.DateEcheance,
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
        public async Task<IActionResult> CreateCustomChecklist(int prsId, [FromBody] List<PrsChecklist> elements)
        {
            try
            {
                var userLogin = GetCurrentUserLogin();

                // Assigner les propriétés communes
                foreach (var element in elements)
                {
                    element.PRSId = prsId;
                    element.CreatedByLogin = userLogin;
                    element.DateCreation = DateTime.Now;
                    element.EstCoche = false;
                    element.Statut = null;
                }

                var success = await _checklistService.CreateCustomChecklistAsync(prsId, elements, userLogin);

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
                        c.Priorite,
                        c.Obligatoire,
                        c.EstCoche,
                        c.DateEcheance,
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
    }
}