using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PlanifPRS.Data;
using PlanifPRS.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PlanifPRS.Pages.Prs
{
    public class EditModel : PageModel
    {
        private readonly PlanifPrsDbContext _context;

        public EditModel(PlanifPrsDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Models.Prs Prs { get; set; }

        // ✅ Support pour les deux structures de vue
        public SelectList FamillesSelectList { get; set; }
        public SelectList LignesSelectList { get; set; }
        public IList<PrsFamille> Familles { get; set; } // ✅ Ajouté pour la nouvelle structure

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Prs = await _context.Prs.FindAsync(id);

            if (Prs == null)
            {
                return NotFound();
            }

            // ✅ Chargement avec fallback SQL en cas d'erreur Entity Framework
            await ChargerFamillesAsync();
            await ChargerLignesAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (Prs.DateDebut >= Prs.DateFin)
            {
                ModelState.AddModelError(string.Empty, "La date de début doit être antérieure à la date de fin.");
            }

            if (!ModelState.IsValid)
            {
                // ✅ Recharger les listes en cas d'erreur
                await ChargerFamillesAsync();
                await ChargerLignesAsync();
                return Page();
            }

            var prsFromDb = await _context.Prs.FindAsync(Prs.Id);
            if (prsFromDb == null)
                return NotFound();

            prsFromDb.Titre = Prs.Titre;
            prsFromDb.Equipement = Prs.Equipement;
            prsFromDb.ReferenceProduit = Prs.ReferenceProduit;
            prsFromDb.Quantite = Prs.Quantite;
            prsFromDb.BesoinOperateur = Prs.BesoinOperateur;
            prsFromDb.PresenceClient = Prs.PresenceClient;
            prsFromDb.DateDebut = Prs.DateDebut;
            prsFromDb.DateFin = Prs.DateFin;
            prsFromDb.Statut = Prs.Statut;
            prsFromDb.InfoDiverses = Prs.InfoDiverses;
            prsFromDb.FamilleId = Prs.FamilleId;
            prsFromDb.LigneId = Prs.LigneId;
            prsFromDb.DerniereModification = DateTime.Now;

            await _context.SaveChangesAsync();

            return RedirectToPage("/Index");
        }

        // ✅ Méthode pour charger les familles avec fallback SQL
        private async Task ChargerFamillesAsync()
        {
            try
            {
                // ✅ Tentative avec Entity Framework d'abord
                var familles = await _context.PrsFamilles
                    .Where(f => f != null && !string.IsNullOrEmpty(f.Libelle))
                    .OrderBy(f => f.Libelle)
                    .ToListAsync();

                FamillesSelectList = new SelectList(familles, "Id", "Libelle", Prs.FamilleId);
                Familles = familles; // ✅ Pour la nouvelle structure de vue

                Console.WriteLine($"✅ Familles chargées via EF (Edit): {familles.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Erreur EF familles, basculement SQL: {ex.Message}");

                // ✅ Fallback avec SQL brut
                await ChargerFamillesSQLAsync();
            }
        }

        // ✅ Méthode pour charger les lignes avec fallback SQL
        private async Task ChargerLignesAsync()
        {
            try
            {
                // ✅ Tentative avec Entity Framework d'abord
                var lignes = await _context.Lignes
                    .Where(l => l != null && l.Activation == true && !string.IsNullOrEmpty(l.Nom))
                    .OrderBy(l => l.Nom)
                    .ToListAsync();

                LignesSelectList = new SelectList(lignes, "Id", "Nom", Prs.LigneId);

                Console.WriteLine($"✅ Lignes chargées via EF (Edit): {lignes.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Erreur EF lignes, basculement SQL: {ex.Message}");

                // ✅ Fallback avec SQL brut
                await ChargerLignesSQLAsync();
            }
        }

        // ✅ Fallback SQL pour les familles
        private async Task ChargerFamillesSQLAsync()
        {
            try
            {
                var famillesList = new List<PrsFamille>();
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Id, Libelle, CouleurHex 
                    FROM [PlanifPRS].[dbo].[PRS_Famille] 
                    WHERE Libelle IS NOT NULL AND Libelle != ''
                    ORDER BY Libelle";

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var idObj = reader[0];
                    var libelleObj = reader[1];
                    var couleurObj = reader[2];

                    int id = 0;
                    if (idObj != null && idObj != DBNull.Value)
                    {
                        if (int.TryParse(idObj.ToString(), out int parsedId))
                        {
                            id = parsedId;
                        }
                    }

                    if (id > 0)
                    {
                        famillesList.Add(new PrsFamille
                        {
                            Id = id,
                            Libelle = libelleObj?.ToString() ?? "Sans nom",
                            CouleurHex = couleurObj?.ToString() ?? "#009dff"
                        });
                    }
                }

                FamillesSelectList = new SelectList(famillesList, "Id", "Libelle", Prs.FamilleId);
                Familles = famillesList;

                Console.WriteLine($"✅ Familles chargées via SQL (Edit): {famillesList.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur SQL familles (Edit): {ex.Message}");
                FamillesSelectList = new SelectList(new List<object>(), "Id", "Libelle");
                Familles = new List<PrsFamille>();
            }
        }

        // ✅ Fallback SQL pour les lignes
        private async Task ChargerLignesSQLAsync()
        {
            try
            {
                var lignesList = new List<SelectListItem>();
                var connection = _context.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Id, Nom 
                    FROM [PlanifPRS].[dbo].[Lignes] 
                    WHERE activation = 1 AND Nom IS NOT NULL AND Nom != ''
                    ORDER BY Nom";

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var idObj = reader[0];
                    var nomObj = reader[1];

                    int id = 0;
                    if (idObj != null && idObj != DBNull.Value)
                    {
                        if (int.TryParse(idObj.ToString(), out int parsedId))
                        {
                            id = parsedId;
                        }
                    }

                    if (id > 0)
                    {
                        lignesList.Add(new SelectListItem
                        {
                            Value = id.ToString(),
                            Text = nomObj?.ToString() ?? "Sans nom",
                            Selected = Prs?.LigneId == id
                        });
                    }
                }

                LignesSelectList = new SelectList(lignesList, "Value", "Text", Prs?.LigneId);

                Console.WriteLine($"✅ Lignes chargées via SQL (Edit): {lignesList.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur SQL lignes (Edit): {ex.Message}");
                LignesSelectList = new SelectList(new List<SelectListItem>(), "Value", "Text");
            }
        }
    }
}