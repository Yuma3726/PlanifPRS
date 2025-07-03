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
    public class CreateModel : PageModel
    {
        private readonly PlanifPrsDbContext _context;

        public CreateModel(PlanifPrsDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Models.Prs Prs { get; set; }

        public SelectList LigneList { get; set; }

        public IList<PrsFamille> Familles { get; set; }

        [TempData]
        public string Flash { get; set; }

        public void OnGet()
        {
            ChargerFamilles();
            ChargerLignes();

            var now = DateTime.Now;
            var rounded = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);

            // Valeurs par défaut
            Prs = new Models.Prs
            {
                Statut = "En attente",
                DateDebut = rounded,
                DateFin = rounded.AddHours(1)
            };

            // ✅ Écrase les valeurs par défaut si une date est passée dans l'URL
            if (Request.Query.ContainsKey("start"))
            {
                if (DateTime.TryParse(Request.Query["start"], out var parsedStart))
                {
                    Prs.DateDebut = parsedStart;
                    Prs.DateFin = parsedStart.AddHours(1);
                }
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            ChargerFamilles();
            ChargerLignes();

            if (Prs.DateDebut >= Prs.DateFin)
            {
                ModelState.AddModelError(string.Empty, "La date de début doit être antérieure à la date de fin.");
            }

            if (Prs.LigneId == 0)
            {
                ModelState.AddModelError("Prs.LigneId", "La sélection d'une ligne est obligatoire.");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                Prs.DateCreation = DateTime.Now;
                Prs.DerniereModification = DateTime.Now;

                _context.Prs.Add(Prs);
                await _context.SaveChangesAsync();

                Flash = "PRS ajoutée avec succès ✅";

                return RedirectToPage("./Create");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Erreur lors de l'ajout de la PRS.");
                Console.WriteLine(">>>> ERREUR : " + ex.Message);
                return Page();
            }
        }

        private void ChargerFamilles()
        {
            try
            {
                var famillesList = new List<PrsFamille>();
                var connection = _context.Database.GetDbConnection();
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Id, Libelle, CouleurHex 
                    FROM [PlanifPRS].[dbo].[PRS_Famille] 
                    WHERE Libelle IS NOT NULL AND Libelle != ''
                    ORDER BY Libelle";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    // ✅ Accès par index au lieu du nom pour éviter les erreurs de type
                    var idObj = reader[0]; // Première colonne = Id
                    var libelleObj = reader[1]; // Deuxième colonne = Libelle
                    var couleurObj = reader[2]; // Troisième colonne = CouleurHex

                    // Conversion sécurisée de l'ID
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

                Familles = famillesList;
                Console.WriteLine($"✅ Familles chargées: {famillesList.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur ChargerFamilles: {ex.Message}");
                Familles = new List<PrsFamille>();
            }
        }

        private void ChargerLignes()
        {
            try
            {
                var lignesList = new List<SelectListItem>();
                var connection = _context.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                    connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Id, Nom 
                    FROM [PlanifPRS].[dbo].[Lignes] 
                    WHERE activation = 1 AND Nom IS NOT NULL AND Nom != ''
                    ORDER BY Nom";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    // ✅ Accès par index au lieu du nom pour éviter les erreurs de type
                    var idObj = reader[0]; // Première colonne = Id
                    var nomObj = reader[1]; // Deuxième colonne = Nom

                    // Conversion sécurisée de l'ID
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
                            Text = nomObj?.ToString() ?? "Sans nom"
                        });
                    }
                }

                LigneList = new SelectList(lignesList, "Value", "Text");
                Console.WriteLine($"✅ Lignes chargées: {lignesList.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur ChargerLignes: {ex.Message}");
                LigneList = new SelectList(new List<SelectListItem>(), "Value", "Text");
            }
        }
    }
}