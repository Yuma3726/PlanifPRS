using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlanifPRS.Data;
using PlanifPRS.Models;

namespace PlanifPRS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChecklistAssignationController : ControllerBase
    {
        private readonly PlanifPrsDbContext _context;

        public ChecklistAssignationController(PlanifPrsDbContext context)
        {
            _context = context;
        }

        [HttpPost("assign-users/{checklistId}")]
        public async Task<IActionResult> AssignUsers(int checklistId, [FromBody] List<int> userIds)
        {
            try
            {
                // Supprimer les assignations existantes
                var existingAssignations = await _context.ChecklistUtilisateurs
                    .Where(cu => cu.ChecklistId == checklistId)
                    .ToListAsync();
                _context.ChecklistUtilisateurs.RemoveRange(existingAssignations);

                // Ajouter les nouvelles assignations
                foreach (var userId in userIds)
                {
                    _context.ChecklistUtilisateurs.Add(new ChecklistUtilisateur
                    {
                        ChecklistId = checklistId,
                        UtilisateurId = userId
                    });
                }

                await _context.SaveChangesAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest($"Erreur lors de l'assignation: {ex.Message}");
            }
        }

        [HttpPost("assign-groups/{checklistId}")]
        public async Task<IActionResult> AssignGroups(int checklistId, [FromBody] List<int> groupIds)
        {
            try
            {
                // Supprimer les assignations existantes
                var existingAssignations = await _context.ChecklistGroupes
                    .Where(cg => cg.ChecklistId == checklistId)
                    .ToListAsync();
                _context.ChecklistGroupes.RemoveRange(existingAssignations);

                // Ajouter les nouvelles assignations
                foreach (var groupId in groupIds)
                {
                    _context.ChecklistGroupes.Add(new ChecklistGroupe
                    {
                        ChecklistId = checklistId,
                        GroupeId = groupId
                    });
                }

                await _context.SaveChangesAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest($"Erreur lors de l'assignation: {ex.Message}");
            }
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _context.Utilisateurs
                .Where(u => u.DateDeleted == null)
                .Select(u => new { u.Id, u.Nom, u.Prenom, u.LoginWindows })
                .ToListAsync();
            return Ok(users);
        }

        // Le controller reste le même
        [HttpGet("groups")]
        public async Task<IActionResult> GetGroups()
        {
            var groups = await _context.GroupesUtilisateurs
                .Where(g => g.Actif)
                .Select(g => new { g.Id, g.NomGroupe, g.Description })
                .ToListAsync();
            return Ok(groups);
        }
    }
}